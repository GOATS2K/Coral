import type { SimpleTrackDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';
import type { RepeatMode } from '@/lib/state';

type AudioSourceType = 'buffer' | 'element';

interface ScheduledSource {
  source: AudioBufferSourceNode | MediaElementAudioSourceNode;
  sourceType: AudioSourceType;
  audioElement?: HTMLAudioElement; // For MediaElement sources
  startTime: number;
  duration: number;
  trackIndex: number;
}

export class WebAudioPlayer {
  private audioContext: AudioContext;
  private gainNode: GainNode;
  private tracks: SimpleTrackDto[] = [];
  private currentTrackIndex = 0;
  private scheduledSources: ScheduledSource[] = [];
  private isPlaying = false;
  private startTime = 0;
  private pauseTime = 0;
  private trackChangeCallback: ((index: number) => void) | null = null;
  private schedulingRaf: number | null = null;
  private repeatMode: RepeatMode = 'off';
  private audioBuffers: Map<string, AudioBuffer> = new Map();
  private schedulingInProgress: Set<number> = new Set();
  private prefetchInProgress: Set<string> = new Set(); // Track IDs being prefetched
  private bufferDecodeInProgress: Set<string> = new Set(); // Track IDs being decoded for caching

  constructor() {
    this.audioContext = new AudioContext();
    this.gainNode = this.audioContext.createGain();
    this.gainNode.connect(this.audioContext.destination);
  }

  async loadQueue(tracks: SimpleTrackDto[], startIndex: number = 0, clearCache: boolean = true) {
    this.tracks = tracks;
    this.currentTrackIndex = startIndex;
    this.clearScheduledSources();

    // Clear cache when loading a completely new playlist
    if (clearCache) {
      this.clearAudioCache();
    }

    // Start playback from the specified track
    await this.scheduleTrack(startIndex, this.audioContext.currentTime);
    this.isPlaying = true;
    this.startSchedulingLoop();

    // Prefetch next 2 tracks in background to avoid gaps
    this.prefetchUpcomingTracks(startIndex);
  }

  updateQueue(tracks: SimpleTrackDto[], currentIndex: number) {
    console.info('[WebAudio] Updating queue, new length:', tracks.length, 'current index:', currentIndex);

    // Update internal queue reference
    this.tracks = tracks;
    this.currentTrackIndex = currentIndex;

    // Clear pre-scheduled tracks to prevent wrong tracks from playing after queue changes
    // (e.g., shuffle/reorder operations)
    const hadScheduledSources = this.scheduledSources.length > 1;
    if (hadScheduledSources) {
      console.info('[WebAudio] Queue changed - clearing', this.scheduledSources.length - 1, 'pre-scheduled track(s)');
      // Keep only the currently playing track (index 0)
      const currentlyPlaying = this.scheduledSources[0];
      this.clearScheduledSources();
      if (currentlyPlaying && this.isPlaying) {
        this.scheduledSources = [currentlyPlaying];
        console.info('[WebAudio] Preserved currently playing track, will re-schedule upcoming tracks');
      }
    }

    // Remove cached tracks that are no longer in the queue
    const trackIdsInQueue = new Set(tracks.map(t => t.id));
    const cachedTrackIds = Array.from(this.audioBuffers.keys());

    for (const cachedId of cachedTrackIds) {
      if (!trackIdsInQueue.has(cachedId)) {
        console.info('[WebAudio] Removing cached track no longer in queue:', cachedId);
        this.audioBuffers.delete(cachedId);
        this.prefetchInProgress.delete(cachedId);
      }
    }

    // If currently playing, prefetch upcoming tracks in the new queue order
    if (this.isPlaying) {
      this.prefetchUpcomingTracks(currentIndex);
    }
  }

  clearAudioCache() {
    const cacheSize = this.audioBuffers.size;
    if (cacheSize > 0) {
      console.info('[WebAudio] Clearing audio cache, removing', cacheSize, 'cached tracks');
      this.audioBuffers.clear();
      this.prefetchInProgress.clear();
    }
  }

  private createMediaElementSource(url: string, track: SimpleTrackDto): {
    audioElement: HTMLAudioElement;
    source: MediaElementAudioSourceNode;
    duration: Promise<number>;
  } {
    const audioElement = new Audio(url);
    audioElement.crossOrigin = 'anonymous';
    audioElement.preload = 'auto';

    const source = this.audioContext.createMediaElementSource(audioElement);
    source.connect(this.gainNode);

    // Return promise that resolves to duration once loaded
    const durationPromise = new Promise<number>((resolve) => {
      const onLoadedMetadata = () => {
        resolve(audioElement.duration);
        audioElement.removeEventListener('loadedmetadata', onLoadedMetadata);
      };
      audioElement.addEventListener('loadedmetadata', onLoadedMetadata);
    });

    return { audioElement, source, duration: durationPromise };
  }

  private async scheduleTrack(trackIndex: number, startTime: number, preferBuffer: boolean = false) {
    if (trackIndex >= this.tracks.length) return;

    // Check if already scheduling this track
    if (this.schedulingInProgress.has(trackIndex)) {
      console.warn('[WebAudio] ⚠️  Skipping schedule for track', trackIndex, '- already in progress');
      return;
    }

    // Mark as in progress
    this.schedulingInProgress.add(trackIndex);

    try {
      const track = this.tracks[trackIndex];
      console.info('[WebAudio] Scheduling track', trackIndex, track.title, 'at', startTime.toFixed(3));

      // Fetch original audio file URL
      const fileUrl = `${baseUrl}/api/library/tracks/${track.id}/original`;

      // Check if we have a cached buffer
      const cachedBuffer = this.audioBuffers.get(track.id);
      const currentTime = this.audioContext.currentTime;
      const timeDelta = startTime - currentTime;

      // Use cached buffer if available, or if explicitly requested and we have time
      if (cachedBuffer || (preferBuffer && timeDelta > 2)) {
        console.info('[WebAudio] Using AudioBuffer source (cached or preferred)');
        const audioBuffer = cachedBuffer || await this.fetchAndDecode(fileUrl, track.id);

        // Create source node
        const source = this.audioContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(this.gainNode);

        // Check for gaps
        if (startTime < currentTime) {
          const gap = currentTime - startTime;
          console.warn('[WebAudio] ⚠️  GAP DETECTED:', gap.toFixed(3), 's - scheduled start time was in the past!');
        } else if (timeDelta < 0.5) {
          console.warn('[WebAudio] ⚠️  TIGHT TIMING:', timeDelta.toFixed(3), 's until scheduled start');
        } else {
          console.info('[WebAudio] ✓ Good timing:', timeDelta.toFixed(3), 's until scheduled start');
        }

        // Schedule playback
        const actualStartTime = Math.max(startTime, currentTime);
        source.start(actualStartTime);
        console.info('[WebAudio] ▶️  Track', trackIndex, 'playback started (buffer) at', actualStartTime.toFixed(3));

        // Track when this source will end
        const scheduledSource: ScheduledSource = {
          source,
          sourceType: 'buffer',
          startTime,
          duration: audioBuffer.duration,
          trackIndex
        };

        this.scheduledSources.push(scheduledSource);

        // Handle track end
        source.onended = () => {
          console.info('[WebAudio] Track', trackIndex, 'ended');
          this.handleTrackEnd(scheduledSource);
        };

        console.info('[WebAudio] Scheduled track', trackIndex, 'duration:', audioBuffer.duration.toFixed(2), 's');
      } else {
        // Use MediaElement for fast startup (streaming)
        console.info('[WebAudio] Using MediaElement source (streaming for fast startup)');

        const { audioElement, source, duration } = this.createMediaElementSource(fileUrl, track);

        // Start playback immediately or at scheduled time
        const actualStartTime = Math.max(startTime, currentTime);
        const playDelay = Math.max(0, actualStartTime - currentTime);

        if (playDelay > 0) {
          setTimeout(() => {
            audioElement.play().catch(err => console.error('[WebAudio] MediaElement play failed:', err));
          }, playDelay * 1000);
        } else {
          audioElement.play().catch(err => console.error('[WebAudio] MediaElement play failed:', err));
        }

        console.info('[WebAudio] ▶️  Track', trackIndex, 'playback started (streaming) at', actualStartTime.toFixed(3));

        // Get duration
        const trackDuration = await duration;

        // Track when this source will end
        const scheduledSource: ScheduledSource = {
          source,
          sourceType: 'element',
          audioElement,
          startTime: actualStartTime,
          duration: trackDuration,
          trackIndex
        };

        this.scheduledSources.push(scheduledSource);

        // Handle track end
        audioElement.addEventListener('ended', () => {
          console.info('[WebAudio] Track', trackIndex, 'ended (streaming)');
          this.handleTrackEnd(scheduledSource);
        });

        console.info('[WebAudio] Scheduled track', trackIndex, 'duration:', trackDuration.toFixed(2), 's (streaming)');

        // Start decoding buffer in background for future gapless playback
        if (!this.bufferDecodeInProgress.has(track.id)) {
          console.info('[WebAudio] Starting background decode for gapless capability');
          this.bufferDecodeInProgress.add(track.id);

          this.fetchAndDecode(fileUrl, track.id)
            .then(() => {
              console.info('[WebAudio] ✓ Background decode complete for track:', track.id);
              this.bufferDecodeInProgress.delete(track.id);
            })
            .catch(err => {
              console.warn('[WebAudio] Background decode failed:', err);
              this.bufferDecodeInProgress.delete(track.id);
            });
        }
      }
    } finally {
      // Always remove from in-progress set
      this.schedulingInProgress.delete(trackIndex);
    }
  }

  private async fetchAndDecode(url: string, trackId: string): Promise<AudioBuffer> {
    // Check cache first
    const cached = this.audioBuffers.get(trackId);
    if (cached) {
      console.info('[WebAudio] Using cached audio buffer for track:', trackId);
      return cached;
    }

    console.info('[WebAudio] Fetching audio file:', url);

    // Fetch the complete audio file directly (FLAC, MP3, etc.)
    const audioResponse = await fetch(url);
    const arrayBuffer = await audioResponse.arrayBuffer();

    console.info('[WebAudio] Decoding audio, size:', (arrayBuffer.byteLength / 1024 / 1024).toFixed(2), 'MB');
    const audioBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
    console.info('[WebAudio] Decoded, duration:', audioBuffer.duration.toFixed(2), 's');

    // Cache the buffer
    this.audioBuffers.set(trackId, audioBuffer);

    return audioBuffer;
  }

  private prefetchUpcomingTracks(startIndex: number) {
    // Prefetch next 2 tracks in background (don't await - fire and forget)
    for (let i = 1; i <= 2; i++) {
      let nextIndex = startIndex + i;

      // Handle repeat-all wrapping
      if (nextIndex >= this.tracks.length && this.repeatMode === 'all') {
        nextIndex = nextIndex % this.tracks.length;
      }

      if (nextIndex < this.tracks.length) {
        const track = this.tracks[nextIndex];

        // Skip if already cached or currently being prefetched
        if (this.audioBuffers.has(track.id)) {
          continue;
        }
        if (this.prefetchInProgress.has(track.id)) {
          continue;
        }

        const fileUrl = `${baseUrl}/api/library/tracks/${track.id}/original`;
        console.info('[WebAudio] Prefetching track', nextIndex, track.title);

        // Mark as in progress
        this.prefetchInProgress.add(track.id);

        // Fire and forget - just cache the decoded audio
        this.fetchAndDecode(fileUrl, track.id)
          .then(() => {
            this.prefetchInProgress.delete(track.id);
          })
          .catch(err => {
            console.warn('[WebAudio] Prefetch failed for track', nextIndex, err);
            this.prefetchInProgress.delete(track.id);
          });
      }
    }
  }

  private async handleTrackEnd(scheduledSource: ScheduledSource) {
    // Remove from scheduled sources
    const index = this.scheduledSources.indexOf(scheduledSource);
    if (index > -1) {
      this.scheduledSources.splice(index, 1);
    }

    // Only handle if this was the currently playing track
    if (scheduledSource.trackIndex !== this.currentTrackIndex) {
      return;
    }

    // Handle repeat one mode
    if (this.repeatMode === 'one') {
      console.info('[WebAudio] Repeat one - restarting track');
      await this.scheduleTrack(this.currentTrackIndex, this.audioContext.currentTime, true); // Prefer buffer for repeat
      return;
    }

    // Calculate next index
    let nextIndex = this.currentTrackIndex + 1;

    // Handle end of queue
    if (nextIndex >= this.tracks.length) {
      if (this.repeatMode === 'all') {
        console.info('[WebAudio] Repeat all - wrapping to start');
        nextIndex = 0;
      } else {
        console.info('[WebAudio] End of queue, stopping playback');
        this.isPlaying = false;
        this.stopSchedulingLoop();
        return;
      }
    }

    // Move to next track
    this.currentTrackIndex = nextIndex;

    // If next track is being scheduled, wait briefly for it to complete
    if (this.schedulingInProgress.has(nextIndex)) {
      console.info('[WebAudio] Next track', nextIndex, 'is being scheduled, waiting...');
      // Wait up to 500ms for scheduling to complete
      const maxWait = 500;
      const startWait = Date.now();
      while (this.schedulingInProgress.has(nextIndex) && (Date.now() - startWait) < maxWait) {
        await new Promise(resolve => setTimeout(resolve, 50));
      }
      if (this.schedulingInProgress.has(nextIndex)) {
        console.warn('[WebAudio] ⚠️  Next track', nextIndex, 'still being scheduled after timeout');
      }
    }

    // Check if next track is already scheduled
    const existingSchedule = this.scheduledSources.find(s => s.trackIndex === nextIndex);
    if (existingSchedule) {
      // Next track is already scheduled - let Web Audio's sample-accurate scheduling handle it
      // No need to reschedule - the AudioBufferSourceNode will start at the exact scheduled time
      const currentTime = this.audioContext.currentTime;
      const timeUntilStart = existingSchedule.startTime - currentTime;

      if (timeUntilStart > 0) {
        console.info(`[WebAudio] ✓ Track ${nextIndex} already scheduled, will start in ${(timeUntilStart * 1000).toFixed(0)}ms`);
      } else {
        console.info(`[WebAudio] ✓ Track ${nextIndex} already playing (sample-accurate transition)`);
      }
    } else {
      // Not scheduled yet, schedule immediately, try to use buffer if available
      console.info('[WebAudio] Track ended, scheduling next track:', nextIndex);
      await this.scheduleTrack(nextIndex, this.audioContext.currentTime, true);
    }

    // Notify UI
    if (this.trackChangeCallback) {
      this.trackChangeCallback(this.currentTrackIndex);
    }

    // Prefetch upcoming tracks for smooth transitions
    this.prefetchUpcomingTracks(this.currentTrackIndex);
  }

  private startSchedulingLoop() {
    if (this.schedulingRaf) return;

    const schedule = () => {
      if (!this.isPlaying) return;

      const currentTime = this.audioContext.currentTime;
      const lookAheadTime = 20; // Schedule 20 seconds ahead (increased from 5s to allow time for large file decoding)

      // Check if we need to schedule the next track
      const lastScheduled = this.scheduledSources[this.scheduledSources.length - 1];
      if (lastScheduled) {
        // Calculate end time based on source type
        let endTime: number;
        if (lastScheduled.sourceType === 'element' && lastScheduled.audioElement) {
          // For MediaElement: calculate based on element's current position and remaining time
          const remainingTime = lastScheduled.audioElement.duration - lastScheduled.audioElement.currentTime;
          endTime = currentTime + remainingTime;
        } else {
          // For AudioBuffer: use scheduled start time + duration
          endTime = lastScheduled.startTime + lastScheduled.duration;
        }
        const timeUntilEnd = endTime - currentTime;

        // Determine next index based on repeat mode
        let nextIndex = lastScheduled.trackIndex + 1;
        let shouldSchedule = false;

        if (nextIndex < this.tracks.length) {
          // Normal sequential playback
          shouldSchedule = true;
        } else if (this.repeatMode === 'all') {
          // Wrap to start with repeat-all
          nextIndex = 0;
          shouldSchedule = true;
        }
        // Note: repeat-one is handled in handleTrackEnd, not here

        // Schedule next track if we're within lookAheadTime
        if (shouldSchedule && timeUntilEnd < lookAheadTime) {
          // Check if not already scheduled or being scheduled
          const alreadyScheduled = this.scheduledSources.some(s => s.trackIndex === nextIndex);
          const schedulingInProgress = this.schedulingInProgress.has(nextIndex);

          if (!alreadyScheduled && !schedulingInProgress) {
            console.info('[WebAudio] Scheduling next track, time until current ends:', timeUntilEnd.toFixed(3), 's');

            // Prefer buffer for gapless transitions (we have time to decode)
            this.scheduleTrack(nextIndex, endTime, true).catch(err => {
              console.error('[WebAudio] Failed to schedule track', nextIndex, err);
              // Remove from in-progress on error
              this.schedulingInProgress.delete(nextIndex);
            });

            // Also prefetch the track after next for even smoother transitions
            this.prefetchUpcomingTracks(nextIndex);
          }
        }
      }

      this.schedulingRaf = requestAnimationFrame(schedule);
    };

    this.schedulingRaf = requestAnimationFrame(schedule);
  }

  private stopSchedulingLoop() {
    if (this.schedulingRaf) {
      cancelAnimationFrame(this.schedulingRaf);
      this.schedulingRaf = null;
    }
  }

  private clearScheduledSources() {
    // Stop all scheduled sources
    this.scheduledSources.forEach(s => {
      try {
        if (s.sourceType === 'buffer') {
          // AudioBufferSourceNode
          (s.source as AudioBufferSourceNode).onended = null;
          (s.source as AudioBufferSourceNode).stop();
        } else {
          // MediaElementAudioSourceNode
          if (s.audioElement) {
            s.audioElement.pause();
            s.audioElement.currentTime = 0;
            s.audioElement.src = ''; // Release resources
          }
        }
        s.source.disconnect();
      } catch (e) {
        // Already stopped
      }
    });
    this.scheduledSources = [];
  }

  play() {
    if (this.audioContext.state === 'suspended') {
      this.audioContext.resume();
    }

    // Resume any MediaElement sources
    this.scheduledSources.forEach(s => {
      if (s.sourceType === 'element' && s.audioElement && s.trackIndex === this.currentTrackIndex) {
        s.audioElement.play().catch(err => console.error('[WebAudio] MediaElement play failed:', err));
      }
    });

    this.isPlaying = true;
    this.startSchedulingLoop();
  }

  pause() {
    this.isPlaying = false;
    this.stopSchedulingLoop();

    // Pause any MediaElement sources
    this.scheduledSources.forEach(s => {
      if (s.sourceType === 'element' && s.audioElement) {
        s.audioElement.pause();
      }
    });

    this.audioContext.suspend();
  }

  async togglePlayPause() {
    if (this.isPlaying) {
      this.pause();
    } else {
      this.play();
    }
  }

  async seekTo(position: number) {
    console.info('[WebAudio] Seeking to position:', position.toFixed(2), 's');

    // Find current source
    const currentSource = this.scheduledSources.find(s => s.trackIndex === this.currentTrackIndex);

    // If using MediaElement, just seek within it
    if (currentSource?.sourceType === 'element' && currentSource.audioElement) {
      const clampedPosition = Math.max(0, Math.min(position, currentSource.duration));
      currentSource.audioElement.currentTime = clampedPosition;
      console.info('[WebAudio] Seek completed (MediaElement) to:', clampedPosition.toFixed(2), 's');
      return;
    }

    // For AudioBuffer sources or if not found, recreate the source
    // Stop all scheduled sources
    this.clearScheduledSources();

    // Get the current track
    const track = this.tracks[this.currentTrackIndex];
    if (!track) return;

    // Get the audio buffer (should be cached)
    const audioBuffer = this.audioBuffers.get(track.id);
    if (!audioBuffer) {
      console.warn('[WebAudio] Audio buffer not found for track:', track.id);
      return;
    }

    // Clamp position to valid range
    const clampedPosition = Math.max(0, Math.min(position, audioBuffer.duration));

    // Create new source starting from the seek position
    const source = this.audioContext.createBufferSource();
    source.buffer = audioBuffer;
    source.connect(this.gainNode);

    // Start from the seek position
    const startTime = this.audioContext.currentTime;
    source.start(startTime, clampedPosition);

    // Track the scheduled source
    const scheduledSource: ScheduledSource = {
      source,
      sourceType: 'buffer',
      startTime: startTime - clampedPosition, // Adjust startTime to account for seek offset
      duration: audioBuffer.duration,
      trackIndex: this.currentTrackIndex
    };

    this.scheduledSources.push(scheduledSource);

    // Handle track end
    source.onended = () => {
      console.info('[WebAudio] Track', this.currentTrackIndex, 'ended after seek');
      this.handleTrackEnd(scheduledSource);
    };

    // Resume scheduling if playing
    if (this.isPlaying) {
      this.startSchedulingLoop();
    }

    console.info('[WebAudio] Seek completed to:', clampedPosition.toFixed(2), 's');
  }

  async skip(direction: 1 | -1) {
    let newIndex = this.currentTrackIndex + direction;

    // Handle wrapping with repeat-all mode
    if (newIndex < 0) {
      if (this.repeatMode === 'all') {
        newIndex = this.tracks.length - 1;
      } else {
        return; // Can't skip backward from first track
      }
    } else if (newIndex >= this.tracks.length) {
      if (this.repeatMode === 'all') {
        newIndex = 0;
      } else {
        return; // Can't skip forward from last track
      }
    }

    this.clearScheduledSources();
    this.currentTrackIndex = newIndex;

    await this.scheduleTrack(newIndex, this.audioContext.currentTime);

    if (this.trackChangeCallback) {
      this.trackChangeCallback(newIndex);
    }

    // Prefetch upcoming tracks
    this.prefetchUpcomingTracks(newIndex);
  }

  async playFromIndex(index: number) {
    if (index < 0 || index >= this.tracks.length) return;

    this.clearScheduledSources();
    this.currentTrackIndex = index;

    await this.scheduleTrack(index, this.audioContext.currentTime);

    if (this.trackChangeCallback) {
      this.trackChangeCallback(index);
    }

    // Prefetch upcoming tracks
    this.prefetchUpcomingTracks(index);
  }

  getIsPlaying(): boolean {
    return this.isPlaying;
  }

  getCurrentTime(): number {
    // Calculate position based on scheduled sources
    if (this.scheduledSources.length === 0) return 0;

    const currentTime = this.audioContext.currentTime;
    const currentSource = this.scheduledSources.find(s =>
      s.trackIndex === this.currentTrackIndex
    );

    if (currentSource) {
      if (currentSource.sourceType === 'element' && currentSource.audioElement) {
        // For MediaElement, use the element's currentTime directly
        return currentSource.audioElement.currentTime;
      } else {
        // For AudioBuffer, calculate from Web Audio context time
        return currentTime - currentSource.startTime;
      }
    }

    return 0;
  }

  getDuration(): number {
    const currentSource = this.scheduledSources.find(s => s.trackIndex === this.currentTrackIndex);
    return currentSource?.duration || 0;
  }

  getVolume(): number {
    return this.gainNode.gain.value;
  }

  setVolume(volume: number) {
    this.gainNode.gain.value = Math.max(0, Math.min(1, volume));
  }

  getCurrentIndex(): number {
    return this.currentTrackIndex;
  }

  getCurrentTrack(): SimpleTrackDto | null {
    return this.tracks[this.currentTrackIndex] || null;
  }

  setTrackChangeCallback(callback: (index: number) => void) {
    this.trackChangeCallback = callback;
  }

  getRepeatMode(): RepeatMode {
    return this.repeatMode;
  }

  setRepeatMode(mode: RepeatMode) {
    console.info('[WebAudio] Setting repeat mode:', mode);
    this.repeatMode = mode;
  }

  destroy() {
    this.stopSchedulingLoop();
    this.clearScheduledSources();
    this.audioBuffers.clear();
    this.prefetchInProgress.clear();
    this.schedulingInProgress.clear();
    this.bufferDecodeInProgress.clear();
    this.audioContext.close();
  }
}

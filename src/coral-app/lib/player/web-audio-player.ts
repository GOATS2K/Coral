import type { SimpleTrackDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';
import type { RepeatMode } from '@/lib/state';

interface ScheduledSource {
  source: AudioBufferSourceNode;
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

  private async scheduleTrack(trackIndex: number, startTime: number) {
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

      // Fetch and decode the complete audio file
      const audioBuffer = await this.fetchAndDecode(fileUrl, track.id);

      // Create source node
      const source = this.audioContext.createBufferSource();
      source.buffer = audioBuffer;
      source.connect(this.gainNode);

      // Check for gaps - if startTime is in the past, we'll have a gap
      const currentTime = this.audioContext.currentTime;
      const timeDelta = startTime - currentTime;

      if (startTime < currentTime) {
        const gap = currentTime - startTime;
        console.warn('[WebAudio] ⚠️  GAP DETECTED:', gap.toFixed(3), 's - scheduled start time was in the past!');
        console.warn('[WebAudio] Track', trackIndex, 'will start immediately instead of at scheduled time');
      } else if (timeDelta < 0.5) {
        console.warn('[WebAudio] ⚠️  TIGHT TIMING:', timeDelta.toFixed(3), 's until scheduled start - may cause gap');
      } else {
        console.info('[WebAudio] ✓ Good timing:', timeDelta.toFixed(3), 's until scheduled start');
      }

      // Schedule playback
      const actualStartTime = Math.max(startTime, currentTime);
      source.start(actualStartTime);
      console.info('[WebAudio] ▶️  Track', trackIndex, 'playback started at', actualStartTime.toFixed(3));

      // Track when this source will end
      const scheduledSource: ScheduledSource = {
        source,
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
      await this.scheduleTrack(this.currentTrackIndex, this.audioContext.currentTime);
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
      // If scheduled to start in the future (gapless preload), clear it and reschedule immediately
      const currentTime = this.audioContext.currentTime;
      if (existingSchedule.startTime > currentTime) {
        console.info('[WebAudio] Track ended early, rescheduling next track immediately (was scheduled for:', existingSchedule.startTime.toFixed(3), ')');
        // Clear the pre-scheduled source
        existingSchedule.source.onended = null;
        try {
          existingSchedule.source.stop();
        } catch (e) {
          // Already stopped
        }
        const index = this.scheduledSources.indexOf(existingSchedule);
        if (index > -1) {
          this.scheduledSources.splice(index, 1);
        }
        // Schedule immediately
        await this.scheduleTrack(nextIndex, currentTime);
      } else {
        // Already playing (gapless transition worked)
        const transitionQuality = (currentTime >= existingSchedule.startTime && currentTime < existingSchedule.startTime + 0.05) ? '✓ PERFECT' : '✓ GOOD';
        console.info(`[WebAudio] ${transitionQuality} gapless transition to track`, nextIndex);
      }
    } else {
      // Not scheduled yet, schedule immediately
      console.info('[WebAudio] Track ended, scheduling next track:', nextIndex);
      await this.scheduleTrack(nextIndex, this.audioContext.currentTime);
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
        const endTime = lastScheduled.startTime + lastScheduled.duration;
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

            // Schedule in background - don't await to avoid blocking the loop
            this.scheduleTrack(nextIndex, endTime).catch(err => {
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
        // Remove the onended callback to prevent it from firing during cleanup
        s.source.onended = null;
        s.source.stop();
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
    this.isPlaying = true;
    this.startSchedulingLoop();
  }

  pause() {
    this.isPlaying = false;
    this.stopSchedulingLoop();
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
      currentTime >= s.startTime && currentTime < s.startTime + s.duration
    );

    if (currentSource) {
      return currentTime - currentSource.startTime;
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
    this.audioContext.close();
  }
}

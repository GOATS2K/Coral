import type { SimpleTrackDto } from '@/lib/client/schemas';
import type { RepeatMode } from '@/lib/state';
import EventEmitter from 'eventemitter3';
import { ProgressiveAudioLoader } from './progressive-audio-loader';

interface ScheduledSource {
  source: AudioBufferSourceNode;
  startTime: number;
  duration: number;
  trackIndex: number;
  trackId: string; // Stable track identifier
  chunkIndex: number;
  isLastChunk: boolean;
}

interface ScheduledTrackMetadata {
  trackId: string;
  trackIndex: number;
  startTime: number; // AudioContext time when first chunk starts
  duration: number; // Total duration from HLS playlist (sum of all fragment durations)
  isPlaylistComplete: boolean; // True when #EXT-X-ENDLIST is present (live: false)
}

export interface PlayerEvents {
  playbackStateChanged: { isPlaying: boolean };
  trackChanged: { index: number };
  bufferingStateChanged: { isBuffering: boolean };
}

// Event name constants
export const PlayerEventNames = {
  PLAYBACK_STATE_CHANGED: 'playbackStateChanged',
  TRACK_CHANGED: 'trackChanged',
  BUFFERING_STATE_CHANGED: 'bufferingStateChanged',
} as const;

export class WebAudioPlayer extends EventEmitter<PlayerEvents> {
  private audioContext: AudioContext;
  private gainNode: GainNode;
  private tracks: SimpleTrackDto[] = [];
  private currentTrackIndex = 0;
  private scheduledSources: ScheduledSource[] = [];
  private scheduledTracks: Map<string, ScheduledTrackMetadata> = new Map(); // Track metadata by trackId
  private isPlaying = false;
  private trackChangeCallback: ((index: number) => void) | null = null;
  private repeatMode: RepeatMode = 'off';
  private progressiveLoader: ProgressiveAudioLoader;
  private schedulingInProgress: Set<number> = new Set();
  private currentTrackLoaded: boolean = false;

  constructor() {
    super(); // Initialize EventEmitter
    this.audioContext = new AudioContext();
    this.gainNode = this.audioContext.createGain();
    this.gainNode.connect(this.audioContext.destination);
    this.progressiveLoader = new ProgressiveAudioLoader(this.audioContext);
  }

  async loadQueue(tracks: SimpleTrackDto[], startIndex: number = 0, clearCache: boolean = true) {
    this.tracks = tracks;
    this.currentTrackIndex = startIndex;
    this.clearScheduledSources();

    if (clearCache) {
      this.progressiveLoader.clearAllCaches();
    }

    this.isPlaying = false;
    this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: false });

    if (this.audioContext.state !== 'suspended' && this.audioContext.state !== 'closed') {
      await this.audioContext.suspend();
    }

    await this.scheduleTrack(startIndex, this.audioContext.currentTime);
  }

  updateQueue(tracks: SimpleTrackDto[], currentIndex: number) {
    this.tracks = tracks;
    this.currentTrackIndex = currentIndex;
  }

  private async scheduleTrack(trackIndex: number, startTime: number) {
    if (trackIndex >= this.tracks.length) return;

    if (this.schedulingInProgress.has(trackIndex)) {
      console.warn('[WebAudio] ⚠️  Skipping schedule for track', trackIndex, '- already in progress');
      return;
    }

    this.schedulingInProgress.add(trackIndex);

    const isCurrentTrack = trackIndex === this.currentTrackIndex;

    if (isCurrentTrack) {
      this.currentTrackLoaded = false;
      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: true });
    }

    try {
      const track = this.tracks[trackIndex];
      console.info('[WebAudio] Scheduling track', trackIndex, track.title, 'at', startTime.toFixed(3));

      // Initialize track metadata
      const initialDuration = this.progressiveLoader.getTrackDuration(track.id) || 0;
      const isComplete = this.progressiveLoader.isPlaylistComplete(track.id);

      this.scheduledTracks.set(track.id, {
        trackId: track.id,
        trackIndex,
        startTime,
        duration: initialDuration,
        isPlaylistComplete: isComplete,
      });

      console.info('[WebAudio] 📊 Initial metadata: duration', initialDuration.toFixed(2), 's, complete:', isComplete);

      let chunkIndex = 0;
      let currentChunkStartTime = startTime;
      let firstChunkScheduled = false;

      // Load and schedule chunks progressively
      for await (const audioBuffer of this.progressiveLoader.loadTrack(track.id)) {
        // Check if scheduling was aborted
        if (isCurrentTrack && trackIndex !== this.currentTrackIndex) {
          console.info('[WebAudio] Current track changed during fetch (was scheduling', trackIndex, ', now at', this.currentTrackIndex, '), aborting');
          return;
        }

        if (this.tracks[trackIndex]?.id !== track.id) {
          console.info('[WebAudio] Track at index', trackIndex, 'changed during fetch, aborting');
          return;
        }

        // Create and schedule chunk source
        const source = this.audioContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(this.gainNode);

        // Resume audio context on first chunk
        if (!firstChunkScheduled && this.audioContext.state === 'suspended') {
          await this.audioContext.resume();
          this.isPlaying = true;
          this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: true });
        }

        // Check timing on first chunk
        if (!firstChunkScheduled) {
          const currentTime = this.audioContext.currentTime;
          const timeDelta = currentChunkStartTime - currentTime;

          if (currentChunkStartTime < currentTime) {
            const gap = currentTime - currentChunkStartTime;
            console.warn('[WebAudio] ⚠️  GAP DETECTED:', gap.toFixed(3), 's - scheduled start time was in the past!');
            console.warn('[WebAudio] Track', trackIndex, 'will start immediately instead of at scheduled time');
          } else if (timeDelta < 0.5) {
            console.warn('[WebAudio] ⚠️  TIGHT TIMING:', timeDelta.toFixed(3), 's until scheduled start - may cause gap');
          } else {
            console.info('[WebAudio] ✓ Good timing:', timeDelta.toFixed(3), 's until scheduled start');
          }
        }

        const actualStartTime = Math.max(currentChunkStartTime, this.audioContext.currentTime);
        source.start(actualStartTime);

        if (!firstChunkScheduled) {
          console.info('[WebAudio] ▶️  Track', trackIndex, 'chunk', chunkIndex, 'playback started at', actualStartTime.toFixed(3));
          firstChunkScheduled = true;
        }

        // We don't know if this is the last chunk yet, so we'll mark it false for now
        // and update it after the loop if needed
        const scheduledSource: ScheduledSource = {
          source,
          startTime: actualStartTime,
          duration: audioBuffer.duration,
          trackIndex,
          trackId: track.id,
          chunkIndex,
          isLastChunk: false, // Will be updated for last chunk
        };

        this.scheduledSources.push(scheduledSource);

        // Update start time for next chunk
        currentChunkStartTime = actualStartTime + audioBuffer.duration;
        chunkIndex++;

        // Emit buffering complete after first chunk
        if (chunkIndex === 1 && isCurrentTrack) {
          this.currentTrackLoaded = true;
          this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: false });
          console.info('[WebAudio] 🔄 First chunk loaded');
        }
      }

      // Mark the last chunk
      const trackSources = this.scheduledSources.filter(s => s.trackIndex === trackIndex);
      if (trackSources.length > 0) {
        const lastSource = trackSources[trackSources.length - 1];
        lastSource.isLastChunk = true;

        // Add onended handler only to last chunk
        lastSource.source.onended = () => {
          this.handleTrackEnd(lastSource);
        };

        const totalDuration = currentChunkStartTime - startTime;
        console.info('[WebAudio] ✅ Scheduled track', trackIndex, 'with', chunkIndex, 'chunks');
        console.info('[WebAudio]   📊 Total duration:', totalDuration.toFixed(2), 's (~' + (totalDuration / 60).toFixed(1), 'min)');
        console.info('[WebAudio]   ⏰ Starts at:', startTime.toFixed(3), 's, ends at:', currentChunkStartTime.toFixed(3), 's');
      }

      // Update metadata with final duration and playlist completion status
      const finalDuration = this.progressiveLoader.getTrackDuration(track.id) || 0;
      const playlistComplete = this.progressiveLoader.isPlaylistComplete(track.id);

      this.scheduledTracks.set(track.id, {
        trackId: track.id,
        trackIndex,
        startTime,
        duration: finalDuration,
        isPlaylistComplete: playlistComplete,
      });

      console.info('[WebAudio] 📊 Final metadata: duration', finalDuration.toFixed(2), 's, playlist complete:', playlistComplete);

      if (trackIndex === this.currentTrackIndex) {
        // Start prefetching upcoming tracks in background
        console.info('[WebAudio] 🔄 Track loaded, prefetching next track');
        this.prefetchNextTrack(trackIndex);

        // Only schedule next track if current track's playlist is complete
        if (playlistComplete) {
          console.info('[WebAudio] 🎯 Playlist complete, scheduling next track for gapless playback');
          this.scheduleNextTrackIfNeeded(trackIndex);
        } else {
          console.info('[WebAudio] ⏳ Playlist still transcoding, will schedule next track when complete');
        }
      }
    } finally {
      this.schedulingInProgress.delete(trackIndex);
    }
  }

  private cancelBuffering(trackId: string) {
    this.progressiveLoader.cancelTrack(trackId);
  }

  private getNextIndex(currentIndex: number, direction: 1 | -1 = 1): number | null {
    let nextIndex = currentIndex + direction;

    if (nextIndex < 0) {
      if (this.repeatMode === 'all') {
        return this.tracks.length - 1;
      }
      return null;
    }

    if (nextIndex >= this.tracks.length) {
      if (this.repeatMode === 'all') {
        return 0;
      }
      return null;
    }

    return nextIndex;
  }

  private removeScheduledSource(scheduledSource: ScheduledSource) {
    const index = this.scheduledSources.indexOf(scheduledSource);
    if (index > -1) {
      this.scheduledSources.splice(index, 1);
    }
  }

  private async waitForSchedulingComplete(trackIndex: number, trackTitle?: string): Promise<void> {
    if (!this.schedulingInProgress.has(trackIndex)) {
      return;
    }

    console.info('[WebAudio] ⏳ Next track', trackIndex, trackTitle, 'is being scheduled, waiting...');

    const maxWait = 500;
    const startWait = Date.now();

    while (this.schedulingInProgress.has(trackIndex) && (Date.now() - startWait) < maxWait) {
      await new Promise(resolve => setTimeout(resolve, 50));
    }

    if (this.schedulingInProgress.has(trackIndex)) {
      console.warn('[WebAudio] ⚠️  Next track', trackIndex, 'still being scheduled after timeout');
    }
  }

  private async handleGaplessTransition(nextIndex: number, existingSchedule: ScheduledSource | undefined): Promise<void> {
    const currentTime = this.audioContext.currentTime;
    const nextTrack = this.tracks[nextIndex];

    if (existingSchedule) {
      // If scheduled to start in the future (gapless preload), check timing
      if (existingSchedule.startTime > currentTime) {
        const scheduledGap = existingSchedule.startTime - currentTime;
        console.warn('[WebAudio] ⚠️  Track ended EARLY! Next was scheduled for', existingSchedule.startTime.toFixed(3), 's, but it\'s only', currentTime.toFixed(3), 's now');
        console.warn('[WebAudio]   ⏸️  Gap would be:', scheduledGap.toFixed(3), 's - rescheduling immediately');

        // Clear the pre-scheduled source
        existingSchedule.source.onended = null;
        try {
          existingSchedule.source.stop();
        } catch {
          // Already stopped
        }
        this.removeScheduledSource(existingSchedule);

        // Schedule immediately
        await this.scheduleTrack(nextIndex, currentTime);
      } else {
        // Already playing (gapless transition worked)
        const transitionTiming = currentTime - existingSchedule.startTime;
        let transitionQuality = '✅ PERFECT';
        if (transitionTiming > 0.05) {
          transitionQuality = '✅ GOOD';
        }
        if (transitionTiming > 0.1) {
          transitionQuality = '⚠️  LATE';
        }
        console.info(`[WebAudio] ${transitionQuality} gapless transition to track`, nextIndex, nextTrack?.title);
        console.info('[WebAudio]   🎵 Next track started:', existingSchedule.startTime.toFixed(3), 's, now:', currentTime.toFixed(3), 's, timing:', transitionTiming.toFixed(3), 's');

        // Schedule the next track to maintain gapless playback chain
        console.info('[WebAudio] 🔄 Gapless transition successful, prefetching and scheduling next track');
        this.prefetchNextTrack(nextIndex);
        this.scheduleNextTrackIfNeeded(nextIndex);
      }
    } else {
      // Not scheduled yet, schedule immediately - this indicates a GAP
      console.warn('[WebAudio] ⚠️  GAP! Next track', nextIndex, 'was NOT pre-scheduled, scheduling now');
      await this.scheduleTrack(nextIndex, this.audioContext.currentTime);

      // After reactive scheduling, ensure we schedule the next track
      console.info('[WebAudio] 🔄 Reactive scheduling complete, prefetching and scheduling next track');
      this.prefetchNextTrack(nextIndex);
      this.scheduleNextTrackIfNeeded(nextIndex);
    }
  }

  private async playTrackAtIndex(index: number): Promise<void> {
    this.clearScheduledSources();

    const bufferingIndexes = Array.from(this.schedulingInProgress);
    for (const bufferingIndex of bufferingIndexes) {
      const track = this.tracks[bufferingIndex];
      if (track) {
        this.cancelBuffering(track.id);
        this.schedulingInProgress.delete(bufferingIndex);
      }
    }

    this.currentTrackIndex = index;

    this.isPlaying = false;
    this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: false });

    if (this.audioContext.state !== 'suspended' && this.audioContext.state !== 'closed') {
      await this.audioContext.suspend();
    }

    await this.scheduleTrack(index, this.audioContext.currentTime);

    if (this.trackChangeCallback) {
      this.trackChangeCallback(index);
    }

    this.emit(PlayerEventNames.TRACK_CHANGED, { index });
  }

  private scheduleNextTrackIfNeeded(currentIndex: number) {
    const nextIndex = this.getNextIndex(currentIndex);

    if (nextIndex === null) {
      console.info('[WebAudio] End of queue, no next track to schedule');
      return;
    }

    if (this.scheduledSources.some(s => s.trackIndex === nextIndex)) {
      console.info('[WebAudio] Track', nextIndex, 'already scheduled, skipping');
      return;
    }

    if (this.schedulingInProgress.has(nextIndex)) {
      console.info('[WebAudio] Track', nextIndex, 'scheduling in progress, skipping');
      return;
    }

    const currentTrack = this.tracks[currentIndex];
    if (!currentTrack) {
      console.warn('[WebAudio] Cannot find current track at index', currentIndex);
      return;
    }

    // Get metadata for current track
    const metadata = this.scheduledTracks.get(currentTrack.id);
    if (!metadata) {
      console.warn('[WebAudio] Cannot find metadata for track', currentIndex, currentTrack.id);
      return;
    }

    // Only schedule next track if current track's playlist is complete
    if (!metadata.isPlaylistComplete) {
      console.info('[WebAudio] ⏳ Current track playlist not complete yet, cannot schedule next track');
      return;
    }

    // Calculate end time from metadata
    const endTime = metadata.startTime + metadata.duration;
    const nextTrack = this.tracks[nextIndex];

    console.info('[WebAudio] 📅 Scheduling next track', nextIndex, nextTrack?.title, 'to start at', endTime.toFixed(3), 's');

    this.scheduleTrack(nextIndex, endTime).catch(err => {
      console.error('[WebAudio] ❌ Failed to schedule next track', nextIndex, err);
      this.schedulingInProgress.delete(nextIndex);
    });
  }

  private prefetchNextTrack(currentIndex: number) {
    const nextIndex = this.getNextIndex(currentIndex);
    if (nextIndex === null) return;

    const nextTrack = this.tracks[nextIndex];
    if (!nextTrack) return;

    // Prefetch first few chunks of next track
    console.info('[WebAudio] Prefetching first 3 chunks of track', nextIndex, nextTrack.title);
    this.progressiveLoader.prefetchChunks(nextTrack.id, 0, 3).catch(err => {
      console.warn('[WebAudio] Prefetch failed for track', nextIndex, err);
    });
  }

  private async handleTrackEnd(scheduledSource: ScheduledSource) {
    const currentTime = this.audioContext.currentTime;
    const expectedEndTime = scheduledSource.startTime + scheduledSource.duration;
    const timingDelta = currentTime - expectedEndTime;

    console.info('[WebAudio] 🏁 Track', scheduledSource.trackIndex, 'ended');
    console.info('[WebAudio]   ⏱️  Expected end:', expectedEndTime.toFixed(3), 's, Actual:', currentTime.toFixed(3), 's, Delta:', timingDelta.toFixed(3), 's');

    this.removeScheduledSource(scheduledSource);

    if (scheduledSource.trackIndex !== this.currentTrackIndex) {
      console.info('[WebAudio]   ℹ️  Not current track, ignoring');
      return;
    }

    if (this.repeatMode === 'one') {
      console.info('[WebAudio] 🔁 Repeat one - restarting track');
      await this.scheduleTrack(this.currentTrackIndex, this.audioContext.currentTime);
      return;
    }

    const nextIndex = this.getNextIndex(this.currentTrackIndex);

    if (nextIndex === null) {
      console.info('[WebAudio] 🛑 End of queue, stopping playback');
      this.isPlaying = false;
      this.emit('playbackStateChanged', { isPlaying: false });
      return;
    }

    if (nextIndex === 0 && this.repeatMode === 'all') {
      console.info('[WebAudio] 🔁 Repeat all - wrapping to start');
    }

    this.currentTrackIndex = nextIndex;
    const nextTrack = this.tracks[nextIndex];

    await this.waitForSchedulingComplete(nextIndex, nextTrack?.title);

    const existingSchedule = this.scheduledSources.find(s => s.trackIndex === nextIndex);
    await this.handleGaplessTransition(nextIndex, existingSchedule);

    if (this.trackChangeCallback) {
      this.trackChangeCallback(this.currentTrackIndex);
    }

    this.emit('trackChanged', { index: this.currentTrackIndex });
  }


  private clearScheduledSources(clearMetadata: boolean = true) {
    this.scheduledSources.forEach(s => {
      try {
        s.source.onended = null;
        s.source.stop();
      } catch {
        // Already stopped
      }
    });
    this.scheduledSources = [];

    // Only clear metadata when loading a new queue or switching tracks
    if (clearMetadata) {
      this.scheduledTracks.clear();
    }
  }

  async play() {
    if (this.audioContext.state === 'suspended') {
      await this.audioContext.resume();
    }
    this.isPlaying = true;
    this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: true });
  }

  async pause() {
    this.isPlaying = false;
    if (this.audioContext.state !== 'suspended' && this.audioContext.state !== 'closed') {
      await this.audioContext.suspend();
    }
    this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: false });
  }

  async togglePlayPause() {
    if (this.isPlaying) {
      await this.pause();
    } else {
      await this.play();
    }
  }

  async seekTo(position: number) {
    // Clear scheduled sources but preserve track metadata
    this.clearScheduledSources(false);

    const track = this.tracks[this.currentTrackIndex];
    if (!track) return;

    console.info('[WebAudio] Seeking to', position.toFixed(2), 's in track', track.title);

    // Find which chunk contains this position
    const targetChunkIndex = await this.progressiveLoader.getChunkIndexForPosition(track.id, position);
    if (targetChunkIndex === null) {
      console.warn('[WebAudio] Could not determine chunk for seek position');
      return;
    }

    console.info('[WebAudio] Seek target is chunk', targetChunkIndex);

    // Fetch the target chunk (will use cache if available)
    const targetChunk = await this.progressiveLoader.getChunk(track.id, targetChunkIndex);
    if (!targetChunk) {
      console.warn('[WebAudio] Could not load target chunk');
      return;
    }

    // Calculate offset within the chunk
    // Each chunk is ~10 seconds, so offset = position % 10 (approximately)
    const chunkStartTime = targetChunkIndex * 10; // Rough estimate
    const offsetWithinChunk = Math.max(0, position - chunkStartTime);
    const clampedOffset = Math.min(offsetWithinChunk, targetChunk.duration);

    console.info('[WebAudio] 🎯 Seek target breakdown:');
    console.info('[WebAudio]   Chunk', targetChunkIndex, 'starts at ~' + chunkStartTime.toFixed(1), 's');
    console.info('[WebAudio]   Offset within chunk:', clampedOffset.toFixed(2), 's');
    console.info('[WebAudio]   Chunk duration:', targetChunk.duration.toFixed(2), 's');

    // Schedule the target chunk with offset
    const source = this.audioContext.createBufferSource();
    source.buffer = targetChunk;
    source.connect(this.gainNode);

    const startTime = this.audioContext.currentTime;
    source.start(startTime, clampedOffset);

    const scheduledSource: ScheduledSource = {
      source,
      startTime: startTime - clampedOffset,
      duration: targetChunk.duration,
      trackIndex: this.currentTrackIndex,
      trackId: track.id,
      chunkIndex: targetChunkIndex,
      isLastChunk: false, // Will be updated if needed
    };

    this.scheduledSources.push(scheduledSource);

    // Schedule subsequent chunks
    let nextChunkStartTime = startTime + (targetChunk.duration - clampedOffset);
    let nextChunkIndex = targetChunkIndex + 1;

    // Prefetch and schedule next few chunks
    this.progressiveLoader.prefetchChunks(track.id, nextChunkIndex, 5).then(async () => {
      // Schedule the prefetched chunks
      for (let i = nextChunkIndex; i < nextChunkIndex + 5; i++) {
        const chunk = await this.progressiveLoader.getChunk(track.id, i);
        if (!chunk) break;

        const chunkSource = this.audioContext.createBufferSource();
        chunkSource.buffer = chunk;
        chunkSource.connect(this.gainNode);

        chunkSource.start(nextChunkStartTime);

        const isLastChunk = i === await this.progressiveLoader.getChunkIndexForPosition(track.id, Infinity) ?? false;

        const chunkScheduledSource: ScheduledSource = {
          source: chunkSource,
          startTime: nextChunkStartTime,
          duration: chunk.duration,
          trackIndex: this.currentTrackIndex,
          trackId: track.id,
          chunkIndex: i,
          isLastChunk,
        };

        if (isLastChunk) {
          chunkSource.onended = () => {
            this.handleTrackEnd(chunkScheduledSource);
          };
        }

        this.scheduledSources.push(chunkScheduledSource);
        nextChunkStartTime += chunk.duration;
      }
    }).catch(err => {
      console.warn('[WebAudio] Failed to schedule chunks after seek:', err);
    });

    // Don't schedule next track here - it will be scheduled automatically
    // when the current track's playlist is complete
  }

  async skip(direction: 1 | -1) {
    const newIndex = this.getNextIndex(this.currentTrackIndex, direction);

    if (newIndex === null) {
      return;
    }

    await this.playTrackAtIndex(newIndex);
  }

  async playFromIndex(index: number) {
    if (index < 0 || index >= this.tracks.length) return;

    await this.playTrackAtIndex(index);
  }

  getIsPlaying(): boolean {
    return this.isPlaying;
  }

  getCurrentTime(): number {
    const currentTrack = this.tracks[this.currentTrackIndex];
    if (!currentTrack) return 0;

    if (this.scheduledSources.length === 0) return 0;

    const currentTime = this.audioContext.currentTime;

    // Find all chunks for current track
    const trackChunks = this.scheduledSources
      .filter(s => s.trackIndex === this.currentTrackIndex)
      .sort((a, b) => a.chunkIndex - b.chunkIndex);

    if (trackChunks.length === 0) return 0;

    // Find which chunk is currently playing
    const currentChunk = trackChunks.find(s =>
      currentTime >= s.startTime && currentTime < s.startTime + s.duration
    );

    if (currentChunk) {
      // Calculate position within current chunk
      const positionInChunk = currentTime - currentChunk.startTime;

      // Get cumulative time before this chunk from the HLS playlist
      // This accounts for seeks where not all chunks are scheduled
      const cumulativeTime = this.progressiveLoader.getCumulativeTimeBeforeChunk(
        currentTrack.id,
        currentChunk.chunkIndex
      );

      if (cumulativeTime !== null) {
        return cumulativeTime + positionInChunk;
      }

      // Fallback: sum scheduled chunks (only accurate if all chunks are scheduled from start)
      let positionFromStart = 0;
      for (const chunk of trackChunks) {
        if (chunk.chunkIndex < currentChunk.chunkIndex) {
          positionFromStart += chunk.duration;
        }
      }
      positionFromStart += positionInChunk;

      return positionFromStart;
    }

    return 0;
  }

  getDuration(): number {
    const currentTrack = this.tracks[this.currentTrackIndex];
    if (!currentTrack) return 0;

    // Get duration from metadata (from HLS playlist)
    const metadata = this.scheduledTracks.get(currentTrack.id);
    if (metadata && metadata.duration > 0) {
      return metadata.duration;
    }

    // Fallback: sum all chunk durations for current track
    const trackChunks = this.scheduledSources.filter(s => s.trackIndex === this.currentTrackIndex);
    if (trackChunks.length === 0) return 0;

    return trackChunks.reduce((total, chunk) => total + chunk.duration, 0);
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
    this.repeatMode = mode;
  }

  checkAndScheduleNext() {
    this.scheduleNextTrackIfNeeded(this.currentTrackIndex);
  }

  destroy() {
    this.clearScheduledSources();
    this.schedulingInProgress.clear();
    this.progressiveLoader.clearAllCaches();
    this.audioContext.close();
  }
}

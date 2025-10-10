import type { SimpleTrackDto } from '@/lib/client/schemas';
import { baseUrl } from '@/lib/client/fetcher';
import type { RepeatMode } from '@/lib/state';
import EventEmitter from 'eventemitter3';

interface ScheduledSource {
  source: AudioBufferSourceNode;
  startTime: number;
  duration: number;
  trackIndex: number;
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
  private isPlaying = false;
  private trackChangeCallback: ((index: number) => void) | null = null;
  private repeatMode: RepeatMode = 'off';
  private audioBuffers: Map<string, AudioBuffer> = new Map();
  private schedulingInProgress: Set<number> = new Set();
  private prefetchInProgress: Set<string> = new Set();
  private currentTrackLoaded: boolean = false;
  private abortControllers: Map<string, AbortController> = new Map();

  constructor() {
    super(); // Initialize EventEmitter
    this.audioContext = new AudioContext();
    this.gainNode = this.audioContext.createGain();
    this.gainNode.connect(this.audioContext.destination);
  }

  async loadQueue(tracks: SimpleTrackDto[], startIndex: number = 0, clearCache: boolean = true) {
    this.tracks = tracks;
    this.currentTrackIndex = startIndex;
    this.clearScheduledSources();

    if (clearCache) {
      this.clearAudioCache();
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

    const trackIdsInQueue = new Set(tracks.map(t => t.id));
    this.pruneCache(trackIdsInQueue);
  }

  clearAudioCache() {
    const cacheSize = this.audioBuffers.size;
    if (cacheSize > 0) {
      console.info('[WebAudio] Clearing audio cache, removing', cacheSize, 'cached tracks');
      this.audioBuffers.clear();
      this.prefetchInProgress.clear();
    }
  }

  private pruneCache(validTrackIds: Set<string>) {
    const cachedTrackIds = Array.from(this.audioBuffers.keys());

    for (const cachedId of cachedTrackIds) {
      if (!validTrackIds.has(cachedId)) {
        console.info('[WebAudio] Removing cached track no longer in queue:', cachedId);
        this.audioBuffers.delete(cachedId);
        this.prefetchInProgress.delete(cachedId);
      }
    }
  }

  private pruneOldCacheEntries() {
    const keepTrackIds = new Set<string>();

    // Keep previous track (if exists)
    if (this.currentTrackIndex > 0) {
      const prevTrack = this.tracks[this.currentTrackIndex - 1];
      if (prevTrack) keepTrackIds.add(prevTrack.id);
    }

    // Keep current track
    const currentTrack = this.tracks[this.currentTrackIndex];
    if (currentTrack) keepTrackIds.add(currentTrack.id);

    // Keep next track (if exists)
    if (this.currentTrackIndex < this.tracks.length - 1) {
      const nextTrack = this.tracks[this.currentTrackIndex + 1];
      if (nextTrack) keepTrackIds.add(nextTrack.id);
    }

    // Evict everything else
    const cachedIds = Array.from(this.audioBuffers.keys());
    for (const cachedId of cachedIds) {
      if (!keepTrackIds.has(cachedId)) {
        console.info('[WebAudio] Evicting cache for old track:', cachedId);
        this.audioBuffers.delete(cachedId);
        this.prefetchInProgress.delete(cachedId);
      }
    }
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

      const fileUrl = `${baseUrl}/api/library/tracks/${track.id}/original`;
      const audioBuffer = await this.fetchAndDecode(fileUrl, track.id);

      // Only abort if we were scheduling the current track and it changed
      // Allow pre-scheduling of next tracks even if current index changes
      if (isCurrentTrack && trackIndex !== this.currentTrackIndex) {
        console.info('[WebAudio] Current track changed during fetch (was scheduling', trackIndex, ', now at', this.currentTrackIndex, '), aborting');
        return;
      }

      // Check if the track at this index is still the same track (in case queue was modified)
      if (this.tracks[trackIndex]?.id !== track.id) {
        console.info('[WebAudio] Track at index', trackIndex, 'changed during fetch, aborting');
        return;
      }

      const source = this.audioContext.createBufferSource();
      source.buffer = audioBuffer;
      source.connect(this.gainNode);

      if (this.audioContext.state === 'suspended') {
        await this.audioContext.resume();
        this.isPlaying = true;
        this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: true });
      }

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

      const actualStartTime = Math.max(startTime, this.audioContext.currentTime);
      source.start(actualStartTime);
      console.info('[WebAudio] ▶️  Track', trackIndex, 'playback started at', actualStartTime.toFixed(3));

      const scheduledSource: ScheduledSource = {
        source,
        startTime: actualStartTime,
        duration: audioBuffer.duration,
        trackIndex
      };

      this.scheduledSources.push(scheduledSource);

      source.onended = () => {
        this.handleTrackEnd(scheduledSource);
      };

      console.info('[WebAudio] Scheduled track', trackIndex, 'duration:', audioBuffer.duration.toFixed(2), 's');

      if (trackIndex === this.currentTrackIndex) {
        this.currentTrackLoaded = true;
        this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: false });
        console.info('[WebAudio] 🔄 Current track loaded, prefetching upcoming tracks');
        this.prefetchUpcomingTracks(trackIndex);

        // Immediately schedule the next track for gapless playback
        console.info('[WebAudio] 🎯 Current track playing, scheduling next track immediately');
        this.scheduleNextTrackIfNeeded(trackIndex);
      }
    } finally {
      this.schedulingInProgress.delete(trackIndex);
    }
  }

  private async fetchAndDecode(url: string, trackId: string): Promise<AudioBuffer> {
    const cached = this.audioBuffers.get(trackId);
    if (cached) {
      console.info('[WebAudio] Using cached audio buffer for track:', trackId);
      return cached;
    }

    console.info('[WebAudio] Fetching audio file:', url);
    const controller = new AbortController();
    this.abortControllers.set(trackId, controller);

    try {
      const audioResponse = await fetch(url, { signal: controller.signal });
      const arrayBuffer = await audioResponse.arrayBuffer();

      console.info('[WebAudio] Decoding audio, size:', (arrayBuffer.byteLength / 1024 / 1024).toFixed(2), 'MB');
      const audioBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
      console.info('[WebAudio] Decoded, duration:', audioBuffer.duration.toFixed(2), 's');

      this.audioBuffers.set(trackId, audioBuffer);
      return audioBuffer;
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        console.info('[WebAudio] Fetch aborted for track:', trackId);
        throw error;
      }
      console.error('[WebAudio] Error fetching/decoding track:', trackId, error);
      throw error;
    } finally {
      this.abortControllers.delete(trackId);
    }
  }

  private cancelBuffering(trackId: string) {
    const controller = this.abortControllers.get(trackId);
    if (controller) {
      controller.abort();
      this.abortControllers.delete(trackId);
    }
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
        this.prefetchUpcomingTracks(nextIndex);
        this.scheduleNextTrackIfNeeded(nextIndex);
      }
    } else {
      // Not scheduled yet, schedule immediately - this indicates a GAP
      console.warn('[WebAudio] ⚠️  GAP! Next track', nextIndex, 'was NOT pre-scheduled, scheduling now');
      await this.scheduleTrack(nextIndex, this.audioContext.currentTime);

      // After reactive scheduling, ensure we schedule the next track
      console.info('[WebAudio] 🔄 Reactive scheduling complete, prefetching and scheduling next track');
      this.prefetchUpcomingTracks(nextIndex);
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

    this.pruneOldCacheEntries();

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

    const currentSource = this.scheduledSources.find(s => s.trackIndex === currentIndex);
    if (!currentSource) {
      console.warn('[WebAudio] Cannot find current source for track', currentIndex);
      return;
    }

    const endTime = currentSource.startTime + currentSource.duration;
    const nextTrack = this.tracks[nextIndex];

    console.info('[WebAudio] 📅 Scheduling next track', nextIndex, nextTrack?.title, 'to start at', endTime.toFixed(3), 's');

    this.scheduleTrack(nextIndex, endTime).catch(err => {
      console.error('[WebAudio] ❌ Failed to schedule next track', nextIndex, err);
      this.schedulingInProgress.delete(nextIndex);
    });
  }

  private prefetchUpcomingTracks(startIndex: number) {
    for (let i = 1; i <= 2; i++) {
      let nextIndex = startIndex + i;

      if (nextIndex >= this.tracks.length && this.repeatMode === 'all') {
        nextIndex = nextIndex % this.tracks.length;
      }

      if (nextIndex < this.tracks.length) {
        const track = this.tracks[nextIndex];

        if (this.audioBuffers.has(track.id)) {
          continue;
        }
        if (this.prefetchInProgress.has(track.id)) {
          continue;
        }

        const fileUrl = `${baseUrl}/api/library/tracks/${track.id}/original`;
        console.info('[WebAudio] Prefetching track', nextIndex, track.title);

        this.prefetchInProgress.add(track.id);

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

    this.pruneOldCacheEntries();
    await this.waitForSchedulingComplete(nextIndex, nextTrack?.title);

    const existingSchedule = this.scheduledSources.find(s => s.trackIndex === nextIndex);
    await this.handleGaplessTransition(nextIndex, existingSchedule);

    if (this.trackChangeCallback) {
      this.trackChangeCallback(this.currentTrackIndex);
    }

    this.emit('trackChanged', { index: this.currentTrackIndex });
  }


  private clearScheduledSources() {
    this.scheduledSources.forEach(s => {
      try {
        s.source.onended = null;
        s.source.stop();
      } catch {
        // Already stopped
      }
    });
    this.scheduledSources = [];
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
    this.clearScheduledSources();

    const track = this.tracks[this.currentTrackIndex];
    if (!track) return;

    const audioBuffer = this.audioBuffers.get(track.id);
    if (!audioBuffer) {
      return;
    }

    const clampedPosition = Math.max(0, Math.min(position, audioBuffer.duration));

    const source = this.audioContext.createBufferSource();
    source.buffer = audioBuffer;
    source.connect(this.gainNode);

    const startTime = this.audioContext.currentTime;
    source.start(startTime, clampedPosition);

    const scheduledSource: ScheduledSource = {
      source,
      startTime: startTime - clampedPosition,
      duration: audioBuffer.duration,
      trackIndex: this.currentTrackIndex
    };

    this.scheduledSources.push(scheduledSource);

    source.onended = () => {
      this.handleTrackEnd(scheduledSource);
    };

    // Immediately reschedule the next track for gapless playback after seek
    console.info('[WebAudio] 🔄 Seek completed, rescheduling next track for gapless playback');
    this.scheduleNextTrackIfNeeded(this.currentTrackIndex);
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
    this.repeatMode = mode;
  }

  checkAndScheduleNext() {
    this.scheduleNextTrackIfNeeded(this.currentTrackIndex);
  }

  destroy() {
    this.clearScheduledSources();
    this.audioBuffers.clear();
    this.prefetchInProgress.clear();
    this.schedulingInProgress.clear();
    this.abortControllers.forEach(controller => controller.abort());
    this.abortControllers.clear();
    this.audioContext.close();
  }
}

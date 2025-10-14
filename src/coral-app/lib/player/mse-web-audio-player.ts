import type { SimpleTrackDto } from '@/lib/client/schemas';
import type { RepeatMode } from '@/lib/state';
import EventEmitter from 'eventemitter3';
import { MSEAudioLoader } from './mse-audio-loader';

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

export class MSEWebAudioPlayer extends EventEmitter<PlayerEvents> {
  private audioContext: AudioContext;
  private gainNode: GainNode;
  private mediaElementSource: MediaElementAudioSourceNode | null = null;
  private mseLoader: MSEAudioLoader;
  private audioElement: HTMLAudioElement;

  private tracks: SimpleTrackDto[] = [];
  private currentTrackIndex = 0;
  private isPlaying = false;
  private repeatMode: RepeatMode = 'off';
  private trackChangeCallback: ((index: number) => void) | null = null;

  private isInitialized = false;

  constructor() {
    super();
    this.audioContext = new AudioContext();
    this.gainNode = this.audioContext.createGain();
    this.gainNode.connect(this.audioContext.destination);

    this.mseLoader = new MSEAudioLoader();
    this.audioElement = this.mseLoader.getAudioElement();

    this.setupEventListeners();
  }

  private setupEventListeners() {
    // Track ended event
    this.audioElement.addEventListener('ended', () => {
      console.info('[MSEPlayer] 🏁 Track ended');
      this.handleTrackEnd();
    });

    // Waiting event (buffering)
    this.audioElement.addEventListener('waiting', () => {
      console.info('[MSEPlayer] ⏳ Buffering...');
      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: true });
    });

    // Can play event (buffering complete)
    this.audioElement.addEventListener('canplay', () => {
      console.info('[MSEPlayer] ✅ Can play');
      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: false });
    });

    // Playing event
    this.audioElement.addEventListener('playing', () => {
      console.info('[MSEPlayer] ▶️  Playing');
      this.isPlaying = true;
      this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: true });
    });

    // Pause event
    this.audioElement.addEventListener('pause', () => {
      console.info('[MSEPlayer] ⏸️  Paused');
      this.isPlaying = false;
      this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: false });
    });

    // Error event
    this.audioElement.addEventListener('error', (e) => {
      console.error('[MSEPlayer] ❌ Audio element error:', e);
      const error = this.audioElement.error;
      if (error) {
        console.error('[MSEPlayer]   Code:', error.code);
        console.error('[MSEPlayer]   Message:', error.message);
      }
    });

    // Time update - check for track transitions
    this.audioElement.addEventListener('timeupdate', () => {
      this.checkTrackTransition();
    });
  }

  async loadQueue(tracks: SimpleTrackDto[], startIndex: number = 0, clearCache: boolean = true) {
    console.info('[MSEPlayer] 📋 Loading queue with', tracks.length, 'tracks, starting at index', startIndex);

    this.tracks = tracks;
    this.currentTrackIndex = startIndex;

    // Stop current playback
    if (this.isPlaying) {
      await this.pause();
    }

    // Clear existing loader
    if (this.isInitialized) {
      this.mseLoader.destroy();
      this.mseLoader = new MSEAudioLoader();
      this.audioElement = this.mseLoader.getAudioElement();

      this.setupEventListeners();

      // Reconnect to Web Audio API
      if (this.mediaElementSource) {
        this.mediaElementSource.disconnect();
      }
    }

    const currentTrack = this.tracks[startIndex];
    if (!currentTrack) {
      console.error('[MSEPlayer] ❌ No track at index', startIndex);
      return;
    }

    console.info('[MSEPlayer] 🎵 Initializing with track:', currentTrack.title);

    // Initialize MSE with first track
    this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: true });

    await this.mseLoader.initialize(currentTrack.id);

    // Connect audio element to Web Audio API
    if (!this.mediaElementSource) {
      this.mediaElementSource = this.audioContext.createMediaElementSource(this.audioElement);
      this.mediaElementSource.connect(this.gainNode);
      console.info('[MSEPlayer] ✅ Connected to Web Audio API');
    }

    this.isInitialized = true;

    // Queue next track for gapless playback if available
    const nextIndex = this.getNextIndex(startIndex);
    if (nextIndex !== null && nextIndex !== startIndex) {
      const nextTrack = this.tracks[nextIndex];
      console.info('[MSEPlayer] 📋 Queueing next track for gapless:', nextTrack.title);
      await this.mseLoader.appendTrack(nextTrack.id);
    }

    // Start playback
    await this.play();
  }

  updateQueue(tracks: SimpleTrackDto[], currentIndex: number) {
    this.tracks = tracks;
    this.currentTrackIndex = currentIndex;
  }

  async play() {
    console.info('[MSEPlayer] ▶️  Play requested');

    if (this.audioContext.state === 'suspended') {
      await this.audioContext.resume();
    }

    try {
      await this.audioElement.play();
    } catch (error) {
      console.error('[MSEPlayer] ❌ Play failed:', error);
      throw error;
    }
  }

  async pause() {
    console.info('[MSEPlayer] ⏸️  Pause requested');
    this.audioElement.pause();

    if (this.audioContext.state !== 'suspended' && this.audioContext.state !== 'closed') {
      await this.audioContext.suspend();
    }
  }

  async togglePlayPause() {
    if (this.isPlaying) {
      await this.pause();
    } else {
      await this.play();
    }
  }

  async seekTo(position: number) {
    const bufferInfo = this.mseLoader.getCurrentTrackBufferInfo();
    console.info('[MSEPlayer] 🎯 Seeking to', position.toFixed(2), 's');

    if (!bufferInfo) {
      this.audioElement.currentTime = position;
      return;
    }

    // Convert relative position to absolute buffer time
    const absoluteTime = bufferInfo.bufferStartTime + position;
    console.info('[MSEPlayer] ⏩ Absolute buffer time:', absoluteTime.toFixed(2), 's');

    // Reset fragment index to match the seek position
    this.mseLoader.resetToPosition(absoluteTime);

    // Set currentTime - browser may reject if unbuffered
    this.audioElement.currentTime = absoluteTime;

    console.info('[MSEPlayer] 🎯 audioElement.currentTime after seek:', this.audioElement.currentTime.toFixed(2), 's');

    // Trigger buffer check to start loading fragments at the seek position
    // (seeked event won't fire until data is available, so we need to manually trigger loading)
    await this.mseLoader.checkBufferAndLoad();

    // Retry seek after fragments are loaded - browser may have rejected initial seek
    console.info('[MSEPlayer] 🔄 Retrying seek to:', absoluteTime.toFixed(2), 's');
    this.audioElement.currentTime = absoluteTime;
    console.info('[MSEPlayer] 🎯 audioElement.currentTime after retry:', this.audioElement.currentTime.toFixed(2), 's');
  }

  async skip(direction: 1 | -1) {
    const newIndex = this.getNextIndex(this.currentTrackIndex, direction);

    if (newIndex === null) {
      console.info('[MSEPlayer] 🛑 Cannot skip - at boundary');
      return;
    }

    await this.playFromIndex(newIndex);
  }

  async playFromIndex(index: number) {
    if (index < 0 || index >= this.tracks.length) {
      console.error('[MSEPlayer] ❌ Invalid track index:', index);
      return;
    }

    console.info('[MSEPlayer] 🎵 Playing from index:', index);

    this.currentTrackIndex = index;
    await this.loadQueue(this.tracks, index, false);
    await this.play();

    if (this.trackChangeCallback) {
      this.trackChangeCallback(index);
    }

    this.emit(PlayerEventNames.TRACK_CHANGED, { index });
  }

  private checkTrackTransition() {
    const currentTime = this.audioElement.currentTime;
    const actualTrackId = this.mseLoader.getTrackIdAtTime(currentTime);
    const currentTrackId = this.mseLoader.getCurrentTrackId();

    // Check if we've transitioned to a different track
    if (actualTrackId && actualTrackId !== currentTrackId) {
      console.info('[MSEPlayer] 🔄 Track transition detected at', currentTime.toFixed(2), 's');

      // Find the index of the new track
      const newTrackIndex = this.tracks.findIndex(t => t.id === actualTrackId);
      if (newTrackIndex !== -1 && newTrackIndex !== this.currentTrackIndex) {
        this.handleTrackTransition(newTrackIndex);
      }
    }
  }

  private async handleTrackTransition(newIndex: number) {
    console.info('[MSEPlayer] ➡️  Transitioning to track', newIndex);

    this.currentTrackIndex = newIndex;
    const newTrack = this.tracks[newIndex];

    // Update MSE loader's current track ID
    this.mseLoader.setCurrentTrackId(newTrack.id);

    if (this.trackChangeCallback) {
      this.trackChangeCallback(newIndex);
    }

    this.emit(PlayerEventNames.TRACK_CHANGED, { index: newIndex });

    // Queue the following track for continuous gapless playback
    const followingIndex = this.getNextIndex(newIndex);
    if (followingIndex !== null && followingIndex !== newIndex) {
      const followingTrack = this.tracks[followingIndex];
      console.info('[MSEPlayer] 📋 Queueing following track:', followingTrack.title);
      await this.mseLoader.appendTrack(followingTrack.id);
    }
  }

  private async handleTrackEnd() {
    console.info('[MSEPlayer] 🏁 Handling track end at index', this.currentTrackIndex);

    if (this.repeatMode === 'one') {
      console.info('[MSEPlayer] 🔁 Repeat one - seeking to start');
      const bufferInfo = this.mseLoader.getCurrentTrackBufferInfo();
      if (bufferInfo) {
        this.audioElement.currentTime = bufferInfo.bufferStartTime;
      }
      await this.play();
      return;
    }

    const nextIndex = this.getNextIndex(this.currentTrackIndex);

    if (nextIndex === null) {
      console.info('[MSEPlayer] 🛑 End of queue, stopping playback');
      this.isPlaying = false;
      this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: false });
      return;
    }

    if (nextIndex === 0 && this.repeatMode === 'all') {
      console.info('[MSEPlayer] 🔁 Repeat all - wrapping to start');
    }

    this.currentTrackIndex = nextIndex;
    const nextTrack = this.tracks[nextIndex];

    // Update MSE loader's current track ID to the next track
    this.mseLoader.setCurrentTrackId(nextTrack.id);

    // Note: With MSE, the next track should already be queued and will
    // start playing automatically. We just need to update our state.
    console.info('[MSEPlayer] ➡️  Transitioning to track', nextIndex);

    if (this.trackChangeCallback) {
      this.trackChangeCallback(nextIndex);
    }

    this.emit(PlayerEventNames.TRACK_CHANGED, { index: nextIndex });

    // Queue the following track for continuous gapless playback
    const followingIndex = this.getNextIndex(nextIndex);
    if (followingIndex !== null && followingIndex !== nextIndex) {
      const followingTrack = this.tracks[followingIndex];
      console.info('[MSEPlayer] 📋 Queueing following track:', followingTrack.title);
      await this.mseLoader.appendTrack(followingTrack.id);
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

  // Getters
  getIsPlaying(): boolean {
    return this.isPlaying;
  }

  getCurrentTime(): number {
    const bufferInfo = this.mseLoader.getCurrentTrackBufferInfo();
    if (!bufferInfo) return this.audioElement.currentTime;

    // Return time relative to current track (0 = start of current track)
    return this.audioElement.currentTime - bufferInfo.bufferStartTime;
  }

  getDuration(): number {
    const currentTrack = this.tracks[this.currentTrackIndex];
    if (!currentTrack) return 0;

    // Use duration from track metadata (always accurate, even during transcoding)
    return currentTrack.durationInSeconds || 0;
  }

  getVolume(): number {
    return this.gainNode.gain.value;
  }

  getCurrentIndex(): number {
    return this.currentTrackIndex;
  }

  getCurrentTrack(): SimpleTrackDto | null {
    return this.tracks[this.currentTrackIndex] || null;
  }

  getRepeatMode(): RepeatMode {
    return this.repeatMode;
  }

  // Setters
  setVolume(volume: number) {
    this.gainNode.gain.value = Math.max(0, Math.min(1, volume));
  }

  setTrackChangeCallback(callback: (index: number) => void) {
    this.trackChangeCallback = callback;
  }

  setRepeatMode(mode: RepeatMode) {
    this.repeatMode = mode;
    console.info('[MSEPlayer] 🔁 Repeat mode set to:', mode);
  }

  // Unused methods for compatibility
  checkAndScheduleNext() {
    // Not needed with MSE - tracks are automatically queued
  }

  destroy() {
    console.info('[MSEPlayer] 🗑️  Destroying player');

    this.audioElement.pause();

    if (this.mediaElementSource) {
      this.mediaElementSource.disconnect();
      this.mediaElementSource = null;
    }

    this.mseLoader.destroy();
    this.audioContext.close();
  }
}

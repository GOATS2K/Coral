import type { SimpleTrackDto } from '@/lib/client/schemas';
import type { RepeatMode } from '@/lib/state';
import EventEmitter from 'eventemitter3';
import { MSEAudioLoader } from './mse-audio-loader';
import type { PlayerBackend, PlayerEvents } from './player-backend';
import { PlayerEventNames } from './player-backend';

// Re-export for backwards compatibility
export type { PlayerEvents };
export { PlayerEventNames };

export class MSEWebAudioPlayer extends EventEmitter<PlayerEvents> implements PlayerBackend {
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
    this.audioElement.addEventListener('ended', () => {
      this.handleTrackEnd();
    });

    this.audioElement.addEventListener('waiting', () => {
      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: true });
    });

    this.audioElement.addEventListener('canplay', () => {
      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: false });
    });

    this.audioElement.addEventListener('playing', () => {
      this.isPlaying = true;
      this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: true });
    });

    this.audioElement.addEventListener('pause', () => {
      this.isPlaying = false;
      this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: false });
    });

    this.audioElement.addEventListener('error', (e) => {
      console.error('[MSEPlayer] Audio element error:', e);
      const error = this.audioElement.error;
      if (error) {
        console.error('[MSEPlayer] Error code:', error.code, 'Message:', error.message);
      }
    });

    this.audioElement.addEventListener('timeupdate', () => {
      this.checkTrackTransition();
    });
  }

  async loadQueue(tracks: SimpleTrackDto[], startIndex: number = 0) {
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
      console.error('[MSEPlayer] No track at index', startIndex);
      return;
    }

    // Initialize MSE with first track
    this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: true });

    await this.mseLoader.initialize(currentTrack.id);

    // Connect audio element to Web Audio API
    if (!this.mediaElementSource) {
      this.mediaElementSource = this.audioContext.createMediaElementSource(this.audioElement);
      this.mediaElementSource.connect(this.gainNode);
    }

    this.isInitialized = true;

    // Queue next track for gapless playback if available
    const nextIndex = this.getNextIndex(startIndex);
    if (nextIndex !== null && nextIndex !== startIndex) {
      const nextTrack = this.tracks[nextIndex];
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
    if (this.audioContext.state === 'suspended') {
      await this.audioContext.resume();
    }

    try {
      await this.audioElement.play();
    } catch (error) {
      console.error('[MSEPlayer] Play failed:', error);
      throw error;
    }
  }

  async pause() {
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

    if (!bufferInfo) {
      this.audioElement.currentTime = position;
      return;
    }

    // Cancel any pending seek wait (allows user to seek elsewhere while waiting)
    this.mseLoader.cancelPendingSeek();

    // Track if we were playing before the seek
    const wasPlaying = this.isPlaying;

    // Convert relative position to absolute buffer time
    const absoluteTime = bufferInfo.bufferStartTime + position;

    // Check if seeking past available transcoded data
    if (this.mseLoader.isSeekBeyondAvailable(absoluteTime)) {
      // Pause audio immediately - don't let it keep playing from old position
      this.audioElement.pause();

      // Show buffering state while waiting for transcoding to catch up
      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: true });

      // Wait for the target fragment to become available
      const success = await this.mseLoader.waitForSeekFragment(absoluteTime);

      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: false });

      if (!success) {
        console.warn('[MSE] Seek target fragment never became available');
        return;
      }
    } else {
      // Fragment exists - set the index immediately
      this.mseLoader.resetToPosition(absoluteTime);
    }

    // Set currentTime - browser may reject if unbuffered
    this.audioElement.currentTime = absoluteTime;

    // Trigger buffer check to start loading fragments at the seek position
    // (seeked event won't fire until data is available, so we need to manually trigger loading)
    await this.mseLoader.checkBufferAndLoad();

    // Retry seek after fragments are loaded - browser may have rejected initial seek
    this.audioElement.currentTime = absoluteTime;

    // Resume playback if we were playing before the seek
    if (wasPlaying) {
      try {
        // Wait for the 'seeked' event - fires when seek completes AND browser has enough data
        // Add timeout to prevent infinite wait if browser state machine gets stuck
        await Promise.race([
          new Promise<void>((resolve) => {
            this.audioElement.addEventListener('seeked', () => resolve(), { once: true });
          }),
          // Timeout after 500ms if seeked doesn't fire
          new Promise<void>((resolve) => setTimeout(resolve, 500))
        ]);

        // If audioElement.paused is false OR audioContext is suspended from a previous
        // seek, force a pause/play cycle to reset the state machine.
        if (!this.audioElement.paused || this.audioContext.state === 'suspended') {
          this.audioElement.pause();
          if (this.audioContext.state === 'running') {
            await this.audioContext.suspend();
          }
        }

        await this.play();
      } catch (error) {
        console.error('[MSE] Play failed after seek:', error);
      }
    }
  }

  async skip(direction: 1 | -1) {
    const newIndex = this.getNextIndex(this.currentTrackIndex, direction);

    if (newIndex === null) return;

    await this.playFromIndex(newIndex);
  }

  async playFromIndex(index: number) {
    if (index < 0 || index >= this.tracks.length) {
      console.error('[MSEPlayer] Invalid track index:', index);
      return;
    }

    this.currentTrackIndex = index;
    await this.loadQueue(this.tracks, index);
    await this.play();

    if (this.trackChangeCallback) {
      this.trackChangeCallback(index);
    }

    this.emit(PlayerEventNames.TRACK_CHANGED, { index });
  }

  private checkTrackTransition() {
    // Don't transition if current track is still transcoding - duration may grow
    if (this.mseLoader.isCurrentTrackTranscoding()) {
      return;
    }

    const currentTime = this.audioElement.currentTime;
    const actualTrackId = this.mseLoader.getTrackIdAtTime(currentTime);
    const currentTrackId = this.mseLoader.getCurrentTrackId();

    // Check if we've transitioned to a different track
    if (actualTrackId && actualTrackId !== currentTrackId) {
      // Find the index of the new track
      const newTrackIndex = this.tracks.findIndex(t => t.id === actualTrackId);
      if (newTrackIndex !== -1 && newTrackIndex !== this.currentTrackIndex) {
        this.handleTrackTransition(newTrackIndex);
      }
    }
  }

  private async notifyTrackChange(newIndex: number) {
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
      await this.mseLoader.appendTrack(followingTrack.id);
    }
  }

  private async handleTrackTransition(newIndex: number) {
    this.currentTrackIndex = newIndex;
    await this.notifyTrackChange(newIndex);
  }

  private async handleTrackEnd() {
    if (this.repeatMode === 'one') {
      const bufferInfo = this.mseLoader.getCurrentTrackBufferInfo();
      if (bufferInfo) {
        this.audioElement.currentTime = bufferInfo.bufferStartTime;
      }
      await this.play();
      return;
    }

    const nextIndex = this.getNextIndex(this.currentTrackIndex);

    if (nextIndex === null) {
      this.isPlaying = false;
      this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: false });
      return;
    }

    this.currentTrackIndex = nextIndex;

    // Note: With MSE, the next track should already be queued and will
    // start playing automatically. We just need to update our state.
    await this.notifyTrackChange(nextIndex);
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
  }

  destroy() {
    this.audioElement.pause();

    if (this.mediaElementSource) {
      this.mediaElementSource.disconnect();
      this.mediaElementSource = null;
    }

    this.mseLoader.destroy();
    this.audioContext.close();
  }
}

import type { SimpleTrackDto } from '@/lib/client/schemas';
import type { RepeatMode } from '@/lib/state';
import EventEmitter from 'eventemitter3';
import type { PlayerEvents } from './mse-web-audio-player';
import { PlayerEventNames } from './mse-web-audio-player';

/**
 * IPC Proxy for MpvPlayer that runs in the Electron renderer process
 * Communicates with the main process via IPC to control the MpvPlayer instance
 */
export class MpvIpcProxy extends EventEmitter<PlayerEvents> {
  private trackChangeCallback: ((index: number) => void) | null = null;
  private isInitialized = false;
  private dummyAudio: HTMLAudioElement | null = null;
  private dummyAudioBlobUrl: string | null = null;

  // Cached state updated by IPC events (matches MSE player API pattern)
  private cachedState = {
    isPlaying: false,
    currentTime: 0,
    duration: 0,
    volume: 1,
    currentIndex: 0,
    currentTrack: null as SimpleTrackDto | null,
    repeatMode: 'off' as RepeatMode,
  };

  constructor() {
    super();
    this.setupIpcListeners();
  }

  /**
   * Initialize the player with the backend URL
   * Must be called after construction
   */
  async initialize(baseUrl: string): Promise<void> {
    try {
      console.info('[MpvIpcProxy] Initializing with baseUrl:', baseUrl);
      const result = await this.invoke('mpv:initialize', baseUrl);
      if (result.success) {
        this.isInitialized = true;
        console.info('[MpvIpcProxy] Initialized successfully');

        // Create dummy audio element to activate MediaSession for OS media controls
        // Chrome requires an active <audio> element for MediaSession integration
        // Without this, OS media controls won't display track metadata
        // Optimized: only plays when mpv is playing, uses volume=0, longer duration
        this.createDummyAudio();
      } else {
        console.error('[MpvIpcProxy] Initialization failed:', result.error);
        throw new Error(result.error || 'Failed to initialize MpvPlayer');
      }
    } catch (error) {
      console.error('[MpvIpcProxy] Initialization error:', error);
      throw error;
    }
  }

  private setupIpcListeners() {
    if (typeof window === 'undefined' || !(window as any).electronAPI) {
      console.error('[MpvIpcProxy] electronAPI not available');
      return;
    }

    const { ipcRenderer } = (window as any).electronAPI;

    console.info('[MpvIpcProxy] Setting up IPC listeners');

    // Listen for events from main process
    ipcRenderer.on('mpv:playbackStateChanged', (_: any, isPlaying: boolean) => {
      this.cachedState.isPlaying = isPlaying;

      // Note: Dummy audio play/pause is now handled in useMediaSession hook
      // to ensure it's synced with navigator.mediaSession.playbackState

      this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying });
    });

    ipcRenderer.on('mpv:trackChanged', (_: any, index: number) => {
      this.cachedState.currentIndex = index;
      if (this.trackChangeCallback) {
        this.trackChangeCallback(index);
      }
      this.emit(PlayerEventNames.TRACK_CHANGED, { index });
    });

    ipcRenderer.on('mpv:bufferingStateChanged', (_: any, isBuffering: boolean) => {
      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering });
    });

    ipcRenderer.on('mpv:timeUpdate', (_: any, position: number, duration: number) => {
      this.cachedState.currentTime = position;
      this.cachedState.duration = duration;
      this.emit(PlayerEventNames.TIME_UPDATE, { position, duration });
    });
  }

  /**
   * Creates a silent, looping audio element to activate MediaSession.
   * Browsers require an active <audio> element for MediaSession to connect to OS media controls.
   * Since mpv plays natively (outside the browser), we need this dummy element.
   *
   * Optimized to minimize performance impact:
   * - 10-second duration (Chrome requires ≥5s for media controls)
   * - Volume set to 0.01 (must be > 0 for Chrome to activate audio pipeline and OS integration)
   * - Only plays when mpv is actively playing (synced with playback state)
   * - Loops to support long tracks
   * - Low sample rate (8kHz) and 8-bit mono to minimize processing overhead
   */
  private createDummyAudio() {
    if (typeof window === 'undefined' || typeof Audio === 'undefined') {
      return;
    }

    try {
      // Generate a 10-second silent WAV file programmatically
      const sampleRate = 8000; // Low sample rate for efficiency
      const duration = 10; // 10 seconds (meets Chrome's ≥5s requirement)
      const numSamples = sampleRate * duration;

      // WAV file format (PCM, mono, 8-bit)
      const wavHeader = new ArrayBuffer(44);
      const view = new DataView(wavHeader);

      // RIFF header
      view.setUint32(0, 0x52494646, false); // "RIFF"
      view.setUint32(4, 36 + numSamples, true); // File size - 8
      view.setUint32(8, 0x57415645, false); // "WAVE"

      // fmt chunk
      view.setUint32(12, 0x666d7420, false); // "fmt "
      view.setUint32(16, 16, true); // Chunk size
      view.setUint16(20, 1, true); // Audio format (PCM)
      view.setUint16(22, 1, true); // Channels (mono)
      view.setUint32(24, sampleRate, true); // Sample rate
      view.setUint32(28, sampleRate, true); // Byte rate
      view.setUint16(32, 1, true); // Block align
      view.setUint16(34, 8, true); // Bits per sample

      // data chunk
      view.setUint32(36, 0x64617461, false); // "data"
      view.setUint32(40, numSamples, true); // Data size

      // Create silent samples (128 = silence for 8-bit unsigned PCM)
      const samples = new Uint8Array(numSamples);
      samples.fill(128);

      // Combine header and samples
      const wavFile = new Blob([wavHeader, samples], { type: 'audio/wav' });
      this.dummyAudioBlobUrl = URL.createObjectURL(wavFile);

      // Create audio element and add to DOM so useMediaSession can control it
      this.dummyAudio = new Audio();
      this.dummyAudio.src = this.dummyAudioBlobUrl;
      this.dummyAudio.loop = true;
      this.dummyAudio.volume = 0.01; // Very quiet but > 0 for Chrome to activate audio pipeline
      this.dummyAudio.style.display = 'none'; // Hide from UI
      this.dummyAudio.setAttribute('data-mpv-dummy', 'true'); // Mark for identification

      // Add to DOM so useMediaSession can find and control it
      if (typeof document !== 'undefined') {
        document.body.appendChild(this.dummyAudio);
      }

      // Auto-play to activate MediaSession (10s duration minimizes performance impact vs 1s original)
      this.dummyAudio.play().catch(err => {
        console.warn('[MpvIpcProxy] Dummy audio autoplay blocked (this may affect MediaSession):', err);
      });

      console.info('[MpvIpcProxy] Dummy audio element created, added to DOM, and playing (10s loop, volume=0.01)');
    } catch (error) {
      console.error('[MpvIpcProxy] Failed to create dummy audio element:', error);
    }
  }

  private async invoke(channel: string, ...args: any[]): Promise<any> {
    if (typeof window === 'undefined' || !(window as any).electronAPI) {
      throw new Error('[MpvIpcProxy] electronAPI not available');
    }

    const { ipcRenderer } = (window as any).electronAPI;
    return await ipcRenderer.invoke(channel, ...args);
  }

  async loadQueue(tracks: SimpleTrackDto[], startIndex: number = 0) {
    const result = await this.invoke('mpv:loadQueue', tracks, startIndex);
    if (!result.success) {
      throw new Error(result.error || 'Failed to load queue');
    }
  }

  updateQueue(tracks: SimpleTrackDto[], currentIndex: number) {
    this.invoke('mpv:updateQueue', tracks, currentIndex).catch(err => {
      console.error('[MpvIpcProxy] updateQueue failed:', err);
    });
  }

  async play() {
    const result = await this.invoke('mpv:play');
    if (!result.success) {
      throw new Error(result.error || 'Failed to play');
    }
  }

  async pause() {
    const result = await this.invoke('mpv:pause');
    if (!result.success) {
      throw new Error(result.error || 'Failed to pause');
    }
  }

  async togglePlayPause() {
    const result = await this.invoke('mpv:togglePlayPause');
    if (!result.success) {
      throw new Error(result.error || 'Failed to toggle play/pause');
    }
  }

  async seekTo(position: number) {
    const result = await this.invoke('mpv:seekTo', position);
    if (!result.success) {
      throw new Error(result.error || 'Failed to seek');
    }
  }

  async skip(direction: 1 | -1) {
    const result = await this.invoke('mpv:skip', direction);
    if (!result.success) {
      throw new Error(result.error || 'Failed to skip');
    }
  }

  async playFromIndex(index: number) {
    const result = await this.invoke('mpv:playFromIndex', index);
    if (!result.success) {
      throw new Error(result.error || 'Failed to play from index');
    }
  }

  // Getters (return cached state updated by IPC events)
  getIsPlaying(): boolean {
    return this.cachedState.isPlaying;
  }

  getCurrentTime(): number {
    return this.cachedState.currentTime;
  }

  getDuration(): number {
    return this.cachedState.duration;
  }

  getVolume(): number {
    return this.cachedState.volume;
  }

  getCurrentIndex(): number {
    return this.cachedState.currentIndex;
  }

  getCurrentTrack(): SimpleTrackDto | null {
    return this.cachedState.currentTrack;
  }

  getRepeatMode(): RepeatMode {
    return this.cachedState.repeatMode;
  }

  // Async versions of getters for actual values
  async getIsPlayingAsync(): Promise<boolean> {
    return await this.invoke('mpv:getIsPlaying');
  }

  async getCurrentTimeAsync(): Promise<number> {
    return await this.invoke('mpv:getCurrentTime');
  }

  async getDurationAsync(): Promise<number> {
    return await this.invoke('mpv:getDuration');
  }

  async getVolumeAsync(): Promise<number> {
    return await this.invoke('mpv:getVolume');
  }

  async getCurrentIndexAsync(): Promise<number> {
    return await this.invoke('mpv:getCurrentIndex');
  }

  async getCurrentTrackAsync(): Promise<SimpleTrackDto | null> {
    return await this.invoke('mpv:getCurrentTrack');
  }

  async getRepeatModeAsync(): Promise<RepeatMode> {
    return await this.invoke('mpv:getRepeatMode');
  }

  // Setters
  setVolume(volume: number) {
    this.cachedState.volume = volume;
    this.invoke('mpv:setVolume', volume).catch(err => {
      console.error('[MpvIpcProxy] setVolume failed:', err);
    });
  }

  setTrackChangeCallback(callback: (index: number) => void) {
    this.trackChangeCallback = callback;
  }

  setRepeatMode(mode: RepeatMode) {
    this.cachedState.repeatMode = mode;
    this.invoke('mpv:setRepeatMode', mode).catch(err => {
      console.error('[MpvIpcProxy] setRepeatMode failed:', err);
    });
  }

  destroy() {
    this.invoke('mpv:destroy').catch(err => {
      console.error('[MpvIpcProxy] destroy failed:', err);
    });

    // Clean up dummy audio element
    if (this.dummyAudio) {
      this.dummyAudio.pause();
      this.dummyAudio.src = '';

      // Remove from DOM
      if (this.dummyAudio.parentNode) {
        this.dummyAudio.parentNode.removeChild(this.dummyAudio);
      }

      this.dummyAudio = null;
      console.info('[MpvIpcProxy] Dummy audio element cleaned up and removed from DOM');
    }

    // Revoke blob URL to free memory
    if (this.dummyAudioBlobUrl) {
      URL.revokeObjectURL(this.dummyAudioBlobUrl);
      this.dummyAudioBlobUrl = null;
    }

    // Remove event listeners
    if (typeof window !== 'undefined' && (window as any).electronAPI) {
      const { ipcRenderer } = (window as any).electronAPI;
      ipcRenderer.removeAllListeners('mpv:playbackStateChanged');
      ipcRenderer.removeAllListeners('mpv:trackChanged');
      ipcRenderer.removeAllListeners('mpv:bufferingStateChanged');
      ipcRenderer.removeAllListeners('mpv:timeUpdate');
    }
  }
}

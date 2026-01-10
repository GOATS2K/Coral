import type { SimpleTrackDto } from '@/lib/client/schemas';
import type { RepeatMode } from '@/lib/state';

/**
 * Events emitted by player backends
 */
export interface PlayerEvents {
  playbackStateChanged: { isPlaying: boolean };
  trackChanged: { index: number };
  bufferingStateChanged: { isBuffering: boolean };
  timeUpdate: { position: number; duration: number };
}

/**
 * Event name constants for type-safe event handling
 */
export const PlayerEventNames = {
  PLAYBACK_STATE_CHANGED: 'playbackStateChanged',
  TRACK_CHANGED: 'trackChanged',
  BUFFERING_STATE_CHANGED: 'bufferingStateChanged',
  TIME_UPDATE: 'timeUpdate',
} as const;

/**
 * Common interface for all player backends (MSE, MPV, Native, Remote)
 *
 * All methods that perform I/O or async operations return Promises.
 * State query methods return cached/sync values for UI responsiveness.
 */
export interface PlayerBackend {
  // ─────────────────────────────────────────────────────────────────────────────
  // Queue Management
  // ─────────────────────────────────────────────────────────────────────────────

  /**
   * Load a new queue and start playback from the specified index
   */
  loadQueue(tracks: SimpleTrackDto[], startIndex?: number): Promise<void>;

  /**
   * Update the queue without interrupting playback
   */
  updateQueue(tracks: SimpleTrackDto[], currentIndex: number): void;

  // ─────────────────────────────────────────────────────────────────────────────
  // Playback Control
  // ─────────────────────────────────────────────────────────────────────────────

  /**
   * Start or resume playback
   */
  play(): Promise<void>;

  /**
   * Pause playback
   */
  pause(): Promise<void>;

  /**
   * Toggle between play and pause
   */
  togglePlayPause(): Promise<void>;

  // ─────────────────────────────────────────────────────────────────────────────
  // Navigation
  // ─────────────────────────────────────────────────────────────────────────────

  /**
   * Seek to a position in seconds within the current track
   */
  seekTo(position: number): Promise<void>;

  /**
   * Skip to next (direction=1) or previous (direction=-1) track
   */
  skip(direction: 1 | -1): Promise<void>;

  /**
   * Play the track at the specified index in the queue
   */
  playFromIndex(index: number): Promise<void>;

  // ─────────────────────────────────────────────────────────────────────────────
  // Volume & Modes
  // ─────────────────────────────────────────────────────────────────────────────

  /**
   * Set volume (0.0 to 1.0)
   */
  setVolume(volume: number): void;

  /**
   * Set repeat mode
   */
  setRepeatMode(mode: RepeatMode): void;

  // ─────────────────────────────────────────────────────────────────────────────
  // State Queries (sync - return cached state)
  // ─────────────────────────────────────────────────────────────────────────────

  /**
   * Whether playback is currently active
   */
  getIsPlaying(): boolean;

  /**
   * Current playback position in seconds
   */
  getCurrentTime(): number;

  /**
   * Duration of the current track in seconds
   */
  getDuration(): number;

  /**
   * Current volume (0.0 to 1.0)
   */
  getVolume(): number;

  /**
   * Index of the current track in the queue
   */
  getCurrentIndex(): number;

  /**
   * The current track, or null if no track is loaded
   */
  getCurrentTrack(): SimpleTrackDto | null;

  /**
   * Current repeat mode
   */
  getRepeatMode(): RepeatMode;

  // ─────────────────────────────────────────────────────────────────────────────
  // Lifecycle
  // ─────────────────────────────────────────────────────────────────────────────

  /**
   * Clean up resources and stop playback
   */
  destroy(): void;

  // ─────────────────────────────────────────────────────────────────────────────
  // Events
  // ─────────────────────────────────────────────────────────────────────────────

  /**
   * Subscribe to player events
   */
  on<K extends keyof PlayerEvents>(
    event: K,
    listener: (data: PlayerEvents[K]) => void
  ): void;

  /**
   * Unsubscribe from player events
   */
  off<K extends keyof PlayerEvents>(
    event: K,
    listener: (data: PlayerEvents[K]) => void
  ): void;

  /**
   * Set a callback for track changes (legacy support, prefer using 'on' with 'trackChanged')
   */
  setTrackChangeCallback(callback: (index: number) => void): void;
}

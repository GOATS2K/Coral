/**
 * MpvPlayer - libmpv-based audio player for Electron
 * JavaScript implementation for Electron main process
 */

/* eslint-env node */
import EventEmitter from 'node:events';
import {
  koffi,
  mpv_create,
  mpv_initialize,
  mpv_destroy,
  mpv_set_property_string,
  mpv_get_property_string,
  mpv_command_string,
  mpv_wait_event,
  mpv_observe_property,
  checkMpvError,
  MpvEventId,
  MpvEventStruct,
  MpvEventPropertyStruct,
  MpvFormat,
} from './mpv-bindings.mjs';

// Event names matching the React Native player interface
export const PlayerEventNames = {
  PLAYBACK_STATE_CHANGED: 'playbackStateChanged',
  TRACK_CHANGED: 'trackChanged',
  BUFFERING_STATE_CHANGED: 'bufferingStateChanged',
  TIME_UPDATE: 'timeUpdate',
};

export class MpvPlayer extends EventEmitter {
  constructor(baseUrl) {
    super();
    this.handle = null;
    this.tracks = [];
    this.currentTrackIndex = 0;
    this.isPlaying = false;
    this.repeatMode = 'off'; // 'off' | 'all' | 'one'
    this.trackChangeCallback = null;
    this.isRunning = false;
    this.eventPollingInterval = null;
    this.isInitialized = false;
    this.baseUrl = baseUrl;
    this._loggedInvalidEvent = false;

    // Property observation IDs
    this.OBSERVE_PLAYLIST_POS = 1;
    this.OBSERVE_PAUSE = 2;
    this.OBSERVE_TIME_POS = 3;
    this.OBSERVE_DURATION = 4;
    this.OBSERVE_IDLE = 5;

    this.initializeMpv();
  }

  initializeMpv() {
    try {
      // Create mpv handle
      this.handle = mpv_create();
      if (!this.handle) {
        throw new Error('Failed to create mpv handle');
      }

      // Configure for gapless playback
      mpv_set_property_string(this.handle, 'gapless-audio', 'yes');
      mpv_set_property_string(this.handle, 'prefetch-playlist', 'yes');
      mpv_set_property_string(this.handle, 'audio-buffer', '1.0');

      // Disable video (audio-only player)
      mpv_set_property_string(this.handle, 'vid', 'no');
      mpv_set_property_string(this.handle, 'video', 'no');

      // Set idle mode (allows mpv to stay running without a file loaded)
      mpv_set_property_string(this.handle, 'idle', 'yes');

      // Don't keep file open at end - allow auto-advance in playlist
      mpv_set_property_string(this.handle, 'keep-open', 'no');

      // Initialize mpv
      const initResult = mpv_initialize(this.handle);
      checkMpvError(initResult, 'Initialize mpv');

      // Observe properties for state changes
      mpv_observe_property(this.handle, this.OBSERVE_PLAYLIST_POS, 'playlist-pos', MpvFormat.MPV_FORMAT_INT64);
      mpv_observe_property(this.handle, this.OBSERVE_PAUSE, 'pause', MpvFormat.MPV_FORMAT_FLAG);
      mpv_observe_property(this.handle, this.OBSERVE_TIME_POS, 'time-pos', MpvFormat.MPV_FORMAT_DOUBLE);
      mpv_observe_property(this.handle, this.OBSERVE_DURATION, 'duration', MpvFormat.MPV_FORMAT_DOUBLE);
      mpv_observe_property(this.handle, this.OBSERVE_IDLE, 'idle-active', MpvFormat.MPV_FORMAT_FLAG);

      // Start event loop
      this.startEventLoop();

      this.isInitialized = true;
      console.info('[MpvPlayer] Initialized successfully');
    } catch (error) {
      console.error('[MpvPlayer] Initialization failed:', error);
      throw error;
    }
  }

  startEventLoop() {
    // Poll mpv events and state periodically
    this.isRunning = true;

    // Poll mpv events every 50ms
    this.eventPollingInterval = setInterval(() => {
      this.pollEvents();
    }, 50);

    // Poll playback state every 250ms as fallback
    // Property change events should fire, but polling ensures UI never gets stuck
    this.statePollingInterval = setInterval(() => {
      this.pollPlaybackState();
    }, 250);

  }

  pollEvents() {
    if (!this.isRunning || !this.handle) return;

    // Process multiple events per poll (up to 10) to catch up if needed
    const MAX_EVENTS = 10;
    let processedCount = 0;

    while (processedCount < MAX_EVENTS) {
      try {
        // Get event pointer from mpv
        const eventPtr = mpv_wait_event(this.handle, 0);

        if (!eventPtr) {
          console.warn('[MpvPlayer] mpv_wait_event returned null');
          break;
        }

        // Decode the pointer to get actual struct data
        const event = koffi.decode(eventPtr, MpvEventStruct);

        // Validate event after decoding
        if (!event || typeof event.event_id !== 'number') {
          if (!this._loggedInvalidEvent) {
            console.warn('[MpvPlayer] Received invalid event after decode:', event);
            this._loggedInvalidEvent = true;
          }
          break;
        }

        // Reset invalid event flag when we get a valid event
        this._loggedInvalidEvent = false;

        // If no event, stop polling
        if (event.event_id === MpvEventId.MPV_EVENT_NONE) {
          break;
        }

        this.handleEvent(event);
        processedCount++;
      } catch (error) {
        console.error('[MpvPlayer] Error processing event:', error);
        break;
      }
    }
  }

  pollPlaybackState() {
    if (!this.handle) return;

    try {
      // Check if playing state changed
      const currentlyPlaying = this.getIsPlaying();
      if (currentlyPlaying !== this.isPlaying) {
        this.isPlaying = currentlyPlaying;
        this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: currentlyPlaying });
      }

      // Check if track changed
      const currentIndex = this.getCurrentIndex();
      if (currentIndex !== this.currentTrackIndex) {
        this.currentTrackIndex = currentIndex;
        this.emit(PlayerEventNames.TRACK_CHANGED, { index: currentIndex });
      }
    } catch (error) {
      console.error('[MpvPlayer] Error polling playback state:', error);
    }
  }

  handleEvent(event) {
    switch (event.event_id) {
      case MpvEventId.MPV_EVENT_PROPERTY_CHANGE:
        this.handlePropertyChange(event);
        break;

      case MpvEventId.MPV_EVENT_END_FILE:
        this.handleEndFile(event);
        break;

      case MpvEventId.MPV_EVENT_FILE_LOADED:
        this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: false });
        break;
    }
  }

  handlePropertyChange(event) {
    const observeId = event.reply_userdata;

    switch (observeId) {
      case this.OBSERVE_PAUSE:
        const currentlyPlaying = this.getIsPlaying();
        if (currentlyPlaying !== this.isPlaying) {
          this.isPlaying = currentlyPlaying;
          this.emit(PlayerEventNames.PLAYBACK_STATE_CHANGED, { isPlaying: currentlyPlaying });
        }
        break;

      case this.OBSERVE_PLAYLIST_POS:
        const currentIndex = this.getCurrentIndex();
        if (currentIndex !== this.currentTrackIndex) {
          this.currentTrackIndex = currentIndex;
          this.emit(PlayerEventNames.TRACK_CHANGED, { index: currentIndex });
        }
        break;

      case this.OBSERVE_TIME_POS:
        try {
          if (event.data) {
            const propertyEvent = koffi.decode(event.data, MpvEventPropertyStruct);
            if (propertyEvent.format === MpvFormat.MPV_FORMAT_DOUBLE && propertyEvent.data) {
              const position = koffi.decode(propertyEvent.data, 'double');
              const duration = this.getDuration();
              this.emit(PlayerEventNames.TIME_UPDATE, { position, duration });
            }
          }
        } catch (_error) {
          // Silently ignore decoding errors
        }
        break;
    }
  }

  handleEndFile(event) {
    if (this.repeatMode === 'one') {
      // Restart current track
      this.playFromIndex(this.currentTrackIndex);
      return;
    }

    // Let mpv handle playlist advancement for gapless playback
  }

  async loadQueue(tracks, startIndex = 0) {
    if (!this.handle) {
      throw new Error('[MpvPlayer] Not initialized');
    }

    this.tracks = tracks;
    this.currentTrackIndex = startIndex;

    try {
      // Clear existing playlist
      mpv_command_string(this.handle, 'playlist-clear');

      this.emit(PlayerEventNames.BUFFERING_STATE_CHANGED, { isBuffering: true });

      // Load all tracks into playlist
      for (let i = 0; i < tracks.length; i++) {
        const track = tracks[i];
        const url = `${this.baseUrl}/api/library/tracks/${track.id}/original`;
        const mode = i === 0 ? 'replace' : 'append';

        const loadCmd = `loadfile "${url}" ${mode}`;
        const result = mpv_command_string(this.handle, loadCmd);

        if (result < 0) {
          console.error(`[MpvPlayer] Failed to load track ${track.id}`);
        }
      }

      // Jump to start index if not 0
      if (startIndex > 0) {
        mpv_set_property_string(this.handle, 'playlist-pos', startIndex.toString());
      }

      // Update repeat mode
      this.updateRepeatMode();

      console.info(`[MpvPlayer] Loaded ${tracks.length} tracks, starting at index ${startIndex}`);
    } catch (error) {
      console.error('[MpvPlayer] Failed to load queue:', error);
      throw error;
    }
  }

  /**
   * Get current playlist count from mpv
   */
  getPlaylistCount() {
    if (!this.handle) return 0;
    const countStr = mpv_get_property_string(this.handle, 'playlist-count');
    return parseInt(countStr, 10) || 0;
  }

  updateQueue(tracks, currentIndex) {
    if (!this.handle) {
      throw new Error('[MpvPlayer] Not initialized');
    }

    try {
      // Get actual playlist count from mpv to verify sync
      const actualPlaylistCount = this.getPlaylistCount();

      if (this.tracks.length !== actualPlaylistCount) {
        console.warn(`[MpvPlayer] WARNING: this.tracks is out of sync with mpv playlist! this.tracks=${this.tracks.length}, mpv=${actualPlaylistCount}. Rebuilding playlist...`);

        // Out of sync - rebuild entire playlist instead of reconciling
        mpv_command_string(this.handle, 'playlist-clear');

        for (let i = 0; i < tracks.length; i++) {
          const track = tracks[i];
          const url = `${this.baseUrl}/api/library/tracks/${track.id}/original`;
          const mode = i === 0 ? 'replace' : 'append';
          mpv_command_string(this.handle, `loadfile "${url}" ${mode}`);
        }

        this.tracks = tracks;

        if (currentIndex !== this.currentTrackIndex) {
          this.currentTrackIndex = currentIndex;
          mpv_set_property_string(this.handle, 'playlist-pos', currentIndex.toString());
        }

        return;
      }

      const mpvState = [...this.tracks];
      const newTracks = tracks;

      for (let targetPos = 0; targetPos < newTracks.length; targetPos++) {
        const desiredTrack = newTracks[targetPos];
        const currentTrack = mpvState[targetPos];

        if (currentTrack && currentTrack.id === desiredTrack.id) {
          continue;
        }

        const currentPos = mpvState.findIndex(t => t && t.id === desiredTrack.id);

        if (currentPos !== -1 && currentPos !== targetPos) {
          mpv_command_string(this.handle, `playlist-move ${currentPos} ${targetPos}`);

          const [movedTrack] = mpvState.splice(currentPos, 1);
          mpvState.splice(targetPos, 0, movedTrack);
        } else if (currentPos === -1) {
          const url = `${this.baseUrl}/api/library/tracks/${desiredTrack.id}/original`;

          // Get playlist count BEFORE appending to know where it will go
          const beforeAppendCountStr = mpv_get_property_string(this.handle, 'playlist-count');
          const beforeAppendCount = parseInt(beforeAppendCountStr, 10) || 0;

          mpv_command_string(this.handle, `loadfile "${url}" append`);

          // The track was appended to mpv's ACTUAL playlist end, not mpvState's end
          const actualAppendedPos = beforeAppendCount;

          // Update shadow state to match
          mpvState.push(desiredTrack);
          const shadowAppendedPos = mpvState.length - 1;

          if (actualAppendedPos !== targetPos) {
            mpv_command_string(this.handle, `playlist-move ${actualAppendedPos} ${targetPos}`);

            // Also update shadow state to reflect the move
            const [movedTrack] = mpvState.splice(shadowAppendedPos, 1);
            mpvState.splice(targetPos, 0, movedTrack);
          }
        }
      }

      const newTrackIds = new Set(newTracks.map(t => t.id));
      for (let i = mpvState.length - 1; i >= 0; i--) {
        if (!newTrackIds.has(mpvState[i].id)) {
          mpv_command_string(this.handle, `playlist-remove ${i}`);
          mpvState.splice(i, 1);
        }
      }

      this.tracks = newTracks;

      if (currentIndex !== this.currentTrackIndex) {
        this.currentTrackIndex = currentIndex;
        mpv_set_property_string(this.handle, 'playlist-pos', currentIndex.toString());
      }
    } catch (error) {
      console.error('[MpvPlayer] Failed to update queue:', error);
      throw error;
    }
  }

  async play() {
    if (!this.handle) {
      throw new Error('[MpvPlayer] Not initialized');
    }

    try {
      mpv_set_property_string(this.handle, 'pause', 'no');
    } catch (error) {
      console.error('[MpvPlayer] Play failed:', error);
      throw error;
    }
  }

  async pause() {
    if (!this.handle) {
      throw new Error('[MpvPlayer] Not initialized');
    }

    try {
      mpv_set_property_string(this.handle, 'pause', 'yes');
    } catch (error) {
      console.error('[MpvPlayer] Pause failed:', error);
      throw error;
    }
  }

  async togglePlayPause() {
    if (this.isPlaying) {
      await this.pause();
    } else {
      await this.play();
    }
  }

  async seekTo(position) {
    if (!this.handle) {
      throw new Error('[MpvPlayer] Not initialized');
    }

    try {
      const seekCmd = `seek ${position} absolute`;
      mpv_command_string(this.handle, seekCmd);
    } catch (error) {
      console.error('[MpvPlayer] Seek failed:', error);
      throw error;
    }
  }

  async skip(direction) {
    const newIndex = this.getNextIndex(this.currentTrackIndex, direction);

    if (newIndex === null) return;

    await this.playFromIndex(newIndex);
  }

  async playFromIndex(index) {
    if (!this.handle) {
      throw new Error('[MpvPlayer] Not initialized');
    }

    if (index < 0 || index >= this.tracks.length) {
      console.error('[MpvPlayer] Invalid track index:', index);
      return;
    }

    try {
      this.currentTrackIndex = index;
      mpv_set_property_string(this.handle, 'playlist-pos', index.toString());

      if (this.trackChangeCallback) {
        this.trackChangeCallback(index);
      }

      this.emit(PlayerEventNames.TRACK_CHANGED, { index });

      // Ensure playback starts
      await this.play();
    } catch (error) {
      console.error('[MpvPlayer] Failed to play from index:', error);
      throw error;
    }
  }

  getNextIndex(currentIndex, direction = 1) {
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

  updateRepeatMode() {
    if (!this.handle) return;

    try {
      switch (this.repeatMode) {
        case 'off':
          mpv_set_property_string(this.handle, 'loop-playlist', 'no');
          mpv_set_property_string(this.handle, 'loop-file', 'no');
          break;
        case 'all':
          mpv_set_property_string(this.handle, 'loop-playlist', 'inf');
          mpv_set_property_string(this.handle, 'loop-file', 'no');
          break;
        case 'one':
          mpv_set_property_string(this.handle, 'loop-playlist', 'no');
          mpv_set_property_string(this.handle, 'loop-file', 'inf');
          break;
      }
    } catch (error) {
      console.error('[MpvPlayer] Failed to update repeat mode:', error);
    }
  }

  // Getters
  getIsPlaying() {
    if (!this.handle) return false;

    try {
      const pauseStr = mpv_get_property_string(this.handle, 'pause');
      return pauseStr === 'no';
    } catch (_error) {
      return this.isPlaying;
    }
  }

  getCurrentTime() {
    if (!this.handle) return 0;

    try {
      const timeStr = mpv_get_property_string(this.handle, 'time-pos');
      return parseFloat(timeStr) || 0;
    } catch (_error) {
      return 0;
    }
  }

  getDuration() {
    if (!this.handle) return 0;

    try {
      const durationStr = mpv_get_property_string(this.handle, 'duration');
      return parseFloat(durationStr) || 0;
    } catch (_error) {
      // Fallback to track metadata
      const currentTrack = this.tracks[this.currentTrackIndex];
      return currentTrack?.durationInSeconds || 0;
    }
  }

  getVolume() {
    if (!this.handle) return 1;

    try {
      const volumeStr = mpv_get_property_string(this.handle, 'volume');
      // mpv volume is 0-100, we use 0-1
      return parseFloat(volumeStr) / 100 || 1;
    } catch (_error) {
      return 1;
    }
  }

  getCurrentIndex() {
    if (!this.handle) return this.currentTrackIndex;

    try {
      const posStr = mpv_get_property_string(this.handle, 'playlist-pos');
      const pos = parseInt(posStr, 10);

      if (!isNaN(pos) && pos >= 0) {
        return pos;
      }
    } catch (_error) {

    }

    return this.currentTrackIndex;
  }

  getCurrentTrack() {
    return this.tracks[this.currentTrackIndex] || null;
  }

  getRepeatMode() {
    return this.repeatMode;
  }

  // Setters
  setVolume(volume) {
    if (!this.handle) return;

    try {
      // Clamp volume to 0-1 range, then convert to mpv's 0-100 range
      const clampedVolume = Math.max(0, Math.min(1, volume));
      const mpvVolume = clampedVolume * 100;
      mpv_set_property_string(this.handle, 'volume', mpvVolume.toString());
    } catch (error) {
      console.error('[MpvPlayer] Failed to set volume:', error);
    }
  }

  setTrackChangeCallback(callback) {
    this.trackChangeCallback = callback;
  }

  setRepeatMode(mode) {
    this.repeatMode = mode;
    this.updateRepeatMode();
  }

  destroy() {
    console.info('[MpvPlayer] Destroying player');

    // Stop event loop
    this.isRunning = false;

    // Stop event polling
    if (this.eventPollingInterval) {
      clearInterval(this.eventPollingInterval);
      this.eventPollingInterval = null;
    }

    // Stop state polling
    if (this.statePollingInterval) {
      clearInterval(this.statePollingInterval);
      this.statePollingInterval = null;
    }


    // Destroy mpv handle
    if (this.handle) {
      try {
        mpv_destroy(this.handle);
      } catch (error) {
        console.error('[MpvPlayer] Failed to destroy mpv handle:', error);
      }
      this.handle = null;
    }

    this.isInitialized = false;
  }
}

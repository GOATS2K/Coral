/**
 * IPC handlers for MpvPlayer in Electron main process
 * JavaScript implementation
 */

/* eslint-env node */
import { ipcMain, BrowserWindow } from 'electron';
import { MpvPlayer } from './mpv-player.mjs';

/**
 * Helper to forward player events to renderer process
 */
function forwardEventToRenderer(channel, ...args) {
  const mainWindow = BrowserWindow.getAllWindows()[0];
  if (mainWindow) {
    mainWindow.webContents.send(channel, ...args);
  }
}

/**
 * Sets up IPC handlers for the MpvPlayer in the Electron main process
 * @param {string} defaultBaseUrl - Default backend URL
 */
export function setupMpvIpcHandlers(defaultBaseUrl) {
  let player = null;
  let baseUrl = defaultBaseUrl;

  // Update base URL (called from renderer when config loads)
  ipcMain.handle('mpv:setBaseUrl', (_, newBaseUrl) => {
    try {
      baseUrl = newBaseUrl;
      console.info('[MpvIpcMain] Base URL updated to:', baseUrl);

      // If player exists, recreate it with new URL
      if (player) {
        player.destroy();
        player = new MpvPlayer(baseUrl);
      }

      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] Failed to set base URL:', error);
      return { success: false, error: String(error) };
    }
  });

  // Initialize player
  ipcMain.handle('mpv:initialize', (_, providedBaseUrl) => {
    try {
      if (player) {
        player.destroy();
      }

      // Use provided base URL if available, otherwise use stored one
      const urlToUse = providedBaseUrl || baseUrl;
      player = new MpvPlayer(urlToUse);

      // Forward player events to renderer
      player.on('playbackStateChanged', ({ isPlaying }) => {
        forwardEventToRenderer('mpv:playbackStateChanged', isPlaying);
      });

      player.on('trackChanged', ({ index }) => {
        forwardEventToRenderer('mpv:trackChanged', index);
      });

      player.on('bufferingStateChanged', ({ isBuffering }) => {
        forwardEventToRenderer('mpv:bufferingStateChanged', isBuffering);
      });

      player.on('timeUpdate', ({ position, duration }) => {
        forwardEventToRenderer('mpv:timeUpdate', position, duration);
      });

      console.info('[MpvIpcMain] Player initialized');
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] Failed to initialize player:', error);
      return { success: false, error: String(error) };
    }
  });

  // Load queue
  ipcMain.handle('mpv:loadQueue', async (_, tracks, startIndex) => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      await player.loadQueue(tracks, startIndex);
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] loadQueue failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Update queue
  ipcMain.handle('mpv:updateQueue', (_, tracks, currentIndex) => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      player.updateQueue(tracks, currentIndex);
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] updateQueue failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Play
  ipcMain.handle('mpv:play', async () => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      await player.play();
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] play failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Pause
  ipcMain.handle('mpv:pause', async () => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      await player.pause();
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] pause failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Toggle play/pause
  ipcMain.handle('mpv:togglePlayPause', async () => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      await player.togglePlayPause();
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] togglePlayPause failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Seek
  ipcMain.handle('mpv:seekTo', async (_, position) => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      await player.seekTo(position);
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] seekTo failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Skip
  ipcMain.handle('mpv:skip', async (_, direction) => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      await player.skip(direction);
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] skip failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Play from index
  ipcMain.handle('mpv:playFromIndex', async (_, index) => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      await player.playFromIndex(index);
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] playFromIndex failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Getters
  ipcMain.handle('mpv:getIsPlaying', () => {
    return player ? player.getIsPlaying() : false;
  });

  ipcMain.handle('mpv:getCurrentTime', () => {
    return player ? player.getCurrentTime() : 0;
  });

  ipcMain.handle('mpv:getDuration', () => {
    return player ? player.getDuration() : 0;
  });

  ipcMain.handle('mpv:getVolume', () => {
    return player ? player.getVolume() : 1;
  });

  ipcMain.handle('mpv:getCurrentIndex', () => {
    return player ? player.getCurrentIndex() : 0;
  });

  ipcMain.handle('mpv:getCurrentTrack', () => {
    return player ? player.getCurrentTrack() : null;
  });

  ipcMain.handle('mpv:getRepeatMode', () => {
    return player ? player.getRepeatMode() : 'off';
  });

  // Setters
  ipcMain.handle('mpv:setVolume', (_, volume) => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      player.setVolume(volume);
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] setVolume failed:', error);
      return { success: false, error: String(error) };
    }
  });

  ipcMain.handle('mpv:setRepeatMode', (_, mode) => {
    try {
      if (!player) {
        throw new Error('Player not initialized');
      }
      player.setRepeatMode(mode);
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] setRepeatMode failed:', error);
      return { success: false, error: String(error) };
    }
  });

  // Destroy
  ipcMain.handle('mpv:destroy', () => {
    try {
      if (player) {
        player.destroy();
        player = null;
      }
      return { success: true };
    } catch (error) {
      console.error('[MpvIpcMain] destroy failed:', error);
      return { success: false, error: String(error) };
    }
  });

  console.info('[MpvIpcMain] IPC handlers registered');
}

/**
 * Cleans up IPC handlers
 */
export function cleanupMpvIpcHandlers() {
  const channels = [
    'mpv:setBaseUrl',
    'mpv:initialize',
    'mpv:loadQueue',
    'mpv:updateQueue',
    'mpv:play',
    'mpv:pause',
    'mpv:togglePlayPause',
    'mpv:seekTo',
    'mpv:skip',
    'mpv:playFromIndex',
    'mpv:getIsPlaying',
    'mpv:getCurrentTime',
    'mpv:getDuration',
    'mpv:getVolume',
    'mpv:getCurrentIndex',
    'mpv:getCurrentTrack',
    'mpv:getRepeatMode',
    'mpv:setVolume',
    'mpv:setRepeatMode',
    'mpv:destroy',
  ];

  channels.forEach(channel => {
    ipcMain.removeHandler(channel);
  });

  console.info('[MpvIpcMain] IPC handlers cleaned up');
}

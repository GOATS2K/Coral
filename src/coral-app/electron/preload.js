/* eslint-env node */
const { contextBridge, ipcRenderer } = require('electron');

/**
 * Preload script for Electron
 *
 * This script runs before the renderer process loads and can expose
 * safe APIs to the web app via contextBridge.
 *
 * Since we're using AsyncStorage (which uses localStorage on web),
 * we don't need to expose any special APIs - the React Native app
 * can use standard web APIs directly.
 */

// Expose Electron API with IPC support for MpvPlayer
contextBridge.exposeInMainWorld('electronAPI', {
  isElectron: true,
  ipcRenderer: {
    invoke: (channel, ...args) => ipcRenderer.invoke(channel, ...args),
    on: (channel, callback) => {
      // Whitelist channels that can be listened to
      const validChannels = [
        'mpv:playbackStateChanged',
        'mpv:trackChanged',
        'mpv:bufferingStateChanged',
        'mpv:timeUpdate',
      ];
      if (validChannels.includes(channel)) {
        ipcRenderer.on(channel, callback);
      }
    },
    removeAllListeners: (channel) => {
      ipcRenderer.removeAllListeners(channel);
    },
  },
});

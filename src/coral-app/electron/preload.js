const { contextBridge } = require('electron');

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

// Expose a simple flag to detect if running in Electron
contextBridge.exposeInMainWorld('electronAPI', {
  isElectron: true,
});

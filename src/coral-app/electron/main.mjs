/* eslint-env node */
import { app, BrowserWindow } from 'electron';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import serve from 'electron-serve';
import { setupMpvIpcHandlers, cleanupMpvIpcHandlers } from './mpv-ipc-main.mjs';

// ESM equivalent of __dirname
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Set AppUserModelId for Windows to show app name in media controls
// In development: use process.execPath so Windows recognizes the electron.exe
// In production: use custom appId from electron-builder.json
const isDevelopment = process.env.NODE_ENV === 'development';
if (isDevelopment) {
  app.setAppUserModelId(process.execPath);
} else {
  app.setAppUserModelId('com.goats2k.coral');
}

// Keep a global reference of window to prevent garbage collection
let mainWindow = null;

// Default backend URL (should match Config.ts default)
const DEFAULT_BACKEND_URL = 'http://localhost:5031';

// Setup electron-serve for production (called before app.whenReady)
const loadURL = serve({ directory: 'dist' });

async function createMainWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false
    },
    title: 'Coral',
    backgroundColor: '#000000'
  });

  // Load the app: development uses Metro, production uses electron-serve
  if (isDevelopment) {
    await mainWindow.loadURL('http://localhost:8081');
  } else {
    await loadURL(mainWindow);
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  // Open DevTools in development
  if (isDevelopment) {
    mainWindow.webContents.openDevTools();
  }
}

// App lifecycle
app.whenReady().then(async () => {
  await createMainWindow();

  // Initialize MPV IPC handlers
  setupMpvIpcHandlers(DEFAULT_BACKEND_URL);

  app.on('activate', () => {
    // On macOS, re-create window when dock icon is clicked and no windows open
    if (BrowserWindow.getAllWindows().length === 0) {
      createMainWindow();
    }
  });
});

app.on('window-all-closed', () => {
  // Cleanup MPV IPC handlers
  cleanupMpvIpcHandlers();

  // On macOS, apps typically stay active until Cmd+Q
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

/* eslint-env node */
import { app, BrowserWindow, ipcMain, nativeTheme } from 'electron';
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
  // Base window options
  const windowOptions = {
    width: 1200,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false
    },
    title: 'Coral',
    backgroundColor: '#000000',
    autoHideMenuBar: true // Hide menu bar (File/Edit/View), accessible via Alt
  };

  // Platform-specific title bar configuration
  if (process.platform === 'darwin') {
    // macOS: hiddenInset keeps traffic light buttons visible
    windowOptions.titleBarStyle = 'hiddenInset';
  } else if (process.platform === 'win32') {
    // Windows: Use titleBarOverlay to get native controls overlaid on custom UI
    // Note: titleBarOverlay requires keeping the frame, not using frame: false
    windowOptions.titleBarStyle = 'hidden';
    // Use system theme for initial colors (will be updated by renderer on load)
    const systemIsDark = nativeTheme.shouldUseDarkColors;
    const initialColors = getTitleBarColors(systemIsDark ? 'dark' : 'light');
    windowOptions.titleBarOverlay = {
      ...initialColors,
      height: 32 // Slightly shorter than TitleBar (33px) so border shows below controls
    };
  } else {
    // Linux: hidden title bar (behavior varies by desktop environment)
    windowOptions.titleBarStyle = 'hidden';
  }

  mainWindow = new BrowserWindow(windowOptions);

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

// Helper function to get theme colors for titleBarOverlay
// Colors match theme.ts THEME configuration
function getTitleBarColors(theme) {
  if (theme === 'dark') {
    return {
      color: '#0a0a0a', // Dark background - hsl(0 0% 3.9%)
      symbolColor: '#fafafa' // Light foreground - hsl(0 0% 98%)
    };
  } else {
    return {
      color: '#ffffff', // Light background - hsl(0 0% 100%)
      symbolColor: '#0a0a0a' // Dark foreground - hsl(0 0% 3.9%)
    };
  }
}

// App lifecycle
app.whenReady().then(async () => {
  // Initialize MPV IPC handlers BEFORE creating window to avoid race condition
  // (renderer may try to invoke handlers immediately on load)
  setupMpvIpcHandlers(DEFAULT_BACKEND_URL);

  await createMainWindow();

  // IPC handler for theme changes (Windows titleBarOverlay)
  ipcMain.on('theme:changed', (event, theme) => {
    if (process.platform === 'win32' && mainWindow) {
      const colors = getTitleBarColors(theme);
      mainWindow.setTitleBarOverlay({
        ...colors,
        height: 32 // Slightly shorter than TitleBar (33px) so border shows below controls
      });
    }
  });

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

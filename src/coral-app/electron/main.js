const { app, BrowserWindow } = require('electron');
const path = require('path');

// Keep a global reference of window to prevent garbage collection
let mainWindow = null;

const isDevelopment = process.env.NODE_ENV === 'development';
const webUrl = isDevelopment ? 'http://localhost:8081' : `file://${path.join(__dirname, '../dist/index.html')}`;

function createMainWindow() {
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

  mainWindow.loadURL(webUrl);

  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  // Open DevTools in development
  if (isDevelopment) {
    mainWindow.webContents.openDevTools();
  }
}

// App lifecycle
app.whenReady().then(() => {
  createMainWindow();

  app.on('activate', () => {
    // On macOS, re-create window when dock icon is clicked and no windows open
    if (BrowserWindow.getAllWindows().length === 0) {
      createMainWindow();
    }
  });
});

app.on('window-all-closed', () => {
  // On macOS, apps typically stay active until Cmd+Q
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

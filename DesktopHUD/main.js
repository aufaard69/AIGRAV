/**
 * AIGRAV Desktop HUD - Main Process (Phase 6.2 Window Visibility & Navigation Repair)
 * Electron frameless floating window management with backend health verification.
 * Enforces strict security boundaries, single-instance lock, reliable visibility lifecycle,
 * and unified main-process-directed navigation orchestration.
 */

const { app, BrowserWindow, ipcMain, shell } = require('electron');
const path = require('path');
const http = require('http');
const fs = require('fs');
const { spawn } = require('child_process');
const sttService = require('./stt_service');

const BACKEND_URL = 'http://127.0.0.1:8080';
const BACKEND_STATUS_URL = 'http://127.0.0.1:8080/api/status';
const HUD_URL = 'http://127.0.0.1:8080/aigrav_voice_hud.html';
const PYTHON_PATH = 'C:\\Users\\sapuser\\AppData\\Local\\Programs\\Python\\Python314\\python.exe';
const SERVER_PY = 'C:\\AIGRAV\\server.py';
const STARTUP_LOG = 'C:\\AIGRAV\\server_startup.log';

let mainWindow = null;
let isPinned = false;

// 1. Single Instance Lock (Prevent multiple HUD instances)
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
  console.log('[DESKTOP_HUD]: Another instance is already running. Exiting secondary process immediately.');
  app.quit();
  process.exit(0);
} else {
  app.on('second-instance', (event, commandLine, workingDirectory) => {
    console.log('[DESKTOP_HUD]: Second instance launch attempt detected. Focusing primary window.');
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore();
      if (!mainWindow.isVisible()) {
        console.log('[DESKTOP_HUD]: Showing previously hidden window.');
        mainWindow.show();
      }
      mainWindow.focus();

      // If still displaying splash, verify backend and transition to HUD
      checkBackendAlive(3000).then((ready) => {
        if (ready && mainWindow && !mainWindow.isDestroyed()) {
          const currentUrl = mainWindow.webContents.getURL() || '';
          if (currentUrl.includes('loading.html')) {
            console.log('[DESKTOP_HUD]: Backend verified. Transitioning splash to HUD_URL:', HUD_URL);
            mainWindow.loadURL(HUD_URL);
          }
        }
      });
    }
  });
}

/**
 * Check if existing AIGRAV backend server is responsive
 */
function checkBackendAlive(timeoutMs = 4000) {
  return new Promise((resolve) => {
    const req = http.get(BACKEND_STATUS_URL, { timeout: timeoutMs }, (res) => {
      res.resume(); // CRITICAL: consume data to release socket back to agent pool
      resolve(res.statusCode === 200);
    });
    req.on('error', () => resolve(false));
    req.on('timeout', () => {
      req.destroy();
      resolve(false);
    });
  });
}

/**
 * Ensure backend is running without creating duplicates
 */
async function ensureBackendRunning() {
  const isAlive = await checkBackendAlive(4000);
  if (isAlive) {
    console.log('[DESKTOP_HUD]: Backend server is already running on port 8080.');
    return true;
  }

  console.log('[DESKTOP_HUD]: Backend server not detected. Spawning server.py...');
  try {
    const logFd = fs.openSync(STARTUP_LOG, 'a');
    let spawnEnv = { ...process.env, PYTHONUNBUFFERED: '1' };
    const runtimeEnvFile = 'C:\\AIGRAV\\KnowledgeBase\\antigravity_runtime_env.json';
    if (fs.existsSync(runtimeEnvFile)) {
      try {
        const rtData = JSON.parse(fs.readFileSync(runtimeEnvFile, 'utf8'));
        if (rtData.ANTIGRAVITY_LS_ADDRESS) spawnEnv.ANTIGRAVITY_LS_ADDRESS = rtData.ANTIGRAVITY_LS_ADDRESS;
        if (rtData.ANTIGRAVITY_CSRF_TOKEN) spawnEnv.ANTIGRAVITY_CSRF_TOKEN = rtData.ANTIGRAVITY_CSRF_TOKEN;
      } catch (e) {
        // ignore parse error
      }
    }
    const sub = spawn(PYTHON_PATH, [SERVER_PY], {
      cwd: 'C:\\AIGRAV',
      detached: true,
      stdio: ['ignore', logFd, logFd],
      env: spawnEnv
    });
    console.log(`[DESKTOP_HUD]: Spawned server.py process (PID: ${sub.pid}). Startup log: ${STARTUP_LOG}`);
    sub.unref();
  } catch (err) {
    console.error('[DESKTOP_HUD]: Failed to spawn backend process:', err);
  }

  // Poll until ready (up to 20 attempts @ 1000ms = 20s)
  for (let i = 0; i < 20; i++) {
    await new Promise((r) => setTimeout(r, 1000));
    const ready = await checkBackendAlive(3000);
    if (ready) {
      console.log(`[DESKTOP_HUD]: Backend server became ready after ${(i + 1) * 1000}ms.`);
      return true;
    }
  }

  console.error('[DESKTOP_HUD]: Backend server failed to start within timeout.');
  return false;
}

/**
 * Create the Frameless Floating Window
 */
function createWindow(backendAlreadyAlive = false) {
  console.log('[MAIN_WINDOW]: Initializing BrowserWindow (480x720, frameless, transparent, show:false)...');
  mainWindow = new BrowserWindow({
    width: 480,
    height: 720,
    minWidth: 360,
    minHeight: 180,
    frame: false,
    transparent: true,
    hasShadow: true,
    show: false, // Explicitly start hidden to prevent flash; reveal when ready
    alwaysOnTop: false,
    backgroundColor: '#00000000',
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
      sandbox: true,
      webSecurity: true,
      enableRemoteModule: false
    }
  });

  let isWindowShown = false;
  function revealWindow(trigger) {
    if (!mainWindow || mainWindow.isDestroyed()) return;
    if (!isWindowShown) {
      isWindowShown = true;
      console.log(`[MAIN_WINDOW]: Revealing window via '${trigger}'. Bounds:`, mainWindow.getBounds());
      mainWindow.show();
      mainWindow.focus();
    }
  }

  // 1. Diagnostic Logging & Visibility Lifecycle
  mainWindow.once('ready-to-show', () => {
    console.log('[MAIN_WINDOW]: Event ready-to-show emitted.');
    revealWindow('ready-to-show');
  });

  mainWindow.on('show', () => {
    console.log('[MAIN_WINDOW]: Event show emitted. isVisible =', mainWindow.isVisible());
  });

  mainWindow.webContents.on('did-finish-load', () => {
    const loadedUrl = mainWindow.webContents.getURL();
    console.log('[MAIN_WINDOW]: Event did-finish-load for URL:', loadedUrl);
    revealWindow('did-finish-load');
  });

  mainWindow.webContents.on('did-fail-load', (event, errorCode, errorDescription, validatedURL) => {
    console.error('[MAIN_FAIL_LOAD]: Failed to load URL:', { errorCode, errorDescription, validatedURL });
    if (validatedURL === HUD_URL) {
      console.warn('[MAIN_FAIL_LOAD]: HUD_URL failed, loading loading.html splash as fallback...');
      mainWindow.loadFile(path.join(__dirname, 'loading.html'));
    }
    revealWindow('did-fail-load-fallback');
  });

  // Safety fallback: Guarantee window visibility within 1.5 seconds regardless
  setTimeout(() => {
    revealWindow('safety-timeout-fallback');
  }, 1500);

  // Pipe all renderer console messages directly into main process stdout for diagnostics
  mainWindow.webContents.on('console-message', (event, level, message, line, sourceId) => {
    const levelName = ['DEBUG', 'INFO', 'WARN', 'ERROR'][level] || 'LOG';
    console.log(`[RENDERER_${levelName}]: ${message} (at ${path.basename(sourceId || '')}:${line})`);
  });

  // 2. Diagnostic Logging: Auto-grant & log microphone / media permissions
  const MEDIA_PERMISSIONS = ['media', 'microphone', 'speech', 'audioCapture'];

  mainWindow.webContents.session.setPermissionCheckHandler((webContents, permission, requestingOrigin, details) => {
    const isGranted = MEDIA_PERMISSIONS.includes(permission);
    console.log(`[MAIN_PERM_CHECK]: permission='${permission}', origin='${requestingOrigin}', details=${JSON.stringify(details || {})}, granted=${isGranted}`);
    return isGranted;
  });

  mainWindow.webContents.session.setPermissionRequestHandler((webContents, permission, callback, details) => {
    const requestingUrl = details ? details.requestingUrl : '';
    console.log(`[MAIN_PERM_REQUEST]: permission='${permission}', requestingUrl='${requestingUrl}', details=${JSON.stringify(details || {})}`);
    if (MEDIA_PERMISSIONS.includes(permission)) {
      console.log(`[MAIN_PERM_REQUEST]: Explicitly GRANTED '${permission}' permission.`);
      return callback(true);
    }
    console.log(`[MAIN_PERM_REQUEST]: DENIED permission '${permission}'.`);
    return callback(false);
  });

  // Restrict navigation strictly to localhost/127.0.0.1:8080 and local files
  mainWindow.webContents.on('will-navigate', (event, targetUrl) => {
    try {
      const parsed = new URL(targetUrl);
      const isLocalHost = parsed.origin === 'http://localhost:8080' || parsed.origin === 'http://127.0.0.1:8080';
      if (!isLocalHost && !targetUrl.startsWith('file://')) {
        event.preventDefault();
        shell.openExternal(targetUrl);
      }
    } catch (e) {
      event.preventDefault();
    }
  });

  // Open external links in default OS browser
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });

  // Single Unified Navigation Architecture (Orchestrated by Main Process)
  if (backendAlreadyAlive) {
    console.log('[MAIN_NAV]: Backend confirmed alive. Loading HUD_URL directly:', HUD_URL);
    mainWindow.loadURL(HUD_URL).then(() => {
      console.log('[MAIN_NAV]: Direct navigation to HUD_URL succeeded.');
    }).catch((err) => {
      console.error('[MAIN_NAV_ERR]: Direct loadURL to HUD_URL failed:', err);
      mainWindow.loadFile(path.join(__dirname, 'loading.html'));
    });
  } else {
    console.log('[MAIN_NAV]: Backend not yet verified. Loading loading.html splash while polling...');
    mainWindow.loadFile(path.join(__dirname, 'loading.html'));

    ensureBackendRunning().then((ready) => {
      if (ready && mainWindow && !mainWindow.isDestroyed()) {
        console.log('[MAIN_NAV]: Backend ready. Navigating mainWindow from splash to HUD_URL:', HUD_URL);
        mainWindow.loadURL(HUD_URL).then(() => {
          console.log('[MAIN_NAV]: Navigation to HUD_URL succeeded.');
        }).catch((err) => {
          const currentUrl = (mainWindow && !mainWindow.isDestroyed()) ? (mainWindow.webContents.getURL() || '') : '';
          if (currentUrl.includes('aigrav_voice_hud.html')) {
            console.log('[MAIN_NAV]: Window already arrived at HUD_URL (ignoring redundant navigation abort).');
          } else {
            console.error('[MAIN_NAV_ERR]: Navigation to HUD_URL failed:', err);
          }
        });
      } else if (!ready && mainWindow && !mainWindow.isDestroyed()) {
        console.error('[MAIN_NAV_ERR]: Backend could not be started or verified.');
      }
    });
  }

  mainWindow.on('closed', () => {
    console.log('[MAIN_WINDOW]: Window closed event received.');
    mainWindow = null;
  });
}

// 2. IPC Window Controls
ipcMain.on('window-minimize', () => {
  if (mainWindow) {
    console.log('[IPC]: window-minimize');
    mainWindow.minimize();
  }
});

ipcMain.on('window-close', () => {
  if (mainWindow) {
    console.log('[IPC]: window-close');
    mainWindow.close();
  }
});

ipcMain.handle('window-toggle-pin', () => {
  if (mainWindow) {
    isPinned = !mainWindow.isAlwaysOnTop();
    mainWindow.setAlwaysOnTop(isPinned, 'screen-saver');
    console.log(`[IPC]: window-toggle-pin -> isPinned = ${isPinned}`);
    return isPinned;
  }
  return false;
});

ipcMain.on('window-set-mode', (event, mode) => {
  if (!mainWindow) return;
  console.log(`[IPC]: window-set-mode -> ${mode}`);
  if (mode === 'compact') {
    mainWindow.setSize(380, 210, true);
  } else {
    mainWindow.setSize(480, 720, true);
  }
});

ipcMain.handle('window-toggle-maximize', () => {
  if (mainWindow) {
    if (mainWindow.isMaximized()) {
      mainWindow.unmaximize();
      console.log('[IPC]: window-toggle-maximize -> unmaximized');
      return false;
    } else {
      mainWindow.maximize();
      console.log('[IPC]: window-toggle-maximize -> maximized');
      return true;
    }
  }
  return false;
});

ipcMain.handle('window-is-maximized', () => {
  return mainWindow ? mainWindow.isMaximized() : false;
});

ipcMain.handle('get-backend-status', async () => {
  return new Promise((resolve) => {
    http.get(BACKEND_STATUS_URL, (res) => {
      let raw = '';
      res.on('data', (chunk) => (raw += chunk));
      res.on('end', () => {
        try {
          resolve(JSON.parse(raw));
        } catch (e) {
          resolve({ error: 'invalid_json' });
        }
      });
    }).on('error', (e) => {
      resolve({ error: e.message });
    });
  });
});

ipcMain.handle('ensure-backend-running', async () => {
  console.log('[IPC]: Received ensure-backend-running request from renderer.');
  const alive = await checkBackendAlive(3500);
  if (alive) {
    console.log('[IPC]: Backend is already alive.');
    return { success: true, alreadyRunning: true, message: 'Backend is already running.' };
  }
  console.log('[IPC]: Backend offline, initiating controlled start via ensureBackendRunning()...');
  const started = await ensureBackendRunning();
  return {
    success: started,
    alreadyRunning: false,
    message: started ? 'Backend started successfully.' : 'Backend failed to start within timeout.'
  };
});

// 3. Local STT (whisper.cpp) IPC Handlers
ipcMain.handle('stt-get-status', async () => {
  return sttService.checkStatus();
});

ipcMain.handle('stt-transcribe-audio', async (event, audioBuffer) => {
  console.log('[IPC]: Received stt-transcribe-audio request.');
  return sttService.transcribe(audioBuffer);
});

// App Lifecycle
app.whenReady().then(async () => {
  console.log('[DESKTOP_HUD]: app.whenReady fired. Checking backend status...');
  const backendAlive = await checkBackendAlive(2000);
  createWindow(backendAlive);

  app.on('activate', async () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      const alive = await checkBackendAlive(2000);
      createWindow(alive);
    }
  });
});

app.on('window-all-closed', () => {
  console.log('[DESKTOP_HUD]: All windows closed. Quitting Electron app.');
  // On Windows, quit app when window is closed (backend stays running as independent daemon)
  app.quit();
});

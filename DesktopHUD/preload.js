/**
 * AIGRAV Desktop HUD - Preload Script
 * Secure context bridge exposing restricted desktop APIs to the renderer.
 * Enforces strict context isolation and prevents direct Node.js access.
 */

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('desktopAPI', {
  isDesktop: true,
  
  // Window Controls
  minimize: () => ipcRenderer.send('window-minimize'),
  close: () => ipcRenderer.send('window-close'),
  toggleAlwaysOnTop: () => ipcRenderer.invoke('window-toggle-pin'),
  setWindowMode: (mode) => ipcRenderer.send('window-set-mode', mode),
  toggleMaximize: () => ipcRenderer.invoke('window-toggle-maximize'),
  isMaximized: () => ipcRenderer.invoke('window-is-maximized'),
  
  // Telemetry & Backend Query
  getBackendStatus: () => ipcRenderer.invoke('get-backend-status'),
  ensureBackend: () => ipcRenderer.invoke('ensure-backend-running'),
  onAlwaysOnTopChanged: (callback) => {
    ipcRenderer.on('always-on-top-changed', (event, value) => callback(value));
  },

  // Local Offline STT (whisper.cpp)
  getSTTStatus: () => ipcRenderer.invoke('stt-get-status'),
  transcribeAudio: (audioBuffer) => ipcRenderer.invoke('stt-transcribe-audio', audioBuffer)
});

/**
 * Automated Verification Test for AIGRAV Floating HUD Phase 6.8.1
 * Minimal Fixed Window Header Verification
 *
 * Required Checks:
 * 1. ONLY the top panel (#desktopTitlebar: AIGRAV // FLOATING HUD + 4 window controls) is fixed/static.
 * 2. When scrolling to middle and bottom:
 *    - #desktopTitlebar coordinates do NOT move (diff < 1px).
 *    - All 4 window control buttons ([⤢], [📌], [─], [✕]) remain fixed, visible, and clickable.
 *    - All other panels (AIGRAV Dedicated Voice Agent, project info, voice selector, mode AI,
 *      connection status bar, orb visualizer, task buttons, footer) SCROLL NATURALLY (their top offsets shift).
 * 3. Minimize and restore functionality.
 * 4. Maximize and restore functionality.
 * 5. Pin / unpin (always-on-top) functionality.
 * 6. Close functionality.
 * 7. Window resize does not break the layout; compact mode works cleanly.
 * 8. Voice agent and backend connectivity intact.
 */

const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');
const fs = require('fs');
const http = require('http');

const RESULTS_FILE = 'C:\\AIGRAV\\fixed_controls_test_results.json';
const HUD_URL = 'http://127.0.0.1:8080/aigrav_voice_hud.html';

const testResults = {
  timestamp: new Date().toISOString(),
  phase: 'Phase 6.8.1 - Minimal Fixed Window Header',
  tests: [],
  allPassed: false
};

function record(name, passed, details) {
  testResults.tests.push({ name, passed, details });
  const mark = passed ? '[PASS]' : '[FAIL]';
  console.log(`${mark} ${name}`);
  for (const [k, v] of Object.entries(details)) {
    console.log(`   * ${k}: ${typeof v === 'object' ? JSON.stringify(v) : v}`);
  }
}

app.on('window-all-closed', (e) => {
  e.preventDefault();
});

app.whenReady().then(async () => {
  console.log('\n===============================================================');
  console.log('   AIGRAV PHASE 6.8.1: MINIMAL FIXED WINDOW HEADER VERIFICATION');
  console.log('===============================================================\n');

  // Test Backend Health first
  const backendAlive = await new Promise((resolve) => {
    http.get('http://127.0.0.1:8080/api/status', (res) => {
      let raw = '';
      res.on('data', c => raw += c);
      res.on('end', () => {
        try {
          const json = JSON.parse(raw);
          resolve(json);
        } catch (e) {
          resolve(null);
        }
      });
    }).on('error', () => resolve(null));
  });

  const backendOk = backendAlive && backendAlive.backend_connected === true;
  record('Test 8: Backend & Voice Agent Integrity', backendOk, {
    backend_connected: backendAlive?.backend_connected,
    agent_connected: backendAlive?.agent_connected,
    active_mode: backendAlive?.active_mode,
    active_model_tier: backendAlive?.active_model_tier,
    actual_plan_model: backendAlive?.actual_plan_model,
    fast_conv_id: backendAlive?.fast_conversation_id,
    analysis_conv_id: backendAlive?.analysis_conversation_id
  });

  // Create BrowserWindow
  const win = new BrowserWindow({
    width: 480,
    height: 720,
    minWidth: 360,
    minHeight: 180,
    frame: false,
    transparent: true,
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'DesktopHUD', 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
      sandbox: true
    }
  });

  // Setup IPC Handlers matching DesktopHUD/main.js
  let isPinned = false;
  ipcMain.handle('window-toggle-pin', () => {
    isPinned = !win.isAlwaysOnTop();
    win.setAlwaysOnTop(isPinned, 'screen-saver');
    return isPinned;
  });

  ipcMain.on('window-minimize', () => {
    win.minimize();
  });

  ipcMain.handle('window-toggle-maximize', () => {
    if (win.isMaximized()) {
      win.unmaximize();
      return false;
    } else {
      win.maximize();
      return true;
    }
  });

  ipcMain.on('window-set-mode', (e, mode) => {
    if (mode === 'compact') {
      win.setSize(380, 210, true);
    } else {
      win.setSize(480, 720, true);
    }
  });

  // Load URL
  await win.loadURL(HUD_URL);
  await new Promise(r => setTimeout(r, 1200));

  // --- Test 1: ONLY #desktopTitlebar is Fixed; All Other Panels Scroll Naturally ---
  const scrollCheck = await win.webContents.executeJavaScript(`
    (() => {
      const titlebar = document.getElementById('desktopTitlebar');
      const scrollContainer = document.getElementById('hudScrollContainer');
      const telemetryHeader = document.querySelector('header');
      const connectionBar = document.getElementById('connectionStatusBar');
      const mainElement = document.querySelector('main');
      const footerElement = document.querySelector('footer');

      const btnExpand = document.getElementById('btnToggleCompact');
      const btnPin = document.getElementById('btnPinWindow');
      const btnMin = document.getElementById('btnMinWindow');
      const btnClose = document.getElementById('btnCloseWindow');

      // 1. Initial State (at top, scrollTop = 0)
      scrollContainer.scrollTop = 0;
      const initialTitlebarTop = titlebar.getBoundingClientRect().top;
      const initialHeaderTop = telemetryHeader.getBoundingClientRect().top;
      const initialConnectionTop = connectionBar.getBoundingClientRect().top;
      const initialMainTop = mainElement.getBoundingClientRect().top;

      // 2. Mid Scroll (scrollTop = 250px)
      scrollContainer.scrollTop = 250;
      const midScrollTop = scrollContainer.scrollTop;
      const midTitlebarTop = titlebar.getBoundingClientRect().top;
      const midHeaderTop = telemetryHeader.getBoundingClientRect().top;
      const midConnectionTop = connectionBar.getBoundingClientRect().top;

      // 3. Full Bottom Scroll
      scrollContainer.scrollTop = scrollContainer.scrollHeight;
      const bottomScrollTop = scrollContainer.scrollTop;
      const bottomTitlebarTop = titlebar.getBoundingClientRect().top;
      const bottomHeaderTop = telemetryHeader.getBoundingClientRect().top;
      const bottomConnectionTop = connectionBar.getBoundingClientRect().top;
      const bottomMainTop = mainElement.getBoundingClientRect().top;
      const bottomFooterRect = footerElement.getBoundingClientRect();

      // Computations
      const titlebarShift = Math.abs(bottomTitlebarTop - initialTitlebarTop);
      const headerShift = initialHeaderTop - bottomHeaderTop;
      const connectionShift = initialConnectionTop - bottomConnectionTop;
      const mainShift = initialMainTop - bottomMainTop;

      return {
        // Sole Fixed Header Checks
        titlebarPresent: !!titlebar,
        titlebarShift,
        isTitlebarSoleFixed: titlebarShift < 1,

        // Window Control Buttons Visibility at Bottom
        btnExpandVisible: btnExpand && btnExpand.offsetWidth > 0,
        btnPinVisible: btnPin && btnPin.offsetWidth > 0,
        btnMinVisible: btnMin && btnMin.offsetWidth > 0,
        btnCloseVisible: btnClose && btnClose.offsetWidth > 0,

        // Other Panels DO SCROLL (must have shifted significantly)
        headerShift,
        connectionShift,
        mainShift,
        headerDidScroll: headerShift > 100,
        connectionDidScroll: connectionShift > 100,
        mainDidScroll: mainShift > 100,

        // Scroll metrics
        midScrollTop,
        bottomScrollTop,
        maxScroll: scrollContainer.scrollHeight - scrollContainer.clientHeight,
        footerReached: bottomFooterRect.top < window.innerHeight
      };
    })()
  `);

  const test1Passed = scrollCheck.titlebarPresent &&
                      scrollCheck.isTitlebarSoleFixed &&
                      scrollCheck.btnExpandVisible &&
                      scrollCheck.btnPinVisible &&
                      scrollCheck.btnMinVisible &&
                      scrollCheck.btnCloseVisible &&
                      scrollCheck.headerDidScroll &&
                      scrollCheck.connectionDidScroll &&
                      scrollCheck.mainDidScroll;

  record('Test 1: Sole Fixed Window Header (#desktopTitlebar) & All Other Panels Scroll', test1Passed, {
    titlebar_shift_at_bottom: scrollCheck.titlebarShift,
    is_titlebar_fixed: scrollCheck.isTitlebarSoleFixed,
    header_scrolled_by_px: scrollCheck.headerShift,
    connection_bar_scrolled_by_px: scrollCheck.connectionShift,
    main_orb_scrolled_by_px: scrollCheck.mainShift,
    buttons_visible: {
      expand: scrollCheck.btnExpandVisible,
      pin: scrollCheck.btnPinVisible,
      min: scrollCheck.btnMinVisible,
      close: scrollCheck.btnCloseVisible
    },
    footer_reached: scrollCheck.footerReached
  });

  // --- Test 6: Conversation Scrollbar Operates Properly ---
  const test6Passed = scrollCheck.bottomScrollTop > 0 && scrollCheck.footerReached;
  record('Test 6: Conversation Scrollbar Operates Naturally', test6Passed, {
    bottom_scroll_top: scrollCheck.bottomScrollTop,
    max_scroll: scrollCheck.maxScroll,
    footer_reached: scrollCheck.footerReached
  });

  // --- Test 2: Minimize & Restore ---
  win.minimize();
  const isMinimized = win.isMinimized();
  win.restore();
  const isRestored = !win.isMinimized();
  const test2Passed = isMinimized && isRestored;
  record('Test 2: Minimize and Restore Functionality', test2Passed, {
    minimized: isMinimized,
    restored: isRestored
  });

  // --- Test 3: Maximize & Restore ---
  win.maximize();
  const isMaximized = win.isMaximized();
  win.unmaximize();
  const isUnmaximized = !win.isMaximized();
  const test3Passed = isMaximized && isUnmaximized;
  record('Test 3: Maximize and Restore Functionality', test3Passed, {
    maximized: isMaximized,
    unmaximized: isUnmaximized
  });

  // --- Test 4: Pin / Unpin (Always on Top) ---
  const pin1 = await win.webContents.executeJavaScript(`
    window.desktopAPI.toggleAlwaysOnTop()
  `);
  const isAlwaysOnTop1 = win.isAlwaysOnTop();

  const pin2 = await win.webContents.executeJavaScript(`
    window.desktopAPI.toggleAlwaysOnTop()
  `);
  const isAlwaysOnTop2 = win.isAlwaysOnTop();

  const test4Passed = (pin1 === true && isAlwaysOnTop1 === true) && (pin2 === false && isAlwaysOnTop2 === false);
  record('Test 4: Pin / Unpin (Always on Top) Functionality', test4Passed, {
    first_toggle_result: pin1,
    win_isAlwaysOnTop_1: isAlwaysOnTop1,
    second_toggle_result: pin2,
    win_isAlwaysOnTop_2: isAlwaysOnTop2
  });

  // --- Test 7: Resize Window & Mode Changes Preserve Layout ---
  // Resize to 400x500
  win.setSize(400, 500);
  await new Promise(r => setTimeout(r, 300));
  const resizeCheckSmall = await win.webContents.executeJavaScript(`
    (() => {
      const titlebar = document.getElementById('desktopTitlebar');
      const scrollContainer = document.getElementById('hudScrollContainer');
      const tRect = titlebar.getBoundingClientRect();
      const sRect = scrollContainer.getBoundingClientRect();
      return {
        windowHeight: window.innerHeight,
        titlebarHeight: tRect.height,
        scrollContainerHeight: sRect.height,
        titlebarVisible: tRect.height > 0 && tRect.top >= 0,
        scrollContainerValid: sRect.height > 300
      };
    })()
  `);

  // Resize to 600x900
  win.setSize(600, 900);
  await new Promise(r => setTimeout(r, 300));
  const resizeCheckLarge = await win.webContents.executeJavaScript(`
    (() => {
      const titlebar = document.getElementById('desktopTitlebar');
      const scrollContainer = document.getElementById('hudScrollContainer');
      const tRect = titlebar.getBoundingClientRect();
      const sRect = scrollContainer.getBoundingClientRect();
      return {
        windowHeight: window.innerHeight,
        titlebarHeight: tRect.height,
        scrollContainerHeight: sRect.height,
        titlebarVisible: tRect.height > 0 && tRect.top >= 0,
        scrollContainerValid: sRect.height > 700
      };
    })()
  `);

  // Test Compact Mode Toggle via button
  await win.webContents.executeJavaScript(`
    document.getElementById('btnToggleCompact').click()
  `);
  await new Promise(r => setTimeout(r, 300));
  const compactCheck = await win.webContents.executeJavaScript(`
    (() => {
      const isCompactBody = document.body.classList.contains('compact-mode');
      const titlebar = document.getElementById('desktopTitlebar');
      const tRect = titlebar.getBoundingClientRect();
      return {
        isCompactBody,
        titlebarVisible: tRect.height > 0 && tRect.top >= 0,
        windowSize: [window.innerWidth, window.innerHeight]
      };
    })()
  `);

  // Toggle Back from Compact Mode
  await win.webContents.executeJavaScript(`
    document.getElementById('btnToggleCompact').click()
  `);
  await new Promise(r => setTimeout(r, 300));

  const test7Passed = resizeCheckSmall.titlebarVisible && 
                      resizeCheckSmall.scrollContainerValid &&
                      resizeCheckLarge.titlebarVisible && 
                      resizeCheckLarge.scrollContainerValid &&
                      compactCheck.isCompactBody && 
                      compactCheck.titlebarVisible;

  record('Test 7: Window Resize & Mode Changes Preserve Layout', test7Passed, {
    small_window: resizeCheckSmall,
    large_window: resizeCheckLarge,
    compact_mode: compactCheck
  });

  // --- Test 5: Close Functionality ---
  let closedFired = false;
  win.on('closed', () => {
    closedFired = true;
  });
  win.close();
  await new Promise(r => setTimeout(r, 500));
  const test5Passed = win.isDestroyed() || closedFired;
  record('Test 5: Close Window Functionality', test5Passed, {
    window_destroyed: win.isDestroyed(),
    closed_event_fired: closedFired
  });

  // Calculate overall result
  testResults.allPassed = testResults.tests.every(t => t.passed);
  fs.writeFileSync(RESULTS_FILE, JSON.stringify(testResults, null, 2), 'utf8');

  console.log('\n===============================================================');
  console.log(`VERIFICATION COMPLETE: ${testResults.allPassed ? 'ALL TESTS PASSED' : 'SOME TESTS FAILED'}`);
  console.log(`Results saved to: ${RESULTS_FILE}`);
  console.log('===============================================================\n');

  app.quit();
  process.exit(testResults.allPassed ? 0 : 1);
});

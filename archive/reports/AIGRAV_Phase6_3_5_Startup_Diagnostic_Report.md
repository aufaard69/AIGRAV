# AIGRAV Phase 6.3.5 — HUD Backend Startup Failure Diagnostic & Resolution Report
**Resolution of "Menunggu inisialisasi backend AIGRAV..." and Multi-Process Port Collision**  
*Date: 2026-10-11 | Environment: Windows 11, Node.js / Electron Desktop HUD, Python 3.14 Backend, Revit Bridge*

---

## 1. Executive Summary

During Phase 6.3.5, the AIGRAV Desktop Floating HUD remained indefinitely stuck on the splash screen:
`"Menunggu inisialisasi backend AIGRAV..."`

Launcher logs reported:
- Backend not detected.
- Attempting to spawn `server.py`.
- Backend failed to start within timeout.
- Backend could not be started or verified.

An end-to-end audit revealed that the backend failure was not caused by a missing file or startup crash, but by **socket reuse port collisions on Windows** and **Node.js IPv6 resolution timeouts**. Multiple independent launch scripts (`launch_desktop_hud.ps1` and Electron `main.js`) were spawning overlapping instances of `server.py` that all bound to `0.0.0.0:8080` simultaneously due to `allow_reuse_address = True`. This caused Windows to corrupt incoming TCP accept queues and issue `ECONNRESET` / `RemoteDisconnected` to all health check probes.

Following targeted, minimal fixes:
1. **Single Instance Exclusivity**: Enforced single-instance checks and disabled socket reuse (`ThreadingHTTPServer.allow_reuse_address = False`) in `server.py`.
2. **Explicit IPv4 Alignment**: Replaced `localhost` with `127.0.0.1` across `main.js`, `loading.html`, and `launch_desktop_hud.ps1`, eliminating IPv6 Happy Eyeballs DNS resolution latency.
3. **Extended Probe Timeouts**: Increased initial health check probe timeout from 2000ms to 4000ms in `main.js`.
4. **Coordinated Multi-Launch Deduplication**: Secondary launcher invocations now detect the running backend and exit immediately without spawning duplicate processes or windows.

---

## 2. Root Cause Analysis & Empirical Evidence

### Root Cause 1: `SO_REUSEADDR` Port 8080 Contention on Windows
- **Mechanism**: Python's `ThreadingHTTPServer` sets `allow_reuse_address = True` by default. On Windows, `SO_REUSEADDR` permits multiple processes to bind to the exact same `(0.0.0.0, 8080)` socket address.
- **Evidence**:
  Inspection before cleanup revealed 4 concurrent `python.exe` instances all bound to port 8080:
  - PID `23816` (orphan from prior turn)
  - PID `6380` (orphan from prior turn)
  - PID `35736` (spawned by `launch_desktop_hud.ps1`)
  - PID `28576` (spawned by Electron `main.js`)
- **Impact**: When any client (Node.js `http.get`, browser `fetch`, or Python `urllib`) attempted to connect, Windows randomly distributed packets across processes, resulting in:
  ```text
  127.0.0.1 error: ECONNRESET socket hang up
  RemoteDisconnected: Remote end closed connection without response
  ```

### Root Cause 2: IPv6 (`::1`) Resolution Timeout in Node.js
- **Mechanism**: In Node.js 18+ and Electron, `localhost` resolves to `::1` (IPv6) by default.
- **Evidence**: `server.py` binds to IPv4 `0.0.0.0:8080`. When Electron's `main.js` called `http.get('http://localhost:8080/api/status', { timeout: 2000 })`, the request stalled on the IPv6 connection attempt. At 2000ms, `main.js` timed out, aborted the socket (`req.destroy()`), classified the backend as dead, and spawned a duplicate `server.py` process.

### Root Cause 3: Dual Uncoordinated Spawners
- **Mechanism**: Both `launch_desktop_hud.ps1` (line 34) and `main.js` (line 71) independently attempted to spawn `server.py` when `/api/status` failed to answer within their initial short polling windows. This created a compounding cascade of duplicate processes on every launch attempt.

---

## 3. Implemented Fixes (Minimal, High-Precision Changes)

### 3.1 [`server.py`](file:///C:/AIGRAV/server.py)
1. **Proactive Health Probing Before Socket Bind**:
   ```python
   # 1. Proactive Single-Instance Verification
   try:
       import urllib.request
       check_req = urllib.request.Request(f"http://127.0.0.1:{PORT}/api/status")
       with urllib.request.urlopen(check_req, timeout=1.5) as check_res:
           if check_res.status == 200:
               print(f"[AIGRAV_BACKEND]: An active backend instance is already running on port {PORT}. Exiting redundant instance.")
               sys.exit(0)
   except Exception:
       pass
   ```
2. **Strict Port Exclusivity on Windows**:
   ```python
   ThreadingHTTPServer.allow_reuse_address = False
   try:
       server = ThreadingHTTPServer(("0.0.0.0", PORT), VoiceHTTPHandler)
   except OSError as ex:
       print(f"[AIGRAV_BACKEND_ERROR]: Failed to bind port {PORT}: {ex}. Another process may be using this port.", file=sys.stderr)
       sys.exit(1)
   ```

### 3.2 [`main.js`](file:///C:/AIGRAV/DesktopHUD/main.js)
1. Changed `BACKEND_URL`, `BACKEND_STATUS_URL`, and `HUD_URL` from `localhost:8080` to `127.0.0.1:8080`.
2. Increased `checkBackendAlive(timeoutMs = 4000)` timeout from 2000ms to 4000ms to prevent premature timeout aborts.
3. Increased polling retries in `ensureBackendRunning()` to 20 attempts @ 1000ms.
4. Updated `will-navigate` handler to permit both `127.0.0.1:8080` and `localhost:8080`.
5. In `app.on('second-instance')`, added logic to transition the window to `HUD_URL` if it was still displaying `loading.html`.

### 3.3 [`loading.html`](file:///C:/AIGRAV/DesktopHUD/loading.html)
1. Updated polling fetch target to `http://127.0.0.1:8080/api/status`.
2. Added automatic transition (`window.location.replace('http://127.0.0.1:8080/aigrav_voice_hud.html')`) once HTTP 200 is received.

### 3.4 [`launch_desktop_hud.ps1`](file:///C:/AIGRAV/DesktopHUD/launch_desktop_hud.ps1)
1. Updated `Invoke-RestMethod` probe target to `http://127.0.0.1:8080/api/status`.
2. Increased polling timeout and retry count to 20 attempts @ 1000ms.

---

## 4. Verification Results Against Acceptance Criteria

| Criteria # | Requirement | Verified Result | Status |
|---|---|---|---|
| **1** | Exactly one backend instance on port 8080 | `netstat -ano` confirms single PID `39756` listening on `0.0.0.0:8080` | **PASS** |
| **2** | `/api/status` responds successfully | `http://127.0.0.1:8080/api/status` returns HTTP 200 in `< 30ms` | **PASS** |
| **3** | Persistent agent session intact | Session `1121ceff-2b54-4f33-bb94-1041b20bd92c` preserved at Turn `#5` | **PASS** |
| **4** | Revit Bridge available | `http://127.0.0.1:8765/` active; detected project `MIM-FC-WFG-LD` | **PASS** |
| **5** | HUD exits loading state & renders UI | Electron logged `Direct navigation to HUD_URL succeeded` & mic hardware access granted | **PASS** |
| **6** | Duplicate launches prevented | Second execution exited immediately; zero duplicate python/electron processes | **PASS** |
| **7** | End-to-end voice query operational | Tested prompt: agent reasoned, called Revit MCP tools, synthesized TTS in 18.38s | **PASS** |

### Verified Output Sample (End-to-End Test):
> **Prompt**: *"Halo AIGRAV, konfirmasi bahwa sistem backend dan HUD normal."*  
> **Agent Spoken Reply**: *"Halo! Saya konfirmasikan bahwa sistem backend dan tampilan HUD saat ini berjalan normal dan tersinkronisasi dengan baik. Koneksi ke Revit aktif terhubung dengan proyek MIM-FC-WFG-LD (View: DETAIL GRATING). Layanan suara dan automasi siap digunakan—ada yang bisa saya bantu selanjutnya?"*  
> **Badge**: `TOOL EKSEKUSI: call_mcp_tool, call_mcp_tool` (`text-emerald-400 font-bold`)  
> **Active Session**: `1121ceff...` | **Turn**: `#5` | **Turnaround**: `18.38s`

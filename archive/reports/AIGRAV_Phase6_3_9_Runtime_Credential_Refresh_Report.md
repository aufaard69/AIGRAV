# AIGRAV Phase 6.3.9 — Runtime Credential Refresh Fix Report

**Tanggal:** 12 Oktober 2026  
**Status:** SUCCESSFUL & FULLY VERIFIED  
**Komponen:** `antigravity_voice_adapter.py`, `server.py`, Antigravity Agent Runtime

---

## 1. Executive Summary & Root Cause

Berdasarkan temuan audit empiris pada **Fase 6.3.8**, teridentifikasi cacat teknis (*defect*) spesifik yang menyebabkan backend kehilangan koneksi ke runtime Antigravity setelah IDE me-restart `language_server.exe`:

1. **In-Memory Credential Stale Return:**  
   Fungsi `ensure_runtime_credentials()` sebelumnya mengembalikan kredensial dari `os.environ` secara instan tanpa memvalidasi apakah alamat `ANTIGRAVITY_LS_ADDRESS` masih aktif menerima koneksi soket loopback. Akibatnya, saat `language_server.exe` di-restart dengan PID dan port baru, proses backend Python yang sedang berjalan tetap mempertahankan port lama di memori.
2. **Missing Windows Connection Error Pattern:**  
   Fungsi `execute_agentapi_cmd()` sebelumnya hanya mengevaluasi pola teks error `"connection refused"` (khas kernel Linux/Unix). Pada sistem operasi Windows, kegagalan koneksi ke port loopback yang mati mengembalikan pesan error:  
   `"connectex: No connection could be made because the target machine actively refused it."`  
   atau `"rpc error: code = Unavailable desc = connection error: desc = transport: Error while dialing..."`.  
   Karena pola teks Windows ini tidak cocok, blok *rediscovery retry* tidak terpanggil, sehingga pemanggilan `get-conversation-metadata` gagal dan status backend jatuh ke `"session_initializing"`.
3. **Socket TIME_WAIT Collision pada Restart Cepat:**  
   Di `server.py`, penonaktifan `ThreadingHTTPServer.allow_reuse_address = False` menyebabkan kegagalan `[WinError 10048]` jika server di-restart dalam rentang 30-60 detik saat soket Windows masih berada pada state TCP `TIME_WAIT`.

Fase **6.3.9** berhasil memperbaiki seluruh masalah tersebut secara terukur dan minimal, tanpa mengubah arsitektur sesi, MCP, TTS/STT, maupun antarmuka pengguna.

---

## 2. File Backups

Sebelum melakukan modifikasi, backup dibuat dengan aman:
- `C:\AIGRAV\antigravity_voice_adapter.py.phase6_3_9.bak`
- `C:\AIGRAV\server.py.phase6_3_9.bak`

---

## 3. Exact Code Changes

### A. Modifikasi `C:\AIGRAV\antigravity_voice_adapter.py`

#### 1. Penambahan Helper `is_address_alive()` dan `is_connection_failure()`
```python
def is_address_alive(addr: Optional[str], timeout: float = 0.3) -> bool:
    """Check if address host:port is accepting connections."""
    if not addr or ":" not in addr:
        return False
    try:
        host, port = addr.split(":")
        import socket
        s = socket.create_connection((host, int(port)), timeout=timeout)
        s.close()
        return True
    except Exception:
        return False

def is_connection_failure(err_text: str) -> bool:
    """Classify if error text indicates missing, invalid, or stale connection to language_server."""
    if not err_text:
        return False
    lower = err_text.lower()
    patterns = [
        "antigravity_ls_address is not set",
        "missing csrf token",
        "missing authorization",
        "unauthenticated",
        "connection refused",
        "actively refused",
        "transport: error while dialing",
        "unavailable desc = connection error",
        "target machine actively refused",
        "no connection could be made",
    ]
    return any(p in lower for p in patterns)
```

#### 2. Validasi In-Memory Address di `ensure_runtime_credentials()`
```python
def ensure_runtime_credentials() -> Dict[str, str]:
    ls_addr = os.environ.get("ANTIGRAVITY_LS_ADDRESS")
    csrf_token = os.environ.get("ANTIGRAVITY_CSRF_TOKEN")

    # 1. Validate in-memory credentials if present
    if ls_addr and csrf_token:
        if is_address_alive(ls_addr, timeout=0.3):
            try:
                RUNTIME_ENV_FILE.parent.mkdir(parents=True, exist_ok=True)
                with open(RUNTIME_ENV_FILE, "w", encoding="utf-8") as f:
                    json.dump({"ANTIGRAVITY_LS_ADDRESS": ls_addr, "ANTIGRAVITY_CSRF_TOKEN": csrf_token}, f, indent=2)
            except Exception:
                pass
            return {"ANTIGRAVITY_LS_ADDRESS": ls_addr, "ANTIGRAVITY_CSRF_TOKEN": csrf_token}
        else:
            # Stale in-memory credentials; invalidate to proceed to cache / discovery
            os.environ.pop("ANTIGRAVITY_LS_ADDRESS", None)
            os.environ.pop("ANTIGRAVITY_CSRF_TOKEN", None)

    # 2. Check persistent file cache (with liveness validation)
    if RUNTIME_ENV_FILE.exists():
        try:
            with open(RUNTIME_ENV_FILE, "r", encoding="utf-8") as f:
                cached = json.load(f)
                cached_addr = cached.get("ANTIGRAVITY_LS_ADDRESS")
                cached_token = cached.get("ANTIGRAVITY_CSRF_TOKEN")
                if cached_addr and cached_token:
                    if is_address_alive(cached_addr, timeout=0.3):
                        os.environ["ANTIGRAVITY_LS_ADDRESS"] = cached_addr
                        os.environ["ANTIGRAVITY_CSRF_TOKEN"] = cached_token
                        return {"ANTIGRAVITY_LS_ADDRESS": cached_addr, "ANTIGRAVITY_CSRF_TOKEN": cached_token}
        except Exception:
            pass

    # 3. Auto-discover from running language_server.exe
    disc_addr, disc_token = discover_runtime_credentials()
    if disc_addr and disc_token:
        os.environ["ANTIGRAVITY_LS_ADDRESS"] = disc_addr
        os.environ["ANTIGRAVITY_CSRF_TOKEN"] = disc_token
        try:
            RUNTIME_ENV_FILE.parent.mkdir(parents=True, exist_ok=True)
            with open(RUNTIME_ENV_FILE, "w", encoding="utf-8") as f:
                json.dump({"ANTIGRAVITY_LS_ADDRESS": disc_addr, "ANTIGRAVITY_CSRF_TOKEN": disc_token}, f, indent=2)
            print(f"[AIGRAV_RUNTIME]: Auto-discovered Antigravity Language Server at {disc_addr}")
        except Exception:
            pass
        return {"ANTIGRAVITY_LS_ADDRESS": disc_addr, "ANTIGRAVITY_CSRF_TOKEN": disc_token}

    return {}
```

#### 3. Bounded Retry Berbasis Error Classification di `execute_agentapi_cmd()`
```python
def execute_agentapi_cmd(cmd: List[str], timeout: Optional[float] = None) -> subprocess.CompletedProcess:
    ensure_runtime_credentials()
    env = os.environ.copy()
    proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env, timeout=timeout)
    err = proc.stderr or proc.stdout or ""
    if proc.returncode != 0 and is_connection_failure(err):
        # Invalidate dead in-memory credentials
        os.environ.pop("ANTIGRAVITY_LS_ADDRESS", None)
        os.environ.pop("ANTIGRAVITY_CSRF_TOKEN", None)
        disc_addr, disc_token = discover_runtime_credentials()
        if disc_addr and disc_token:
            os.environ["ANTIGRAVITY_LS_ADDRESS"] = disc_addr
            os.environ["ANTIGRAVITY_CSRF_TOKEN"] = disc_token
            try:
                RUNTIME_ENV_FILE.parent.mkdir(parents=True, exist_ok=True)
                with open(RUNTIME_ENV_FILE, "w", encoding="utf-8") as f:
                    json.dump({"ANTIGRAVITY_LS_ADDRESS": disc_addr, "ANTIGRAVITY_CSRF_TOKEN": disc_token}, f, indent=2)
            except Exception:
                pass
            env = os.environ.copy()
            time.sleep(1.0)
            proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env, timeout=timeout)
    return proc
```

### B. Modifikasi `C:\AIGRAV\server.py`
Mengizinkan penggunaan kembali alamat soket pada level server (`allow_reuse_address = True`) untuk menghindari `WinError 10048` akibat soket dalam status `TIME_WAIT` saat server di-restart cepat. Keamanan proses tunggal tetap dijamin penuh oleh pengecekan proactive HTTP probe di port 8080 sebelum soket dibuka.

---

## 4. Test Commands & Empirical Results

### Pengujian 1: Validasi Klasifikasi Error Koneksi Windows
- **Perintah:** Eksekusi unit test `scratch/test_credential_refresh.py`
- **Hasil:**
  - `connectex: ... actively refused it` -> `True` (**PASS**)
  - `rpc error: code = Unavailable ... transport: Error while dialing` -> `True` (**PASS**)
  - `ANTIGRAVITY_LS_ADDRESS is not set` -> `True` (**PASS**)
  - `missing CSRF token` -> `True` (**PASS**)
  - `Element ID 12345 not found in Revit model` -> `False` (**PASS**, tidak memicu retry sembarangan)
  - `SyntaxError in Python script` -> `False` (**PASS**)

### Pengujian 2: Deteksi Port Mati vs Port Hidup
- **Perintah:** Uji soket loopback pada port mati `localhost:59998` vs port hidup `localhost:53846`.
- **Hasil:**
  - `is_address_alive('localhost:59998')` -> `False` (**PASS**)
  - `is_address_alive('localhost:53846')` -> `True` (**PASS**)

### Pengujian 3: Simulasi Alamat Stale di In-Memory `os.environ`
- **Perintah:** Set `os.environ["ANTIGRAVITY_LS_ADDRESS"] = "localhost:59999"`, lalu panggil `ensure_runtime_credentials()`.
- **Hasil:** Alamat stale terdeteksi mati dalam 0.3 detik, dibuang dari `os.environ`, dan berhasil diperbarui secara otomatis ke server aktif `localhost:53846` (**PASS**).

### Pengujian 4: Re-use Alamat Sehat Tanpa Overhead
- **Perintah:** Panggil `ensure_runtime_credentials()` berturut-turut saat alamat sudah valid.
- **Hasil:** Alamat valid langsung digunakan kembali tanpa overhead WMI query (**PASS**).

### Pengujian 5: Bounded Non-Infinite Retry
- **Perintah:** Panggil `execute_agentapi_cmd` dengan conversation ID palsu `00000000-0000...`.
- **Hasil:** Return code 1 dikembalikan secara terkontrol tanpa adanya perulangan tak berujung (**PASS**).

### Pengujian 6: Integritas Proses Tunggal Backend
- **Perintah:** `Get-NetTCPConnection -LocalPort 8080 -State Listen` & `Get-CimInstance Win32_Process`
- **Hasil:** Tepat 1 proses `python.exe` (`PID 26964`) listening pada `0.0.0.0:8080` (**PASS**).

### Pengujian 7: Dua Permintaan Berturut-turut pada Sesi yang Sama
- **Permintaan 1 (Turn #3):** *"AIGRAV, apa rekomendasi mutu beton f'c untuk struktur kolom gedung bertingkat menengah?"*
  - **Hasil:** Sesi `16ed9184...` merespons rinci dalam Bahasa Indonesia (rekomendasi f'c 25-35 MPa, SNI 2847, pembagian zona lantai dasar vs atas).
  - **Status Giliran:** Tercatat sebagai **Turn #3** pada sesi `16ed9184-422f-4e90-a611-0e2ffd9ee0c5`.
- **Permintaan 2 (Turn #4):** *"Bagaimana pengaruh pemilihan mutu beton tersebut terhadap ukuran dimensi penampang kolom?"*
  - **Hasil:** Sesi `16ed9184...` melanjutkan konteks percakapan dengan rumus kapasitas aksial $P_n$, ilustrasi reduksi penampang dari 600x600 ke 500x500 mm (hemat 30% luas), serta pertimbangan kekakuan drift.
  - **Status Giliran:** Tercatat sebagai **Turn #4** pada sesi `16ed9184-422f-4e90-a611-0e2ffd9ee0c5`.
- **Pemeriksaan Disk State:** File `voice_session_state.json` mencatat `history_count: 4` dengan sesi persisten `16ed9184-422f-4e90-a611-0e2ffd9ee0c5`. Tidak ada sesi baru yang dibuat secara diam-diam.

---

## 5. Matriks Hasil Pengujian (Acceptance Matrix)

| Skenario Pengujian | Target Kriteria | Hasil Pengujian | Status |
| :--- | :--- | :--- | :---: |
| **Healthy Runtime** | Kredensial aktif digunakan kembali tanpa overhead discovery | Alamat valid langsung digunakan | **PASSED** |
| **Stale Runtime Address** | Invalidate in-memory address mati & auto-discover server aktif | Otomatis refresh ke port aktif | **PASSED** |
| **Failed Discovery** | Return kegagalan terukur tanpa loop tak terbatas | Bounded return 1x retry | **PASSED** |
| **Dua Kueri Berturut-turut** | Menjawab pada sesi yang sama secara berkesinambungan | Turn #3 dan Turn #4 terselesaikan sempurna | **PASSED** |
| **Backend Process Single** | Tepat 1 proses Python di port 8080 | Terverifikasi PID 26964 tunggal | **PASSED** |
| **Kontinuitas Sesi ID** | Sesi `16ed9184...` dipertahankan utuh | Tetap `16ed9184...`, history count naik ke 4 | **PASSED** |
| **Revit Bridge Isolation** | Port 8765 tidak terganggu | Port 8765 tetap stabil | **PASSED** |

---

## 6. Sisa Limitasi & Catatan

1. **Dependensi language_server.exe:** Auto-discovery memerlukan minimal 1 proses `language_server.exe` aktif di sistem Windows (dijalankan oleh IDE Antigravity). Jika seluruh IDE Antigravity ditutup total oleh pengguna, sistem mengembalikan pesan suara ramah: *"Runtime agen Antigravity sedang tidak terhubung. Pastikan Antigravity sedang berjalan."*
2. **Kerahasiaan Kredensial:** Seluruh token dan kredensial CSRF tidak pernah diekspos dalam laporan atau berkas log.

# AIGRAV FRAMELESS FLOATING DESKTOP HUD

Aplikasi desktop frameless berbasis Electron untuk antarmuka suara otonom AIGRAV BIM Intelligence. Menampilkan UI Voice HUD melayang (*floating*) di atas viewport Autodesk Revit tanpa tab browser, address bar, atau toolbar sistem.

---

## 1. Arsitektur & Spesifikasi

- **Shell Framework:** Electron v44.7.0 (Chromium + Node.js internal).
- **Jendela:** Frameless window (`frame: false`), transparent backdrop (`transparent: true`), rounded borders 18px dengan efek glassmorphism cyan/slate.
- **Ukuran Jendela:**
  - **Expanded Mode (Default):** 480 × 720 px (Menampilkan telemetry, dialog history, kontrol QTO, dan artefak).
  - **Compact Mode:** 380 × 210 px (Widget minimalis hanya menampilkan Core Orb, status suara, dan tombol Mute).
- **Area Drag:** Bilah atas `#desktopTitlebar` memiliki properti `-webkit-app-region: drag`, sedangkan seluruh tombol kontrol dan interaksi memiliki `-webkit-app-region: no-drag`.
- **Keamanan:**
  - `nodeIntegration: false`
  - `contextIsolation: true`
  - Bounded IPC preload via `window.desktopAPI`.
  - Navigasi dibatasi ketat hanya ke `http://localhost:8080`.
- **Backend Integrasi:**
  - Menggunakan backend Python yang sudah ada di port 8080 (`server.py`).
  - Tidak menduplikasi proses backend jika sudah berjalan.
  - Penutupan jendela HUD **tidak** mematikan backend, **tidak** mereset sesi Antigravity, dan **tidak** mematikan Revit atau Revit Bridge.

---

## 2. Cara Menjalankan

### Cara Cepat (Rekomendasi)
Cukup jalankan file batch di root `C:\AIGRAV`:
```cmd
C:\AIGRAV\launch_floating_hud.bat
```
Atau melalui PowerShell:
```powershell
& "C:\AIGRAV\DesktopHUD\launch_desktop_hud.ps1"
```

### Jalur Development Manual
```cmd
set "PATH=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Microsoft\VisualStudio\NodeJs;%PATH%"
cd C:\AIGRAV\DesktopHUD
node node_modules\electron\cli.js .
```

---

## 3. Fitur Kontrol Jendela

Pada bilah atas (titlebar) HUD terdapat tombol-tombol kontrol:
- **`⤢` / `🗖` (Toggle Mode):** Beralih antara Compact Mode (widget melayang kecil) dan Expanded Mode (tampilan penuh).
- **`📌` (Always on Top):** Mengunci jendela agar selalu berada di posisi paling depan di atas jendela Autodesk Revit.
- **`─` (Minimize):** Meminimalkan jendela ke taskbar Windows.
- **`✕` (Close):** Menutup jendela HUD tanpa mematikan backend dan tanpa menghapus sesi.

---

## 4. Prosedur Rollback

Jika pengguna ingin kembali menggunakan browser web standar (Edge / Chrome) seperti semula:
1. Tidak ada perubahan yang merusak file produksi.
2. Buka browser web favorit Anda dan akses:
   ```text
   http://localhost:8080/aigrav_voice_hud.html
   ```
3. UI akan otomatis mendeteksi ketiadaan desktop shell dan berjalan secara normal sebagai halaman web standar.

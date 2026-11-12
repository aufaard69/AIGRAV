# AIGRAV — AI-Powered BIM & Structural Engineering Suite

AIGRAV adalah platform orkestrasi AI untuk Autodesk Revit dan Structural Engineering yang mengintegrasikan Model Context Protocol (MCP), database profil baja SNI, standar sambungan struktur, dan add-in Revit Bridge.

---

## 📦 Komponen & Database yang Disertakan

1. **Database Baja & Profil SNI**:
   - `Revit/AIGRAV.SteelDatabase.csv`: 230 katalog profil WF & H-Beam lengkap dengan berat/meter dan panjang standar.
   - `Skills/SteelDatabase/AIGRAV.SteelDatabase.json` & `AIGRAV_SteelMasterDatabase_v2.json`.
2. **Database Sambungan Standar**:
   - `KnowledgeBase/SteelConnectionDatabase/AIGRAV_SteelConnectionDatabase_v1.json`: Database sambungan resmi acuan STD-BJ-01 s/d 07 & STD-PDS-01 s/d 15 (Base Plate, End Plate, Gusset, Baut, Las).
   - `KnowledgeBase/SteelConnectionDatabase/AIGRAV.ConnectionDatabase.json`.
3. **Revit Connection Family Library**:
   - `ConnectionLibrary/AIGRAV_BasePlate_WF400.rfa`
   - `ConnectionLibrary/AIGRAV_EndPlate_WF400.rfa`
4. **Revit Bridge & Skills (Precompiled)**:
   - `AIGRAV.RevitBridge.dll` (Add-in C# dengan HTTP listener di port 8765)
   - Skills DLL: `AIGRAV.Skills.BimAudit.dll`, `AIGRAV.Skills.QTO.dll`, `AIGRAV.Skills.ConnectionGenerator.dll`, `AIGRAV.Skills.SteelTypeGenerator.dll`, dll.
5. **AIGRAV MCP Python Server**:
   - `MCP/TestServer/server.py`: Server Model Context Protocol (MCP) dengan 50+ tool otomatisasi Revit.
6. **Antigravity Plugin & Rules**:
   - Plugin `aigrav-bim` (dengan skill `revit-qto-schedule-audit` dan `install-aigrav`).
   - `AGENTS.md`: Revit Standard Operating Protocol (Mandatory Workflow).

---

## 🚀 Cara Instalasi di PC Baru

### Cara 1: Langsung Lewat Antigravity Prompt (Paling Mudah)
1. Copy folder / zip AIGRAV ke PC baru dan tempatkan di `C:\AIGRAV`.
2. Buka aplikasi **Antigravity** di PC tersebut.
3. Di kolom prompt chat Antigravity, cukup ketik:
   > **"install aigrav"**  atau  **"instalkan aigrav sekarang"**
4. Antigravity akan otomatis mendeteksi script installer, mengeksekusi instalasi, mendaftarkan Revit Add-in, mengonfigurasi Python MCP dan plugin, serta memberikan laporan status lengkap!

### Cara 2: 1-Click Double Click Windows Explorer
1. Klik kanan pada file **`install.bat`**.
2. Pilih **Run as Administrator** (atau double-click biasa).
3. Script otomatis melakukan semua konfigurasi dalam beberapa detik.

---

## 🔍 Pemeriksaan & Diagnostik (Health Check)
Untuk memverifikasi instalasi kapan saja, jalankan di PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File C:\AIGRAV\Verify-AIGRAV.ps1
```

---

## 🛠️ Cara Penggunaan Setelah Instalasi
1. Buka aplikasi **Autodesk Revit** (Revit 2024 / 2025 / 2026). Tab AIGRAV akan muncul di Ribbon Revit.
2. Buka **Antigravity**. Antigravity akan otomatis mendeteksi MCP Server `aigrav-test` dan plugin `aigrav-bim`.
3. Anda dapat langsung memberikan instruksi dalam bahasa alami di Antigravity:
   - *"Cek status Revit dan elemen aktif"*
   - *"Buat sambungan kolom ke rafter WF 400 sesuai standar STD-BJ"*
   - *"Jalankan QTO schedule audit struktur"*

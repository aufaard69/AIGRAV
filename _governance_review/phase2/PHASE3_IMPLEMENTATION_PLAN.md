# AIGRAV Phase 3 Implementation Plan (Master Execution Blueprint)

**Status:** `APPROVED BLUEPRINT — READY FOR PHASE 3 EXECUTION`  
**Tanggal Penyusunan:** 12 November 2026  
**Otoritas Perancang:** Lead BIM Engineer & Antigravity Autonomous Orchestrator  
**Lokasi Dokumen:** `C:\AIGRAV\_governance_review\phase2\PHASE3_IMPLEMENTATION_PLAN.md`  
**Dasar Acuan:** 
1. Seluruh 21 Dokumen Tata Kelola Fase 2 (`C:\AIGRAV\_governance_review\phase2\`)
2. Hasil Audit Kode Sumber Nyata (`AIGRAV_RULES_CODE_VALIDATION.md` & inspeksi fisik `C:\AIGRAV\Revit\`)
3. Keputusan Resmi Pengguna atas Checkpoint 1–4 dan Koreksi Wajib 1 & 2

---

## 1. Landasan Arsitektur & Prinsip Eksekusi

Rencana implementasi ini adalah cetak biru teknis (*technical execution blueprint*) untuk memandu penulisan kode, refactoring biner C# .NET, pembaruan FastMCP Python, dan pengujian regresi pada **Fase 3**. Seluruh pekerjaan dibagi menjadi **4 Batch Terstruktur** dengan mengintegrasikan persyaratan mutlak:
1. **Single-Writer Dispatcher:** Seluruh transaksi tulis tetap melalui antrean serial tunggal `AIGRAVRequestManager.cs` pada UI thread.
2. **Operation Journal & Compensating Rollback (Opsi 1A):** Pembedaan tegas status mutasi (`CREATED`, `MODIFIED`, `DELETED`), larangan *blind delete*, penanganan `NON_COMPENSABLE`, dan larangan menyebut compensating rollback sebagai *atomic rollback*.
3. **Controlled DirectShape Exception (Opsi 2A):** Mempertahankan call sites eksisting di `ConnectionGeneratorSkill.cs` (baris 91, 109, 140) dengan audit QTO solid volume, melarang penambahan baru, dan melarang keras DirectShape/Generic Model sebagai fallback elemen primer.
4. **Prioritas Endpoint Native C#:** 
   - Batch 2: Pintu & Jendela wall-hosted (`aigrav_create_door`, `aigrav_create_window`).
   - Batch 3: Atap native (`aigrav_create_roof`) dan Ruang arsitektural (`aigrav_create_room`).
5. **Keamanan Hot-Reload Produksi (Opsi 4A):** Pemuatan dinamis DLL dinonaktifkan di mode produksi; pembaruan biner wajib melalui restart Revit.
6. **Mandatory Live Capture & Visual Feedback Loop pada SETIAP Batch:** Setiap batch wajib menyertakan penangkapan baseline (*pre-batch*), capture pasca-commit (*post-batch*), penegakan *View Coverage Manifest*, penandaan `UNCOVERED`, dan siklus perbaikan (*repair plan*) maksimal 2 iterasi.

---

## 2. Pemetaan Kode Sumber Nyata (Real Codebase Audit & Mapping)

Berdasarkan audit fisik terhadap repositori C# di `C:\AIGRAV\Revit\AIGRAV.RevitBridge\` dan FastMCP di `C:\AIGRAV\MCP\TestServer\server.py`, berikut adalah pemetaan lengkap status modul:

### 2.1. Modul yang DIPERTAHANKAN (PRESERVE)
Modul-modul ini telah terbukti stabil, sesuai standar tata kelola, dan tidak boleh dirombak sembarangan:

| Modul & Berkas C# / Python | Fungsi Saat Ini | Alasan Dipertahankan |
| :--- | :--- | :--- |
| **`AIGRAVRequestManager.cs`** | Antrean thread-safe FIFO `ConcurrentQueue<AIGRAVRequest>` | Menjamin invarian Single-Writer Dispatcher ke Revit UI Thread. |
| **`RevitExternalEventHandler.cs`** | Eksekusi serial handler Revit ExternalEvent | Memastikan akses Revit API berjalan 100% pada Main UI Thread. |
| **`AIGRAVHttpServer.cs`** | Listener HTTP Bridge lokal (port 8765) | Jalur komunikasi stabil antara FastMCP dan Revit Bridge. |
| **`BimAuditSkill.cs:26240, 29471`** | Generator Schedule Pelat Sambungan Baja | Sudah benar mengimplementasikan tepat **5 kolom terlihat** baku (`fLok, fType, fThk, fQty, fWeight`). |
| **`VouteProfileQTOSkill.cs:115`** | Schedule Voute Baja (`OST_StructConnectionProfiles`) | Menghitung kuantitas voute dari **volume solid eksak** ($\text{Volume} \times 7850.0\text{ kg/m}^3$) dengan 7 kolom baku. |
| **`AIGRAV.SteelDatabase.csv`** | Database Profil Baja SNI & Gunung Garuda | Menjadi sumber kebenaran tunggal kalibrasi berat balok/kolom baja. |
| **`ConnectionGeneratorSkill.cs:91, 109, 140`** | DirectShape Pelat Simpul Khusus Baja | Dipertahankan sementara di bawah *Controlled-Exception Opsi 2A* agar tidak merusak alur detailing baja aktif. |
| **`BimAuditSkill.SpatialCreators.cs`** | Pembuatan Level, Grid, Dinding Bata, Kolom, Balok, Pelat | Inti pembuatan elemen struktural/arsitektur native yang sudah bekerja. |

---

### 2.2. Modul yang PERLU DIPERBAIKI / DI-REFACTOR (FIX / REFACTOR)
Modul-modul ini memiliki cacat teknis, risiko keamanan, atau penyimpangan tata kelola yang wajib diperbaiki pada Fase 3:

| Modul & Berkas Sumber | Masalah Teknis Nyata | Rencana Refactoring pada Fase 3 |
| :--- | :--- | :--- |
| **`BimAuditSkill.cs:14493`** | `failuresAccessor.DeleteAllWarnings()` menghapus seluruh warning baru secara buta (*blind suppression*). | **Ganti dengan Warning Allowlist**: Hanya supresi `FailureDefinitionId` jinak terdaftar; catat delta penambahan warning ke telemetri (*Baseline Warning Diff*). |
| **`AIGRAVSkillLoader.cs:40`** | Memuat DLL dari disk secara dinamis tanpa verifikasi mode produksi atau tanda tangan biner. | **Terapkan Opsi 4A**: Nonaktifkan pemanggilan hot-reload di mode produksi (`ERR_HOT_RELOAD_DISABLED_IN_PROD`). Di mode dev, wajib prompt persetujuan interaktif pengguna dan verifikasi SHA-256. |
| **`BimAuditSkill.VisualAndPython.cs:18`** | `CaptureRevitViewportJson` hanya mengekspor 1 view aktif; `aigrav_inspect_model_visually` hanya mengumpulkan 3-4 view dokumen yang sudah ada. | **Bangun Multi-View Camera Synthesizer**: Tambahkan kemampuan membuat orientasi kamera otomatis untuk Tampak Depan, Belakang, Samping Kiri/Kanan, Atas, Potongan, dan Isometrik 3D. |
| **`AIGRAVToolExecutor.cs:2153`** | Pemanggilan `place_family_instance` unhosted (`XYZ location`) gagal saat dipanggil untuk pintu/jendela ber-host dinding. | **Pisahkan Alur Hosted vs Unhosted**: Hentikan pemaksaan penempatan unhosted untuk elemen ber-host; tolak keras pengalihan ke Generic Model. |
| **`HouseType36Builder.cs:180, 215`** | Memanggil `CreateDirectShapeBox` untuk demonstrasi kotak massa. | **Hapus CreateDirectShapeBox**: Refactor skrip demonstrasi agar murni menggunakan System Family `Wall` dan `Floor`. |
| **`AIGRAVToolExecutor.cs:522`** | `create_generic_model` dapat dipanggil secara bebas tanpa penandaan provenance. | **Pasang Gerbang Validasi Semantik**: Batasi Generic Model hanya jika parameter `AIGRAV_PROVENANCE == "INTENTIONAL_GENERIC_MODEL"`. |

---

### 2.3. Modul yang PERLU DIBUAT BARU (CREATE NEW)
Modul-modul ini belum tersedia di kode sumber dan wajib dibangun dari nol pada Fase 3:

| Nama Modul Baru | Bahasa & Lokasi Target | Fungsi & Spesifikasi Teknis |
| :--- | :---: | :--- |
| **`OperationJournalManager.cs`** | C# (`AIGRAV.RevitBridge`) | Mengelola pencatatan operasi pemodelan dengan klasifikasi 3 status: `CREATED`, `MODIFIED` (snapshot before-state), dan `DELETED`. Menyediakan fungsi *Compensating Rollback* yang aman tanpa *blind delete*. |
| **`DoorWindowHostedCreator.cs`** | C# (`AIGRAV.Skills.BimAudit`) | Implementasi endpoint native `aigrav_create_door` dan `aigrav_create_window` menggunakan `doc.Create.NewFamilyInstance(location, symbol, hostWall, level, NonStructural)`. Validasi host, parameter sill, dan kategori semantik. |
| **`RoofNativeCreator.cs`** | C# (`AIGRAV.Skills.BimAudit`) | Implementasi endpoint native `aigrav_create_roof` menggunakan `doc.Create.NewFootPrintRoof` dengan multi-curve closed polygon, kemiringan slope per-edge, dan overhang. |
| **`RoomSpatialCreator.cs`** | C# (`AIGRAV.Skills.BimAudit`) | Implementasi endpoint native `aigrav_create_room` menggunakan `doc.Create.NewRoom`. Validasi batas ruang tertutup (*room-bounding boundaries*) sebelum pembuatan elemen. |
| **`CameraOrientationEngine.cs`** | C# (`AIGRAV.Skills.BimAudit`) | Mesin pengarah kamera otomatis untuk menghasilkan 9 sudut pandang deterministik (*View Coverage Manifest*) secara terprogram. |
| **Pendaftaran Tool di `server.py`** | Python (`C:\AIGRAV\MCP\TestServer\`) | Mendaftarkan fungsi MCP untuk `aigrav_create_door`, `aigrav_create_window`, `aigrav_create_roof`, dan `aigrav_create_room`. |

---

## 3. Rencana Implementasi Bertahap (Batch 1 – Batch 4)

```
[FASE 3: MASTER ROADMAP]
       │
       ├── BATCH 1: Foundation, Security & Execution Engine
       │     └── Journal 3-Status, Compensating Rollback, Hot-Reload Lockdown, Warning Allowlist
       │
       ├── BATCH 2: Wall-Hosted Architectural Openings (Prioritas 1)
       │     └── Native Door & Window (Wall-Hosted), Eliminasi DirectShapeBox, Fail-Closed
       │
       ├── BATCH 3: Building Shell & Spatial Enclosure (Prioritas 2 & 3)
       │     └── Native Roof, Native Room Enclosure, Zero-Overlap Donut Slabs
       │
       └── BATCH 4: Live Capture Multi-View Engine & QTO Full Manifest
             └── Camera Synthesizer (9 Views), Uncovered View Protocol, QTO Final Audit
```

---

### 🚀 BATCH 1: Foundation, Security & Transaction Engine

#### A. Sasaran Teknis & Lingkup Pekerjaan:
1. **Pembangunan `OperationJournalManager.cs`:**
   - Menyediakan struktur data in-memory thread-safe untuk mencatat mutasi per-operasi.
   - Mengelompokkan entri ke dalam:
     * `CREATED`: Menyimpan `ElementId`.
     * `MODIFIED`: Menyimpan `ElementId`, parameter asli, koordinat asli (*before-state snapshot*), dan nama metode inverse.
     * `DELETED`: Menyimpan snapshot geometri/tipe untuk pemulihan.
   - Menyediakan metode `ExecuteCompensatingRollback(string taskId)`:
     * Menghapus elemen `CREATED` via `doc.Delete(elementIds)`.
     * Mengembalikan parameter elemen `MODIFIED` via snapshot (*inverse operation*).
     * Melarang keras *blind delete* terhadap elemen `MODIFIED`!
     * Menandai operasi tanpa mekanisme inverse sebagai `NON_COMPENSABLE` dan melakukan eskalasi.
     * Mencatat dalam log bahwa rollback yang terjadi adalah *compensating rollback*, bukan *atomic rollback*.
2. **Penguncian Keamanan Hot-Reload (`AIGRAVSkillLoader.cs`):**
   - Menerapkan Opsi 4A: Nonaktifkan pemanggilan `aigrav_reload_skills` jika berjalan pada `Production Mode` (mengembalikan `ERR_HOT_RELOAD_DISABLED_IN_PROD`).
   - Pada `Development Mode`, tambahkan prompt konfirmasi interaktif dan kalkulasi checksum SHA-256 berkas DLL.
3. **Penyempurnaan Penanganan Warning (`BimAuditSkill.cs:14493`):**
   - Hapus pemanggilan `failuresAccessor.DeleteAllWarnings()`.
   - Ganti dengan `WarningAllowlistHandler`: Hanya menghapus warning jinak yang terdaftar eksplisit (misal: duplicate marks sementara). Seluruh warning baru dihitung deltanya (*Baseline Warning Diff*) dan dicatat ke log telemetri.

#### B. Persyaratan Live Capture pada Batch 1:
- **Baseline Capture:** Mengambil tangkapan layar sebelum transaksi batch dibuka untuk merekam kondisi dokumen awal.
- **Post-Commit Capture:** Mengambil tangkapan layar setelah transaksi kompensasi atau modifikasi commit.
- **Kualitas Berkas:** Memvalidasi berkas PNG berukuran $> 20\text{ KB}$, resolusi $\ge 1280\text{ px}$, dan header valid `0x89 0x50 0x4E 0x47`.

#### C. Acceptance Test & Kriteria Keberhasilan Batch 1:
- [ ] **TC-08 (Warning Allowlist):** Warning kritis dinding bertumpuk membatalkan transaksi dan tidak dihapus buta; warning jinak diserap dan dicatat.
- [ ] **TC-10 (Compensating Rollback Opsi 1A):** Kegagalan di tengah batch menghapus elemen `CREATED` dan memulihkan elemen `MODIFIED`; laporan secara jujur menyebut compensating rollback (bukan atomic rollback).
- [ ] **TC-15 (Production Hot-Reload Lockdown):** Pemanggilan `aigrav_reload_skills` di mode produksi diblokir dengan pesan kewajiban restart Revit.
- [ ] **TC-16 & TC-25 (Live Capture Baseline & Clarity):** Tangkapan layar baseline tersimpan dengan nama unik, resolusi $\ge 1280\text{ px}$, dan format PNG valid.

---

### 🚀 BATCH 2: Wall-Hosted Architectural Openings (Prioritas 1)

#### A. Sasaran Teknis & Lingkup Pekerjaan:
1. **Pembangunan `DoorWindowHostedCreator.cs` di C# Bridge:**
   - Mengimplementasikan overload Revit API resmi:
     ```csharp
     FamilyInstance door = doc.Create.NewFamilyInstance(
         locationXYZ, doorSymbol, hostWall, hostLevel, StructuralType.NonStructural);
     ```
   - Mengimplementasikan penempatan jendela dengan penyetelan parameter ambang bawah:
     ```csharp
     FamilyInstance window = doc.Create.NewFamilyInstance(
         locationXYZ, windowSymbol, hostWall, hostLevel, StructuralType.NonStructural);
     window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(sillHeightFeet);
     ```
   - Validasi pra-syarat (*Preflight Assertions*):
     * Memastikan `hostWall != null` dan bertipe `Wall` di dokumen yang sama.
     * Memastikan `symbol.Category` identik dengan `OST_Doors` atau `OST_Windows`.
     * Mengaktifkan symbol via `symbol.Activate()` jika belum aktif.
   - Respons terstruktur: Mengembalikan JSON dengan `element_id`, `category`, `host_id`, `width_mm`, `height_mm`, dan `sill_mm`.
2. **Pendaftaran Endpoint di FastMCP `server.py`:**
   - Mendaftarkan tool `@mcp.tool()`: `aigrav_create_door` dan `aigrav_create_window` dengan skema parameter lengkap.
3. **Pembersihan Fallback Ilegal & Sandbox Deprecation:**
   - Hapus pemanggilan `CreateDirectShapeBox` pada `HouseType36Builder.cs` (baris 180, 215).
   - Terapkan kebijakan *Fail-Closed*: Jika family jendela/pintu tidak ditemukan, sistem melempar error terstruktur `ERR_FAMILY_SYMBOL_NOT_FOUND` dan **DILARANG KERAS dialihkan ke Generic Model atau DirectShape**.

#### B. Persyaratan Live Capture pada Batch 2:
- **Tangkapan Layar Wajib (3 View):**
  1. Denah Dinding Host (memverifikasi simbol bukaan dan ayunan pintu / *door swing*).
  2. Tampak Depan Fasad Dinding (memverifikasi ambang bawah jendela dan void bukaan dinding terbentuk sempurna).
  3. 3D Isometrik Lokal (memverifikasi ketebalan kusen dan transparansi material kaca $\ge 50\%$).
- **Siklus Perbaikan Otomatis:** Jika kusen terpasang terbalik atau menembus kolom, agen menyusun `repair_plan` dan mengambil ulang (*re-capture*) ketiga view tersebut (maksimal 2 iterasi).

#### C. Acceptance Test & Kriteria Keberhasilan Batch 2:
- [ ] **TC-01 (Wall Native Creation):** Pembuatan dinding menggunakan `WallType` resmi menghasilkan kategori `OST_Walls`.
- [ ] **TC-02 (Door Wall-Hosted):** Pintu terbuat dengan kategori `OST_Doors`, `Host.Id == wall.Id`, void cutout dinding terbentuk, dan muncul di schedule pintu.
- [ ] **TC-03 (Window Wall-Hosted):** Jendela terbuat dengan kategori `OST_Windows`, `Host.Id == wall.Id`, dan parameter sill height terukur tepat $\pm 1\text{ mm}$.
- [ ] **TC-04 (Fail-Closed Behavior):** Permintaan jendela dengan family fiktif berhenti dengan error terstruktur tanpa membuat Generic Model atau DirectShape.
- [ ] **TC-05 (Authorized Generic Model):** Generic Model hanya diizinkan jika pengguna secara eksplisit memintanya dengan flag provenance resmi.
- [ ] **TC-22 & TC-23 (Visual Defect & Re-Capture):** Cacat elevasi bukaan menghasilkan repair plan terstruktur dan pengambilan ulang gambar otomatis.

---

### 🚀 BATCH 3: Building Shell & Spatial Enclosure (Prioritas 2 & 3)

#### A. Sasaran Teknis & Lingkup Pekerjaan:
1. **Pembangunan `RoofNativeCreator.cs` di C# Bridge (Prioritas 2):**
   - Mengimplementasikan pembuatan atap footprint resmi:
     ```csharp
     ModelCurveArray mapping = new ModelCurveArray();
     FootPrintRoof roof = doc.Create.NewFootPrintRoof(boundaryCurveArray, roofLevel, roofType, out mapping);
     ```
   - Menerapkan slope per-edge (kemiringan metal sheet $\ge 5^\circ$, genteng $\ge 25^\circ$) dan overhang teritisan ($\ge 800\text{ mm}$).
   - Mencegah kemiringan terbalik (*negative back-fall drainage*).
   - Mendaftarkan tool `aigrav_create_roof` di `server.py`.
2. **Pembangunan `RoomSpatialCreator.cs` di C# Bridge (Prioritas 3):**
   - Mengimplementasikan penempatan ruang arsitektural:
     ```csharp
     Room room = doc.Create.NewRoom(level, new UV(xFeet, yFeet));
     room.Name = roomName;
     room.Number = roomNumber;
     ```
   - Validasi Pra-Syarat: Memeriksa apakah titik koordinat berada di dalam batas dinding pembatas (*room-bounding boundaries*). Menolak eksekusi jika ruang terbuka (`ERR_ROOM_NOT_ENCLOSED`).
   - Mendaftarkan tool `aigrav_create_room` di `server.py`.
3. **Penegakan Mandat Pelat Bebas Tumpang Tindih (*Zero-Overlap Donut Slabs*):**
   - Memastikan pembuatan pelat konsentris menggunakan `CurveLoop` multi-loop (Loop 0 = Outer boundary, Loop 1..N = Inner void openings).
   - Menghilangkan tumpang tindih pelat pejal (*solid-on-solid overlap*) untuk mengeliminasi Z-fighting dan benturan volume beton ganda.

#### B. Persyaratan Live Capture pada Batch 3:
- **Tangkapan Layar Wajib (4 View):**
  1. Tampak Atas Atap / Roof Plan (memverifikasi pertemuan nok, talang, dan arah kemiringan atap).
  2. Potongan Melintang Bangunan (memverifikasi tinggi bebas *headroom* $\ge 2.20\text{ m}$ dan hubungan pelat-dinding-kuda-kuda).
  3. Perspektif Eksterior Bangunan (memverifikasi estetika selubung keseluruhan).
  4. Perspektif Interior Ruang Utama (memverifikasi penutupan batas ruang / *visual seal*, plafon, dan pencahayaan).

#### C. Acceptance Test & Kriteria Keberhasilan Batch 3:
- [ ] **TC-06 (Native Roof Footprint):** Atap terbuat dengan kategori `OST_Roofs`, slope presisi, tanpa fallback DirectShape prisma.
- [ ] **TC-07 (Room Boundary Enclosure Gate):** Pembuatan ruang pada area bocor/terbuka ditolak dengan error `ERR_ROOM_NOT_ENCLOSED`; ruang tertutup terbuat dengan luas area dan volume valid.
- [ ] **TC-09 (Matriks Kontak Sah vs Benturan):** Join dinding-pelat diizinkan; penetrasi furnitur ke kolom pejal terdeteksi sebagai benturan keras (*hard clash*).
- [ ] **TC-26 (Interior View Seal Check):** Perspektif interior memvalidasi ruang tertutup bersih tanpa tembus latar langit luar akibat dinding bolong.

---

### 🚀 BATCH 4: Live Capture Multi-View Engine, Full Manifest & QTO Audit

#### A. Sasaran Teknis & Lingkup Pekerjaan:
1. **Pembangunan `CameraOrientationEngine.cs` di C# Bridge:**
   - Mengembangkan generator orientasi kamera otomatis yang mampu mengatur bounding box dan target kamera untuk menghasilkan 9 sudut pandang baku *View Coverage Manifest*:
     * Tampak Depan (*Front Elevation*)
     * Tampak Belakang (*Rear Elevation*)
     * Tampak Kiri (*Left Elevation*)
     * Tampak Kanan (*Right Elevation*)
     * Tampak Atas (*Top/Roof Plan*)
     * Denah Lantai 1 (*Ground Floor Plan*)
     * Potongan Utama (*Building Cross Section*)
     * 3D Isometrik Luar (*Exterior Isometric*)
     * Perspektif Interior Utama (*Interior Key View*)
   - Jika suatu view belum ada di dokumen, mesin akan membuat view baru secara terprogram atau menandai status `UNCOVERED` jika terdapat kendala geometris.
2. **Penegakan Protokol Uncovered View & Gerbang Penyelesaian:**
   - Mengintegrasikan pengecekan coverage: jika ada sudut pandang wajib berstatus `UNCOVERED`, sistem **MEMBLOKIR STATUS SELESAI** (`BLOCKED_INCOMPLETE_VISUAL_COVERAGE`).
3. **Verifikasi Akhir Standar QTO & Schedule Kuantitas:**
   - Memastikan schedule pelat sambungan (`OST_StructConnectionPlates`) tampil dengan **tepat 5 kolom** (`Lokasi | Tipe Sambungan | Thickness | Qty | Berat`).
   - Memastikan kolom A selalu berupa parameter `Lokasi`.
   - Memastikan schedule voute (`OST_StructConnectionProfiles`) tampil dengan 7 kolom baku dan berat dikalibrasi dari volume solid eksak ($\rho = 7850.0\text{ kg/m}^3$).
   - Memverifikasi filter anti-double counting (`Lokasi Equal ""` untuk bangunan utama).

#### B. Persyaratan Live Capture pada Batch 4:
- **Full View Coverage Manifest (9 View Lengkap):** Seluruh 9 sudut pandang wajib diekspor dengan resolusi $\ge 1280\text{ px}$.
- **Galeri Komparasi Visual Sebelum vs Sesudah:** Menyajikan laporan visual komprehensif kepada pengguna.
- **Batas 2 Iterasi Perbaikan:** Memastikan loop otomatis berhenti jika 2 kali koreksi belum menyelesaikan masalah (*Fail-Safe Stop*).

#### C. Acceptance Test & Kriteria Keberhasilan Batch 4:
- [ ] **TC-12 (Task Scope Classifier Bypass):** Tugas ringan manipulasi link/view sheet selesai dalam 1 langkah tanpa memicu milestone gedung penuh.
- [ ] **TC-13 (5 Kolom Baku Pelat QTO):** Schedule pelat memiliki tepat 5 kolom terlihat, kolom A adalah Lokasi, dan grand total aktif.
- [ ] **TC-14 (Kalibrasi Volume Eksak Voute):** Berat voute dihitung dari volume solid eksak dikalikan $7850.0$, dengan filter anti-double counting.
- [ ] **TC-17 s/d TC-21 (Full Manifest & Reality Disclosure):** Sistem mengekspor seluruh view manifest, secara jujur mencatat status `UNCOVERED` jika gagal, dan mengakui status fitur kamera.
- [ ] **TC-24 (Batas Maksimal 2 Iterasi):** Sistem berhenti setelah 2 iterasi perbaikan otomatis dan menerbitkan eskalasi jika masih terdapat cacat visual.
- [ ] **TC-27 (Pemblokiran Tugas Tidak Lengkap):** Tugas ditolak selesai jika terdapat view wajib yang belum lengkap.

---

## 4. Matriks Ringkasan Pelaksanaan Batch 1–4

| Parameter Evaluasi | BATCH 1: Foundation & Engine | BATCH 2: Wall-Hosted Openings | BATCH 3: Shell & Enclosure | BATCH 4: Multi-View & QTO Final |
| :--- | :--- | :--- | :--- | :--- |
| **Fokus Utama** | Journal 3-Status, Rollback Kompensasi, Security, Warning | Pintu & Jendela Wall-Hosted (Prioritas 1), Anti-Fallback | Atap Native (P2), Ruang Native (P3), Donut Slabs | Generator Kamera 9 View, Manifest Check, QTO Audit |
| **Modul C# Terkait** | `OperationJournalManager.cs`, `AIGRAVSkillLoader.cs`, `BimAuditSkill.cs` | `DoorWindowHostedCreator.cs`, `HouseType36Builder.cs`, `AIGRAVToolExecutor.cs` | `RoofNativeCreator.cs`, `RoomSpatialCreator.cs`, `BimAuditSkill.SpatialCreators.cs` | `CameraOrientationEngine.cs`, `VouteProfileQTOSkill.cs`, `QTOSkill.cs` |
| **Endpoint Baru** | Internal Journal & Security Handlers | `aigrav_create_door`, `aigrav_create_window` | `aigrav_create_roof`, `aigrav_create_room` | `aigrav_synthesize_camera_views` |
| **Kewajiban Live Capture** | Pre-Batch Baseline Capture & File Clarity ($>20\text{ KB}$, $\ge 1280\text{ px}$) | 3 View Lokal: Denah Bukaan, Tampak Fasad, 3D Kusen Transparan | 4 View Shell: Atap Top Plan, Potongan Gedung, 3D Eksterior, Interior Seal | 9 View Lengkap (Manifest Penuh), Zero UNCOVERED, Galeri Sebelum vs Sesudah |
| **Batas Iterasi Repair** | Maks 2 iterasi rollback | Maks 2 iterasi koreksi kusen | Maks 2 iterasi penutupan ruang/atap | Maks 2 iterasi koreksi visual gedung |
| **Kasus Uji Validasi** | TC-08, TC-10, TC-15, TC-16, TC-25 | TC-01, TC-02, TC-03, TC-04, TC-05, TC-22, TC-23 | TC-06, TC-07, TC-09, TC-26 | TC-12, TC-13, TC-14, TC-17, TC-18, TC-19, TC-20, TC-21, TC-24, TC-27 |

---

## 5. Protokol Keselamatan Eksekusi Fase 3 (*Phase 3 Safety Guardrails*)

Untuk menjamin eksekusi Fase 3 tidak merusak kestabilan sistem yang sedang berjalan:
1. **Branching & Staging Compilation:**  
   Seluruh kode baru ditulis dan dikompilasi pada direktori staging/branch terisolasi. Biner DLL tidak disalin ke folder instalasi aktif Revit (`C:\AIGRAV\Skills\`) sebelum pengujian unit lokal lolos $100\%$.
2. **Uji Regresi Bertahap per Batch:**  
   Batch $N+1$ **DILARANG DIMULAI** sebelum seluruh acceptance criteria pada Batch $N$ diverifikasi dan dinyatakan lulus.
3. **Kepatuhan Hak Akses & Sandbox:**  
   Dilarang mematikan kontrol keamanan atau memodifikasi file konfigurasi host di luar lingkup AIGRAV.
4. **Persetujuan Pengguna Sebelum Eksekusi Batch 1:**  
   Eksekusi Batch 1 baru dapat dimulai setelah Pengguna meninjau cetak biru implementasi ini dan memberikan instruksi persetujuan resmi.

---

## 6. Pernyataan Penutup Cetak Biru

> [!IMPORTANT] KESIAPAN FASE 3 (EXECUTION READINESS)
> Dokumen `PHASE3_IMPLEMENTATION_PLAN.md` ini telah selesai disusun, mencakup audit kode fisik, pemetaan modul (Preserve, Fix, Create), urutan 4 Batch deterministik, integrasi Live Capture pada setiap tahap, serta penetapan kriteria penerimaan berbasis 27 kasus uji resmi.  
> **Sistem saat ini berhenti (*HOLD*) dan menanti persetujuan Pengguna sebelum penulisan kode Batch 1 dimulai.**

# AIGRAV Rules Codebase Validation Report (Read-Only Audit)

**Dokumen:** `AIGRAV_RULES_CODE_VALIDATION.md`  
**Tanggal Audit:** 11 November 2026  
**Auditor:** Lead BIM Engineer & Antigravity Autonomous Orchestrator  
**Dasar Audit:** Evaluasi Read-Only Kode Nyata (`C:\AIGRAV`) terhadap `AIGRAV_RULES_AUDIT_REPORT.md`  
**Status Eksekusi:** Selesai (100% Read-Only, Zero Source Code Modification, Zero Open Document Modification)

---

## 1. Executive Summary

Audit validasi kode ini dilakukan secara ketat dengan status **Read-Only** untuk memverifikasi kesesuaian antara aturan tata kelola (Markdown) dengan implementasi nyata pada C# .NET (`AIGRAV.RevitBridge`), FastMCP Python (`server.py`), dan konfigurasi runtime Antigravity.

### Temuan Inti Audit:
1. **Single-Writer Guarantee Terbukti di Kode:** Mekanisme antrean FIFO `ConcurrentQueue<AIGRAVRequest>` di `AIGRAVRequestManager.cs` yang diproses oleh `IExternalEventHandler.Execute()` pada Revit main UI thread menjamin bahwa **seluruh transaksi tulis ke Revit dieksekusi secara serial (1 writer)**. Dokumen lama `KnowledgeBase/AIGRAV_Revit_Execution_Protocol.md` yang menyebut "eksekusi paralel 3 sub-agent per zona fisik" terbukti **usang (obsolete)** dan bertentangan dengan kode.
2. **Kesenjangan Alat (Tool Registry Gap):** Dari seluruh tool pembuatan elemen yang didefinisikan dalam SOP milestone, `aigrav_create_door`, `aigrav_create_window`, `aigrav_create_roof`, dan `aigrav_create_room` **TIDAK DIIMPLEMENTASIKAN** di kode C#.
3. **Penyebab Utama Bug Generic Model Teridentifikasi:** `AIGRAVToolExecutor.cs` (baris 2153) hanya mengimplementasikan overload `NewFamilyInstance(XYZ, symbol, level, structuralType)` tanpa parameter `host`. Elemen berpintu/jendela (*wall-hosted*) akan gagal jika dipanggil lewat API ini. Akibatnya, sistem menyediakan jalan pintas `create_generic_model` (baris 522) dan `CreateDirectShapeBox`, sehingga agen terdorong membuat jendela/pintu palsu berkategori `OST_GenericModel`.
4. **DirectShape Masih Aktif di Sejumlah Skill:** Meskipun `AGENTS.md` melarang keras `DirectShape`, kode aktif di `ConnectionGeneratorSkill.cs` (baris 91, 109, 140) dan `BimAuditSkill.VisualAndPython.cs` (baris 149, 212) **masih membuat `DirectShape` berkategori `OST_GenericModel`**.
5. **Clash Detection Terbatas & Warning Swallower Buta:** `detect_spatial_clashes` hanya mengecek `OST_Furniture` vs `OST_Walls` menggunakan *Bounding Box* (bukan Boolean solid), sementara `ClashWarningSwallower` (baris 14493) memanggil `failuresAccessor.DeleteAllWarnings()`, menghapus seluruh peringatan Revit secara tanpa filter.
6. **Konsistensi QTO Pelat Sambungan:** Kode C# aktif di `BimAuditSkill.cs` (baris 26240, 29471) telah benar-benar mengimplementasikan **5 kolom** (`fLok, fType, fThk, fQty, fWeight`), sesuai target `AGENTS.md` dan `QTO_Template_Reference.md`. Format 4 kolom hanya tersisa sebagai cuplikan usang di file teks `qto_revit_api_guide.md`.
7. **Batas Transaksi Milestone Hanya Konvensi Prompt:** Tidak ada `TransactionGroup` di level Bridge yang membungkus keseluruhan 4 makro-milestone. Setiap pemanggilan tool membuka dan menutup `Transaction` lokalnya sendiri (tercatat 57 transaksi independen di C#).

---

## 2. Exact Instruction-Loading Chain and Evidence

Berikut adalah pemetaan rantai pemuatan instruksi (*instruction-loading chain*) yang terbukti secara teknis pada runtime Antigravity saat ini:

| Sumber Instruksi | Lokasi Path Fisik | Pemicu Pemuatan (*Trigger*) | Tingkat Otoritas | Bukti Konfigurasi & Runtime (*Evidence*) |
| :--- | :--- | :--- | :---: | :--- |
| **Workspace Master Rule** | `c:\AIGRAV\AGENTS.md` | **Otomatis / Permanen** saat workspace adalah `c:\AIGRAV` | **Tertinggi (Root Governance)** | Dimuat langsung ke dalam blok `<user_rules>` sistem Antigravity: `<RULE[c:\AIGRAV\AGENTS.md]>`. |
| **User Global Rule** | `C:\Users\sapuser\.gemini\config\plugins\rtk\rules\AGENTS.md` | **Otomatis / Permanen** untuk seluruh sesi terminal | Khusus CLI Condensation | Dimuat di `<user_rules>` via plugin `rtk`. Tidak mengatur Revit. |
| **Domain Skills** | `c:\AIGRAV\.agents\skills\*\SKILL.md` (7 skills) | **On-Demand Discovery** dipindai dari folder `.agents\skills\` | Standar Spesialisasi Domain | Terdaftar aktif di manifest `<skills>` sesi Antigravity (`revit-milestone-modeling`, `revit-qto-schedule-audit`, dll.). |
| **Technical API References** | `c:\AIGRAV\.agents\skills\*\references\*.md` (3 files) | **Referenced by Skill** (hanya dibaca saat skill induk memanggil) | Referensi Teknis Implementasi | Tidak dimuat ke context prompt awal; diakses dinamis via tool baca. |
| **Workspace .gemini Directory** | `C:\AIGRAV\.gemini\` | **TIDAK AKTIF / NON-EXISTENT** | — | `Test-Path 'C:\AIGRAV\.gemini'` = `False`. Workspace tidak memiliki folder `.gemini` lokal. |
| **KnowledgeBase Execution Protocol** | `C:\AIGRAV\KnowledgeBase\AIGRAV_Revit_Execution_Protocol.md` | **Manual / Tidak Dimuat Otomatis** | Proposal Historis | Tidak tertaut di `AGENTS.md` dan tidak berada di folder `.agents`. Klaim bahwa file ini "otomatis termuat di root workspace" terbukti **salah secara teknis**. |

---

## 3. Tool Mapping Matrix

Hasil audit komparatif antara tool yang tertulis di dokumentasi, registrasi di FastMCP Python, dan implementasi nyata di C# Revit Bridge:

| Documented Tool Name | FastMCP (`server.py`) | C# Executor / Skill Class & Method | Element Category | Status Implementasi | Sifat Operasi | Kepemilikan Transaksi |
| :--- | :---: | :--- | :--- | :---: | :---: | :---: |
| `aigrav_ping` | ❌ | `AIGRAVHttpServer.cs` (Route `/ping`) | System | **Implemented** | Read | Tanpa Transaksi |
| `aigrav_get_project_context` | Line 106 | `BimAuditSkill.SpatialTelemetry.cs:17` | Datum/Model | **Implemented** | Read | Tanpa Transaksi |
| `aigrav_get_active_view` | ❌ | `AIGRAVToolExecutor.cs:141` | Views | **Implemented** | Read | Tanpa Transaksi |
| `aigrav_get_selection` | ❌ | `AIGRAVToolExecutor.cs:170` | Elements | **Implemented** | Read | Tanpa Transaksi |
| `aigrav_get_element` | ❌ | `AIGRAVToolExecutor.cs:188` | Generic | **Implemented** | Read | Tanpa Transaksi |
| `aigrav_create_level` | ❌ | `AIGRAVToolExecutor.cs:1725` (`CreateLevel`) | `OST_Levels` | **Implemented** | Write | `Transaction` Lokal (Line 1752) |
| `aigrav_create_grid` | ❌ | `AIGRAVToolExecutor.cs:1771` (`CreateGrid`) | `OST_Grids` | **Implemented** | Write | `Transaction` Lokal (Line 1814) |
| `aigrav_create_grid_matrix` | Line 169 | `BimAuditSkill.RelativeHelpers.cs:14` | `OST_Grids` | **Implemented** | Write | `Transaction` Lokal |
| `aigrav_create_wall` | ❌ | `BimAuditSkill.SpatialCreators.cs:122` / `AIGRAVToolExecutor.cs:1837` | `OST_Walls` | **Implemented** | Write | `Transaction` Lokal (Line 131) |
| `aigrav_create_floor` | ❌ | `BimAuditSkill.SpatialCreators.cs:220` / `AIGRAVToolExecutor.cs:1943` | `OST_Floors` | **Implemented** | Write | `Transaction` Lokal (Line 254) |
| `aigrav_create_column` | ❌ | `BimAuditSkill.SpatialCreators.cs:150` / `AIGRAVToolExecutor.cs:500` | `OST_StructuralColumns` | **Implemented** | Write | `Transaction` Lokal (Line 167) |
| `aigrav_create_beam` | ❌ | `BimAuditSkill.SpatialCreators.cs:185` / `AIGRAVToolExecutor.cs:508` | `OST_StructuralFraming` | **Implemented** | Write | `Transaction` Lokal (Line 207) |
| `aigrav_create_door` | ❌ | ❌ **TIDAK ADA METODE DI C#** | `OST_Doors` | 🔴 **MISSING** | — | — |
| `aigrav_create_window` | ❌ | ❌ **TIDAK ADA METODE DI C#** | `OST_Windows` | 🔴 **MISSING** | — | — |
| `aigrav_create_roof` | ❌ | ❌ **TIDAK ADA METODE DI C#** (Hanya `get_roofs`) | `OST_Roofs` | 🔴 **MISSING** | — | — |
| `aigrav_create_room` | ❌ | ❌ **TIDAK ADA METODE DI C#** (Hanya `get_rooms`) | `OST_Rooms` | 🔴 **MISSING** | — | — |
| `place_family_instance` | ❌ | `AIGRAVToolExecutor.cs:2074` (`CreateFamilyInstance`) | Dynamic (Unhosted) | **Partially Implemented** | Write | `Transaction` Lokal (Line 2138) |
| `create_generic_model` | ❌ | `AIGRAVToolExecutor.cs:522` | `OST_GenericModel` | **Implemented** (Risiko Fallback) | Write | `Transaction` Lokal (Line 2138) |
| `aigrav_place_on_top_of` | Line 233 | `BimAuditSkill.RelativeHelpers.cs:120` | Dynamic | **Implemented** | Write | `Transaction` Lokal |
| `aigrav_connect_elements` | Line 291 | `BimAuditSkill.RelativeHelpers.cs:240` | Framing | **Implemented** | Write | `Transaction` Lokal |
| `aigrav_detect_spatial_clashes` | Line 477 | `BimAuditSkill.DeterministicValidator.cs:110` | Furniture vs Wall | **Partially Implemented** (Hanya BBox) | Read | Tanpa Transaksi |
| `aigrav_audit_material_transparency` | Line 452 | `BimAuditSkill.DeterministicValidator.cs:18` | Materials | **Implemented** | Read | Tanpa Transaksi |
| `aigrav_audit_unresolved_warnings` | Line 502 | `BimAuditSkill.DeterministicValidator.cs:150` | Warnings | **Implemented** (Tanpa Baseline) | Read | Tanpa Transaksi |
| `aigrav_capture_revit_viewport` | Line 378 | `BimAuditSkill.VisualAndPython.cs:18` | Views (PNG) | **Implemented** | Read/Temp Write | `Transaction` Sementara |
| `aigrav_inspect_model_visually` | Line 402 | `BimAuditSkill.VisualAndPython.cs:91` | Views (4-PNG) | **Implemented** | Read/Temp Write | `Transaction` Sementara |
| `aigrav_check_model_health` | ❌ | ❌ **TIDAK ADA SEBAGAI TOOL MANDIRI** | Warnings/Model | 🔴 **MISSING** | — | — |
| `aigrav_reload_skills` | ❌ | `AIGRAVSkillManagementSkill.cs:40` | System | **Implemented** | Hot-Reload | Tanpa Transaksi |
| `aigrav_execute_hide_portal_links` | ❌ | `BimAuditSkill.cs:11030` | `OST_RvtLinks` | **Implemented** | Write | `Transaction` Lokal (Line 11051) |
| `aigrav_inspect_portal_sheets` | ❌ | `BimAuditSkill.cs:10950` | Views/Sheets | **Implemented** | Read | Tanpa Transaksi |
| `aigrav_generate_voute_schedules` | ❌ | `VouteProfileQTOSkill.cs:115` | `OST_StructConnectionProfiles`| **Implemented** | Write | `Transaction` Lokal (Line 151) |
| `aigrav_qa_audit_all_schedules` | ❌ | `BimAuditSkill.cs:33200` | Schedules | **Implemented** | Read | Tanpa Transaksi |

---

## 4. Transaction and Single-Writer Findings

### A. Pembuktian Single-Writer Architecture:
- Jalur pemanggilan: `Client / FastMCP` $\rightarrow$ `HTTP POST :8765` $\rightarrow$ `AIGRAVHttpServer.ProcessRequestAsync()` $\rightarrow$ `AIGRAVRequestManager.Enqueue(request)` $\rightarrow$ `AIGRAVApp.ExternalEvent.Raise()`.
- Di `AIGRAVRequestManager.cs` (baris 7):
  ```csharp
  private static readonly ConcurrentQueue<AIGRAVRequest> Queue = new();
  ```
- Di `RevitExternalEventHandler.cs` (baris 11):
  ```csharp
  while (AIGRAVRequestManager.TryDequeue(out AIGRAVRequest? request)) { ... }
  ```
- **Kesimpulan Kode:** Eksekusi tulis dijamin **100% serial dan bebas deadlock/race condition** karena diproses berurutan satu per satu dalam antrean FIFO pada Revit UI Thread.
- **Kontradiksi Dokumentasi:** Klaim di `KnowledgeBase/AIGRAV_Revit_Execution_Protocol.md` (Bagian 2 & 3) yang menginstruksikan "3 Sub-Agent paralel menulis elemen per zona" terbukti secara teknis tidak didukung dan tidak mungkin berjalan secara paralel di Revit.

### B. Kepemilikan Transaksi (*Transaction Ownership*):
- Pencarian kode di seluruh solusi C# menemukan **57 instansiasi `new Transaction(...)`** dan **hanya 2 instansiasi `new TransactionGroup(...)`** (`ConnectionV14Skill.cs:111` dan `DynamoSteelDiscoverySkill.cs:63`).
- **Fakta:** Tidak ada `TransactionGroup` makro yang mengontrol 4 Makro-Milestone. Setiap tool mengeksekusi transaksi atomik sendiri-sendiri.
- **Risiko Nested Transaction:** Jika panduan di masa depan membungkus panggilan tool ke dalam `TransactionGroup` di Python, akan aman karena Revit mengizinkan `Transaction` di dalam `TransactionGroup`. Namun, jika ada tool helper C# yang mencoba memanggil tool C# lain yang sama-sama membuka `new Transaction()`, Revit API akan melempar exception fatal: *"A transaction is already active in this document"*.

---

## 5. Family and Category Integrity Matrix

| Elemen Target | Jalur Kode Pembuatan Aktual | Category Revit yang Dihasilkan | Verifikasi Host | Verifikasi Parameter & Symbol | Risiko Fallback Non-Native |
| :--- | :--- | :--- | :---: | :---: | :--- |
| **Dinding (Wall)** | `BimAuditSkill.SpatialCreators.cs:134` (`Wall.Create`) | `BuiltInCategory.OST_Walls` | N/A | Cek `WallType.Name` | Nol. Murni Native System Family. |
| **Lantai (Floor)** | `BimAuditSkill.SpatialCreators.cs:256` (`Floor.Create`) | `BuiltInCategory.OST_Floors` | N/A | Cek `FloorType.Name` | Nol. Murni Native System Family. |
| **Kolom Struktur** | `BimAuditSkill.SpatialCreators.cs:170` (`NewFamilyInstance`) | `BuiltInCategory.OST_StructuralColumns` | N/A (Level) | Cek `FamilySymbol.IsActive` | Nol. Menggunakan Category resmi. |
| **Balok Struktur** | `BimAuditSkill.SpatialCreators.cs:210` (`NewFamilyInstance`) | `BuiltInCategory.OST_StructuralFraming` | N/A (Level) | Cek `FamilySymbol.IsActive` | Nol. Menggunakan Category resmi. |
| **Pintu (Door)** | ❌ Tidak ada tool native dedicated | Tidak ada (`AIGRAVToolExecutor:2153` gagal) | ❌ Tidak Ada | ❌ Tidak Ada | 🔴 **TINGGI:** Terdorong memakai `create_generic_model`. |
| **Jendela (Window)**| ❌ Tidak ada tool native dedicated | Tidak ada (`AIGRAVToolExecutor:2153` gagal) | ❌ Tidak Ada | ❌ Tidak Ada | 🔴 **TINGGI:** Terdorong memakai `create_generic_model` / `DirectShape`. |
| **Atap (Roof)** | ❌ Tidak ada tool native dedicated | Tidak ada | ❌ Tidak Ada | ❌ Tidak Ada | 🔴 **TINGGI:** Terdorong memakai ekstrusi massing manual. |
| **Ruang (Room)** | ❌ Tidak ada tool native dedicated | Tidak ada | ❌ Tidak Ada | ❌ Tidak Ada | 🔴 **TINGGI:** Room tidak bisa di-tag otomatis. |
| **Pelat Sambungan** | `ConnectionGeneratorSkill.cs:91` | `BuiltInCategory.OST_GenericModel` | N/A | ❌ Tidak Ada | 🔴 **KRITIS:** Membuat `DirectShape` di bawah `OST_GenericModel`. |
| **Angkur Baja** | `ConnectionGeneratorSkill.cs:109` | `BuiltInCategory.OST_GenericModel` | N/A | ❌ Tidak Ada | 🔴 **KRITIS:** Membuat `DirectShape` di bawah `OST_GenericModel`. |
| **Stiffener Samb.**| `ConnectionGeneratorSkill.cs:140` | `BuiltInCategory.OST_GenericModel` | N/A | ❌ Tidak Ada | 🔴 **KRITIS:** Membuat `DirectShape` di bawah `OST_GenericModel`. |
| **Voute Sambungan** | `VouteProfileQTOSkill.cs:148` | `BuiltInCategory.OST_StructConnectionProfiles` | Native Advance Steel | Otomatis dari Revit | Nol. Menggunakan native connection profile resmi. |

---

## 6. Validator and Clash Capability Matrix

| Validator Tool | Implementasi File & Baris | Metode Inspeksi Geometri | Deteksi Pertemuan Sah vs Tabrakan | Audit Baseline Warning | Hak Veto Transaksi |
| :--- | :--- | :--- | :---: | :---: | :---: |
| `detect_spatial_clashes` | `BimAuditSkill.DeterministicValidator.cs:120-145` | **Bounding Box Only** (`BoundingBoxIntersectsFilter` dengan offset 0.05 ft). Bukan B-Rep Solid Boolean. | ❌ Tidak Ada (Hanya mengecek `OST_Furniture` vs `OST_Walls`). | ❌ Tidak Ada | ❌ **TIDAK ADA.** Tool berjalan read-only setelah transaksi commit. |
| `audit_material_transparency` | `BimAuditSkill.DeterministicValidator.cs:18-90` | Pengecekan parameter properti material: `Material.Transparency >= 50`. | N/A | N/A | ❌ **TIDAK ADA.** Hanya mengembalikan JSON status evaluasi. |
| `audit_unresolved_warnings` | `BimAuditSkill.DeterministicValidator.cs:150-180` | Mengambil `doc.GetWarnings()`. | N/A | ❌ **TIDAK ADA.** Menampilkan seluruh warning tanpa perbandingan baseline. | ❌ **TIDAK ADA.** |
| `capture_revit_viewport` | `BimAuditSkill.VisualAndPython.cs:18-80` | Ekspor PNG via `doc.ExportImage()` pada resolusi dinamis (default 1280px). | N/A | N/A | N/A (Inspeksi Visual). |
| `ClashWarningSwallower` | `BimAuditSkill.cs:14477-14505` | **Unconditional Suppression** (`failuresAccessor.DeleteAllWarnings()`). | ❌ Menelan semua warning secara buta tanpa log. | ❌ Tidak Ada | Mengabaikan kegagalan secara diam-diam. |

---

## 7. QTO Current-State vs Target-Standard Comparison

### A. Tabel Pelat Sambungan (`OST_StructConnectionPlates`)

| Parameter Evaluasi | Dokumen Target (`AGENTS.md` & `QTO_Template_Reference.md`) | Implementasi C# Aktif (`BimAuditSkill.cs:26240, 29471`) | Referensi API (`qto_revit_api_guide.md:22`) | Status & Kesimpulan |
| :--- | :---: | :---: | :---: | :--- |
| **Jumlah Kolom Terlihat** | **5 Kolom** | **5 Kolom** | 4 Kolom (`fThk` dihilangkan) | **KODE SUDAH BENAR (5 Kolom).** File dokumentasi API guide tertinggal (*stale documentation*). |
| **Urutan Kolom Tampil** | `Lokasi \| Tipe Sambungan \| Thickness \| Qty \| Berat` | `fLok, fType, fThk, fQty, fWeight` | `fLok, fType, fCount, fWeight` | 100% Cocok antara target dan kode live C#. |
| **Sorting / Grouping** | By `Type of Connection` $\rightarrow$ `Thickness` (Footer subtotal) | Sesuai di C# | Sesuai di C# | Sesuai standar baku. |

### B. Formula Kalibrasi Berat & Satuan:
- **Profil Kolom & Balok:** `Length (m) * WeightPerMeterKg (Database)` (`QTOExternalSkill.cs:85`).
- **Pelat Sambungan & Baseplate:** Dihitung dari parameter `Exact Weight` atau tebal $\times$ lebar $\times$ panjang $\times 7850\text{ kg/m}^3$.
- **Voute Sambungan Baja:** Diambil dari volume solid eksak `OST_StructConnectionProfiles` $\times 7850\text{ kg/m}^3$ (`VouteProfileQTOSkill.cs:201`).
- **Catatan Notasi:** Penulisan di beberapa teks `7.850 kg/m³` terbukti merupakan salah ketik notasi titik desimal Eropa/Indonesia; di kode C# seluruh kalkulasi menggunakan konstanta numerik integer murni `7850.0`.

---

## 8. Milestone and Dependency Behavior

1. **Orkestrasi 4 Makro-Milestone adalah Panduan Prompt, Bukan Mesin State:**
   - Tidak ada DAG engine, scheduler, atau alur transaksi terkontrol di kode bridge yang memblokir eksekusi Milestone $N+1$ jika Milestone $N$ belum selesai.
   - Pengecekan *Sanity Gate* (Load path, Headroom clearance $\ge 2.20\text{ m}$, Kemiringan atap) sepenuhnya merupakan evaluasi kognitif agent / LLM, bukan fungsi C# deterministik otomatis.
2. **Ketiadaan Task Scope Classifier:**
   - Sistem belum memiliki pemilah lingkup tugas. Permintaan sederhana (seperti "ubah visibilitas link di view portal") tetap membawa beban aturan makro arsitektural 4 milestone kecuali agen secara eksplisit membatasi fokusnya.
3. **Pemeriksaan Dependensi Objek Host:**
   - Di level kode tidak ada validator yang mengecek apakah dinding host sudah ada sebelum menempatkan pintu/jendela, karena tool penempatan pintu/jendela native memang belum diimplementasikan.

---

## 9. Historical Document Influence Analysis

1. **`KnowledgeBase/ConnectionModelingPlan.md`:**
   - Berisi rencana pembuatan komponen sambungan menggunakan `DirectShape`. Rencana ini **TELAH DITINGGALKAN** oleh arsitektur native, namun masih tersimpan di KnowledgeBase tanpa label `HISTORICAL / SUPERSEDED`.
2. **`archive/reports/AIGRAV_AgentOrchestrator_Architecture.md`:**
   - Merupakan proposal pra-implementasi DAG orchestrator. Dokumen ini mendefinisikan *Core Architectural Invariants* yang sangat baik, namun mesin orchestrator-nya belum diimplementasikan di folder aktif.
3. **Hot-Reload DLL Safety (`AIGRAVSkillLoader.cs`):**
   - Menggunakan `AssemblyLoadContext` kolektibel untuk memuat DLL dari folder `Skills\`.
   - **Celah Keamanan:** Tidak ada pemeriksaan tanda tangan digital (*signature*), approval prompt ke pengguna, maupun isolasi izin. Siapapun yang menaruh file `.dll` di folder `Skills\` dapat langsung mengeksekusi kode arbitrary di dalam proses Revit via `aigrav_reload_skills`.

---

## 10. Verified Facts, Assumptions, and Unknowns

### A. Fakta Terverifikasi (Verified Facts)
1. Revit Bridge memproses seluruh transaksi tulis secara serial melalui `ConcurrentQueue` dan `IExternalEventHandler.Execute` pada Revit main thread (Single-Writer 100% terbukti di kode).
2. Tool `create_door`, `create_window`, `create_roof`, `create_room`, dan `check_model_health` tidak ada di kode C#.
3. `place_family_instance` di `AIGRAVToolExecutor.cs:2153` tidak mendukung hosting dinding/lantai.
4. `ConnectionGeneratorSkill.cs` masih membuat DirectShape di bawah `OST_GenericModel`.
5. `ClashWarningSwallower` di `BimAuditSkill.cs:14493` menelan seluruh warning Revit tanpa filter.
6. Kode C# jadwal pelat sambungan (`BimAuditSkill.cs:26240`) sudah mengimplementasikan 5 kolom terlihat.
7. Tidak ada folder `C:\AIGRAV\.gemini` di filesystem.

### B. Asumsi yang Beredar di Dokumen (Document Assumptions)
1. Asumsi bahwa sistem dapat membatalkan satu milestone penuh dengan single `Ctrl+Z` (asumsi ini gugur di kode karena transaksi tidak dibungkus `TransactionGroup` makro).
2. Asumsi bahwa deteksi benturan (`detect_spatial_clashes`) memeriksa seluruh elemen bangunan (faktanya hanya furnitur vs dinding secara *bounding box*).
3. Asumsi bahwa validator memiliki hak veto otomatis membatalkan transaksi sebelum commit (faktanya validator dijalankan pasca-transaksi secara read-only).

### C. Hal yang Belum Diketahui / Butuh Keputusan User (Unknowns)
1. Apakah workflow sambungan baja di masa depan akan 100% mengandalkan native Advance Steel (`OST_StructConnectionProfiles`) atau tetap mempertahankan generator custom family (`.rfa`).
2. Kapan `create_door` dan `create_window` native berbasis host (`doc.Create.NewFamilyInstance(XYZ, symbol, wallHost, ...)` akan ditambahkan ke C# Bridge.

---

## 11. Prioritized Recommendations (P0 / P1 / P2)

### 🔴 P0 — Rekomendasi Kritis (Sebelum Merilis Aturan Baru)
1. **Sinkronkan Aturan Single-Writer:** Hapus/tandai *SUPERSEDED* Bagian 2 & 3 pada `KnowledgeBase/AIGRAV_Revit_Execution_Protocol.md` yang menyebut sub-agent paralel per zona, dan jadikan Pasal 1.1 `AGENTS.md` (Single Writer) sebagai aturan mutlak tak terbantahkan.
2. **Koreksi `qto_revit_api_guide.md`:** Perbarui baris 22 di `qto_revit_api_guide.md` agar mencantumkan `fThk` (5 kolom) sehingga dokumen referensi identik dengan kode C# aktual dan `AGENTS.md`.
3. **Beri Label `SUPERSEDED` pada Proposal DirectShape:** Beri header peringatan keras pada `KnowledgeBase/ConnectionModelingPlan.md` dan laporan historis agar AI tidak menggunakan proposal DirectShape lama sebagai acuan eksekusi.
4. **Implementasikan Tool Native Door & Window:** Tambahkan overload `CreateWallHostedInstance` pada C# Bridge untuk `OST_Doors` dan `OST_Windows` agar agen tidak terpaksa menggunakan `create_generic_model`.

### 🟠 P1 — Rekomendasi Tata Kelola & Keamanan (Arsitektur Jangka Menengah)
1. **Perbaiki `ClashWarningSwallower`:** Ganti penghapusan buta (`DeleteAllWarnings()`) dengan *allowlist* spesifik, serta simpan *warning baseline* sebelum operasi tulis dijalankan.
2. **Tingkatkan Mesin Deteksi Benturan:** Perluas `detect_spatial_clashes` agar memeriksa kolom vs balok dan pelat vs balok menggunakan solid B-Rep (`BooleanOperationsUtils.ExecuteBooleanOperation`).
3. **Bangun Element & Family Registry:** Validasi family dan category di gerbang preflight sebelum data dikirim ke transaksi Revit.

### 🟡 P2 — Rekomendasi Pengerasan Sistem (Harden & Operationalize)
1. **Bungkus Transaksi Milestone:** Jika ingin mendukung single `Ctrl+Z` per milestone, terapkan `TransactionGroup` di boundary pemanggilan alur kerja makro.
2. **Approval Gate Hot-Reload:** Tambahkan konfirmasi pengguna sebelum `aigrav_reload_skills` memuat assembly biner baru ke dalam memori proses Revit.

---

## 12. Daftar Referensi Kode dan Bukti Fisik

| Temuan Materiil | Path File Sumber | Simbol / Nama Kelas & Metode | Nomor Baris Kunci |
| :--- | :--- | :--- | :--- |
| **FIFO Write Queue** | `Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\AIGRAVRequestManager.cs` | `AIGRAVRequestManager.Queue` | Baris 7–16 |
| **UI Thread Serial Dispatcher** | `Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\RevitExternalEventHandler.cs` | `RevitExternalEventHandler.Execute` | Baris 11–36 |
| **Unhosted Family Creator** | `Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\AIGRAVToolExecutor.cs` | `AIGRAVToolExecutor.CreateFamilyInstance` | Baris 2074–2157 |
| **DirectShape Pembuatan Sambungan**| `Revit\AIGRAV.RevitBridge\AIGRAV.Skills.ConnectionGenerator\ConnectionGeneratorSkill.cs` | `ConnectionGeneratorSkill.CreateDirectShape` | Baris 91, 109, 140 |
| **DirectShape Generic Box Tool** | `Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.VisualAndPython.cs` | `SpatialAgenticSandboxSkill.CreateDirectShapeBoxJson` | Baris 149, 188–215 |
| **Bounding Box Clash Detection** | `Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.DeterministicValidator.cs` | `SpatialAgenticSandboxSkill.DetectSpatialClashesJson` | Baris 120–145 |
| **Blind Warning Suppressor** | `Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs` | `ClashWarningSwallower.PreprocessFailures` | Baris 14485–14498 |
| **Jadwal Pelat 5 Kolom Aktual** | `Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs` | `BimAuditSkill.ReorderVisibleFields` | Baris 26240, 29471 |
| **Cuplikan Jadwal 4 Kolom Usang** | `.agents\skills\revit-qto-schedule-audit\references\qto_revit_api_guide.md` | `Section 3A: Pelat Sambungan` | Baris 22 |
| **Voute QTO 7 Kolom Volume** | `Revit\AIGRAV.RevitBridge\AIGRAV.Skills.QTO\VouteProfileQTOSkill.cs` | `VouteProfileQTOSkill.CreateProfileSchedule` | Baris 195–235 |
| **Collectible Skill Hot-Reload** | `Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\AIGRAVSkillLoader.cs` | `AIGRAVSkillLoader.SkillLoadContext` | Baris 30–65 |

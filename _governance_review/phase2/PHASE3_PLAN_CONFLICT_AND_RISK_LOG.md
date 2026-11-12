# AIGRAV Phase 3 Implementation Plan — Conflict and Risk Log

**Status:** APPROVED ARCHITECTURAL RISK & CONFLICT AUDIT  
**Staging Directory:** `C:\AIGRAV\_governance_review\phase2\`  
**Cross-References:**  
- [`PHASE3_IMPLEMENTATION_PLAN_REVISED.md`](file:///C:/AIGRAV/_governance_review/phase2/PHASE3_IMPLEMENTATION_PLAN_REVISED.md)  
- [`LIVE_CAPTURE_AND_VISUAL_FEEDBACK_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/LIVE_CAPTURE_AND_VISUAL_FEEDBACK_POLICY.md)  
- [`ACCEPTANCE_TEST_MATRIX.md`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md)  
- [`EXECUTION_AND_TRANSACTION_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/EXECUTION_AND_TRANSACTION_POLICY.md)  
- [`DIRECTSHAPE_EXCEPTION_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/DIRECTSHAPE_EXCEPTION_POLICY.md)  
- [Review Addendum Artifact](file:///C:/Users/sapuser/.gemini/antigravity/brain/d6d604bd-c0d6-42bb-92dd-0ab6b71e37d0/.user_uploaded/media_1794436843015_5f676aae.md)

---

## 1. Executive Summary

Dokumen ini mendokumentasikan seluruh konflik arsitektur, asumsi teknis yang berbahaya (*technical traps*), celah implementasi (*sequencing gaps*), dan risiko kegagalan API Revit yang teridentifikasi selama audit perencanaan Phase 3 (Review Addendum 2026-11-12).

Setiap temuan telah dipetakan dampaknya, dirumuskan strategi mitigasi arsitekturnya, serta dihubungkan ke pengujian deterministik pada Acceptance Test Matrix (TC-28 s/d TC-37). Dokumen ini menjadi dasar verifikasi sebelum Batch 1 diizinkan untuk dieksekusi.

---

## 2. Risk & Conflict Matrix

| ID | Kategori | Ringkasan Konflik / Risiko | Tingkat Risiko | Status Resolusi | Acceptance Test Terkait |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **R-01** | Sequencing | Early Multi-View Requirement vs Batch 4 Camera Engine | **CRITICAL** | RESOLVED (4-Stage View Rollout) | TC-28, TC-16, TC-17 |
| **R-02** | Architecture | View Generation Scope Too Narrow (`CameraOrientationEngine`) | **HIGH** | RESOLVED (5-Component View System) | TC-35, TC-36 |
| **R-03** | Rollback | Generic Deletion Rollback & Cascaded Element Deletion | **CRITICAL** | RESOLVED (3-Status Journal + Cascade Guard) | TC-31, TC-32, TC-07 |
| **R-04** | Revit API | Roof Slope Units Mismatch (Degrees vs Rise/Run Ratio) | **HIGH** | RESOLVED ($\tan(\theta \times \pi / 180)$ Conversion) | TC-29 |
| **R-05** | Revit API | Room Boundary False-Positive (`NewRoom` Unbounded Success) | **CRITICAL** | RESOLVED (`GetBoundarySegments` Gate & Rollback) | TC-30 |
| **R-06** | Audit/Warning | Warning Preprocessing vs Post-Commit Baseline Diff Confusion | **MEDIUM** | RESOLVED (Separation of Preprocessor & Diff) | TC-06 |
| **R-07** | QA/Visual | Image Quality Gate Flaw (`> 20 KB` Threshold False Passes/Fails) | **MEDIUM** | RESOLVED (Multi-Dimensional Quality Gate) | TC-25, TC-34 |
| **R-08** | Revit API | Window Sill Height Parameter Variance & Unit Conversion | **MEDIUM** | RESOLVED (Parameter Capability Check + Imperial Conversion) | TC-33 |
| **R-09** | Security | Generic Model Provenance Security Boundary Bypassing | **HIGH** | RESOLVED (Server-Side Intent Authorization) | TC-37, TC-02 |
| **R-10** | Lifecycle | Project Pollution / Destruction of User-Authored Views | **HIGH** | RESOLVED (Metadata Ownership & `ViewCleanupManager`) | TC-36 |

---

## 3. Detailed Conflict Analyses & Resolutions

### 3.1. Risk R-01: Sequencing Mismatch — Early Multi-View Requirement vs Batch 4 Camera Engine
- **Deskripsi Konflik Asli:**  
  Rencana awal Phase 3 mewajibkan live capture multi-view (3 view lokal di Batch 2, 4 view di Batch 3), namun mesin orientasi kamera dan view generator baru direncanakan dibuat pada Batch 4 (`CameraOrientationEngine.cs`). Akibatnya, Batch 2 dan Batch 3 tidak memiliki sarana untuk menjamin ketersediaan view yang diwajibkan, memicu kegagalan eksekusi atau pemalsuan status PASS.
- **Dampak Arsitektural:**  
  Deadlock pada Acceptance Criteria Batch 2 dan Batch 3 karena dependensi mundur (*circular dependency*).
- **Strategi Resolusi:**  
  Penyusunan ulang kapabilitas view capture menjadi **4 Tahap Progresif**:
  1. *Batch 1:* **Capture Infrastructure MVP** — Membangun data contract `ViewCoverageManifest` dan `CaptureCoordinator`. Mengutamakan *reuse* view eksisting yang valid dalam model uji. Jika view tidak ditemukan, status dicatat jujur sebagai `UNCOVERED`.
  2. *Batch 2:* **Local View Generation MVP** — Mengembangkan `RevitViewGenerator` khusus untuk membuat view lokal: Plan terfokus, Elevation/Section tegak lurus dinding host, dan Isometric 3D terfokus di sekitar opening/wall.
  3. *Batch 3:* **Roof/Room View Coverage** — Memperluas view generator untuk mencakup Top/Roof Plan, Building Section melintang, Exterior Perspective, dan Interior Enclosure Perspective.
  4. *Batch 4:* **Full-Building Manifest & Synthesizer** — Finalisasi otomatisasi 9-view bangunan penuh, multi-floor coverage, occlusion checking, dan visual comparison gallery.
- **Verifikasi:** [`TC-28`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-28-batch-2-local-view-generation-or-uncovered-blockade) dan [`TC-19`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-19-uncovered-view-protocol-truth-disclosure).

---

### 3.2. Risk R-02: View Generation System Decomposition (`CameraOrientationEngine` Terlalu Sempit)
- **Deskripsi Konflik Asli:**  
  Penamaan dan lingkup awal `CameraOrientationEngine` hanya merepresentasikan manipulasi kamera 3D (eye/up/forward vectors). Namun sistem diwajibkan memproduksi gambar denah (Plan), tampak (Elevation), dan potongan (Section) yang memerlukan API Revit yang sama sekali berbeda.
- **Dampak Arsitektural:**  
  Arsitektur modul menjadi monolitik atau gagal menangani pembuatan view 2D ortogonal.
- **Strategi Resolusi:**  
  Memecah sistem capture menjadi **5 Komponen Modular Terpisah**:
  1. `ViewCoverageCoordinator`: Menentukan daftar view yang wajib berdasarkan scope tugas dan manifest.
  2. `RevitViewGenerator`: Memanggil API spesifik Revit untuk pembuatan view:
     - 3D: `View3D.CreateIsometric(doc, viewFamilyTypeId)` + setting section box.
     - Elevation: `ElevationMarker.CreateElevationMarker(doc, viewFamilyTypeId, origin, scale)` + `marker.CreateElevation(doc, viewPlanId, index)`.
     - Section: `ViewSection.CreateSection(doc, viewFamilyTypeId, boundingBoxXYZ)` dengan transform ortonormal presisi.
     - Plan: Reuse `ViewPlan` eksisting atau `ViewPlan.Create(doc, viewFamilyTypeId, levelId)`.
  3. `CameraOrientationEngine`: Fokus murni pada matematika 3D: perhitungan eye position, target center, up vector, bounding sphere, dan orientasi isometrik standar (SW/SE/NW/NE).
  4. `ViewCaptureExporter`: Menjalankan `doc.ExportImage(options)` dan memverifikasi integritas file gambar yang dihasilkan.
  5. `ViewCleanupManager`: Melacak lifecycle view sementara dan menghapusnya secara aman setelah export.
- **Verifikasi:** [`TC-35`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-35-view-coverage-target-element-visibility-validation) dan [`TC-36`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-36-temporary-view-lifecycle-cleanup-safety).

---

### 3.3. Risk R-03: Rollback Semantics & Cascaded Element Deletion Risks
- **Deskripsi Konflik Asli:**  
  Rencana awal menyatakan compensating rollback dilakukan dengan `doc.Delete()` atau restorasi snapshot secara generik. Kenyataannya di Revit API:
  - Pemanggilan `doc.Delete(elementId)` dapat memicu *cascaded deletion* (misal menghapus dinding akan ikut menghapus pintu, jendela, dimensi, dan balok yang menempel padanya).
  - Elemen yang sudah di-delete TIDAK BISA dipulihkan secara generik karena ElementId, GUID, relasi hosting, dependensi dimensi, dan join geometry-nya hilang permanen.
- **Dampak Arsitektural:**  
  Compensating rollback berpotensi merusak elemen eksisting pengguna yang tidak bersalah, atau mengklaim restorasi palsu atas elemen yang dihapus.
- **Strategi Resolusi:**  
  1. **Strict 3-Status Journal Contract:**
     - `CREATED`: Elemen baru yang aman dihapus via `doc.Delete()`. **Wajib di-delete dengan urutan topologis terbalik** (elemen hosted/dependent dihapus sebelum host). Kumpulan ID yang dihapus oleh Revit (`ICollection<ElementId>`) wajib di-cross-check terhadap daftar ID yang dibuat dalam batch. Jika terdeteksi ID elemen di luar batch ikut terhapus, rollback dihentikan dan sistem memicu peringatan eskalasi darurat.
     - `MODIFIED`: Dilarang di-delete! Wajib memiliki snapshot parameter/posisi sebelum transaksi dan dieksekusi dengan *explicit inverse handler* (mengembalikan parameter ke nilai semula). Jika mutasi melibatkan geometri kompleks yang tidak memiliki handler invers teruji, operasi ditandai `NON_COMPENSABLE`.
     - `DELETED`: **Secara default berstatus `NON_COMPENSABLE`**. Penghapusan elemen eksisting tidak memiliki garansi restorasi otomatis tanpa modul restore khusus berpasangan.
  2. **Idempotensi & Crash Resilience:**
     - Operasi kompensasi dilindungi flag idempotensi (`IsCompensated = true`) agar tidak dieksekusi ganda.
     - Dicatat dalam batasan arsitektur bahwa in-memory journal hanya meng-cover rollback dalam satu sesi aktif, bukan proteksi crash proses Revit (karena transaksi yang crash otomatis di-rollback oleh Revit DB kernel).
- **Verifikasi:** [`TC-31`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-31-deleted-journal-entries-default-to-non_compensable) dan [`TC-32`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-32-compensating-deletion-cascade-effect-guard).

---

### 3.4. Risk R-04: Roof Slope Angle Units Mismatch (Degrees vs Rise/Run Ratio)
- **Deskripsi Konflik Asli:**  
  Revit API pada `FootPrintRoof.SlopeAngle` (dan slope property pada boundary ModelCurve) **bukan menerima satuan derajat (degrees) maupun radian**, melainkan **rasio kemiringan (rise/run ratio)**! Jika pengguna meminta atap sudut $30^\circ$ dan AI memasukkan angka `30` langsung ke API, Revit menginterpretasikannya sebagai rasio $30 : 1$ (tanjakan setara $\approx 88^\circ$ atau dinding vertikal ekstrem).
- **Dampak Arsitektural:**  
  Geometri atap hancur, miring vertikal, atau gagal di-generate (*Roof cannot be created*).
- **Strategi Resolusi:**  
  Modul `aigrav_create_roof` wajib melakukan konversi matematis eksplisit:
  $$\text{slopeRatio} = \tan\left(\theta \times \frac{\pi}{180.0}\right)$$
  - Contoh: Sudut $30^\circ \implies \tan(0.523599) \approx 0.57735$.
  - Batasan sudut dan overhang: Nilai overhang (default 800 mm) dan rentang sudut kemiringan dikonfigurasi sebagai *configurable project parameter*, bukan *hardcoded law*, dengan validasi kurva poligon tertutup dan planar.
- **Verifikasi:** [`TC-29`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-29-roof-slope-rise-over-run-unit-conversion).

---

### 3.5. Risk R-05: Room Boundary False-Positive Trap (`NewRoom` Unbounded Success)
- **Deskripsi Konflik Asli:**  
  Metode Revit API `Document.Create.NewRoom(Level, UV)` akan sukses membuat objek `Room` (mengembalikan objek non-null) meskipun titik koordinat UV berada di lapangan terbuka atau ruangan dengan dinding yang bocor. Ruangan tersebut berstatus `Not Enclosed` dengan `Area == 0.0`. Jika verifikasi hanya mengecek `room != null`, sistem akan melaporkan sukses palsu.
- **Dampak Arsitektural:**  
  Ruangan yang tidak tertutup bocor ke seluruh level, merusak schedule luasan arsitektur (QTO), dan melanggar integritas BIM.
- **Strategi Resolusi:**  
  Alur wajib pembuatan ruang pada `aigrav_create_room`:
  1. Preflight Level, Phase, dan Room-Bounding parameter pada dinding pembatas.
  2. Eksekusi `doc.Create.NewRoom(level, uv)`.
  3. Panggil `doc.Regenerate()` untuk memaksa Revit Engine menghitung kalkulasi spasial.
  4. Query batas ruang via `room.GetBoundarySegments(new SpatialElementBoundaryOptions())`.
  5. Validasi: Boundary segments wajib non-null, sirkuit kurva tertutup $> 0$, dan `room.Area > 0.001`.
  6. Jika batas kosong atau tidak tertutup: **Wajib me-rollback transaksi lokal** dan mengembalikan structured error `ERR_ROOM_NOT_ENCLOSED` disertai titik koordinat kebocoran.
- **Verifikasi:** [`TC-30`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-30-room-boundary-enclosure-failure-rollback).

---

### 3.6. Risk R-06: Warning Preprocessing vs Post-Commit Document Baseline Diff Confusion
- **Deskripsi Konflik Asli:**  
  Rencana awal mencampuradukkan penanganan warning Revit selama transaksi berlangsung dengan audit warning dokumen secara keseluruhan. Akibatnya, ada risiko transaksi di-commit dengan warning berbahaya, atau sebaliknya warning lama yang sudah ada di dokumen menyebabkan transaksi baru dianggap gagal.
- **Dampak Arsitektural:**  
  Kegagalan membedakan warning yang diakibatkan oleh tool baru vs warning warisan (*legacy warnings*), serta ketidakjelasan titik intervensi API.
- **Strategi Resolusi:**  
  Pemisahan tegas dua mekanisme:
  1. **In-Transaction Failure Preprocessing (`IFailuresPreprocessor`):**  
     Bekerja di dalam scope transaksi Revit. Mencegah dialog popup Revit memblokir eksekusi single-thread. Hanya menekan (*suppress*) warning yang terdaftar secara eksplisit dalam **Reviewed Allowlist**. Warning yang tidak dikenal atau bertipe Error wajib memicu transaksi dibatalkan (`RollBack`).
  2. **Post-Commit Document Baseline Diff (`Document.GetWarnings()`):**  
     Bekerja di luar transaksi. Mengambil snapshot ID warning dokumen sebelum batch dimulai ($W_{\text{before}}$) dan sesudah commit ($W_{\text{after}}$). Menghitung $\Delta W = W_{\text{after}} \setminus W_{\text{before}}$. Jika $\Delta W > 0$, warning baru dilaporkan dalam telemetri response sebagai audit temuan.
- **Verifikasi:** [`TC-06`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-06-audit-unresolved-warnings-telemetry).

---

### 3.7. Risk R-07: Image Quality Gate Flaw (`> 20 KB` Threshold False Passes/Fails)
- **Deskripsi Konflik Asli:**  
  Kriteria kualitas gambar sebelumnya mengandalkan ambang batas ukuran file `> 20 KB`. Kriteria ini cacat: gambar denah 2D sederhana yang valid dengan kompresi PNG efisien bisa berukuran 15 KB (akan gagal secara salah/false fail), sementara gambar 3D yang salah sorot (misal kamera menatap ke langit kosong berwarna abu-abu gradien penuh noise kompresi) bisa berukuran 50 KB (akan lolos secara salah/false pass).
- **Dampak Arsitektural:**  
  Verifikasi visual menjadi rapuh dan tidak mencerminkan kualitas gambar yang sebenarnya.
- **Strategi Resolusi:**  
  Menghapus aturan `> 20 KB` dan menggantinya dengan **Multi-Dimensional Quality Gate**:
  1. Validasi header biner PNG (`89 50 4E 47 0D 0A 1A 0A`) dan dekompresi bitmap berhasil.
  2. Dimensi pixel minimum ($W \ge 1280\text{ px}, H \ge 720\text{ px}$).
  3. Blank/Solid Frame Detection: Menghitung varians warna pixel; menolak gambar monokrom pejal (100% putih, 100% hitam, atau solid gray).
  4. Metadata Check: Memastikan file memiliki timestamp baru, view ID yang cocok, dan target elemen berada dalam jangkauan view/frustum.
- **Verifikasi:** [`TC-25`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-25-visual-clarity-gate-rejection) dan [`TC-34`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-34-capture-image-validity-multi-dimensional-check).

---

### 3.8. Risk R-08: Window Sill Height Parameter Variance & Unit Conversion
- **Deskripsi Konflik Asli:**  
  Family jendela custom di Revit memiliki penamaan parameter tinggi ambang bawah (*sill height*) yang berbeda-beda (`INSTANCE_SILL_HEIGHT_PARAM`, `Sill Height`, `Tinggi Ambang`). Mengasumsikan setiap jendela memiliki parameter yang seragam dan bertipe writable memicu exception runtime. Selain itu, input dalam milimeter harus dikonversi ke internal feet Revit ($1\text{ ft} = 304.8\text{ mm}$).
- **Dampak Arsitektural:**  
  Crash transaksi saat penempatan jendela custom atau salah elevasi jendela (misal 900 mm menjadi 900 feet melayang di stratosfer).
- **Strategi Resolusi:**  
  1. Konversi satuan internal: $\text{feetValue} = \text{mmValue} / 304.8$.
  2. Algoritma lookup parameter bertingkat:
     - Cari BuiltInParameter bawaan: `BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM`.
     - Jika null, cari parameter instan berdasarkan nama: `"Sill Height"`, `"Tinggi Ambang"`.
     - Validasi `parameter.IsReadOnly == false` dan `parameter.StorageType == StorageType.Double`.
  3. Jika parameter tidak ditemukan atau read-only, jangan crash! Catat sebagai warning terstruktur dalam response JSON dan laporkan elevasi aktual hasil regenerasi.
- **Verifikasi:** [`TC-33`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-33-window-sill-parameter-variance-safety).

---

### 3.9. Risk R-09: Generic Model Provenance Security Boundary Bypassing
- **Deskripsi Konflik Asli:**  
  Adanya parameter pelacak `AIGRAV_PROVENANCE` dapat disalahartikan sebagai "pagar pengaman". Jika izin penggunaan Generic Model / DirectShape hanya didasarkan pada apakah elemen memiliki parameter teks tersebut, maka klien/skrip luar dapat dengan mudah mem-bypass aturan dengan menyisipkan parameter teks pada elemen ilegal.
- **Dampak Arsitektural:**  
  Pelemahan pengamanan arsitektur; DirectShape atau Generic Model liar bisa masuk ke dalam model tanpa kendali resmi.
- **Strategi Resolusi:**  
  - Parameter `AIGRAV_PROVENANCE` hanya berfungsi sebagai **metadata jejak audit (audit trail)**, BUKAN sistem autentikasi keamanan (*security boundary*).
  - Otorisasi pengecualian DirectShape **WAJIB diverifikasi di sisi server (Server-Side Intent Authorization)**:
    - Memeriksa nama endpoint pemanggil (`ConnectionGeneratorSkill` saja).
    - Memeriksa kategori target (`OST_StructConnections` saja).
    - Menolak keras permohonan pembuatan Generic Model untuk kategori arsitektur inti (Pintu, Jendela, Dinding, Lantai, Atap, Ruang).
- **Verifikasi:** [`TC-37`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-37-generic-model-provenance-server-side-authorization).

---

### 3.10. Risk R-10: View Lifecycle Management & User-Authored View Contamination
- **Deskripsi Konflik Asli:**  
  Jika sistem otomatis membuat view sementara untuk keperluan visual verification dan kemudian menghapusnya secara ceroboh, ada risiko view gambar kerja yang dibuat oleh pengguna manusia terhapus secara tidak sengaja karena memiliki nama atau orientasi yang mirip.
- **Dampak Arsitektural:**  
  Kerusakan data proyek pengguna (*data loss*) dan hilangnya kepercayaan terhadap AIGRAV.
- **Strategi Resolusi:**  
  1. Seluruh view yang dibuat secara otomatis oleh AIGRAV **WAJIB memiliki penanda kepemilikan eksplisit**:
     - Parameter/Extensible Storage: `AIGRAV_VIEW_OWNER == "AIGRAV_AGENT"`.
     - Prefix nama terstandarisasi: `AIGRAV_TEMP_CAPTURE_<Guid>`.
  2. Modul `ViewCleanupManager`:
     - Hanya boleh menghapus view yang memiliki metadata kepemilikan AIGRAV yang valid.
     - View yang dibuat oleh pengguna (**User-Authored Views**) DILARANG KERAS diubah, dipotong, di-override visibilitasnya, atau dihapus.
     - Simpan marker dan view sementara dalam transaksi terpisah yang dapat dibersihkan tanpa mengganggu model utama.
- **Verifikasi:** [`TC-36`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-36-temporary-view-lifecycle-cleanup-safety).

---

### 3.11. Risk R-11: Misklasifikasi Metode Pembuatan View Revit API & Sintaks ElevationMarker
- **Deskripsi Konflik Asli:**  
  Dokumentasi sebelumnya berpotensi mengelompokkan `ViewSection.CreateDetail()` secara keliru sebagai pembuat tampak (*elevation*) atau pembuat potongan utama (*section*), serta menuliskan sintaks pemanggilan static invocation yang salah untuk pembuatan view tampak.
- **Dampak Arsitektural:**  
  Pemanggilan API yang salah sasaran (`CreateDetail` tidak memotong model penuh dan menggunakan `ViewFamily.Detail`) atau runtime exception akibat memanggil metode statis `ElevationMarker.CreateElevation` yang tidak ada dalam SDK Revit.
- **Strategi Resolusi:**  
  - Tetapkan pemetaan metode API yang tepat:
    * `View3D.CreateIsometric()`: Pembuat 3D isometrik native (`ViewFamily.ThreeDimensional`).
    * `View3D.SetOrientation()`: Penyetel orientasi kamera 3D (eye, up, forward vector).
    * `ViewSection.CreateSection()`: Pembuat potongan melintang bangunan penuh dengan transform BBox ortonormal (`ViewFamily.Section`).
    * `ViewSection.CreateDetail()`: Khusus detail view saja (`ViewFamily.Detail`); dilarang keras sebagai generator tampak atau potongan.
    * Sekuens `ElevationMarker`: Dua tahap resmi: `ElevationMarker.CreateElevationMarker` (static factory untuk membuat elemen penanda tampak) dilanjutkan dengan `marker.CreateElevation` (instance method untuk membuat view tampak ber-host). Dilarang keras memanggil static `ElevationMarker.CreateElevation`.
- **Verifikasi:** [`TC-28`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-28-pembuatan-view-lokal-batch-2-atau-pemblokiran-uncovered-poc-view-01--poc-view-02).

---

### 3.12. Risk R-12: Ketidaksesuaian Rujukan Standar Teknis & Batasan Toleransi Universal
- **Deskripsi Konflik Asli:**  
  Penggunaan standar teknis yang tidak tepat klausulnya (misal: AAMA TIR-A11-15 untuk toleransi ereksi fasad padahal mengatur lendutan beban angin; AAMA 501 untuk toleransi padahal metode uji kebocoran air/udara; SNI 03-6572-2001 untuk minimum headroom padahal tata cara ventilasi/AC; atribusi gedung tinggi $> 8$ lantai dengan pasal yang salah atau penambahan "huruf c" pada PP 16/2021; PCI MNL 135-00 usang).
- **Dampak Arsitektural:**  
  Kesalahan kepatuhan hukum dan teknis; toleransi lapangan dipaksakan seragam padahal bervariasi per sistem fasad dan spesifikasi kontrak arsitektur.
- **Strategi Resolusi:**  
  - Audit seluruh standar teknis dengan edisi dan nomor pasal resmi:
    * AAMA TIR-A11-15: Lendutan beban angin rencana ($L/175$ s/d $L/240$). Toleransi ereksi fasad dialihkan ke konfigurasi desain proyek (`ProjectConfig.FacadeTolerance`).
    * AAMA 501 Series: Metode uji laboratorium/lapangan selubung dinding luar terhadap air dan udara.
    * SNI 03-6572-2001: Tata cara ventilasi & AC (kenyamanan termal).
    * SNI 2847:2019: Bedakan Pasal 20.6.1.3.1 (tebal selimut beton yang ditentukan) dari Pasal 26.6.2 (toleransi penempatan tulangan).
    * IBC 2024: Section 202 (definisi high-rise $> 75\text{ ft}$) vs Section 403.1 (ruang lingkup applicability); Section 1208.2 untuk tinggi plafon minimum; Section 1207.2 untuk akustik kelas; Chapter 14 Section 1402 untuk Performance Requirements. IBC diperlakukan sebagai rujukan komparatif / adopsi proyek internasional, bukan regulasi wajib Indonesia.
    * PP No. 16 Tahun 2021: Pasal 9 ayat (6), jo. Lampiran bagian 5 — Ketinggian Bangunan Gedung (Pasal 9 ayat (6) menetapkan kelompok klasifikasi tingkat tinggi dan Lampiran bagian 5 menetapkan ambang batas jumlah lantai eksak $> 8$ lantai). Dipisahkan secara sadar dari ambang batas pengujian arsitektural terkonfigurasi `ProjectConfig.HighRiseThresholdFloors = 8`.
    * Permen PU No. 29/PRT/M/2006: Ditandai sebagai `UNVERIFIED_STANDARD_REFERENCE` (angka numerik eksplisit memerlukan rujukan Perda/spesifikasi proyek).
    * PCI 135-25: Edisi mutakhir 2025 (*Tolerance Manual for Precast and Prestressed Concrete Construction*) menggantikan MNL 135-00 (MNL 135-00 dipertahankan hanya sebagai rujukan historis).
- **Verifikasi:** [`BUILDING_TYPOLOGY_VALIDATION_REPORT.md`](file:///C:/AIGRAV/_governance_review/phase2/BUILDING_TYPOLOGY_VALIDATION_REPORT.md), [`TC-39`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-39), [`TC-40`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-40), [`TC-43`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md#tc-43).

---

### 3.13. Risk R-13: Ambiguitas Penomoran POC & Batasan Operation Journal
- **Deskripsi Konflik Asli:**  
  Penomoran POC sekuensial (POC-01, POC-02, POC-03) tertukar lintas iterasi; Operation Journal hanya difokuskan pada cascade deletion tanpa memvalidasi 4 kelas mutasi utuh; sintaks perbandingan set tidak valid; klaim restorasi universal tanpa dasar API; dan reload otomatis yang membahayakan model kerja bersama (*workshared*).
- **Dampak Arsitektural:**  
  Kebingungan pelacakan hasil uji empiris; korupsi model Revit jika compensating deletion menghapus elemen eksisting atau elemen tersemat (`element.Pinned`); desinkronisasi central model jika file cloud ditimpa otomatis.
- **Strategi Resolusi:**  
  - Tetapkan Registri ID Permanen POC: `POC-VIEW-01`, `POC-VIEW-02`, `POC-JOURNAL-01`, `POC-ROOF-01`, `POC-PERF-01`.
  - Perluas cakupan `POC-JOURNAL-01` untuk memverifikasi secara independen 4 kelas mutasi:
    1. `CREATED`: Dry-run simulation dalam `SubTransaction` terisolasi sebelum penghapusan riil; inspeksi seluruh kaskade kembalian `doc.Delete()` untuk membuktikan zero interaksi dengan 4 kelas elemen terproteksi: elemen dependen yang dipin pengguna (`element.Pinned == true`), elemen pre-existing (`deletedSet.Overlaps(preExistingSet) == false`), elemen modifikasi pengguna, dan elemen dengan kepemilikan tidak dikenal (*unknown ownership*). Jika kaskade menyentuh elemen terproteksi atau unknown, sub-transaksi wajib dibatalkan via `subTrans.RollBack()` dan compensating deletion riil ditolak aman.
    2. `MODIFIED`: Perekaman before-state property snapshot dan translasi spasial invers $-\vec{\Delta}$ (terbatas pada supported-operation matrix: parameter standar dan translasi linear; sketch/compound layers tidak didukung).
    3. `DELETED`: Rekonstruksi elemen dengan `ElementId` baru dan pemetaan identitas logis stabil (`AIGRAV_LOGICAL_ID` GUID via Extensible Storage). Tanpa klaim restorasi universal semata dari parameter dan koordinat XYZ.
    4. `NON_COMPENSABLE`: Menghentikan proses rollback otomatis dan beralih ke prosedur pemulihan manual terkendali. Dilarang keras melakukan reload file atau overwrite otomatis (`RELOAD_BASELINE_FROM_DISK`) pada model Revit Cloud Worksharing (BIM 360 / ACC) atau workshared central model.
- **Verifikasi:** [`TECHNICAL_FEASIBILITY_REPORT.md`](file:///C:/AIGRAV/_governance_review/phase2/TECHNICAL_FEASIBILITY_REPORT.md#3-registri-permanen-pengujian-pembuktian-konsep-permanent-poc-register).

---

### 3.14. Risk R-14: Asumsi Performa Tanpa Bukti Pengukuran Empiris
- **Deskripsi Konflik Asli:**  
  Penetapan angka performa kaku ($T_{\text{base}} = 120\text{ ms}$ dan $k = 4.5\text{ ms/elemen}$) sebelum benchmark model multi-skala dijalankan, serta pengulangan terbatas tanpa pemulihan baseline.
- **Dampak Arsitektural:**  
  False alarm timeout pada model besar atau toleransi berlebih pada model kecil yang berisiko menyamarkan deadlock; akumulasi sampah memori yang membiaskan hasil pengukuran.
- **Strategi Resolusi:**  
  - Hapus klaim konstanta performa baku. Perlakukan angka tersebut murni sebagai hipotesis pengujian.
  - Jalankan `POC-PERF-01` pada model 1k, 10k, dan 50k elemen:
    * 30 repetisi pertama diklasifikasikan sebagai pengukuran rintisan (*pilot measurements*).
    * Pengujian benchmark formal wajib menggunakan sampel $N \ge 100$ repetisi dengan pemulihan baseline model ekivalen per repetisi (*baseline restoration*).
  - Catat statistik Minimum, Maksimum, Median, dan Persentil ke-95 ($p_{95}$).
  - Tetapkan formula ambang batas penerimaan Fase 3: $T_{\text{acceptance}} = p_{95} \times 1.5$, di mana pengali $1.5$ dinyatakan secara sadar sebagai **faktor pengaman kebijakan yang dipilih secara sadar (*explicitly selected policy safety factor*)**, bukan murni turunan matematis empiris.
- **Verifikasi:** [`POC-PERF-01`](file:///C:/AIGRAV/_governance_review/phase2/TECHNICAL_FEASIBILITY_REPORT.md#35-poc-perf-01-benchmark-kinerja-latensi-deletion--rollback-skala-multi-model).

---

### 3.15. Risk R-15: Monolithic Scope Overload pada Batch 4 & Pemisahan Kepemilikan Modul
- **Deskripsi Konflik Asli:**  
  Batch 4 menggabungkan sintesis kamera, evaluasi kualitas fasad, utilitas MEP, dan audit kuantitas QTO dalam satu wadah tanpa batas kepemilikan modul yang tegas.
- **Dampak Arsitektural:**  
  Kegagalan pada salah satu disiplin (misal rute pipa MEP) dapat memblokir verifikasi kamera visual atau mendiskualifikasi schedule baja yang sah.
- **Strategi Resolusi:**  
  - Dekomposisi Batch 4 secara kanonikal menjadi 4 Sub-Modul Terisolasi dengan kepemilikan modul terpisah:
    * 4A: View Synthesis & Visual Verification (`CameraOrientationEngine.cs`, `ViewCoverageCoordinator.cs`, `ViewCaptureExporter.cs`).
    * 4B: Architectural Quality & Typology (`ArchitecturalQualityValidator.cs`).
    * 4C: Façade System (`FacadeSystemCreator.cs`, `CurtainWallNativeModule.cs`).
    * 4D: MEP System & QTO Audit Engine:
      - `MepRoutingCreator.cs`: Pembuatan rute saluran udara (`OST_DuctCurves`) dan perpipaan plumbing (`OST_PipeCurves`) native.
      - `MEPDisciplineValidator.cs`: Validasi integritas semantik, konektivitas, clearance plenum, dan clash detection.
      - `BimQTOAuditEngine.cs`: Audit jadwal kuantitas lintas disiplin (Arsitektur, Struktur, MEP) dengan 5 kolom pelat baku, 7 kolom voute volume solid eksak, dan filter anti double-counting (`Lokasi`).
  - Pertahankan Persyaratan Penerimaan QTO Lintas Disiplin (*Cross-Discipline QTO Acceptance*) untuk memvalidasi kuantitas seluruh elemen arsitektur, struktur, fasad, dan MEP.
- **Verifikasi:** [`PHASE3_IMPLEMENTATION_PLAN_REVISED.md`](file:///C:/AIGRAV/_governance_review/phase2/PHASE3_IMPLEMENTATION_PLAN_REVISED.md#4-sub-modul-4a-view-synthesis--visual-verification).

---

## 4. Kesimpulan & Status Kesiapan

Seluruh 15 risiko dan konflik teknis di atas telah dimitigasi secara tuntas dalam rancangan tata kelola dan cetak biru arsitektur `PHASE3_IMPLEMENTATION_PLAN_REVISED.md`. Tidak ada lagi celah dependensi sirkular antara Live Capture dan pembuatan elemen, tidak ada lagi ambiguitas satuan pada API Revit, seluruh transaksi dilindungi oleh *fail-safe compensating journal*, dan seluruh standar teknis telah diverifikasi edisi dan pasalnya.

Sistem tata kelola kini berstatus **`GOVERNANCE_FROZEN — DOCUMENTATION_AUDIT_COMPLETE — POC_NOT_EXECUTED`**. Tim engineering memiliki panduan preventif yang kokoh dan siap menjalankan pengujian pembuktian konsep mandiri (**Tahap 0: POC Suite**) di lingkungan terisolasi segera setelah otorisasi eksplisit diberikan oleh pengguna.

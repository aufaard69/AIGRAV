# AIGRAV Live Capture & Visual Feedback Loop Policy (Architectural Quality Revision)

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan standar penangkapan visual langsung (*Live Viewport Capture*), arsitektur 5 komponen sistem tampilan, siklus umpan balik visual tertutup (*Observe → Execute → Inspect → Correct → Verify*), penyelarasan *View Coverage Manifest* terhadap Hierarki Pemodelan 8 Tahap, kriteria inspeksi kehalusan arsitektural (*Architectural Refinement & Façade Rhythm*), dan proteksi mutlak terhadap view buatan pengguna.

---

## 1. Siklus Validasi Visual Tertutup (*Closed Visual Feedback Loop*)

Setiap agen pemodelan dan eksekutor batch di AIGRAV **WAJIB** menerapkan siklus 5 tahap:  
$$\text{Observe} \longrightarrow \text{Execute} \longrightarrow \text{Inspect} \longrightarrow \text{Correct} \longrightarrow \text{Verify}$$

```
[1. OBSERVE (Pra-Eksekusi)]
   └── Tangkap baseline gambar viewport (Pre-Batch Baseline Capture)
   └── Validasi kondisi model eksisting & tetapkan View Coverage Manifest per Task Scope
          │
          ▼
[2. EXECUTE (Eksekusi Transaksi)]
   └── Jalankan operasi tulis dalam transaksi lokal C#
   └── Catat mutasi ID ke dalam Operation Journal (CREATED, MODIFIED, DELETED)
          │
          ▼
[3. INSPECT (Pasca-Eksekusi & Audit Visual Multi-View)]
   └── Ekspor seluruh sudut pandang yang diwajibkan dalam Manifest (Makro & Mikro)
   └── Verifikasi Visibilitas Elemen Target (Target Element Visibility Check)
   └── Audit Kualitas Arsitektur:
       * Konsistensi irama fasad (Façade Rhythm & Spacing)
       * Kejelasan transisi vertikal (Podium - Tower - Crown)
       * Ketiadaan cacat visual (Z-fighting, pergeseran bidang, tembus pandang kaca >= 50%)
          │
          ├── APAKAH DITEMUKAN CACAT VISUAL / GEOMETRI?
          │     ├── TIDAK ──> Lanjut ke [5. VERIFY]
          │     └── YA
          │           │
          ▼           ▼
[4. CORRECT (Rencana Perbaikan Terstruktur & Re-Capture)]
   └── Susun Repair Plan terstruktur (ID elemen target, parameter koreksi, pergeseran koordinat)
   └── Eksekusi perbaikan aman (Maksimal 2 iterasi otomatis)
   └── Ambil ulang (*Re-capture*) SELURUH view yang terdampak perbaikan
          │
          ▼
[5. VERIFY (Pernyataan Selesai & Pembersihan)]
   └── Konfirmasi kelengkapan manifest: Zero UNCOVERED mandatory views
   └── Jalankan ViewCleanupManager untuk membersihkan temporary views/markers AIGRAV
   └── Sajikan galeri komparatif Sebelum vs Sesudah kepada Pengguna
```

---

## 2. Penyelarasan View Coverage Manifest terhadap 8 Tahap Pemodelan

Untuk memastikan cakupan visual tidak hanya mengandalkan 1 sudut pandang 3D isometrik, daftar view yang wajib diambil diselaraskan secara spesifik dengan tahap pemodelan yang sedang aktif:

| Tahap Pemodelan | Sudut Pandang Wajib dalam Manifest | Fokus Inspeksi Arsitektural & Struktural |
| :--- | :--- | :--- |
| **Stage 1: Datum & Structure** | 1. Denah Kolom/Grid Struktur<br>2. Potongan Melintang Struktur Portal<br>3. 3D Isometrik Rangka Baja/Beton | Tumpuan menerus kolom (tanpa kolom melayang), hubungan tumpuan balok, elevasi pelat lantai donat bebas Z-fighting. |
| **Stage 2: Enclosure** | 1. Tampak Eksterior 4 Sisi (Depan, Belakang, Kiri, Kanan)<br>2. Denah Atap (*Roof Plan*)<br>3. Potongan Atap & Talang | Selubung perimeter tertutup rapat, kemiringan atap ($\tan(\theta \times \pi / 180)$), overhang teritisan, ketiadaan celah dinding. |
| **Stage 3: Openings** | 1. Denah Dinding Host (Bukaan Pintu & Swing)<br>2. Tampak Depan Fasad Bukaan<br>3. 3D Isometrik Terfokus Bukaan Kusen | Bukaan memotong dinding host, ayunan pintu tidak menabrak struktur, elevasi ambang bawah jendela (*sill height*) akurat. |
| **Stage 4: Façade Detailing** | 1. Elevasi Fasad Penuh (Full Height Elevation)<br>2. Close-up Bay Fasad (Façade Rhythm)<br>3. Detail Transisi Podium & Mahkota (Crown) | Irama repetisi mullion/jendela konsisten ($\le \pm 5\text{ mm}$), artikulasi transisi podium-menara-mahkota, spandrel menutup balok. |
| **Stage 5: Spatial & Interior** | 1. Denah Partisi & Penamaan Ruang (*Room Plan*)<br>2. Perspektif Interior Eye-Level<br>3. Potongan Detail Area Basah | Batas ruang tertutup sempurna (*sealed*), elevasi plafon rapi (*headroom* $\ge 2.20\text{ m}$), penurunan lantai toilet terpasang. |
| **Stage 6: MEP Systems** | 1. Denah Distribusi Saluran Udara & Pipa<br>2. Potongan Shaft Utilitas Vertikal<br>3. 3D Isometrik Koordinasi MEP | Jalur ducting dan pipa berada di plenum plafon/shaft, bebas benturan keras (*hard clash*) terhadap balok struktur. |
| **Stage 7: Final QA** | **Full 9-View Building Manifest:**<br>Front, Rear, Left, Right, Roof Plan, Floor Plan, Cross Section, Exterior 3D, Interior Key | Kepatuhan menyeluruh, perbandingan visual sebelum vs sesudah, zero defect, zero UNCOVERED views. |

---

## 3. Dekomposisi Arsitektur Sistem Tampilan (5 Komponen Modular)

Sistem tampilan AIGRAV membagi tanggung jawab penangkapan gambar ke dalam 5 komponen terisolasi:

```
[ViewCoverageCoordinator] (Menentukan daftar view wajib berdasarkan task scope & manifest)
         │
         ├──> [RevitViewGenerator] (Membuat/menemukan ViewPlan, ViewSection, ElevationMarker, View3D)
         │           │
         ├──> [CameraOrientationEngine] (Kalkulasi Eye, Up, Forward vector & Section Box 3D)
         │           │
         ├──> [ViewCaptureExporter] (Ekspor gambar via Document.ExportImage & uji kualitas multi-dimensi)
         │           │
         └──> [ViewCleanupManager] (Menghapus temporary views/markers milik tool secara aman)
```

### Rincian Tanggung Jawab Komponen:
1. **`ViewCoverageCoordinator`:** Memetakan task scope dan target element ID ke dalam manifest kontrak data. Mengelola evaluasi status: `PASS`, `FAIL`, `UNCOVERED`, `NOT_APPLICABLE`.
2. **`RevitViewGenerator`:** Menghasilkan view native secara terprogram dengan pemetaan metode API yang tepat:
   - **3D Isometric View:** `View3D.CreateIsometric(doc, viewFamilyTypeId)` dengan `ViewFamilyType.ViewFamily == ViewFamily.ThreeDimensional`.
   - **3D Camera Orientation:** `View3D.SetOrientation(new ViewOrientation3D(eye, up, forward))` untuk menyetel arah pandang dan orientasi kamera 3D secara presisi.
   - **Building Section Views:** `ViewSection.CreateSection(doc, viewFamilyTypeId, bbox)` dengan `ViewFamilyType.ViewFamily == ViewFamily.Section`.
   - **Detail Views Khusus:** `ViewSection.CreateDetail(doc, viewFamilyTypeId, bbox)` dengan `ViewFamilyType.ViewFamily == ViewFamily.Detail`.  
     > [!CRITICAL] LARANGAN KLASIFIKASI SALAH UNTUK `CreateDetail()`  
     > `ViewSection.CreateDetail()` **HANYA DIGUNAKAN UNTUK MEMBUAT VIEW DETAIL**. DILARANG KERAS mengklasifikasikan atau menggunakan `CreateDetail()` sebagai generator potongan bangunan (*building section*) maupun tampak fasad (*elevation*)!
   - **Elevation Views:** Pembuatan tampak memerlukan sekuens dua tahap:
     1. *Static Factory Method:* `ElevationMarker marker = ElevationMarker.CreateElevationMarker(doc, viewFamilyTypeId, originXYZ, scale)` untuk membuat elemen penanda tampak (*ElevationMarker*).
     2. *Instance Method:* `ViewSection elevationView = marker.CreateElevation(doc, planViewId, index)` (dengan `index` 0..3 untuk orientasi mata angin) untuk membuat view tampak yang di-host oleh marker tersebut (`ViewFamilyType.ViewFamily == ViewFamily.Elevation`).  
     *(DILARANG memanggil metode statis `ElevationMarker.CreateElevation(...)` karena metode tersebut adalah instance method pada objek marker, bukan static method).*
3. **`CameraOrientationEngine`:** Menghitung sudut kamera 3D, target fokus, batas frustum, dan section box terfokus.
4. **`ViewCaptureExporter`:** Mengekspor citra PNG via `doc.ExportImage()` dan memvalidasi integritas biner pixel.
5. **`ViewCleanupManager`:** Melacak seluruh view/marker sementara berpenanda metadata `AIGRAV_VIEW_OWNER == "AIGRAV_AGENT"` dan membersihkannya secara aman pasca-ekspor.

### 3.1. Spesifikasi Teknis Pembuatan View & Transform Geometri:
- **Prasyarat API & ViewFamilyType:**
  Sebelum memanggil metode pembuatan view, engine wajib melakukan query tipe view yang valid melalui:
  ```csharp
  var vft = new FilteredElementCollector(doc)
      .OfClass(typeof(ViewFamilyType))
      .Cast<ViewFamilyType>()
      .FirstOrDefault(x => x.ViewFamily == targetViewFamily);
  ```
  Jika `vft == null`, engine wajib melemparkan error terstruktur `ERR_VIEW_FAMILY_TYPE_NOT_FOUND` dan memblokir proses tanpa fallback ilegal.
- **Persyaratan Matriks Transform BoundingBoxXYZ:**
  Untuk `ViewSection.CreateSection`, `BoundingBoxXYZ` wajib memiliki transform ortonormal koordinat internal Revit (feet):
  * Vektor basis ortonormal: $\vec{X} \cdot \vec{Y} = 0$, $\vec{Y} \cdot \vec{Z} = 0$, $\vec{X} \cdot \vec{Z} = 0$ dengan magnitudo $|\vec{X}| = |\vec{Y}| = |\vec{Z}| = 1.0$.
  * Determinan matriks transform wajib bernilai $+1.0$ (sistem tangan kanan / *right-handed*).
  * Batas kotak: $\text{Min.X} < \text{Max.X}$, $\text{Min.Y} < \text{Max.Y}$, $\text{Min.Z} < \text{Max.Z}$ dalam satuan kaki (feet).
  * Pelanggaran terhadap kaidah transform melempar exception Revit API `ArgumentException: The bounding box is invalid` (divalidasi melalui `POC-VIEW-01`).
- **Kepemilikan View & Siklus Pembersihan Aman (View Ownership & Cleanup):**
  * Seluruh view, section, dan elevation marker yang dibuat secara terprogram wajib diberi identitas kepemilikan:  
    `AIGRAV_VIEW_OWNER == "AIGRAV_AGENT"`.
  * `ViewCleanupManager` hanya menghapus view yang memiliki identitas tersebut. View buatan pengguna (*user-authored views*) dilindungi mutlak dan tidak boleh diubah crop box maupun visibilitasnya.

> [!CAUTION] PERLINDUNGAN MUTLAK TERHADAP VIEW PENGGUNA
> `ViewCleanupManager` **DILARANG KERAS MENGHAPUS, MEMODIFIKASI CROP BOX, ATAU MENGUBAH VISIBILITAS VIEW ASLI MILIK PENGGUNA** (*User-Authored Views*). Hanya view berpenanda eksplisit AIGRAV yang diizinkan untuk dihapus.

---

## 4. Evaluasi Kualitas Citra Multi-Dimensi (*Multi-Dimensional Image Quality Gate*)

Kriteria kelayakan citra hasil ekspor **TIDAK MENGANDALKAN** batasan kaku ukuran file $> 20\text{ KB}$ (karena citra denah 2D sederhana yang valid dapat berukuran 15 KB, sementara citra abu-abu kosong dengan noise kompresi dapat berukuran 50 KB).

Validasi citra wajib memenuhi **4 Parameter Kualitas Multi-Dimensi**:
1. **Validasi Biner & Format:** Header berkas wajib berupa tanda tangan PNG valid (`0x89 0x50 0x4E 0x47 0x0D 0x0A 0x1A 0x0A`) dan bitmap dapat didekompresi sempurna.
2. **Dimensi Resolusi Minimal:** Lebar $\ge 1280\text{ piksel}$, Tinggi $\ge 720\text{ piksel}$.
3. **Pendeteksian Citra Kosong/Solid (Blank/Black/White Frame Detector):** Citra ditolak jika varians warna piksel mendekati nol (layar 100% putih, 100% hitam, atau solid gray tanpa geometri).
4. **Validasi Keterlihatan Elemen Target (Target Element Visibility Check):** Elemen yang baru dimodelkan wajib terbukti berada di dalam batas frustum/crop box kamera dan tidak terhalang (*occluded*) total oleh geometri lain.

---

## 5. Protokol Cakupan Tampilan Relevan (*Applicable View Coverage Protocol*)

Untuk memastikan evaluasi visual berjalan adil dan realistis tanpa mengorbankan ketelitian inspeksi, sistem menerapkan protokol cakupan berbasis status tiga keadaan eksplisit:

```
                  EVALUASI SETIAP SUDUT PANDANG DALAM MANIFEST
                                       │
         ┌─────────────────────────────┼─────────────────────────────┐
         │                             │                             │
         ▼                             ▼                             ▼
   [NOT_APPLICABLE]                 [PASS]                        [FAIL]
  View tidak relevan          View relevan berhasil        View relevan gagal
  dengan tipologi/scope.      diekspor, target visible,    diekspor, buram/blank,
  Wajib alasan tertulis.      lulus uji kualitas visual.   atau terdeteksi cacat.
                                                                     │
                                                                     ▼
                                                              [UNCOVERED]
                                                           (Kamera terhalang
                                                            / view tak ada)
```

### 5.1. Definisi Tiga Keadaan Status Tampilan:
1. **`NOT_APPLICABLE`:**  
   Sudut pandang berada di luar lingkup tugas atau tidak relevan secara fisik dengan tipologi bangunan (misal: *Crown/Spire Elevation* pada Hunian Type 36, *Interior Key View* pada tugas grading tanah/substructure).  
   *Syarat Wajib:* Manifest wajib mencatat parameter `applicability_reason` secara eksplisit (misal: `"NOT_APPLICABLE: Low-rise residential single-story has no architectural tower crown"`).
2. **`PASS`:**  
   Sudut pandang relevan (*applicable*), citra berhasil diekspor, seluruh elemen target berada di dalam batas frustum/crop box kamera dan tidak terhalang (*unoccluded*), serta memenuhi seluruh kriteria kualitas biner dan estetika.
3. **`FAIL`:**  
   Sudut pandang relevan, namun citra gagal diekspor, berukuran kosong/korup, atau terdeteksi cacat visual arsitektural (misal: kusen melayang, material buram, Z-fighting).
   * **Sub-status `UNCOVERED`:** Merupakan bagian dari `FAIL` di mana kamera tidak dapat memposisikan diri, view tidak tersedia di dokumen, atau elemen target terhalang total oleh geometri luar.

### 5.2. Invarian Mutlak Cakupan Tampilan (*Mandatory Applicable Inspection Invariant*):
> [!CRITICAL] SETIAP VIEW RELEVAN WAJIB DIPERIKSA & WAJIB PASS
> 1. Sistem **DILARANG MENGABAIKAN** satu pun sudut pandang yang berstatus relevan (*applicable*).
> 2. Seluruh sudut pandang relevan **WAJIB DIINSPEKSI DAN WAJIB MENGHASILKAN STATUS `PASS`**.
> 3. Jika ada satu saja sudut pandang relevan yang berstatus `FAIL` atau `UNCOVERED`, tugas **DIBLOKIR TOTAL DARI PENGESAHAN SELESAI** (`BLOCKED_INCOMPLETE_VISUAL_COVERAGE`).

### 5.3. Pembatasan Siklus Perbaikan Otomatis (Maksimal 2 Iterasi):
Siklus perbaikan otomatis (*Automatic Repair Loop*) dibatasi maksimal 2 iterasi (*Fail-Safe Stop*). Jika setelah 2 kali perbaikan citra yang terdampak masih menghasilkan status `FAIL` atau `UNCOVERED`, sistem menghentikan loop perbaikan dan menerbitkan laporan eskalasi teknis kepada Pengguna.

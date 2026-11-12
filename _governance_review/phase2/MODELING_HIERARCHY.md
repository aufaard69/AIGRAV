# AIGRAV Modeling Hierarchy & Discipline Taxonomy (Final Governance Specification)

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan hierarki pemodelan arsitektural berbasis dependensi 8-tahap (*Stage 0 s/d Stage 7*), toleransi fasad berbasis desain dan terkonfigurasi (*design-driven configurable tolerances*), validasi tinggi bebas berbasis tipologi dan fungsi ruang (*typology-aware clearance matrix*), pembedaan tegas sambungan BIM sah vs interseksi tak disengaja, pembatasan syarat podium-tower-crown khusus gedung tinggi, serta seleksi tahap dinamis.

---

## 1. Landasan Filosofi: Kematangan BIM & Kualitas Arsitektur Berkelanjutan

AIGRAV beroperasi sebagai **sistem pemodelan BIM disiplin (*disciplined BIM modeling system*)** di mana pemodelan fisik selalu diuji kelayakannya terhadap kaidah teknik sipil, konstruktibilitas, fungsi ruang, estetika arsitektur, dan konteks tipologi bangunan.

### Prinsip Fundamental:
1. **Geometric Completion $\neq$ Architectural Completion:**  
   Keberhasilan eksekusi API atau penerbitan `ElementId` oleh database Revit **BUKAN BUKTI** bahwa elemen telah selesai atau dapat diterima. Setiap elemen wajib diuji kelayakannya terhadap logika struktural (*load path*), ergonomi manusia (*headroom*), irama fasad (*façade rhythm*), artikulasi transisi bangunan, dan konstruktibilitas fisik.
2. **Kualitas Acuan Visual (Visual Quality Benchmark):**  
   Standar kehalusan arsitektur AIGRAV mengacu pada tingkat presisi tinggi (sebagaimana dicontohkan pada studi pemodelan *One Detroit Center* untuk tipologi menara bertingkat banyak): irama bukaan fasad yang konsisten, artikulasi detail spandrel/mullion, proporsi vertikal yang anggun, serta artikulasi transisi yang jelas antar-zona bangunan.
3. **Kontekstual Tipologi Bangunan (Contextual Typology Awareness):**  
   Standar arsitektur tidak diterapkan secara kaku dan membabi-buta (*no blanket assumptions*). Persyaratan estetika, tinggi bebas, toleransi fabrikasi, dan pembagian zona fasad **WAJIB MENYESUAIKAN DIRI** dengan fungsi bangunan yang sedang dimodelkan (Hunian Sederhana, Perkantoran Menengah, Menara Tinggi, atau Bangunan Industri).
4. **Preservasi Kategori Semantik Mutlak:**  
   Setiap elemen bangunan fisik wajib menggunakan `BuiltInCategory` native yang mencerminkan fungsi aslinya. Penggunaan `OST_GenericModel` atau `DirectShape` sebagai jalan pintas otomatis atas kegagalan pemodelan native **DILARANG KERAS**.

---

## 2. Hierarki Pemodelan Arsitektur Berbasis Dependensi (Stage 0 s/d Stage 7)

Pemodelan bangunan utuh mengikuti urutan dependensi logis 8 tahap berikut. Setiap tahap bergantung pada validasi tahap sebelumnya (*dependency-aware execution*):

```
[STAGE 0: Design Interpretation & Spatial Blueprint]
   │  ├── Analisis fungsi bangunan, program ruang, dan tipologi (Residential/Office/Industrial/High-Rise)
   │  ├── Penentuan orientasi massa, modul struktur, konfigurasi toleransi, dan matriks tinggi bebas
   │  └── Penyusunan Dependency Graph & identifikasi Family terverifikasi
   ▼
[STAGE 1: Datum & Primary Structure]
   │  ├── Level & Grid Matrix (Sumbu X/Y/Z)
   │  ├── Substructure: Pondasi (Bored Pile/Footplate), Pile Cap, Sloof/Tie Beam
   │  └── Superstructure: Kolom Struktur (OST_StructuralColumns), Balok (OST_StructuralFraming), Pelat Struktural
   ▼
[STAGE 2: Architectural Enclosure]
   │  ├── Dinding Perimeter Eksterior Komposit (Bata/Precast/Insulasi)
   │  ├── Sistem Selubung Fasad & Curtain Wall (Mullion, Spandrel, Transom)
   │  └── Struktur Selubung Atap Native (Footprint Roof, Geometri Kemiringan, Nok, Talang)
   ▼
[STAGE 3: Openings & Building Components]
   │  ├── Pintu Arsitektural Ber-Host Dinding (OST_Doors)
   │  ├── Jendela Arsitektural Ber-Host Dinding (OST_Windows) dengan Sill Height Akurat
   │  └── Komponen Sirkulasi Vertikal: Tangga Parametrik, Bordes, Void Bukaan Lantai
   ▼
[STAGE 4: Architectural Detailing & Façade Systems]
   │  ├── Penegakan Irama Fasad dengan Toleransi Terkonfigurasi (Design-Driven Façade Rhythm)
   │  ├── Profil Arsitektural: Cornice, Pilaster, Louver, Canopy, Parapet, Coping
   │  └── Transisi Vertikal Kondisional: Artikulasi Podium-Tower-Crown (HANYA untuk High-Rise Tower)
   ▼
[STAGE 5: Spatial & Interior Modeling]
   │  ├── Dinding Partisi Interior & Penurunan Lantai Area Basah
   │  ├── Penempatan & Komputasi Batas Ruang (OST_Rooms) dengan Luasan dan Volume Tertutup
   │  └── Plafon Arsitektural (Ceilings), Sanitair (Plumbing Fixtures), dan Furnitur Utama
   ▼
[STAGE 6: MEP & Building Systems]
   │  ├── Distribusi Vertikal: Shaft Utilitas, Void MEP
   │  ├── Saluran Udara HVAC (OST_DuctCurves, Diffusers, FCU/AHU)
   │  └── Perpipaan Plumbing (OST_PipeCurves) & Jalur Elektrikal Utama (Cable Trays)
   ▼
[STAGE 7: Final BIM Quality Assurance]
   │  ├── 4-Layer Quality Gate Verification (Semantic, Geometric, Architectural, Live Visual)
   │  ├── Deteksi Benturan Spasial (Clash) vs Pembedaan Sambungan Sah (Intentional Joins)
   │  └── Audit Standar Kuantitas QTO (Schedule 5 Kolom Pelat, Schedule 7 Kolom Voute, Grand Totals)
```

### Aturan Seleksi Tahap Dinamis (*Dynamic Stage Selection*):
- Tahap pemodelan **wajib dipilih sesuai dengan kebutuhan aktual tugas dan spesifikasi proyek**.
- Sistem **DILARANG MEMAKSAKAN** tahap yang tidak relevan pada tugas modifikasi parsial.  
  *Contoh Kasus:*
  * Jika tugas pengguna adalah *"Tambahkan partisi dan pintu di Lantai 2"*, agen hanya mengaktifkan **Stage 3 & Stage 5**, memvalidasi dependensi dinding host eksisting dari Stage 1 & 2 tanpa mengeksekusi ulang Stage 1, 4, atau 6.
  * Jika tugas adalah *"Modelkan selubung fasad kantor 20 lantai"*, agen mengaktifkan **Stage 0, 1, 2, 3, 4, dan 7**, sementara Stage 5 (Interior fit-out) dan Stage 6 (MEP detail) dapat ditandai `SKIPPED_NOT_IN_SCOPE`.

---

## 3. Standar Kualitas Fasad: Toleransi Terkonfigurasi Berbasis Desain (*Design-Driven Configurable Tolerances*)

Sistem menghapus asumsi batas kaku universal $\pm 5\text{ mm}$ untuk seluruh fasad. Toleransi irama fasad (*Façade Rhythm & Bay Spacing*) wajib bersifat **terkonfigurasi dan diturunkan dari spesifikasi sistem fasad yang digunakan**:

```yaml
# Skema Konfigurasi Toleransi Fasad (FacadeRhythmToleranceConfig)
SystemType: enum (UnitizedCurtainWall | StickCurtainWall | PrecastPanel | PunchedMasonry | CustomParametric)
TargetBaySpacingMm: double (Jarak modul as desain, misal 1500.0)
TolerancePositiveMm: double (Batas deviasi positif yang diizinkan)
ToleranceNegativeMm: double (Batas deviasi negatif yang diizinkan)
AllowIntentionalVariableRhythm: bool (True jika fasad mengadopsi pola dinamis/parametrik bergradasi)
```

### Tabel Default Screening Nilai Toleransi Berdasarkan Sistem Fasad:
| Tipe Sistem Fasad | Karakteristik Konstruksi | Default Screening Tolerance | Rujukan Standar Industri & Status |
| :--- | :--- | :---: | :--- |
| **Unitized Curtain Wall** | Panel kaca fabrikasi pabrik presisi tinggi | $\pm 2.0\text{--}3.0\text{ mm}$ | CSI MasterFormat 08 44 00 / Konfigurasi Proyek (`ProjectConfig.FacadeTolerance`). *Catatan: AAMA TIR-A11-15 mengatur lendutan beban angin L/175 s/d L/240, bukan tabel toleransi instalasi.* |
| **Stick-Built Curtain Wall** | Perakitan mullion/transom di lapangan | $\pm 5.0\text{ mm}$ | CSI MasterFormat 08 44 00 / Konfigurasi Proyek (`ProjectConfig.FacadeTolerance`). *Catatan: AAMA 501 adalah metode uji penetrasi air/udara, bukan tabel toleransi instalasi.* |
| **Precast Concrete Cladding** | Panel beton pracetak dengan celah sealant | $\pm 6.0\text{--}10.0\text{ mm}$ | PCI 135-25 (*Tolerance Manual for Precast and Prestressed Concrete Construction*, 2025; MNL 135-00 sebagai acuan historis) |
| **Punched Masonry Openings** | Jendela tertanam pasangan bata merah/hebel | $\pm 10.0\text{--}15.0\text{ mm}$ | ACI 530.1 / TMS 602 Article 3.3 F |
| **Custom / Parametric Façade** | Pola bergradasi atau rasio Fibonacci | Dinamis sesuai kurva desain | Mathematical Design Script Specification |

> [!NOTE] Penegakan Toleransi
> Jika proyek memiliki dokumen spesifikasi teknis (*Employer Information Requirements / BIM Execution Plan*), nilai dari dokumen proyek tersebut **MENJADI OTORITAS TERTINGGI** dan menimpa (*override*) nilai default screening di atas. Angka numerik toleransi ereksi lapangan yang tidak bersumber dari klausul toleransi kode baku diklasifikasikan sebagai parameter yang wajib dikonfigurasi per proyek.

---

## 4. Matriks Validasi Tinggi Bebas Berbasis Tipologi & Fungsi Ruang (*Typology & Room-Function Clearance Matrix*)

Validasi tinggi bebas (*headroom & operational clearance*) tidak boleh menggunakan angka seragam. Tinggi bebas wajib dievaluasi berdasarkan kombinasi **Tipologi Bangunan** dan **Fungsi Ruang Spesifik**:

```
[Input Elemen / Ruang] ──> [Identifikasi Tipologi] ──> [Identifikasi Fungsi Ruang] ──> [Query Matriks Batas] ──> [Evaluasi Kelayakan]
```

| Tipologi Bangunan | Zona / Fungsi Ruang Spesifik | Tinggi Bebas Bersih Minimum (*Clear Headroom*) | Dasar Pertimbangan Teknis & Standar |
| :--- | :--- | :---: | :--- |
| **Hunian (Residential Type 36 / Rumah Tinggal)** | Ruang Tamu, Ruang Tidur, Ruang Keluarga | $\ge 2.40\text{--}2.60\text{ m}$ (toleransi lokal di bawah balok $\ge 2.20\text{ m}$) | Persyaratan ruang hunian tropis (Permen PU No. 29/PRT/M/2006 Bagian Ketiga `UNVERIFIED_STANDARD_REFERENCE` [angka numerik eksplisit memerlukan konfirmasi Perda/spesifikasi proyek]; IBC 2024 Section 1208.2 [rujukan komparatif]). *Catatan: SNI 03-6572 mengatur ventilasi/AC, bukan tinggi plafon.* |
| | Kamar Mandi / Toilet / Dapur / Selasar | $\ge 2.10\text{--}2.20\text{ m}$ | Area servis dengan penurunan lantai dan jalur pipa (Permen PU 29/2006 `UNVERIFIED_STANDARD_REFERENCE`; IBC 2024 Section 1208.2). |
| | Garasi / Carport Luar | $\ge 2.20\text{ m}$ | Ketinggian kendaraan roda empat penumpang. |
| **Gedung Perkantoran (Commercial Office)** | Area Kerja Utama (*Open Plan Office*) | $\ge 2.70\text{--}3.00\text{ m}$ | Distribusi pencahayaan merata dan ruang plenum ducting HVAC. |
| | Lobi Utama & Resepsionis Lantai Dasar | $\ge 3.60\text{--}6.00\text{ m}$ | Skala monumental arsitektur dan artikulasi entrance. |
| | Koridor Utama & Sirkulasi Evakuasi | $\ge 2.40\text{ m}$ | Standar keselamatan evakuasi darurat (NFPA 101). |
| | Toilet Umum & Koridor Servis | $\ge 2.20\text{--}2.40\text{ m}$ | Plenum kabel tray dan pipa drainase (IBC 2024 Section 1208.2). |
| | Tangga Darurat & Bordes | $\ge 2.032\text{ m}$ (vertikal dari nosing) | Batas tinggi bebas kepala pengguna tangga (IBC 2024 Section 1011.3). |
| **Bangunan Industri (Warehouse / Factory)** | Area Produksi / Gudang Utama | $\ge 4.50\text{--}8.00\text{ m}$ (hingga dasar kuda-kuda) | Operasional armada forklift dan sistem racking vertikal. |
| | Loading Dock & Pintu Akses Truk | $\ge 4.20\text{--}4.50\text{ m}$ | Tinggi bebas kendaraan kontainer/truk logistik. |
| | Kantor Pengelola Mezzanine | $\ge 2.60\text{ m}$ | Ergonomi ruang kerja pengawas pabrik. |
| **Semua Tipologi** | Basement / Parkir Kendaraan Ringan | $\ge 2.10\text{--}2.40\text{ m}$ di bawah balok/sprinkler | Jalur aman mobil penumpang dan instalasi pipa gantung. |
| | Ruang Mesin Utama (MEP Plant / Chiller) | $\ge 3.60\text{--}4.50\text{ m}$ | Ruang pemeliharaan unit chiller, trafo, dan pompa. |

---

## 5. Pembedaan Sambungan BIM Sah vs Interseksi Geometri Tak Disengaja (*Intentional Joins vs Unintended Intersections*)

Kebijakan arsitektur AIGRAV **TIDAK MENGGUNAKAN** larangan buta *"Zero Solid Overlap"*. Sebaliknya, sistem secara tegas membedakan antara **Sambungan BIM Sah (*Intentional BIM Connections & Monolithic Joins*)** dengan **Interseksi Tak Disengaja (*Unintended Geometric Clashes*)**:

### 5.1. Sambungan BIM Sah yang DISETUJUI (*Intentional BIM Connections*):
1. **Monolithic Concrete Joins:** Pertemuan kolom dan balok beton cor di tempat (*cast-in-place*), di mana volume di-join via `JoinGeometryUtils.JoinGeometry()` sehingga satu elemen memotong yang lain secara bersih berdasarkan prioritas material (*Concrete Core Priority*) tanpa duplikasi volume beton.
2. **Compound Wall-Floor Bearing:** Dinding yang menumpu di atas pelat lantai dengan lapisan finishing lantai yang membungkus rapi.
3. **Wall-Roof Attachment:** Dinding yang di-attach ke bagian bawah pelat/atap miring (*roof soffit*).
4. **Embedded Structural Steel Hardware:** Baseplate kolom yang tertanam pada mortar grouting di atas pedestal beton, atau angkur baja yang tertanam pada footplate.

### 5.2. Interseksi Tak Disengaja yang DILARANG KERAS (*Unintended Intersections / Defects*):
1. **Coplanar Overlapping Slabs:** Dua atau lebih pelat lantai pejal yang diletakkan pada elevasi bidang horizontal yang sama tanpa pembagian opening void (memicu Z-fighting visual flickers dan pelipatgandaan volume beton di QTO). *Mandat Solusi: Gunakan Multi-Loop CurveLoop Donut Slabs.*
2. **Duplicate Collinear Walls:** Dua dinding yang dimodelkan tumpang tindih di garis as yang sama (*double walling*).
3. **Hard Clashes MEP/Furniture:** Pipa atau ducting menembus balok struktural tanpa opening sleeve resmi; atau furnitur menembus dinding/kolom.

---

## 6. Persyaratan Transisi Fasad Vertikal Berbasis Tipologi (*Conditional Façade Articulation*)

Evaluasi artikulasi transisi vertikal (*Stage 4*) **BERSIFAT KONDISIONAL BERDASARKAN TIPOLOGI BANGUNAN**:

```
                       EVALUASI TRANSISI FASAD VERTIKAL
                                      │
         ┌────────────────────────────┼────────────────────────────┐
         │                            │                            │
         ▼                            ▼                            ▼
  [HIGH-RISE TOWER]         [MID-RISE COMMERCIAL]         [RESIDENTIAL TYPE 36]
  Wajib Artikulasi Tripartit: Wajib Artikulasi Dasar &     TIDAK BERLAKU Podium-Crown.
  • Base / Podium (Lt 1-4)    Mahkota Atap:                Fokus Evaluasi:
  • Shaft / Tower (Lt 5-20)   • Lobi Transparan Lt Dasar   • Teritisan Atap (Overhang)
  • Capital / Crown (Puncak)  • Parapet Screen Mesin Atap  • Elevasi Lantai Plinth
```

1. **Gedung Tinggi (*High-Rise Tower*, Default Screening Parameter > 8 Lantai):**  
   > [!NOTE] Parameter Konfigurasi Ambang Gedung Tinggi vs Klasifikasi Yuridis
   > Nilai ambang batas lantai (`ProjectConfig.HighRiseThresholdFloors = 8`) adalah klasifikasi pengujian dan evaluasi arsitektural internal yang terkonfigurasi (*internal configurable test parameter*), terpisah secara tegas dari klasifikasi yuridis hukum Indonesia pada PP No. 16 Tahun 2021, Pasal 9 ayat (6), jo. Lampiran bagian 5 — Ketinggian Bangunan Gedung (Pasal 9 ayat (6) menetapkan kelompok klasifikasi tingkat tinggi dan Lampiran bagian 5 menetapkan ambang batas eksak jumlah lantai *lebih dari 8 lantai* / `> 8 lantai`). Adapun IBC 2024 mendefinisikan *high-rise building* pada Section 202 (Definitions) sebagai bangunan dengan lantai yang dihuni berada $> 75\text{ ft} \approx 22.86\text{ m}$ di atas akses pemadam kebakaran terendah, dengan ruang lingkup aplikasi pada Section 403.1 (Applicability), dan diperlakukan sebagai rujukan komparatif/adopsi proyek internasional. Nilai ambang arsitektural ini dapat dikonfigurasi ulang per proyek sesuai dokumen spesifikasi desain pengguna.
   
   Jika terklasifikasi sebagai gedung tinggi, wajib memiliki artikulasi tripartit:
   - **Podium/Base:** Skala pejalan kaki, artikulasi entrance megah, kanopi.
   - **Shaft/Tower:** Repetisi irama modul jendela tipikal yang teratur.
   - **Crown/Capital:** Penutup puncak gedung (parapet ekspresif, mechanical penthouse screen, atau spire) yang menyembunyikan mesin chiller dan gondola BMU dari tampak visual jalan.
2. **Gedung Komersial Menengah (Mid-Rise Commercial 3–7 Lantai):**  
   Tidak memerlukan podium masif. Wajib memiliki artikulasi lobi lantai dasar yang transparan dan penutup parapet atap (*roof screen*) yang rapi.
3. **Hunian Sederhana / Rendah (Residential Type 36 / Low-Rise 1–2 Lantai):**  
   **SYARAT PODIUM-TOWER-CROWN TIDAK BERLAKU (NOT APPLICABLE)**. Evaluasi difokuskan pada:
   - Hubungan lantai plinth terhadap muka tanah luar ($+0.20\text{--}+0.30\text{ m}$).
   - Teritisan atap pelana/limasan (overhang $\ge 600\text{--}800\text{ mm}$) untuk proteksi hujan.
   - Proporsi kanopi teras depan yang ramah skala hunian.
4. **Bangunan Industri (Warehouse / Factory):**  
   **SYARAT PODIUM-TOWER-CROWN TIDAK BERLAKU**. Evaluasi difokuskan pada transisi dinding bata/batako bawah (plinth wall $\approx 1.0\text{--}2.0\text{ m}$) ke cladding dinding metal gelombang atas (*metal siding*).

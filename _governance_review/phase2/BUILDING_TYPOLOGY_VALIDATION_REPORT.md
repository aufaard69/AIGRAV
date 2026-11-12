# AIGRAV Building Typology Validation Report (Evidence-Based Revision)

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Melakukan evaluasi berbasis bukti (*evidence-based evaluation*) terhadap tata kelola pemodelan arsitektural AIGRAV dan matriks pengujian penerimaan (TC-01 s/d TC-45) terhadap **tiga tipologi bangunan yang berbeda secara fundamental**: (1) Hunian Sederhana Rumah Tipe 36, (2) Gedung Perkantoran Menengah (*Mid-Rise Commercial Office*), dan (3) Menara Tinggi (*High-Rise Tower*). Memastikan pemisahan tegas antara tipologi bangunan, input desain pengguna, standar proyek, dan asumsi default.

---

## 1. Landasan Pemisahan Otoritas Desain (*Separation of Design Authorities*)

Untuk mencegah agen memaksakan aturan kaku (*rigid bias*), tata kelola AIGRAV secara tegas memisahkan 4 tingkat otoritas data:

```
[TINGKAT 1: USER DESIGN INPUT (Otoritas Tertinggi)]
  └── Dokumen gambar kerja pengguna (CAD/PDF/BIM), brief instruksi prompt, dan parameter kontrak.
  └── DILARANG KERAS dioverride oleh asumsi default agen!
         │
         ▼
[TINGKAT 2: PROJECT & REGULATORY STANDARDS (Standar Proyek & Regulasi)]
  └── Standar teknis yang diadopsi proyek (SNI, IBC, AAMA, PCI, ACI, EIR/BEP).
  └── Mengatur batasan keselamatan, struktural, sanitasi, dan beban angin/gempa.
         │
         ▼
[TINGKAT 3: BUILDING TYPOLOGY CONTEXT (Konteks Tipologi Bangunan)]
  └── Klasifikasi fungsi (Hunian, Kantor Komersial, Industri, Menara Tinggi).
  └── Menentukan matriks batas toleransi yang relevan dan aturan transisi arsitektural.
         │
         ▼
[TINGKAT 4: DEFAULT SCREENING ASSUMPTIONS (Asumsi Default Agen)]
  └── Contoh dimensi (misal: modul 6x6 m pada Tipe 36) atau batas screening (misal: 8 lantai).
  └── HANYA DIGUNAKAN sebagai nilai awal fleksibel jika Tingkat 1 & 2 tidak mendefinisikannya.
```

---

## 2. Inventaris Rujukan Standar Industri & Status Verifikasi (*Standards Reference Register*)

Setiap standar yang dirujuk dalam tata kelola AIGRAV diaudit dan diverifikasi edisi resmi, klausul pasal, lingkup aplikasi riil, sumber otoritatif, dan status keabsahannya:

| Rujukan Standar Resmi | Edisi / Tahun | Klausul / Pasal Spesifik | Persyaratan Aktual & Lingkup Berlaku | Lokasi Sumber Resmi / Otoritatif | Status Verifikasi Bukti |
| :--- | :---: | :--- | :--- | :--- | :---: |
| **IBC (International Building Code)** | Edisi 2024 | **Section 1208.2** (Minimum Ceiling Heights) | Mengatur tinggi plafon minimum ruang berpenghuni (*habitable spaces* $\ge 7\text{ ft } 6\text{ in} \approx 2.286\text{ m}$); kamar mandi, toilet, dapur, dan ruang simpan ($\ge 7\text{ ft } 0\text{ in} \approx 2.134\text{ m}$). *Catatan: Section 1207.2 adalah Enhanced Classroom Acoustics (akustik ruang kelas), bukan tinggi plafon. IBC berlaku sebagai rujukan komparatif / adopsi proyek, bukan regulasi wajib nasional Indonesia.* | International Code Council (ICC) Digital Codes | `STANDARDS_VERIFIED` (Rujukan Adopsi Proyek / Komparatif) |
| **IBC (International Building Code)** | Edisi 2024 | **Section 1011.3** (Headroom) | Mengatur ruang bebas vertikal minimum pada tangga darurat ($\ge 80\text{ in} \approx 2.032\text{ m}$) diukur dari garis nosing pijakan ke permukaan plafon/balok di atasnya. | ICC Digital Codes | `STANDARDS_VERIFIED` (Rujukan Adopsi Proyek / Komparatif) |
| **IBC (International Building Code)** | Edisi 2024 | **Chapter 14, Section 1402** (Performance Requirements) | Mengatur kriteria kinerja dinding luar (*Exterior Walls*): proteksi cuaca, penetrasi air, ketahanan beban angin rencana, dan integritas struktural. *Bukan tabel toleransi instalasi/ereksi fasad.* | ICC Digital Codes | `STANDARDS_VERIFIED` (Rujukan Adopsi Proyek / Komparatif) |
| **IBC (International Building Code)** | Edisi 2024 | **Section 202 & Section 403.1** (High-Rise Buildings) | **Section 202** mendefinisikan gedung tinggi (*High-Rise Building*): bangunan dengan lantai berpenghuni tertinggi $> 75\text{ ft}$ ($22.86\text{ m}$) di atas akses terendah kendaraan pemadam kebakaran. **Section 403.1** mengatur lingkup keberlakuan (*Applicability*). *Bukan regulasi wajib nasional Indonesia.* | ICC Digital Codes | `STANDARDS_VERIFIED` (Rujukan Adopsi Proyek / Komparatif) |
| **PP No. 16 Tahun 2021** | Edisi 2021 | **Pasal 9 ayat (6), jo. Lampiran bagian 5 — Ketinggian Bangunan Gedung** | Klasifikasi hukum yuridis bangunan gedung bertingkat di Indonesia: Pasal 9 ayat (6) menetapkan kelompok klasifikasi tingkat tinggi (*"Bangunan Gedung bertingkat tinggi sebagaimana dimaksud pada ayat (1) huruf c merupakan Bangunan Gedung yang memiliki jumlah lantai lebih dari 8 lantai"*), jo. Lampiran bagian 5 yang mengatur ambang batas jumlah lantai eksak (*Ketinggian Bangunan Gedung*). Ketentuan hukum murni $> 8$ lantai dipisahkan dari kriteria ketinggian meter. | Lembaran Negara Republik Indonesia Tahun 2021 No. 26 | `STANDARDS_VERIFIED` (Regulasi Wajib Nasional RI) |
| **Permen PU No. 29/PRT/M/2006** | Edisi 2006 | **Pedoman Teknis Bangunan Gedung** | Memberikan pedoman umum kenyamanan spasial dan antropometri bangunan gedung. Klausul spesifik dan tabel numerik tinggi bebas ruang hunian ($2.40\text{--}2.80\text{ m}$) tidak memiliki pasal/tabel numerik tunggal dalam dokumen ini (diatur lebih rinci pada Perda Bangunan Gedung daerah / spesifikasi proyek). | Departemen Pekerjaan Umum RI | `UNVERIFIED_STANDARD_REFERENCE` (Memerlukan Rujukan Perda / Spesifikasi Proyek) |
| **SNI 03-1729** | Edisi 2020 | **Bab B & Spesifikasi AISC 360-16** | Desain, fabrikasi, dan ereksi komponen struktur baja gedung, toleransi ereksi profil portal dan sambungan baut/las. | BSN Indonesia | `STANDARDS_VERIFIED` |
| **SNI 2847** | Edisi 2019 | **Pasal 20.6.1.3.1** (Tabel 20.6.1.3.1) | Mengatur tebal selimut beton yang ditentukan (*Specified Concrete Cover*): 75 mm untuk kontak tanah permanen, 40-50 mm untuk terpapar cuaca, 20-40 mm untuk pelat/balok/kolom terlindung. | BSN Indonesia / ACI 318-19 | `STANDARDS_VERIFIED` |
| **SNI 2847** | Edisi 2019 | **Pasal 26.6.2** | Mengatur toleransi fabrikasi dan penempatan tulangan baja di lapangan (*Reinforcement Placement & Tolerances*): toleransi tinggi efektif $d$ dan selimut beton minimum saat perakitan. | BSN Indonesia / ACI 318-19 | `STANDARDS_VERIFIED` |
| **SNI 03-6572** | Edisi 2001 | **Pasal 4 & Pasal 5** | Mengatur tata cara perancangan sistem ventilasi dan pengkondisian udara pada bangunan gedung (laju pertukaran udara, kenyamanan termal). *Dilarang digunakan sebagai rujukan generik minimum headroom.* | BSN Indonesia | `STANDARDS_VERIFIED` |
| **AAMA TIR-A11-15** | Edisi 2015 | **Section 4 & Section 5** | Mengatur lendutan maksimum yang diizinkan (*allowable deflection*) untuk komponen rangka dinding luar di bawah beban angin rencana (desain struktural lentur $L/175$ s/d $L/240$). *Bukan tabel toleransi instalasi/ereksi.* | FGIA (Fenestration & Glazing Industry Alliance) | `STANDARDS_VERIFIED` |
| **AAMA 501 Series** | Edisi 2015 | **AAMA 501.1 & 501.2** | Metode uji laboratorium dan lapangan untuk penetrasi air, kebocoran udara, dan performa struktural dinamis dinding luar. *Tidak mengatur toleransi dimensi ereksi fasad universal.* | FGIA | `STANDARDS_VERIFIED` |
| **Toleransi Ereksi Fasad Lapangan** | Spesifikasi Proyek | CSI MasterFormat 08 44 00 / Kontrak Desain | Toleransi deviasi instalasi stick-built ($\pm 5.0\text{ mm}$) dan unitized ($\pm 2.0\text{--}3.0\text{ mm}$). Angka ini wajib dikonfigurasi melalui `ProjectConfig.FacadeTolerance`, bukan diasumsikan tabel kode universal. | Spesifikasi Teknis Proyek Arsitektur | `UNVERIFIED_STANDARD_REFERENCE` (Wajib Konfigurasi Desain Proyek) |
| **PCI 135-25** | Edisi 2025 | **Tolerance Manual for Precast Construction** | Standar toleransi terkini yang menggantikan (*supersedes*) PCI MNL 135-00. Mengatur deviasi ereksi panel beton pracetak terhadap sumbu grid dan sambungan antar-panel ($\pm 6\text{--}10\text{ mm}$). MNL 135-00 hanya dipertahankan sebagai latar belakang historis. | Precast/Prestressed Concrete Institute (PCI) | `STANDARDS_VERIFIED` |
| **ACI 530.1 / TMS 602** | Edisi 2013 | **Article 3.3 F** | Toleransi deviasi bukaan pasangan bata/hebel terhadap garis as gambar ($\pm 1/2\text{ in} \approx \pm 12.7\text{ mm}$). | The Masonry Society / ACI | `STANDARDS_VERIFIED` |

---

## 3. Ambang Batas Bangunan Tinggi: Parameter Konfigurasi Internal

> [!IMPORTANT] PEMISAHAN HUKUM KLASIFIKASI DARI AMBANG BATAS TELAAH ARSITEKTUR
> Sistem AIGRAV memisahkan secara tegas antara **Klasifikasi Yuridis Hukum** dengan **Ambang Batas Telaah Arsitektural Podium-Tower-Crown**:
> - **Klasifikasi Yuridis Indonesia:** Berdasarkan **PP No. 16 Tahun 2021, Pasal 9 ayat (6), jo. Lampiran bagian 5 — Ketinggian Bangunan Gedung**, bangunan gedung bertingkat tinggi secara yuridis diklasifikasikan ke dalam kelompok bertingkat tinggi pada batang tubuh Pasal 9 ayat (6) dengan ambang batas jumlah lantai eksak yang ditetapkan pada Lampiran bagian 5 sebagai bangunan dengan **jumlah lantai lebih dari 8 (delapan) lantai** (`> 8`). Kriteria meter tidak digabungkan sebagai definisi primer karena regulasi mendasarkannya pada ambang jumlah lantai Lampiran bagian 5.
> - **Rujukan Komparatif Internasional:** Berdasarkan **IBC 2024 Section 202** (Definisi) dan **Section 403.1** (Lingkup Keberlakuan), bangunan tinggi diklasifikasikan berdasarkan ketinggian fisik lantai tertinggi yang dihuni $> 75\text{ ft}$ ($22.86\text{ m}$) di atas akses pemadam kebakaran. Ketentuan ini diperlakukan sebagai rujukan komparatif / adopsi proyek, bukan hukum wajib di Indonesia.
> - **Ambang Batas Telaah Arsitektur Terkonfigurasi:** Angka ambang batas (`HighRiseFloorThreshold`) untuk evaluasi artikulasi fasad tripartit (Podium-Tower-Crown) **BUKAN HUKUM MUTLAK**, melainkan parameter konfigurasi arsitektur (`ProjectConfig.HighRiseThresholdFloors`, nilai bawaan pengujian internal = 8). Jika dokumen desain proyek menetapkan gedung 6 lantai mengadopsi mahkota menara, parameter ini dapat disetel ke 6 sesuai instruksi pengguna.

---

## 4. Evaluasi Tipologi 1: Hunian Sederhana Rumah Tipe 36 (*Residential Type 36 Example*)

### 4.1. Pemisahan Desain Pengguna vs Asumsi Default:
- **Status Dimensi $6 \times 6\text{ m}$ (Luas $36\text{ m}^2$):** Hanyalah **CONTOH KONFIGURASI RELEVAN (*Configurable Example*)**, BUKAN persyaratan mutlak. Jika pengguna memberikan gambar denah Tipe 36 dengan ukuran $6 \times 7\text{ m}$ (Tipe 42) atau $5 \times 7.2\text{ m}$, sistem wajib mematuhi geometri pengguna.
- **Konfigurasi Struktur:** Konstruksi dinding bata pemikul beban diperkuat kolom praktis beton ($150 \times 150\text{ mm}$) dan ring balk ($150 \times 150\text{ mm}$) adalah sistem umum perumahan tropis tapak.

### 4.2. Pemetaan Komponen BIM & Semantik Revit:
| Dimensi Evaluasi | Spesifikasi BIM & Parameter Teknis | Kategori Revit & Tipe Resmi |
| :--- | :--- | :--- |
| **Kategori Native Wajib** | Dinding bata, pelat lantai, kolom praktis, sloof, ring balk, atap pelana, pintu, jendela, ruang. | `OST_Walls`, `OST_Floors`, `OST_StructuralColumns`, `OST_StructuralFraming`, `OST_Roofs`, `OST_Doors`, `OST_Windows`, `OST_Rooms`. |
| **Family Types & Simbol** | • Dinding: `Basic Wall - Bata Merah 150mm` (komposit bata 110mm, plesteran 2x15mm, acian 2x5mm).<br>• Kolom: `M_Concrete-Square-Column - KP 150x150 mm`.<br>• Balok/Sloof/RingBalk: `M_Concrete-Rectangular-Beam` (Sloof 150x200 mm, RB 150x150 mm).<br>• Atap: `Basic Roof - Genteng Metal Rangka Baja Ringan`.<br>• Pintu & Jendela: `Single-Flush` & `M_Window-Casement-Double`. |
| **Relasi Hosting** | • Pintu & Jendela: Wajib **Wall-Hosted** pada dinding bata 150mm.<br>• Kolom Praktis: Level-based (Level 1 ke Ring Balk).<br>• Sloof: Menumpu pada pondasi batu kali/footplate.<br>• Lantai: Slab-on-ground dengan join monolitik terhadap sloof.<br>• Atap: Level-based pada elevasi Top of Ring Balk. |
| **Persyaratan Spasial** | • Ruang Terbuka Hijau & Sempadan Bangunan: Mengikuti garis sempadan jalan (GSB) pada gambar tapak pengguna.<br>• **Penurunan Lantai Area Basah:** Pelat KM/WC wajib drop $-30\text{ s/d } -50\text{ mm}$ terhadap lantai utama ($+0.00$).<br>• **Sirkuit Ruang Tertutup:** Seluruh ruangan wajib memiliki batas *room-bounding* tertutup sempurna (`Area > 0`, zero `Not Enclosed`). |
| **Pemeriksaan Kualitas Arsitektur** | • **Headroom:** Ruang utama $\ge 2.60\text{--}2.80\text{ m}$ (toleransi lokal di bawah balok ring $\ge 2.20\text{ m}$ sesuai IBC 1208.2); KM/WC $\ge 2.10\text{ m}$.<br>• **Toleransi Fasad:** Toleransi bukaan pasangan bata $\pm 10\text{--}15\text{ mm}$ (ACI 530.1 / TMS 602).<br>• **Elevasi Plinth:** Lantai utama $+0.20\text{--}+0.30\text{ m}$ di atas permukaan tanah luar untuk proteksi banjir.<br>• **Teritisan Overhang:** Overhang atap $\ge 600\text{--}800\text{ mm}$ untuk proteksi tampias hujan tropis.<br>• **Aturan Transisi Vertikal:** Syarat Podium-Tower-Crown dinilai **`NOT_APPLICABLE`** (karena rumah 1 lantai tidak memiliki mahkota menara). |
| **Keluaran QTO Baku** | 1. Schedule Dinding (`OST_Walls`): Luas permukaan bata (m²) & volume plesteran.<br>2. Schedule Beton Bertulang (`OST_StructuralColumns` & `Framing`): Volume beton sloof, kolom praktis, ring balk (m³).<br>3. Schedule Penutup Lantai (`OST_Floors`): Luas keramik (m²).<br>4. Schedule Kusen Pintu & Jendela (`OST_Doors`, `OST_Windows`): Hitungan unit (Pcs) & dimensi opening. |

---

## 5. Evaluasi Tipologi 2: Gedung Perkantoran Menengah (*Mid-Rise Commercial Office Example*)

### 5.1. Karakteristik Bangunan & Konteks Desain:
- **Fungsi:** Gedung perkantoran komersial 6 lantai dengan 1 lantai basement parkir (Tinggi total $\approx 24.0\text{ m}$).
- **Sistem Struktur:** Portal daktil beton bertulang (*Special Moment Resisting Frame / SMRF*) dengan balok induk bentang $8.0\text{ m}$, balok anak $4.0\text{ m}$, kolom persegi $600 \times 600\text{ mm}$, dan pelat lantai monolitik $130\text{ mm}$.
- **Sistem Fasad:** Selubung fasad *Stick-Built Curtain Wall* (kaca reflektif *double-glazed*), panel spandrel aluminium, dan sirip penahan panas (*sun-shading louvers*).

### 5.2. Pemetaan Komponen BIM & Semantik Revit:
| Dimensi Evaluasi | Spesifikasi BIM & Parameter Teknis | Kategori Revit & Tipe Resmi |
| :--- | :--- | :--- |
| **Kategori Native Wajib** | Kolom Portal, Balok Induk/Anak, Pelat Lantai, Dinding Core, Curtain Wall Fasad, Pintu Darurat/Utama, Ruang Kantor, Plafon, Shaft, HVAC Ducts, Plumbing Pipes. | `OST_StructuralColumns`, `OST_StructuralFraming`, `OST_Floors`, `OST_Walls`, `OST_CurtainWallPanels`, `OST_CurtainWallMullions`, `OST_Doors`, `OST_Rooms`, `OST_Ceilings`, `OST_DuctCurves`, `OST_PipeCurves`. |
| **Family Types & Simbol** | • Kolom: `M_Concrete-Square-Column - 600x600 mm` (K-350 / fc 30 MPa).<br>• Balok: `M_Concrete-Rectangular-Beam` (B1 400x700 mm, B2 300x500 mm).<br>• Dinding Core: `Basic Wall - Beton Bertulang 250mm` (Shear core tangga/lift).<br>• Fasad: `Curtain Wall - Stick System 1500x3600 mm Grid` (mullion 50x150 mm).<br>• Pintu Evakuasi: `M_Door-Double-Fire-Rated-2Hours - 1800 x 2100 mm`.<br>• HVAC: `Duct - Rectangular Duct with Insulation`. |
| **Relasi Hosting** | • Pintu Core/Partisi: Wall-Hosted pada dinding shear core atau partisi gypsum.<br>• Pintu Masuk Kaca: Hosted di dalam Curtain Grid via Curtain Panel Door.<br>• Curtain Wall: Level-based dari slab edge lantai ke lantai atas.<br>• Plafon: Level-based dengan offset height $2.80\text{ m}$.<br>• Diffuser: Ceiling-Hosted pada kisi plafon akustik.<br>• Join Balok-Kolom: Monolithic join cor di tempat (prioritas kolom memotong balok). |
| **Persyaratan Spasial** | • **Core Vertikal:** 2 unit lift penumpang, 1 lift servis, 2 tangga darurat bertekanan dengan pintu keluar darurat (NFPA 101).<br>• **Open Plan Office:** Modul bentang bebas kolom minimal $8.0 \times 8.0\text{ m}$.<br>• **Toilet Kelompok:** Pemisahan toilet pria, wanita, dan difabel per-lantai.<br>• **Plenum Utilitas:** Ruang plenum di atas plafon minimal $800\text{ mm}$ untuk rute ducting dan cable tray. |
| **Pemeriksaan Kualitas Arsitektur** | • **Headroom:** Area kerja kantor $\ge 2.70\text{--}2.80\text{ m}$; Lobi dasar $\ge 4.20\text{--}5.00\text{ m}$; Koridor $\ge 2.40\text{ m}$; Tangga darurat $\ge 2.032\text{ m}$ (IBC 2024 Section 1011.3).<br>• **Toleransi Fasad:** Evaluasi irama fasad berbasis konfigurasi spesifikasi proyek `StickCurtainWall` ($\pm 5.0\text{ mm}$ via `ProjectConfig.FacadeTolerance`). *Catatan: AAMA 501 adalah metode uji fisik kebocoran air dan udara, bukan tabel toleransi instalasi.*<br>• **Artikulasi Transisi:** Wajib artikulasi lobi dasar transparan dan penutup parapet atap (*roof equipment screen*) minimal setinggi $1.80\text{ m}$ untuk menyembunyikan chiller.<br>• **Bebas Interseksi Tak Disengaja:** Join balok-kolom monolitik sah; tidak boleh ada ducting menembus balok tanpa sleeve resmi. |
| **Keluaran QTO Baku** | 1. Schedule Beton Portal (`OST_StructuralFraming` & `Columns`): Volume beton (m³).<br>2. Schedule Curtain Wall (`OST_CurtainWallPanels`): Luas kaca vision & spandrel (m²).<br>3. Schedule Mullion Fasad (`OST_CurtainWallMullions`): Panjang total profil profil aluminium (m).<br>4. Schedule Pintu Kebakaran (`OST_Doors`): Jumlah unit pintu tahan api.<br>5. Schedule Luas Sewa Ruang (`OST_Rooms`): Luasan Semi-Gross, NLA, dan Core Area (m²). |

---

## 6. Evaluasi Tipologi 3: Menara Tinggi (*High-Rise Tower 25 Floors Example*)

### 6.1. Karakteristik Bangunan & Konteks Desain:
- **Fungsi:** Menara perkantoran & perhotelan terpadu 25 lantai dengan 3 lantai basement parkir (Tinggi total $\approx 105.0\text{ m}$).
- **Sistem Struktur:** Sistem ganda (*Dual System*): Core Dinding Geser Beton Masif (*Reinforced Concrete Shear Core*) dipadukan dengan Mega-Columns Baja/Beton Komposit dan Rangka Baja Portal, dilengkapi balok cadik (*outrigger trusses*) di lantai mekanikal (Lt 12).
- **Sistem Fasad:** *Unitized Curtain Wall System* modular pabrikasi presisi tinggi, panel spandrel insulasi, serta mahkota arsitektural puncak (*Distinctive Crown Spire*) setinggi 15 meter (mengacu pada benchmark arsitektur *One Detroit Center*).

### 6.2. Pemetaan Komponen BIM & Semantik Revit:
| Dimensi Evaluasi | Spesifikasi BIM & Parameter Teknis | Kategori Revit & Tipe Resmi |
| :--- | :--- | :--- |
| **Kategori Native Wajib** | Mega-Columns Baja/Beton, Balok Baja WF/H-Beam, Shear Walls Core, Pelat Metal Deck Komposit, Outrigger Truss Framing, Pelat Sambungan Baja, Baut, Baseplate, Voute Sambungan, Unitized Curtain Panels, Mullions, Crown Spire Roof/Mass, Lifts, Rooms, Ducts, Pipes. | `OST_StructuralColumns`, `OST_StructuralFraming`, `OST_Walls`, `OST_Floors`, `OST_StructConnections`, `OST_StructConnectionPlates`, `OST_StructConnectionProfiles`, `OST_CurtainWallPanels`, `OST_CurtainWallMullions`, `OST_Roofs`, `OST_Doors`, `OST_Rooms`, `OST_DuctCurves`, `OST_PipeCurves`. |
| **Family Types & Simbol** | • Kolom Inti: `M_Concrete-Rectangular-Column - 1200x1200 mm` (fc 50 MPa).<br>• Mega-Columns Fasad: `Wide Flange-Column - W14x730` komposit selimut beton.<br>• Balok Lantai: `Wide Flange - W24x68` (ASTM A992 / SNI 03-1729).<br>• Sambungan Voute: `OST_StructConnectionProfiles` (profil potongan setengah balok voute dengan kalibrasi volume solid eksak).<br>• Pelat Sambungan: `OST_StructConnectionPlates` tebal 16mm, 20mm, 25mm.<br>• Fasad: `Curtain Wall - Unitized High-Performance Low-E Double Glazed Panel 1500x4000 mm`.<br>• Mahkota Puncak: `OST_Roofs` & `OST_StructuralFraming` (Rangka baja tetrahedral menara puncak penutup gondola BMU). |
| **Relasi Hosting** | • Unitized Curtain Wall: Multi-tier floor-edge bracket hosted.<br>• Sambungan Baja: Macro Advance Steel hosted pada pertemuan balok-kolom baja.<br>• Pelat Sambungan & Voute: Sub-elemen hosted pada parent `StructuralConnectionHandler`.<br>• Pelat Lantai: Multi-loop CurveLoop donat dengan void core shaft lift masif di tengah lantai.<br>• Dinding Shear Core: Menerus dari raft foundation hingga atap. |
| **Persyaratan Spasial** | • **Core Vertikal Bertingkat:** Pembagian zona lift (Low-Zone Lt 1–12, High-Zone Lt 13–25, Service Lift penuh).<br>• **Lantai Mekanikal (MEP Transfer Floor):** Terletak pada Lantai 12 dengan tinggi bebas $\ge 4.20\text{ m}$ untuk menampung unit AHU utama, trafo, dan pompa transfer air.<br>• **Shaft Utilitas Khusus:** Shaft kabel tegangan menengah, shaft chiller riser, shaft drainase gravitasi, dan tangga kebakaran bertekanan positif ganda. |
| **Pemeriksaan Kualitas Arsitektur** | • **Artikulasi Tripartit Penuh (Podium-Tower-Crown):**<br>  1. *Podium (Lt 1–4):* Kanopi masuk megah, drop-off VIP, dinding kaca transparansi tinggi, ketinggian lobi dasar $\ge 6.0\text{ m}$.<br>  2. *Tower/Shaft (Lt 5–23):* Irama bay bukaan kaca konsisten dengan toleransi unitized curtain wall $\pm 2.0\text{--}3.0\text{ mm}$ (dikonfigurasi via `ProjectConfig.FacadeTolerance`). *Catatan: AAMA TIR-A11-15 mengatur lendutan framing akibat angin L/175 s/d L/240, bukan tabel toleransi ereksi.*; garis spandrel horizontal menutupi struktur balok baja.<br>  3. *Crown/Puncak (Lt 24–Atap):* Mahkota arsitektural berpuncak lancip/piramidal yang menyembunyikan ruang mesin lift dan gondola BMU dari pandangan darat.<br>• **Headroom:** Area kantor $\ge 2.80\text{ m}$; koridor lift $\ge 2.60\text{ m}$; mechanical floor $\ge 4.00\text{ m}$.<br>• **Integritas Bebas Tumpang Tindih:** Void shaft lift dibuat menggunakan inner openings pelat donat; tidak ada pelat menembus core geser. |
| **Keluaran QTO Baku** | 1. **Schedule Baja Sambungan (Format Baku 5 Kolom):** Tepat 5 kolom terlihat (`Lokasi | Tipe Sambungan | Thickness | Qty | Berat`) dengan Kolom A = `Lokasi`.<br>2. **Schedule Voute Baja (Format Baku 7 Kolom):** Berat dihitung dari $\text{Volume Solid Eksak} \times 7850\text{ kg/m}^3$ (bukan panjang balok utuh).<br>3. Schedule Balok & Kolom Profil Baja (`OST_StructuralFraming`): Berat total berdasarkan database resmi `AIGRAV.SteelDatabase.csv`.<br>4. Schedule Luas Fasad Kaca & Panel Spandrel (m²).<br>5. Schedule Volume Beton Shear Core (m³). |

---

## 7. Matriks Perbandingan Silang Antar-Tipologi (*Cross-Typology Comparative Matrix*)

| Aturan Tata Kelola / Parameter Uji | Residential Type 36 Example | Mid-Rise Commercial Office | High-Rise Tower (25 Fl) |
| :--- | :--- | :--- | :--- |
| **Toleransi Irama Fasad (`TC-39`)** | $\pm 10.0\text{--}15.0\text{ mm}$ (ACI 530.1 / TMS 602) | $\pm 5.0\text{ mm}$ (`StickCurtainWall` Config) | $\pm 2.0\text{--}3.0\text{ mm}$ (`UnitizedCurtainWall` Config) |
| **Tinggi Bebas Ruang Kerja / Utama (`TC-43`)** | $\ge 2.40\text{--}2.60\text{ m}$ (Permen PU 29/2006 `UNVERIFIED_STANDARD_REFERENCE`, lokal balok $\ge 2.20\text{ m}$ IBC 1208.2) | $\ge 2.70\text{--}2.80\text{ m}$ (plafon akustik) | $\ge 2.80\text{--}3.00\text{ m}$ (plafon kantor) |
| **Tinggi Bebas Ruang Servis / Toilet (`TC-43`)** | $\ge 2.10\text{--}2.20\text{ m}$ (Permen PU 29/2006 `UNVERIFIED_STANDARD_REFERENCE`, drop floor $-50\text{ mm}$) | $\ge 2.20\text{--}2.40\text{ m}$ | $\ge 2.40\text{ m}$ |
| **Artikulasi Transisi Podium-Crown (`TC-40`)** | **`NOT_APPLICABLE`** (Atap pelana/eaves) | Parapet screen atap & lobi transparan | **WAJIB TRIPARTIT** (Podium, Tower, Crown) |
| **Kategori Elemen Struktur Kunci** | Kolom Praktis & Sloof Beton | Balok Induk/Anak & Kolom Portal | Mega-Columns, Steel Framing, Shear Walls |
| **Kategori MEP Wajib (`TC-41`)** | Sanitair & Pipa Plumbing dasar | Pipa Plumbing & Saluran Ducting HVAC | HVAC VAV, Hydronic Riser, Shaft Elektrikal |
| **Format QTO Baja Struktural (`TC-13, 14`)** | Tidak berlaku (menggunakan QTO beton) | Opsional (jika balok baja komposit) | **WAJIB:** 5 Kolom Pelat & 7 Kolom Voute |
| **Cakupan Tampilan Manifes (`TC-18`)** | 4 Tampak, 1 Denah, 1 Potongan, 1 3D | 4 Tampak, Denah Tipikal, Potongan, 3D | **Full 9-View Manifest** + Multi-Floor Plans |

---

## 8. Kesimpulan & Bukti Kesiapan Tipologi

1. **Fleksibilitas Terbukti:** Tata kelola pemodelan AIGRAV telah terbukti mampu membedakan kebutuhan skala hunian sederhana dari kompleksitas menara pencakar langit.
2. **Kepatuhan Standar Industri Berbasis Bukti:** Seluruh standar teknis (IBC 2024 Section 202 [definisi high-rise], Section 403.1 [ruang lingkup/applicability], Section 1208.2, 1011.3, 1402 [sebagai rujukan komparatif/adopsi proyek]; SNI 03-1729:2020; SNI 2847:2019 Pasal 20.6.1.3.1 vs 26.6.2; PP No. 16 Tahun 2021, Pasal 9 ayat (6), jo. Lampiran bagian 5 — Ketinggian Bangunan Gedung; Permen PU 29/2006 `UNVERIFIED_STANDARD_REFERENCE`; ACI 530.1; PCI 135-25 [edisi mutakhir 2025, menggantikan MNL 135-00 historis]) memiliki nomor pasal dan lingkup yang terverifikasi dan dapat dilacak. Standar pengujian (AAMA 501) dan batas lendutan angin (AAMA TIR-A11-15) telah dipisahkan secara disiplin dari toleransi ereksi arsitektural proyek.
3. **Parameter Terkonfigurasi:** Ambang batas 8 lantai dan toleransi fasad kini menjadi parameter fleksibel yang menghormati dokumen desain pengguna sebagai pemegang otoritas tertinggi.

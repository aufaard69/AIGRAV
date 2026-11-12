# AIGRAV Element Registry Specification (Revised Specification)

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan spesifikasi formal (*data dictionary & schema*) untuk seluruh tipe elemen yang dapat dimanipulasi atau dimodelkan oleh AIGRAV di Autodesk Revit, mengintegrasikan pemetaan hierarki 8 tahap (*Stage 0 s/d Stage 7*), kriteria evaluasi kualitas arsitektural (*Architectural Quality Standards*), persyaratan host native, serta eliminasi total fallback otomatis ke Generic Model / DirectShape.

---

## 1. Skema Registri Elemen Baku (*Element Specification Schema*)

Setiap entri elemen dalam registri AIGRAV wajib memenuhi skema atribut komprehensif berikut:

```yaml
ElementId: string (Identifier unik, e.g. "ELEM_ARCH_WINDOW_HOSTED")
DisplayName: string (Nama teknis resmi elemen)
ModelingStage: enum (Stage 1 s/d Stage 6)
Discipline: enum (Architectural | Structural | Interior | MEP | Site)
RevitCategory: BuiltInCategory (Kategori resmi Revit, e.g. OST_Windows)
FamilyClass: enum (SystemFamily | LoadableFamily | NativeConnection | AdaptiveComponent)
CreationMethod: string (Tanda tangan metode Revit API resmi)
HostRequirement: enum (None | Wall | Floor | Roof | Level | Face)
RequiredParameters: list of string (Parameter instans dan tipe wajib)
ArchitecturalQualityCriteria: list of string (Pemeriksaan kualitas & estetika)
ScheduleCategory: BuiltInCategory (Kategori schedule kuantitas resmi)
ForbiddenFallbacks: list of string (Metode/kategori terlarang yang ditolak mutlak)
PreflightAssertions: list of string (Pemeriksaan deterministik sebelum commit)
```

---

## 2. Katalog Entri Registri Elemen Berdasarkan Tahap Pemodelan

---

### TAHAP 1: DATUM & PRIMARY STRUCTURE (STAGE 1)

#### A. ELEM_STR_COLUMN (Kolom Struktur Portal)
- **Modeling Stage:** `Stage 1: Datum & Primary Structure`
- **Discipline:** `Structural`
- **Revit Category:** `BuiltInCategory.OST_StructuralColumns`
- **Family Class:** `LoadableFamily` (`FamilySymbol`)
- **Creation Method:** `doc.Create.NewFamilyInstance(XYZ location, FamilySymbol symbol, Level level, StructuralType.Column)`
- **Host Requirement:** `Level`
- **Required Parameters:** `FAMILY_BASE_LEVEL_PARAM`, `FAMILY_TOP_LEVEL_PARAM`, `STRUCTURAL_MATERIAL_PARAM`
- **Architectural Quality Criteria:**
  1. Penempatan kolom terpusat tepat pada perpotongan garis grid sumbu resmi.
  2. Dimensi kolom selaras dengan modul interior (dinding perimeter membungkus kolom dengan rapi tanpa sudut menonjol liar).
  3. Tumpuan menerus vertikal ke pondasi/sloof tanpa *floating columns*.
- **Schedule Category:** `BuiltInCategory.OST_StructuralColumns`
- **Forbidden Fallbacks:** `OST_Columns` (Architectural Column tanpa beban), `OST_GenericModel`, `DirectShape`.
- **Preflight Assertions:**
  1. `symbol.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns`.
  2. Profil baja tervalidasi terhadap database resmi `AIGRAV.SteelDatabase.csv`.

#### B. ELEM_STR_BEAM (Balok Struktur Induk & Anak)
- **Modeling Stage:** `Stage 1: Datum & Primary Structure`
- **Discipline:** `Structural`
- **Revit Category:** `BuiltInCategory.OST_StructuralFraming`
- **Family Class:** `LoadableFamily` (`FamilySymbol`)
- **Creation Method:** `doc.Create.NewFamilyInstance(Curve line, FamilySymbol symbol, Level level, StructuralType.Beam)`
- **Host Requirement:** `Level`
- **Required Parameters:** `STRUCTURAL_BEAM_ORIENTATION`, `START_LEVEL_OFFSET_PARAM`, `END_LEVEL_OFFSET_PARAM`
- **Architectural Quality Criteria:**
  1. Rasio tinggi balok terhadap bentang memenuhi batas rasional: $h \approx L/10\text{--}L/14$ (beton) atau $L/20\text{--}L/24$ (baja).
  2. Mempertahankan *headroom clearance* minimal di bawah dasar balok ($\ge 2.20\text{ m}$ hunian/kantor, $\ge 4.50\text{ m}$ industri).
- **Schedule Category:** `BuiltInCategory.OST_StructuralFraming`
- **Forbidden Fallbacks:** `OST_GenericModel`, `DirectShape`.
- **Preflight Assertions:**
  1. `symbol.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming`.
  2. Ujung balok terikat pada kolom atau balok tumpuan (*load path continuous*).

#### C. ELEM_STR_FLOOR_DONUT (Pelat Lantai Struktural Donat Bebas Tumpang Tindih)
- **Modeling Stage:** `Stage 1: Datum & Primary Structure`
- **Discipline:** `Structural / Architectural`
- **Revit Category:** `BuiltInCategory.OST_Floors`
- **Family Class:** `SystemFamily` (`FloorType`)
- **Creation Method:** `Floor.Create(Document, IList<CurveLoop> loops, ElementId floorTypeId, ElementId levelId)`
- **Host Requirement:** `Level`
- **Required Parameters:** `FLOOR_HEIGHTABOVELEVEL_PARAM`, `FLOOR_ATTR_THICKNESS_PARAM`
- **Architectural Quality Criteria:**
  1. Zero solid-on-solid overlap (tidak ada tumpang tindih pelat untuk mencegah Z-fighting dan benturan QTO).
  2. Lubang shaft lift, tangga, dan void utilitas dibuat menggunakan inner opening loops yang bersih.
- **Schedule Category:** `BuiltInCategory.OST_Floors`
- **Forbidden Fallbacks:** Tumpukan pelat pejal bertumpuk, `DirectShape`.
- **Preflight Assertions:**
  1. `loops[0]` berorientasi Counter-Clockwise (Outer boundary); `loops[1..N]` Clockwise (Inner voids).
  2. Seluruh `CurveLoop` tertutup sempurna, planar, dan bebas irisan diri (*non-self-intersecting*).

---

### TAHAP 2: ARCHITECTURAL ENCLOSURE (STAGE 2)

#### D. ELEM_ARCH_WALL (Dinding Perimeter Eksterior Komposit)
- **Modeling Stage:** `Stage 2: Architectural Enclosure`
- **Discipline:** `Architectural`
- **Revit Category:** `BuiltInCategory.OST_Walls`
- **Family Class:** `SystemFamily` (`WallType`)
- **Creation Method:** `Wall.Create(Document, Curve, ElementId wallTypeId, ElementId levelId, double height, double offset, bool flip, bool structural)`
- **Host Requirement:** `Level`
- **Required Parameters:** `WALL_USER_HEIGHT_PARAM`, `WALL_BASE_CONSTRAINT`, `WALL_STRUCTURAL_SIGNIFICANCE_PARAM`
- **Architectural Quality Criteria:**
  1. Lapisan struktur komposit terdefinisi (bata/precast, plesteran, lapisan kedap air, cat finishing).
  2. Menerus menutup perimeter lantai tanpa celah kebocoran udara (*weatherproofing seal*).
  3. Hubungan sudut dinding menyatu mulus (*clean wall joins*).
- **Schedule Category:** `BuiltInCategory.OST_Walls`
- **Forbidden Fallbacks:** `DirectShape`, `OST_GenericModel`, In-Place Mass tanpa konversi.
- **Preflight Assertions:**
  1. `wallTypeId` valid dan bertipe `WallType`.
  2. Panjang garis dinding $> 1.0\text{ mm}$ dan terikat pada level yang aktif.

#### E. ELEM_ARCH_ROOF (Selubung Atap Native Berkemiringan)
- **Modeling Stage:** `Stage 2: Architectural Enclosure`
- **Discipline:** `Architectural`
- **Revit Category:** `BuiltInCategory.OST_Roofs`
- **Family Class:** `SystemFamily` (`RoofType`)
- **Creation Method:** `doc.Create.NewFootPrintRoof(CurveArray boundary, Level level, RoofType roofType, out ModelCurveArray mapping)`
- **Host Requirement:** `Level`
- **Required Parameters:** `ROOF_SLOPE`, `ROOF_BASE_LEVEL_PARAM`, `ROOF_OVERHANG`
- **Architectural Quality Criteria:**
  1. Kemiringan atap sesuai material: $\tan(\theta \times \pi / 180)$ (metal sheet $\ge 5^\circ$, genteng $\ge 25^\circ$).
  2. Overhang teritisan (default 800 mm) memadai untuk proteksi tampias air hujan tropis.
  3. Bebas kemiringan terbalik (*zero negative drainage / ponding risk*).
- **Schedule Category:** `BuiltInCategory.OST_Roofs`
- **Forbidden Fallbacks:** DirectShape prisma segitiga, Generic Model in-place.
- **Preflight Assertions:**
  1. Kurva poligon batas tertutup (*closed planar polygon loop*).
  2. Pemetaan slope per-edge melalui `ModelCurveArray` terdefinisi presisi.

---

### TAHAP 3: OPENINGS & BUILDING COMPONENTS (STAGE 3)

#### F. ELEM_ARCH_DOOR_HOSTED (Pintu Arsitektural Tertanam Dinding)
- **Modeling Stage:** `Stage 3: Openings & Building Components`
- **Discipline:** `Architectural`
- **Revit Category:** `BuiltInCategory.OST_Doors`
- **Family Class:** `LoadableFamily` (`FamilySymbol`)
- **Creation Method:** `doc.Create.NewFamilyInstance(XYZ location, FamilySymbol symbol, Element wallHost, Level level, StructuralType.NonStructural)`
- **Host Requirement:** `Wall` (Elemen bertipe `Wall` valid di dokumen yang sama)
- **Required Parameters:** `DOOR_WIDTH`, `DOOR_HEIGHT`, `INSTANCE_SILL_HEIGHT_PARAM`
- **Architectural Quality Criteria:**
  1. Ambang atas pintu (*head height*) seragam dan proporsional terhadap tinggi ruangan (standar $2.10\text{ m}$).
  2. Ayunan daun pintu (*door swing*) tidak membentur daun pintu lain, kolom struktur, atau perabot tetap.
  3. Void bukaan dinding terbentuk sempurna memotong lapisan dinding host.
- **Schedule Category:** `BuiltInCategory.OST_Doors`
- **Forbidden Fallbacks:** `OST_GenericModel`, DirectShape, Family unhosted tanpa dinding host.
- **Preflight Assertions:**
  1. `symbol.Category.Id.Value == (long)BuiltInCategory.OST_Doors`.
  2. `wallHost != null && wallHost is Wall && wallHost.IsValidObject`.
  3. Titik penyisipan berada di dalam bidang kurva dinding host.

#### G. ELEM_ARCH_WINDOW_HOSTED (Jendela Fasad Tertanam Dinding)
- **Modeling Stage:** `Stage 3: Openings & Building Components`
- **Discipline:** `Architectural`
- **Revit Category:** `BuiltInCategory.OST_Windows`
- **Family Class:** `LoadableFamily` (`FamilySymbol`)
- **Creation Method:** `doc.Create.NewFamilyInstance(XYZ location, FamilySymbol symbol, Element wallHost, Level level, StructuralType.NonStructural)`
- **Host Requirement:** `Wall` (Elemen bertipe `Wall` valid di dokumen yang sama)
- **Required Parameters:** `WINDOW_WIDTH`, `WINDOW_HEIGHT`, `INSTANCE_SILL_HEIGHT_PARAM`
- **Architectural Quality Criteria:**
  1. Elevasi ambang bawah (*sill height*) akurat sesuai fungsi ruang (misal: 900 mm ruang kerja, 500 mm void kaca).
  2. Material kaca memiliki transparansi terverifikasi $\ge 50\%$.
  3. Tidak memotong atau menabrak kolom struktur utama.
- **Schedule Category:** `BuiltInCategory.OST_Windows`
- **Forbidden Fallbacks:** `OST_GenericModel`, DirectShape Box, Panel Kaca Unhosted.
- **Preflight Assertions:**
  1. `symbol.Category.Id.Value == (long)BuiltInCategory.OST_Windows`.
  2. `wallHost != null && wallHost is Wall && wallHost.IsValidObject`.
  3. Konversi nilai sill height milimeter ke internal feet ($mm / 304.8$) akurat $\pm 1\text{ mm}$.

---

### TAHAP 4: ARCHITECTURAL DETAILING & FAÇADE SYSTEMS (STAGE 4)

#### H. ELEM_ARCH_FACADE_SYSTEM (Sistem Fasad, Curtain Wall & Detail Artikulasi)
- **Modeling Stage:** `Stage 4: Architectural Detailing & Façade Systems`
- **Discipline:** `Architectural`
- **Revit Category:** `BuiltInCategory.OST_CurtainWallPanels` / `BuiltInCategory.OST_CurtainWallMullions`
- **Family Class:** `SystemFamily` (`CurtainGrid` / `CurtainSystem`)
- **Creation Method:** `Wall.Create` (dengan `WallType` berkategori Curtain Wall) atau `CurtainSystem.Create`
- **Host Requirement:** `Level` / `Face`
- **Required Parameters:** `CURTAIN_GRID_SPACING`, `MULLION_PROFILE_TYPE`, `SPANDREL_PANEL_MATERIAL`
- **Architectural Quality Criteria:**
  1. **Irama Fasad Konsisten (Façade Rhythm):** Pembagian bay bukaan kaca dan panel spandrel memiliki repetisi ritmis teratur (toleransi deviasi $\le \pm 5\text{ mm}$).
  2. **Artikulasi Transisi Vertikal:** Transisi tegas antara lantai podium bawah, badan menara tipikal, dan mahkota atap (*crown*) dengan profil cornice, louvre, atau parapet coping.
  3. Profil mullion dan transom tersambung rapi pada setiap sudut pertemuan fasad.
- **Schedule Category:** `BuiltInCategory.OST_CurtainWallPanels`
- **Forbidden Fallbacks:** `OST_GenericModel`, Solid Extrusion tanpa profil curtain grid.
- **Preflight Assertions:**
  1. Grid fasad terikat pada elevasi level lantai tipikal.
  2. Panel spandrel menutupi plenum balok struktur tanpa celah tembus pandang dari luar.

---

### TAHAP 5: SPATIAL & INTERIOR MODELING (STAGE 5)

#### I. ELEM_ARCH_ROOM (Ruang & Zonasi Komputasional)
- **Modeling Stage:** `Stage 5: Spatial & Interior Modeling`
- **Discipline:** `Architectural / Spatial`
- **Revit Category:** `BuiltInCategory.OST_Rooms`
- **Family Class:** `SystemSpatialElement` (`Room`)
- **Creation Method:** `doc.Create.NewRoom(Level level, UV point)`
- **Host Requirement:** `Level` (dengan batas elemen room-bounding tertutup)
- **Required Parameters:** `ROOM_NAME`, `ROOM_NUMBER`, `ROOM_AREA`, `ROOM_VOLUME`
- **Architectural Quality Criteria:**
  1. Sirkulasi antar-ruang logis sesuai program tipologi bangunan.
  2. Ruang basah (toilet/kamar mandi) memiliki penurunan pelat lantai (*floor drop*) $-30\text{ s/d } -50\text{ mm}$.
  3. Seluruh ruang memiliki akses bukaan atau ventilasi yang memadai.
- **Schedule Category:** `BuiltInCategory.OST_Rooms`
- **Forbidden Fallbacks:** Generic Annotation Marker, Text Note manual.
- **Preflight Assertions:**
  1. Titik koordinat UV berada di dalam kurva tertutup elemen room-bounding.
  2. `room.GetBoundarySegments()` wajib mengembalikan segmen kurva non-null dan tertutup. Jika terbuka/bocor, transaksi lokal wajib di-rollback dengan error `ERR_ROOM_NOT_ENCLOSED`.

---

### TAHAP 6: MEP & BUILDING SYSTEMS (STAGE 6)

#### J. ELEM_MEP_DUCT (Saluran Udara HVAC)
- **Modeling Stage:** `Stage 6: MEP & Building Systems`
- **Discipline:** `MEP`
- **Revit Category:** `BuiltInCategory.OST_DuctCurves`
- **Family Class:** `SystemFamily` (`DuctType`)
- **Creation Method:** `Duct.Create(Document, ElementId systemTypeId, ElementId ductTypeId, ElementId levelId, XYZ start, XYZ end)`
- **Host Requirement:** `Level`
- **Required Parameters:** `RBS_CURVE_WIDTH_PARAM`, `RBS_CURVE_HEIGHT_PARAM`, `RBS_OFFSET_PARAM`
- **Architectural Quality Criteria:**
  1. Saluran ducting berada di dalam plenum plafon dengan tinggi bebas dasar ducting tidak melanggar elevasi plafon arsitektur.
  2. Penetrasi ducting menembus balok/dinding struktural menggunakan sleeve koordinasi resmi tanpa melemahkan kapasitas struktur.
- **Schedule Category:** `BuiltInCategory.OST_DuctCurves`
- **Forbidden Fallbacks:** `OST_GenericModel`, Solid Sweep Extrusion.
- **Preflight Assertions:**
  1. `systemTypeId` valid dan bertipe `MEPSystemType` (Supply/Return/Exhaust Air).
  2. Tidak terjadi benturan keras (*hard clash*) terhadap balok struktur.

#### K. ELEM_MEP_PIPE (Perpipaan Plumbing & Drainase)
- **Modeling Stage:** `Stage 6: MEP & Building Systems`
- **Discipline:** `MEP`
- **Revit Category:** `BuiltInCategory.OST_PipeCurves`
- **Family Class:** `SystemFamily` (`PipeType`)
- **Creation Method:** `Pipe.Create(Document, ElementId systemTypeId, ElementId pipeTypeId, ElementId levelId, XYZ start, XYZ end)`
- **Host Requirement:** `Level`
- **Required Parameters:** `RBS_PIPE_DIAMETER_PARAM`, `RBS_OFFSET_PARAM`, `RBS_PIPE_SLOPE`
- **Architectural Quality Criteria:**
  1. Pipa air kotor memiliki kemiringan gravitasi stabil ($1\%\text{--}2\%$) ke arah shaft utilitas utama.
  2. Rute perpipaan terkumpul rapi di dalam shaft vertikal dan tidak memotong ruang hunian terbuka.
- **Schedule Category:** `BuiltInCategory.OST_PipeCurves`
- **Forbidden Fallbacks:** `OST_GenericModel`, Solid Sweep.
- **Preflight Assertions:**
  1. `systemTypeId` valid (Sanitary, Cold Water, Vent).
  2. Titik elevasi pipa air buangan terhubung ke jalur pembuangan resmi.

---

### KATEGORI TERKONTROL KHUSUS: DETAIL BAJA & GENERIC MODEL

#### L. ELEM_STR_VOUTE_PROFILE (Voute Sambungan Baja)
- **Modeling Stage:** `Stage 1 / Detail Sambungan`
- **Discipline:** `Structural Detailing`
- **Revit Category:** `BuiltInCategory.OST_StructConnectionProfiles`
- **Family Class:** `NativeConnection` (Advance Steel Macro)
- **Schedule Category:** `BuiltInCategory.OST_StructConnectionProfiles` (Schedule 7 Kolom Baku)
- **Aturan Khusus:** Berat wajib dikalibrasi dari volume solid eksak ($\text{Volume} \times 7850\text{ kg/m}^3$). Dilarang mengalikan panjang balok utuh.

#### M. ELEM_GENERIC_MODEL_AUTHORIZED (Generic Model Hanya dengan Otorisasi Server)
- **Revit Category:** `BuiltInCategory.OST_GenericModel`
- **Status:** **HANYA DIIZINKAN** untuk objek ornamen non-standar, plakat peresmian, patung seni, atau monumen khusus.
- **Security Boundary:** Otorisasi diverifikasi di sisi server berdasarkan intent tugas dan allowlist kategori. Parameter klien `AIGRAV_PROVENANCE` berfungsi sebagai audit trail, bukan bukti otorisasi.
- **Larangan Mutlak:** DILARANG KERAS digunakan sebagai pengganti elemen Dinding, Kolom, Balok, Pelat, Atap, Pintu, Jendela, Ruang, Ducting, atau Pipa.

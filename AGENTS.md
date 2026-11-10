# AIGRAV Revit Standard Operating Protocol (Mandatory Workflow)

Setiap perintah yang berkaitan dengan pemodelan, audit, atau modifikasi di Autodesk Revit melalui AIGRAV WAJIB mengikuti protokol presisi berikut:

---

## 1. Prinsip Fundamental Arsitektur Revit & Single-Threaded Execution
1. **Revit API adalah Single-Threaded untuk Transaksi Tulis (Write Transactions)**:
   - DILARANG membagi beberapa sub-agent untuk menulis/membuat elemen ke Revit secara paralel (karena akan memicu *transaction deadlock*, antrean lambat, atau *race condition*).
   - **Hanya ada SATU (1) Revit Bridge Executor** yang memegang kontrol transaksi tulis.
   - Analisis, pembacaan data, *planning blueprint*, dan validasi visual BOLEH berjalan paralel.
2. **Kejujuran Laporan & Larangan Silent Failure**:
   - DILARANG menelan eror dengan blok `catch { }` kosong.
   - DILARANG mencetak klaim palsu seperti *"100% SUKSES DENGAN ZERO ERROR"* tanpa verifikasi independen.
   - Setiap panggilan eksekusi wajib mengembalikan JSON terstruktur: `total_created`, `total_failed`, `failed_items` (pesan eror spesifik), dan daftar ID elemen baru.
3. **LARANGAN MUTLAK PENGGUNAAN DIRECTSHAPE UNTUK ELEMEN PEMODELAN**:
   - **DILARANG KERAS** menggunakan `DirectShape` (`DirectShape.CreateElement`) untuk memodelkan elemen arsitektur, struktur, maupun interior (Dinding, Kolom, Balok, Sloof, Ring Balk, Pintu, Jendela, Kanopi, Atap, Plafon, Sanitair, Furnitur).
   - `DirectShape` menghasilkan geometri mati (*dumb solid*) tanpa Family/Type (`TypeId == -1`), tidak memiliki lapisan struktur (*compound layers*), tidak bisa di-edit profile, tidak bisa menjadi host pintu/jendela, dan merusak integritas BIM.
   - **WAJIB Menggunakan Native System Family & Loadable Family**:
     * **Dinding**: Wajib dibuat menggunakan `Wall.Create()` dengan System Family `WallType` berstruktur komposit resmi (bata merah, plester, aci).
     * **Kolom & Balok**: Wajib di-load dari library resmi Revit (`C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\...`) via `doc.LoadFamily()` dan di-duplicate type parametrik (`KP 150x150 mm`, `Sloof 150x200 mm`, `Ring Balk 150x200 mm`).
     * **Pintu & Jendela**: Wajib di-load dari folder `Doors` & `Windows` resmi Revit dan di-host langsung ke dalam `Wall` asli via `doc.Create.NewFamilyInstance()`.
     * **Sanitair & Furnitur**: Wajib di-load dari folder `Plumbing\MEP\Fixtures` dan `Furniture` resmi Revit.
   - **Path Library Resmi Terinstal**: `C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\` (Tersedia lengkap: Annotations, Casework, Columns, Detail Items, Doors, Furniture, Plumbing, Structural Columns, Structural Framing, Windows).

---

## 2. Fase 1: Pre-Execution Review & Spatial Blueprinting
Sebelum memodifikasi atau membuat elemen di Revit:
1. **Verifikasi Gambar Acuan & Standar Resmi**:
   - Cross-check standar teknis (`STD-BJ-01 s/d 07`, `STD-PDS-01 s/d 15`, atau referensi proyek).
   - Pastikan dimensi (mm), tebal pelat, diameter baut, dan elevasi bebas dari salah ketik.
2. **Audit Datum & Konteks Model**:
   - Panggil `aigrav_get_project_context()` untuk memeriksa Level aktif, sistem Grid, dan ketersediaan FamilySymbol.
3. **Formulasi Rencana Eksekusi (Action Plan)**:
   - Tuliskan tabel koordinat $(X, Y, Z)$ dalam milimeter dan jangkar relatif (`anchor points`) sebelum eksekusi dimulai.

---

## 3. Fase 2: Eksekusi Berbasis Relative Helper (1 Executor)
1. **Gunakan Relative Helpers**:
   - Utamakan pemanggilan fungsi relatif (`place_on_top_of`, `connect_elements`, `create_grid_matrix`) daripada menembak koordinat absolut di ruang hampa.
2. **Safe Transaction Sandbox**:
   - Setiap batch operasi dijalankan di dalam `TransactionGroup` dengan pengembalian *Auto-Spatial Telemetry* (`spatial_bounds_mm`, `is_floating`, dan `unjoined_bbox_clashes`).

---

## 4. Fase 3: Dual-Layer Independent Validation & Hak Veto Validator
Validator bertindak secara independen dan **MEMILIKI HAK VETO** penuh untuk menolak hasil Executor:

### Layer 1: Validasi Deterministik Berbasis Kode (Angka & Geometri Pasti)
Validator wajib memanggil tool validasi independen:
1. **Deteksi Benturan Solid (`aigrav_detect_spatial_clashes`)**:
   - Memastikan furnitur tidak menembus dinding (`OST_Furniture` vs `OST_Walls`), dan pelat lantai tidak menembus balok.
2. **Audit Material & Transparansi (`aigrav_audit_material_transparency`)**:
   - Kaca jendela/curtain wall **wajib transparan** (`Transparency >= 50%`). Jika transparan $< 50\%$ (abu-abu solid), status ditolak (*REJECT*).
   - Fasia dan atap wajib memiliki material resmi (tidak boleh material default hijau neon).
3. **Audit Peringatan Revit (`aigrav_audit_unresolved_warnings`)**:
   - Memastikan tidak ada *duplicate elements* atau *overlapping instances*.

### Layer 2: Validasi Visual (Checklist Gambar PNG)
Validator memanggil `aigrav_inspect_model_visually()` atau `aigrav_capture_revit_viewport()`:
* Cek visual **khusus untuk tampilan dan proporsi estetika**, bukan untuk angka dimensi:
  - [ ] Apakah warna render material tampak natural?
  - [ ] Apakah kaca tembus pandang?
  - [ ] Apakah teks anotasi denah saling bertumpuk?
  - [ ] Apakah ada objek lansekap (pohon) yang menutupi tampak depan bangunan?

### Protokol Hak Veto:
* Jika salah satu syarat Layer 1 atau Layer 2 gagal:
  * Validator **REJECT** hasil eksekusi dan mengirimkan daftar poin koreksi spesifik ke Executor.
  * Executor melakukan revisi (maksimal 2 iterasi).
  * Jika setelah 2 iterasi masih gagal, eskalasikan langsung ke pengguna beserta laporannya.

---

## 5. Post-Execution Safety Gate
- Lakukan regenerasi model (`Regenerate`).
- Pastikan tidak ada *error* atau *orphan elements*.
- Sajikan laporan ringkas berupa tabel perbandingan (Sebelum vs Sesudah) kepada pengguna.

---

## 6. Protokol Standar Quantity Takeoff (QTO) Schedule & Kalibrasi Baja
Setiap pembuatan, modifikasi, dan audit jadwal kuantitas (Schedule / QTO) baja struktural WAJIB tunduk pada standar berikut:

### 1. Template Kolom Baku & Posisi Mutlak Kolom A (Lokasi)
- **Kolom A (Index 0 / Kolom Pertama) WAJIB berupa parameter `Lokasi`** (unhidden/terlihat) sebagai penanda zona spasial pada seluruh schedule berbasis lokasi (`C.3.x.y`).
- **Layout Kolom Baku per Kategori Komponen**:
  1. **Pelat Sambungan** (`OST_StructConnectionPlates`): Tepat **5 Kolom**  
     `Lokasi | Tipe Sambungan | Thickness (mm) | Qty | Berat (Kg)`  
     *Sorting*: By `Type of Connection` (Asc), lalu `Thickness` (Asc, **ShowFooter=True** untuk subtotal Qty & Berat).
  2. **Baut Sambungan** (`OST_StructConnectionBolts`): Tepat **3 Kolom** (Baut dihitung butir, tanpa kolom berat)  
     `Lokasi | Tipe Sambungan | Qty`
  3. **Baseplate Kolom** (`OST_StructConnectionPlates`): Tepat **7 Kolom**  
     `Lokasi | Keterangan | Thickness (mm) | Width (mm) | Length (mm) | Qty | Berat (Kg)`  
     *Sorting*: By `Comments`, `Thickness`, `Width`, `Length` (Asc).
  4. **Kolom & Balok Profil** (`OST_StructuralColumns` / `OST_StructuralFraming`): Tepat **6 Kolom**  
     `Lokasi | Family and Type | Length (mm) | Comments | Qty | Berat (Kg)`
  5. **Angkur Baja** (`OST_StructuralFraming` / Anchor Hardware): Tepat **5 Kolom**  
     `Lokasi | Comments | Diameter | Qty | Length`

### 2. Kewajiban Validasi Terhadap Database Profil Baja (`AIGRAV.SteelDatabase.csv`)
- **DILARANG KERAS** menginput berat nominal hasil karangan manual (*hardcoded guesswork*).
- Setiap profil baja struktural (WF, H-Beam, INP, UNP, CNP, L/Angle) **WAJIB tervalidasi terhadap database resmi**:  
  📂 `C:\AIGRAV\Revit\AIGRAV.SteelDatabase.csv` (standar pabrikan resmi Gunung Garuda & SNI).
- **Rumus Kalibrasi Berat Baja**:
  * **Profil Balok & Kolom**:  
    $$\text{Berat (Kg)} = \text{Panjang (m)} \times \text{weightPerMeterKg (dari Database)}$$
  * **Pelat Sambungan & Baseplate**: Berat jenis baja standar $\rho = 7850\text{ kg/m}^3$  
    $$\text{Berat (Kg)} = \text{Thickness (m)} \times \text{Width (m)} \times \text{Length (m)} \times 7850\text{ kg/m}^3$$
- Jika profil di model tidak ditemukan di database, sistem wajib mencatatnya sebagai *uncalibrated profile warning* dan meminta konfirmasi profil ekuivalen resmi.

### 3. Protokol Anti Double-Counting (Pencegahan Duplikasi Kuantitas)
Untuk menjamin elemen yang dipartisi ke sub-zona (misal: *Carton & Cups*, *Waterline*, *Power House*) tidak terhitung ganda di ringkasan utama:
1. **Schedule Bangunan Utama / General (`B.*`, `C.2.*`)**:
   - WAJIB memiliki filter: `Lokasi Equal ""` (string kosong).
   - Elemen yang telah memiliki nama lokasi otomatis dikeluarkan dari hitungan bangunan utama.
2. **Schedule Sub-Zona (`C.3.x.y`)**:
   - Kolom A wajib bernilai `Lokasi Equal "<NamaZona>"`.

### 4. QA/QC Audit Otomatis
- Setiap transaksi schedule wajib divalidasi melalui in-memory audit tool (`aigrav_qa_audit_all_schedules`) untuk memastikan:
  - [ ] Tidak ada schedule duplikat dengan akhiran `Copy 1`, `Copy 2`.
  - [ ] Urutan kolom index 0 selalu `Lokasi`.
  - [ ] Grand totals tercentang aktif (`Title: Total`, `Count=True`, `Totals=True`).
  - [ ] Tidak ada nilai volume/berat yang menghasilkan 0 akibat kesalahan rumus parameter.

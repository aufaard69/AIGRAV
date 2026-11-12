# AIGRAV Governance Acceptance Test Matrix (Complete 45-Test Specification)

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan matriks komprehensif 45 spesifikasi pengujian penerimaan (*Acceptance Tests*) konkret untuk memvalidasi integritas tata kelola pemodelan BIM, penanganan transaksi per-tool dan compensating rollback, pencegahan silent fallback, kepatuhan QTO, keamanan sistem, penegakan siklus Live Capture & Visual Feedback Loop, serta **standar kualitas arsitektur, fidelitas fungsional, dan eksekusi berbasis dependensi** sebelum promosi ke lingkungan produksi.

---

## 1. Ikhtisar Matriks Pengujian (45 Kasus Uji)

Matriks pengujian mencakup 45 kasus uji verifikasi deterministik:
- **TC-01 s/d TC-07**: Pengujian Semantik Pemodelan, Preservasi Kategori, dan Validasi Host Pintu/Jendela.
- **TC-08 & TC-09**: Pengujian Integritas Peringatan (*Warning*) & Benturan Spasial (*Clash*).
- **TC-10 s/d TC-12**: Pengujian Transaksi Opsi 1A (Operation Journal & Compensating Rollback), Preflight Tool, dan Task Scope.
- **TC-13 & TC-14**: Pengujian Standar QTO & Kalibrasi Bobot Baja.
- **TC-15**: Pengujian Keamanan Pemuatan Modul Hot-Reload Opsi 4A (*Production Lockdown*).
- **TC-16 s/d TC-27**: Pengujian Siklus Live Capture & Visual Feedback Loop.
- **TC-28 s/d TC-37**: 10 Pengujian Khusus Resolusi Kesenjangan Teknis & Validasi Kritis (Review Addendum).
- **TC-38 s/d TC-45**: **8 Pengujian Kualitas Arsitektur, Fidelitas Fungsional & Integritas Dependensi (Architectural Fidelity Addendum)**.

---

## 2. Rincian Spesifikasi Kasus Uji (Test Cases 01 – 45)

### TC-01: Validasi Pembuatan Dinding Native System Family
- **Deskripsi**: Membuat elemen dinding menggunakan tipe `WallType` resmi yang valid.
- **Input**: Titik awal $(0,0,0)$, titik akhir $(5000,0,0)$, Level 1, tipe `"Bata Merah 150mm"`.
- **Kriteria Keberhasilan (Pass)**:
  - Elemen terbuat dengan `Category.BuiltInCategory == OST_Walls`.
  - `Wall.WallType.Name == "Bata Merah 150mm"`.
  - Transaksi commit berhasil tanpa warning tumpang tindih.
- **Kondisi Gagal (Fail)**: Dinding dibuat menggunakan Generic Model atau DirectShape.

### TC-02: Pembuatan Pintu Hosted dalam Dinding (*Wall-Hosted FamilyInstance*)
- **Deskripsi**: Membuat pintu yang di-host langsung ke dalam dinding host (Prioritas 1).
- **Input**: Host Wall ID dari TC-01, Titik $(2500,0,0)$, Family `"Single-Flush"`, Type `"0915 x 2134 mm"`.
- **Kriteria Keberhasilan (Pass)**:
  - `Category.BuiltInCategory == OST_Doors`.
  - Validasi host: `FamilyInstance.Host.Id == HostWall.Id`.
  - Validasi parameter: Lebar, tinggi, level, dan orientasi bukaan terisi benar.
  - Bukaan dinding otomatis terbentuk (void cutout ada).
  - Terlihat pada schedule pintu (`OST_Doors`).
  - Respons mengembalikan JSON terstruktur dengan ID elemen baru.
- **Kondisi Gagal (Fail)**: Elemen unhosted, host null, silent fallback ke Generic Model, atau respons error tidak terstruktur.

### TC-03: Pembuatan Jendela Hosted dalam Dinding (*Wall-Hosted FamilyInstance*)
- **Deskripsi**: Membuat jendela yang di-host langsung ke dalam dinding host dengan parameter elevasi ambang bawah (*Sill Height*) (Prioritas 1).
- **Input**: Host Wall ID dari TC-01, Titik $(1000,0,0)$, Sill Height $= 900\text{ mm}$, Family `"Fixed"`.
- **Kriteria Keberhasilan (Pass)**:
  - `Category.BuiltInCategory == OST_Windows`.
  - Validasi host: `FamilyInstance.Host.Id == HostWall.Id`.
  - Parameter `INSTANCE_SILL_HEIGHT_PARAM` bernilai tepat $900\text{ mm}$ ($\pm 1\text{ mm}$).
  - Bukaan dinding terbentuk secara presisi.
- **Kondisi Gagal (Fail)**: Sill height gagal disetel, host null, atau silent fallback ke Generic Model.

### TC-04: Fail-Closed Behavior saat Family Jendela Tidak Ditemukan
- **Deskripsi**: Meminta pembuatan jendela ketika family jendela yang diminta tidak ada di memori maupun disk library.
- **Input**: Host Wall ID dari TC-01, Family `"NonExistentWindowFamily_XYZ"`.
- **Kriteria Keberhasilan (Pass)**:
  - Transaksi lokal dibatalkan secara bersih (*Rollback*).
  - Sistem mengembalikan error terstruktur: `ERR_FAMILY_SYMBOL_NOT_FOUND`.
  - Model bersih 100% tanpa ada elemen Generic Model atau DirectShape yang dibuat sebagai pengganti.
- **Kondisi Gagal (Fail)**: Sistem otomatis membuat Generic Model kubus atau DirectShape untuk "mengakali" tampilan jendela.

### TC-05: Penolakan Generic Model Tanpa Otorisasi Eksplisit
- **Deskripsi**: Mencoba memanggil endpoint Generic Model untuk membuat elemen arsitektural primer.
- **Input**: Perintah membuat pintu dengan memanggil `create_generic_model`.
- **Kriteria Keberhasilan (Pass)**:
  - Eksekusi ditolak di gerbang preflight (*Rejected*).
  - Mengembalikan pesan pelanggaran: `ERR_PROHIBITED_GENERIC_MODEL_SUBSTITUTION`.
- **Kondisi Gagal (Fail)**: Generic Model berhasil dibuat untuk merepresentasikan pintu.

### TC-06: Pembuatan Atap Native Berkemiringan (*Native Footprint Roof*)
- **Deskripsi**: Membuat penutup atap menggunakan `doc.Create.NewFootPrintRoof` (Prioritas 2).
- **Input**: Kurva poligon tertutup perimeter dinding, Level Atap, kemiringan $30^\circ$, overhang $800\text{ mm}$.
- **Kriteria Keberhasilan (Pass)**:
  - Elemen berkategori `OST_Roofs` dan bertipe `FootPrintRoof`.
  - Kemiringan terpasang pada kurva tepi yang ditentukan.
  - Tidak menggunakan DirectShape prisma segitiga.
- **Kondisi Gagal (Fail)**: Menggunakan DirectShape atau In-Place Massing tanpa konversi System Family.

### TC-07: Validasi Penutupan Batas Ruang (*Room Boundary Enclosure Gate*)
- **Deskripsi**: Menempatkan elemen Room arsitektural dengan validasi batas ruang tertutup (Prioritas 3).
- **Input**: Titik koordinat di dalam 4 dinding bata yang tertutup sempurna.
- **Kriteria Keberhasilan (Pass)**:
  - Elemen Room terbuat dengan kategori `OST_Rooms`.
  - Parameter `Area` bernilai positif ($> 0.00\text{ m}^2$) dan perimeter tertutup.
  - Jika ditempatkan di area terbuka/bocor, transaksi lokal di-rollback dengan error `ERR_ROOM_NOT_ENCLOSED`.
- **Kondisi Gagal (Fail)**: Room berstatus "Not Enclosed" tetap di-commit atau diganti teks anotasi biasa.

### TC-08: Supresi Selektif Warning Jinak & Eskalasi Warning Kritis
- **Deskripsi**: Menguji kustom `IFailuresPreprocessor` yang menggantikan pemanggilan `DeleteAllWarnings()`.
- **Input 1**: Menempatkan elemen dengan mark duplikat sementara (Warning Jinak).
- **Input 2**: Menempatkan 2 dinding berhimpitan persis di koordinat yang sama (Warning Kritis).
- **Kriteria Keberhasilan (Pass)**:
  - Input 1: Warning mark duplikat diserap (*suppressed*) dan dicatat dalam log audit telemetri. Transaksi commit.
  - Input 2: Warning dinding bertumpuk tidak diserap; transaksi dibatalkan (*Rollback*) atau memicu compensating rollback.
- **Kondisi Gagal (Fail)**: Seluruh warning dihapus secara buta atau warning kritis diabaikan tanpa audit.

### TC-09: Matriks Kontak Sah vs Benturan Keras (*Spatial Collision*)
- **Deskripsi**: Menguji alat deteksi benturan `aigrav_detect_spatial_clashes`.
- **Input**: Dinding bata yang menumpu di atas pelat beton (Kontak Sah) dan furnitur meja yang menembus kolom struktur (Benturan Keras).
- **Kriteria Keberhasilan (Pass)**:
  - Kontak dinding-pelat diidentifikasi sebagai hubungan tumpuan sah (*Valid Join/Resting*), bukan benturan.
  - Penetrasi furnitur-kolom dilaporkan sebagai *Hard Clash* dengan ID elemen yang terlibat.
- **Kondisi Gagal (Fail)**: Kontak sah dianggap benturan (false positive) atau penetrasi furnitur-kolom diabaikan (false negative).

### TC-10: Eksekusi Compensating Rollback Opsi 1A pada Transaksi Bertahap
- **Deskripsi**: Menguji pembatalan kompensasi bertahap saat transaksi ke-3 dalam satu rangkaian tugas gagal.
- **Input**:
  - Langkah 1: Buat Dinding A (Sukses, committed, dicatat di Operation Journal sebagai `CREATED`).
  - Langkah 2: Modifikasi Dinding B (Sukses, committed, dicatat di Operation Journal sebagai `MODIFIED`).
  - Langkah 3: Buat Jendela dengan parameter korup (Gagal di preflight, commit ditolak).
- **Kriteria Keberhasilan (Pass)**:
  - Agen membaca jurnal operasi dan menjalankan *Compensating Rollback*:
    * Dinding A dihapus melalui `doc.Delete()`.
    * Dinding B dikembalikan ke parameter aslinya melalui snapshot sebelum modifikasi.
  - Laporan status secara jujur menyatakan: *"Operasi dibatalkan via Compensating Rollback (Opsi 1A)"*, BUKAN mengklaim transaksi atomic engine.
- **Kondisi Gagal (Fail)**: Dinding B dihapus fisik padahal hanya dimodifikasi (*blind delete*), atau agen mengklaim terjadi "Atomic Transaction Rollback" di level API.

### TC-11: Klasifikasi Lingkup Tugas Multi-View (*Task Scope Classifier*)
- **Deskripsi**: Menguji penentuan sudut pandang manifest berdasarkan tipe tugas pemodelan.
- **Input**: Perintah pemodelan gedung lengkap (Full Building Generation).
- **Kriteria Keberhasilan (Pass)**:
  - Modul klasifikasi menetapkan `TaskScope.FullBuilding`.
  - Manifes mewajibkan 9 sudut pandang lengkap (Front, Rear, Left, Right, Top/Roof, Floor Plan, Section, 3D Iso, Interior Key).
- **Kondisi Gagal (Fail)**: Pemodelan gedung penuh hanya menghasilkan 1 view isometrik 3D.

### TC-12: Bypass Klasifikasi untuk Tugas Ringan (*Task Scope Classifier Bypass*)
- **Deskripsi**: Meminta perubahan minor (misal: mengganti visibilitas link CAD atau merotasi view di Sheet).
- **Input**: Perintah "Sembunyikan layer CAD di View Denah Lt 1".
- **Kriteria Keberhasilan (Pass)**:
  - Sistem mengklasifikasikan sebagai `TaskScope.LightweightSheetOrLink`.
  - Operasi diselesaikan dalam 1 langkah tanpa memicu siklus 9-view manifest gedung penuh.
- **Kondisi Gagal (Fail)**: Perubahan layer CAD memicu pembukaan 9 kamera dan pembuatan laporan visual penuh.

### TC-13: Penegakan Format Baku 5 Kolom Pelat Sambungan Baja
- **Deskripsi**: Membuat schedule pelat sambungan baja (`OST_StructConnectionPlates`).
- **Input**: Model baja dengan berbagai macam connection plates di berbagai zona.
- **Kriteria Keberhasilan (Pass)**:
  - Schedule yang dihasilkan memiliki **tepat 5 kolom terlihat** (*unhidden*):
    `Lokasi | Tipe Sambungan | Thickness | Qty | Berat`
  - Kolom pertama (Kolom A) adalah `Lokasi`.
  - Grand total (Count dan Weight) tercentang aktif.
- **Kondisi Gagal (Fail)**: Ada kolom tambahan yang tidak disembunyikan (misal: Mark, Comments) atau Kolom A bukan Lokasi.

### TC-14: Kalibrasi Volume Eksak Voute Baja (7 Kolom Baku)
- **Deskripsi**: Membuat schedule voute baja (`OST_StructConnectionProfiles`).
- **Input**: Model baja portal dengan haunch/voute pada sambungan rafter-kolom.
- **Kriteria Keberhasilan (Pass)**:
  - Schedule memiliki 7 kolom baku dengan Kolom A = `Lokasi`.
  - Kolom berat dihitung dari **volume solid eksak** dikalikan $7850.0\text{ kg/m}^3$ (bukan panjang balok utuh).
  - Terdapat filter anti-double counting: `Lokasi Equal ""` untuk bangunan utama.
- **Kondisi Gagal (Fail)**: Berat voute dihitung dari perkalian panjang nominal balok utuh atau kolom A bukan Lokasi.

### TC-15: Penguncian Keamanan Pemuatan Modul Hot-Reload (Opsi 4A)
- **Deskripsi**: Menguji pemanggilan fungsi reload DLL (`aigrav_reload_skills`) saat sistem berjalan di lingkungan Produksi.
- **Input**: Pemanggilan reload modul C# saat konfigurasi sistem disetel ke `Environment = Production`.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem menolak eksekusi dan mengembalikan error keamanan: `ERR_HOT_RELOAD_DISABLED_IN_PROD`.
  - Laporan menyatakan biner produksi hanya diperbarui melalui restart proses Revit.
- **Kondisi Gagal (Fail)**: DLL baru dimuat ke domain memori produksi tanpa restart Revit.

### TC-16: Penangkapan Citra Baseline Sebelum Batch (*Pre-Batch Baseline Capture*)
- **Deskripsi**: Menguji kewajiban pengambilan baseline visual sebelum transaksi pertama dimulai.
- **Input**: Memulai batch pemodelan dinding dan bukaan.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem mengekspor citra viewport aktif dengan nama unik berstempel waktu (misal: `BASELINE_20261112_053000.png`).
  - Berkas PNG tersimpan dengan resolusi $\ge 1280\text{ piksel}$ dan valid.
- **Kondisi Gagal (Fail)**: Transaksi dibuka dan elemen dimodelkan sebelum baseline visual tersimpan.

### TC-17: Penangkapan Citra Pasca-Commit Batch (*Post-Batch Capture*)
- **Deskripsi**: Menguji kewajiban pengambilan gambar setelah transaksi batch di-commit.
- **Input**: Menyelesaikan pemodelan elemen Batch 2.
- **Kriteria Keberhasilan (Pass)**:
  - Tangkapan layar pasca-commit tersimpan dan siap dibandingkan dengan citra baseline.
- **Kondisi Gagal (Fail)**: Batch dinyatakan sukses tanpa tangkapan layar pasca-eksekusi.

### TC-18: Penegakan Cakupan Tampilan Relevan (*Applicable View Coverage Manifest*)
- **Deskripsi**: Memvalidasi evaluasi manifest berbasis status tiga keadaan: `PASS`, `FAIL`, dan `NOT_APPLICABLE`.
- **Input**: Tugas pemodelan fasad yang menetapkan 4 view eksterior relevan, dan menandai view interior sebagai `NOT_APPLICABLE` karena berada di luar lingkup pekerjaan eksterior.
- **Kriteria Keberhasilan (Pass)**:
  - Seluruh 4 sudut pandang relevan diekspor dan diverifikasi dengan status `PASS`.
  - View interior dicatat secara eksplisit sebagai `NOT_APPLICABLE` dengan alasan terdokumentasi (`applicability_reason: "Outside exterior facade task scope"`).
  - Status manifest dinilai LULUS karena 100% applicable views berstatus `PASS`.
- **Kondisi Gagal (Fail)**: Sudut pandang relevan dilewati tanpa status, atau view irrelevant dipaksakan diekspor atau ditandai UNCOVERED/FAIL.

### TC-19: Pengakuan Kejujuran View Tak Terbuka & Pemblokiran Selesai (*Uncovered View Protocol*)
- **Deskripsi**: Menstimulasi kegagalan saat kamera mencoba mengambil tampak belakang yang terhalang geometri luar pada sudut pandang yang relevan (*applicable view*).
- **Input**: View tampak belakang relevan tidak dapat diakses atau terhalang total oleh objek lain.
- **Kriteria Keberhasilan (Pass)**:
  - Status sudut pandang relevan tersebut dicatat secara jujur sebagai `UNCOVERED` (subset dari `FAIL`).
  - Sistem **TIDAK MENCATAT STATUS PASS** dan tidak memalsukan citra dengan view lain.
  - Penyelesaian tugas diblokir dengan kode: `BLOCKED_INCOMPLETE_VISUAL_COVERAGE`.
- **Kondisi Gagal (Fail)**: View yang gagal diambil dicatat sebagai PASS, disembunyikan dari laporan telemetri, atau tugas disahkan selesai meskipun ada applicable view yang UNCOVERED.

### TC-20: Ekspor Komprehensif Seluruh Sudut Pandang Manifes
- **Deskripsi**: Memverifikasi bahwa setiap berkas gambar yang dihasilkan tersimpan di direktori artefak resmi.
- **Input**: Eksekusi 9 sudut pandang manifest penuh.
- **Kriteria Keberhasilan (Pass)**:
  - 9 berkas biner PNG tersimpan dengan resolusi $\ge 1280\text{ piksel}$ dan dapat dibuka tanpa korupsi berkas.
- **Kondisi Gagal (Fail)**: Berkas gambar rusak (0 byte) atau tidak dapat dibaca oleh image viewer standar.

### TC-21: Pengungkapan Status Faktual Telemetri (*Reality Disclosure Telemetry*)
- **Deskripsi**: Memeriksa payload JSON hasil eksekusi live capture.
- **Input**: Panggilan tool `aigrav_inspect_model_visually`.
- **Kriteria Keberhasilan (Pass)**:
  - Respons JSON secara transparan mencantumkan: `views_requested`, `views_captured`, `views_uncovered`, dan daftar path berkas nyata.
- **Kondisi Gagal (Fail)**: JSON mengklaim "Seluruh view sempurna" padahal terdapat view yang tidak ter-generate.

### TC-22: Penyusunan Rencana Perbaikan Cacat Visual (*Visual Defect Repair Plan*)
- **Deskripsi**: Menstimulasi terdeteksinya cacat visual (misal: kusen jendela terpasang terbalik atau melayang).
- **Input**: Tangkapan layar menunjukkan elevasi kusen jendela salah.
- **Kriteria Keberhasilan (Pass)**:
  - Agen secara otomatis menyusun `repair_plan` terstruktur yang mencantumkan ID jendela, parameter sill target, dan tindakan korektif.
- **Kondisi Gagal (Fail)**: Cacat visual diabaikan dan tugas dianggap selesai.

### TC-23: Pengambilan Ulang Wajib Pasca-Koreksi (*Mandatory Re-Capture*)
- **Deskripsi**: Menguji kewajiban pengambilan ulang gambar setelah rencana perbaikan dieksekusi.
- **Input**: Perbaikan parameter elevasi jendela selesai di-commit.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem secara otomatis mengambil ulang (*re-capture*) seluruh sudut pandang yang terdampak oleh perbaikan jendela tersebut.
- **Kondisi Gagal (Fail)**: Perbaikan dilakukan tetapi gambar lama yang cacat tetap digunakan sebagai bukti akhir.

### TC-24: Pembatasan Siklus Perbaikan Otomatis Maksimal 2 Iterasi
- **Deskripsi**: Menstimulasi kondisi cacat visual yang berulang dan tidak dapat diselesaikan secara otomatis.
- **Input**: Geometri dinding rusak yang gagal diperbaiki setelah 2 kali siklus repair plan.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem menghentikan loop perbaikan pada iterasi ke-2 (*Fail-Safe Stop*).
  - Sistem menerbitkan laporan eskalasi darurat kepada pengguna manusia untuk meminta arahan manual.
- **Kondisi Gagal (Fail)**: Sistem terjebak dalam perulangan tak terbatas (*infinite loop*) mencoba memperbaiki model.

### TC-25: Penolakan Citra Buram atau Rusak (*Visual Clarity Gate Rejection*)
- **Deskripsi**: Menstimulasi hasil render/capture yang menghasilkan berkas kosong (blank) atau piksel buram.
- **Input**: Berkas capture bernilai 0 byte atau piksel korup.
- **Kriteria Keberhasilan (Pass)**:
  - Pemeriksaan kelayakan visual menolak berkas tersebut dengan status `REJECT_IMAGE_CORRUPT`.
  - Sistem mencoba mengambil ulang view dengan setting pencahayaan/viewport standar.
- **Kondisi Gagal (Fail)**: Berkas gambar rusak atau kosong disahkan sebagai bukti visual.

### TC-26: Pemeriksaan Ruang Tertutup Interior (*Interior View Seal Check*)
- **Deskripsi**: Memeriksa perspektif interior pada ruang utama bangunan.
- **Input**: Kamera interior diarahkan ke sudut pertemuan dinding dan atap.
- **Kriteria Keberhasilan (Pass)**:
  - Citra memvalidasi bahwa ruang tertutup bersih tanpa ada lubang tembus pandang ke latar langit luar akibat dinding yang tidak menempel atap.
- **Kondisi Gagal (Fail)**: Celah dinding bolong pada interior diabaikan oleh validator visual.

### TC-27: Pemblokiran Penyelesaian Tugas Jika Manifes Belum Lengkap
- **Deskripsi**: Mencoba menyelesaikan tugas ketika masih ada sudut pandang wajib yang berstatus `UNCOVERED`.
- **Input**: Pengguna meminta status final, namun tampak kiri belum berhasil diambil gambarnya.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem menolak status selesai dan mengembalikan status: `BLOCKED_INCOMPLETE_VISUAL_COVERAGE`.
  - Laporan menyatakan tugas belum selesai karena bukti visual tampak kiri belum diverifikasi.
- **Kondisi Gagal (Fail)**: Tugas disahkan selesai meskipun bukti visual cakupan belum lengkap.

### TC-28: Pembuatan View Lokal Batch 2 atau Pemblokiran UNCOVERED (POC-VIEW-01 & POC-VIEW-02)
- **Deskripsi**: Menguji pembuatan view lokal (Plan, Elevation, 3D terfokus) di Batch 2 sebelum mesin full 9-view Batch 4A tersedia, divalidasi via `POC-VIEW-01` (BBox Section Transform) dan `POC-VIEW-02` (ElevationMarker Orientation).
- **Input**: Operasi pembuatan pintu/jendela meminta validasi visual lokal.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem membuat atau menggunakan view lokal terfokus pada dinding host (`ViewPlan.Create`, `ViewSection.CreateSection`, sekuens `ElevationMarker.CreateElevationMarker` [static factory] + `marker.CreateElevation` [instance method]). *Catatan: `ViewSection.CreateDetail` dilarang sebagai pembuat potongan atau tampak.*
  - Jika view lokal tidak dapat dibuat atau transform BBox invalid, operasi ditandai `UNCOVERED` dan batch diblokir selesai.
- **Kondisi Gagal (Fail)**: Batch 2 mengklaim validasi visual lulus tanpa bukti view lokal yang terverifikasi, atau memanggil metode statis `ElevationMarker.CreateElevation` yang tidak ada.

---

### TC-29: Konversi Satuan Kemiringan Atap (Derajat ke Rasio Rise/Run — POC-ROOF-01)
- **Deskripsi**: Menguji konversi sudut kemiringan atap derajat ke rasio rise/run pada `FootPrintRoof.SlopeAngle` via `POC-ROOF-01`.
- **Input**: Input kemiringan $30^\circ$ pada pembuatan atap footprint.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem mengonversi sudut via formula: $\text{slopeRatio} = \tan(30 \times \pi / 180) \approx 0.57735$.
  - Nilai yang disetel ke kurva atap adalah rasio $\approx 0.57735$, bukan angka 30 mentah.
  - Geometri atap yang terbentuk memiliki sudut kemiringan fisik tepat $30^\circ$ ($\pm 0.1^\circ$).
- **Kondisi Gagal (Fail)**: Sistem memasukkan angka 30 langsung ke API sehingga menghasilkan rasio 30:1 (atap miring vertikal ekstrem setara $88^\circ$).

### TC-30: Penolakan Ruang Tidak Tertutup & Rollback Transaksi Lokal
- **Deskripsi**: Menstimulasi pemanggilan `Document.NewRoom(Level, UV)` pada titik koordinat yang terbuka / bocor (tidak dikelilingi elemen *room-bounding* tertutup).
- **Input**: Titik UV di luar batas dinding atau di dalam ruangan dengan celah terbuka $> 300\text{ mm}$.
- **Kriteria Keberhasilan (Pass)**:
  - Metode mengevaluasi `room.GetBoundarySegments(new SpatialElementBoundaryOptions())`.
  - Karena sirkuit batas null atau kosong (area $= 0$), transaksi lokal **DI-ROLLBACK SECARA BERSIH**.
  - Mengembalikan error terstruktur: `ERR_ROOM_NOT_ENCLOSED`.
  - Tidak ada elemen Room berstatus `Not Enclosed` yang tertinggal di dokumen Revit.
- **Kondisi Gagal (Fail)**: Transaksi di-commit dan sistem melaporkan sukses hanya karena `NewRoom` mengembalikan objek non-null.

### TC-31: Entri Jurnal DELETED Berstatus Baku NON_COMPENSABLE
- **Deskripsi**: Menguji pencatatan operasi penghapusan elemen eksisting ke dalam *Operation Journal*.
- **Input**: Operasi penghapusan elemen balok eksisting proyek.
- **Kriteria Keberhasilan (Pass)**:
  - Jurnal mencatat mutasi dengan status `DELETED`.
  - Status kompensasi secara default disetel ke `compensability: "NON_COMPENSABLE"`.
  - Sistem menolak klaim bahwa elemen yang dihapus dapat dipulihkan secara generik tanpa kehilangan referensi ID, relasi host, join, dan dimensi.
  - Jika rollback diminta pada batch ini, sistem menghentikan auto-rollback dan meminta konfirmasi manusia.
- **Kondisi Gagal (Fail)**: Elemen `DELETED` diklaim sebagai `COMPENSABLE` penuh tanpa handler rekonstruksi relasi yang teruji.

### TC-32: Deteksi Efek Samping Kaskade pada Deletion Kompensasi (*Cascade Deletion Guard*)
- **Deskripsi**: Menguji eksekusi *Compensating Rollback* pada elemen baru `CREATED` yang memiliki elemen dependen tidak terduga, mencakup 4 kelas elemen terproteksi: (1) elemen dependen yang dipin pengguna (`element.Pinned == true`), (2) elemen dependen pre-existing pra-batch, (3) elemen dependen yang dimodifikasi pengguna pasca-batch, dan (4) elemen dengan kepemilikan tidak dikenal (*unknown ownership* / missing `AIGRAV_LOGICAL_ID`).
- **Input**: Transaksi kompensasi menghapus dinding baru yang secara tidak sengaja terikat dimensi atau join dengan elemen terproteksi / unknown.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem melakukan simulasi dry-run dalam `SubTransaction` terisolasi dan menginspeksi **seluruh kaskade kembalian** `doc.Delete(createdId)`.
  - Melakukan evaluasi set terhadap seluruh ID hasil kaskade:
    * Tidak boleh beririsan dengan elemen pre-existing (`deletedSet.Overlaps(preExistingSet) == false`).
    * Tidak boleh mencakup elemen dependen yang dipin (`element.Pinned == true`).
    * Tidak boleh mencakup elemen dependen yang telah dimodifikasi pengguna.
    * Tidak boleh mencakup elemen dengan kepemilikan tidak dikenal (*unknown ownership*).
  - Jika terdeteksi satu saja elemen terproteksi atau tidak dikenal dalam kaskade, sistem memicu **penolakan aman (*safe rejection*)**, membatalkan subtransaksi (`subTrans.RollBack()`), dan memblokir penghapusan riil.
  - Sistem menerbitkan peringatan kritis: `ERR_COMPENSATION_CASCADED_DELETION` dan eskalasi ke intervensi manual pengguna.
- **Kondisi Gagal (Fail)**: Elemen eksisting, elemen pinned, elemen user-modified, atau elemen unknown ownership ikut terhapus secara senyap (*silent cascaded loss*) saat penghapusan kompensasi dijalankan.

### TC-33: Penanganan Aman Jendela Tanpa Parameter Sill Standar (*Window Sill Robustness*)
- **Deskripsi**: Memuat family jendela pihak ketiga kustom yang tidak memiliki parameter bawaan `INSTANCE_SILL_HEIGHT_PARAM` atau parameternya berstatus read-only.
- **Input**: Pemasangan jendela kustom pada dinding host.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem memeriksa keberadaan parameter sill, sifat `IsReadOnly`, dan tipe penyimpanan (*StorageType.Double*).
  - Jika parameter standar tidak ditemukan, sistem memeriksa *family parameter mapping* alternatif atau menolak penempatan dengan pesan terstruktur `ERR_SILL_PARAMETER_UNAVAILABLE`.
  - Konversi milimeter ke kaki internal (`feet = mm / 304.8`) diterapkan secara presisi.
- **Kondisi Gagal (Fail)**: Bridge crash akibat `NullReferenceException` saat mencoba mengakses parameter yang tidak ada.

### TC-34: Validasi Deteksi Citra Kosong / Frame Buta (*Blank Frame Rejection*)
- **Deskripsi**: Menstimulasi ekspor gambar viewport yang menghasilkan citra kosong putih atau hitam pekat, tetapi memiliki ukuran berkas besar akibat noise kompresi.
- **Input**: Berkas PNG berukuran 45 KB yang 99% pikselnya bernilai monokrom murni `#FFFFFF` atau `#000000`.
- **Kriteria Keberhasilan (Pass)**:
  - Pemeriksaan kualitas citra mendeteksi *blank/black frame defect*.
  - View ditandai sebagai `FAIL_BLANK_IMAGE` dan tidak disahkan.
  - Gambar garis denah sederhana yang valid berukuran kecil (misal 15 KB) tetap diterima jika pikselnya menunjukkan geometri valid.
- **Kondisi Gagal (Fail)**: Citra kosong disahkan hanya karena ukuran berkasnya $> 20\text{ KB}$, atau gambar denah valid ditolak hanya karena $< 20\text{ KB}$.

### TC-35: Verifikasi Visibilitas Elemen Target pada Manifes Tampilan
- **Deskripsi**: Memverifikasi bahwa setiap sudut pandang dalam *View Coverage Manifest* benar-benar menampilkan elemen target yang dimodelkan, bukan sekadar mengekspor sembarang view.
- **Input**: Pemodelan fasad depan; kamera diarahkan ke tampak belakang sehingga dinding depan terhalang.
- **Kriteria Keberhasilan (Pass)**:
  - `ViewCoverageCoordinator` melakukan uji frustum dan oklusi terhadap `target_element_ids`.
  - Karena dinding target terhalang secara total, view V-01 ditandai sebagai `UNCOVERED_OCCLUDED` dan batch pemodelan diblokir.
- **Kondisi Gagal (Fail)**: View disahkan sebagai `PASS` hanya karena jumlah berkas gambar mencapai 9 buah tanpa memeriksa isi objek di dalamnya.

### TC-36: Pembersihan Aman View Temporer Tanpa Merusak View Pengguna (*View Cleanup Integrity*)
- **Deskripsi**: Memverifikasi siklus pembersihan `ViewCleanupManager` setelah ekspor visual selesai.
- **Input**: Dokumen proyek yang memiliki view asli pengguna bernama `"Tampak Depan Arsitek"` dan view temporer tool bernama `"__AIGRAV_TEMP_VIEW_FRONT_39a1"`.
- **Kriteria Keberhasilan (Pass)**:
  - `ViewCleanupManager` hanya menghapus view yang memiliki metadata awalan deterministik `__AIGRAV_TEMP_VIEW_*` dan marker elevasi temporernya.
  - View asli pengguna `"Tampak Depan Arsitek"` **TIDAK TERSENTUH, TIDAK DIUBAH, DAN TIDAK TERHAPUS**.
- **Kondisi Gagal (Fail)**: View asli pengguna terhapus atau tertimpa oleh generator tampilan tool.

### TC-37: Penegakan Otorisasi Server-Side untuk Generic Model (*Security Provenance Gate*)
- **Deskripsi**: Menguji upaya pembuatan elemen Generic Model dengan cara memanipulasi parameter teks `AIGRAV_PROVENANCE` dari sisi klien tanpa izin tool eksplisit.
- **Input**: Permintaan klien mengirimkan payload pembuatan Generic Model untuk pintu dengan teks parameter `AIGRAV_PROVENANCE = "INTENTIONAL_GENERIC_MODEL"`.
- **Kriteria Keberhasilan (Pass)**:
  - Server Bridge memvalidasi maksud tugas (*Task Scope & Intent*) secara independen pada logika server-side C#.
  - Karena semantik target adalah pintu (`OST_Doors`), server menolak permintaan tersebut dengan error `ERR_FORBIDDEN_GENERIC_MODEL_SUBSTITUTION`.
  - Manipulasi parameter klien tidak dapat membobol gerbang keamanan arsitektur.
- **Kondisi Gagal (Fail)**: Pembuatan Generic Model diizinkan hanya karena parameter teks yang dikirim klien mencantumkan kata kunci persetujuan.

---

### TC-38: Penegakan Urutan Dependensi Pra-Syarat Grafis (*Dependency Graph Pre-Requisite Enforcement*)
- **Deskripsi**: Menguji orkestrator dalam memblokir eksekusi pemodelan elemen hilir (*downstream*) apabila elemen prasyarat hulu (*upstream*) gagal divalidasi.
- **Input**: Permintaan memodelkan Dinding Selubung Lantai 2 (Stage 2) saat Kolom Struktur Lantai 1 (Stage 1) gagal dibuat atau terbukti melayang (*floating*).
- **Kriteria Keberhasilan (Pass)**:
  - Sistem memeriksa status validasi Stage 1 pada dependency graph.
  - Karena Stage 1 belum lulus validasi 4-lapisan, eksekusi Stage 2 **DIBLOKIR TOTAL** dengan kode error `ERR_DEPENDENCY_PREREQUISITE_FAILED`.
  - Tidak ada dinding atau balok lantai atas yang ditempatkan di atas ruang kosong.
- **Kondisi Gagal (Fail)**: Sistem tetap mengeksekusi pemodelan dinding lantai 2 di ruang hampa mengabaikan kegagalan struktur bawah.

### TC-39: Konsistensi Irama Fasad dengan Toleransi Terkonfigurasi (*Design-Driven Façade Rhythm*)
- **Deskripsi**: Menguji evaluasi arsitektural terhadap jarak antar-as bukaan atau modul curtain wall berdasarkan konfigurasi toleransi sistem fasad (`FacadeRhythmToleranceConfig`), bukan toleransi kaku universal.
- **Input 1 (Unitized Curtain Wall)**: Target modul $1500\text{ mm}$, konfigurasi toleransi $\pm 3.0\text{ mm}$. Salah satu panel dipasang $1508\text{ mm}$ ($+8\text{ mm}$ deviasi).
- **Input 2 (Punched Masonry)**: Target modul $1800\text{ mm}$, konfigurasi toleransi pasangan bata $\pm 12.0\text{ mm}$. Jendela dipasang $1808\text{ mm}$ ($+8\text{ mm}$ deviasi).
- **Kriteria Keberhasilan (Pass)**:
  - Input 1: Layer C mendeteksi pelanggaran toleransi unitized curtain wall ($8\text{ mm} > 3\text{ mm}$) $\rightarrow$ Memicu status `IRREGULAR_FACADE_RHYTHM_DETECTED` dan repair plan.
  - Input 2: Layer C meloloskan jendela pasangan bata ($8\text{ mm} \le 12\text{ mm}$) $\rightarrow$ Status `PASS` karena berada dalam rentang toleransi bata yang sah.
- **Kondisi Gagal (Fail)**: Sistem memaksakan toleransi tunggal kaku (misal $\pm 5\text{ mm}$) pada seluruh tipe konstruksi sehingga meloloskan panel kaca presisi yang cacat atau menolak pasangan bata yang sah.

### TC-40: Artikulasi Transisi Vertikal Fasad Berbasis Tipologi (*Conditional Vertical Transition*)
- **Deskripsi**: Menguji evaluasi transisi vertikal arsitektur agar hanya mewajibkan artikulasi tripartit (*Podium-Tower-Crown*) pada tipologi gedung tinggi (mengacu klasifikasi yuridis PP No. 16 Tahun 2021, Pasal 9 ayat (6), jo. Lampiran bagian 5 — Ketinggian Bangunan Gedung: Pasal 9 ayat (6) menetapkan kelompok klasifikasi tingkat tinggi dan Lampiran bagian 5 menetapkan ambang batas jumlah lantai eksak $> 8$ lantai, terpisah dari parameter uji arsitektural terkonfigurasi `ProjectConfig.HighRiseThresholdFloors = 8`; serta membedakan definisi IBC 2024 Section 202 [definisi] dari Section 403.1 [applicability] sebagai rujukan komparatif), dan menerapkan evaluasi kontekstual pada tipologi lain.
- **Input 1 (High-Rise Tower 20 Lantai)**: Model menara kantor 20 lantai diuji kelengkapan artikulasinya.
- **Input 2 (Residential Type 36)**: Model rumah tapak sederhana 1 lantai diuji terhadap aturan transisi vertikal.
- **Kriteria Keberhasilan (Pass)**:
  - Input 1 (Gedung Tinggi): Validator mewajibkan batas visual podium lantai 1–3, repetisi badan menara lantai 4–19, dan penutup mahkota/crown di lantai 20/atap. Hasil: `PASS`.
  - Input 2 (Hunian Type 36): Aturan podium-tower-crown dinilai `NOT_APPLICABLE` (dengan catatan: `"Single-story residential has no tower podium or crown"`). Validator beralih menguji elevasi lantai plinth ($+0.25\text{ m}$) dan teritisan atap ($800\text{ mm}$). Hasil: `PASS`.
- **Kondisi Gagal (Fail)**: Sistem menolak Hunian Type 36 karena tidak memiliki podium atau mahkota menara (*false positive failure*).

### TC-41: Integritas Semantik Disiplin MEP & Pemisahan Modul (*MEP Functional Family Integrity & Routing Separation*)
- **Deskripsi**: Menguji pembuatan elemen utilitas mekanikal dan plumbing pada Stage 6 via `MepRoutingCreator.cs` dan validasi semantik via `MEPDisciplineValidator.cs` agar tidak menggunakan Generic Model atau DirectShape.
- **Input**: Pemodelan jalur supply air ducting dan pipa pembuangan air kotor.
- **Kriteria Keberhasilan (Pass)**:
  - Saluran udara dibuat via `MepRoutingCreator.cs` dengan kategori resmi `OST_DuctCurves` (`DuctType`).
  - Pipa plumbing dibuat via `MepRoutingCreator.cs` dengan kategori resmi `OST_PipeCurves` (`PipeType`) dan terhubung pada konektor fitting yang valid.
  - `MEPDisciplineValidator.cs` memvalidasi rute berada di dalam plenum plafon dengan tinggi bebas tidak menembus batas bawah plafon arsitektur serta ketiadaan penetrasi benturan keras terhadap struktur (*zero hard clash*).
- **Kondisi Gagal (Fail)**: Saluran ducting atau pipa dibuat menggunakan Generic Model sweep atau DirectShape solid cylinder, atau validasi clash dilewati.

### TC-42: Revalidasi Dependensi Terdampak Pasca-Perbaikan (*Downstream Dependency Revalidation*)
- **Deskripsi**: Menguji orkestrator dalam memicu audit ulang atas elemen-elemen anak ketika elemen induknya mengalami pergeseran atau modifikasi.
- **Input**: Posisi dinding host digeser sejauh $500\text{ mm}$ ke arah sumbu X untuk memperbaiki dimensi ruang tidur.
- **Kriteria Keberhasilan (Pass)**:
  - Pintu dan jendela yang menempel pada dinding host tersebut diverifikasi ulang keterikatannya (`instance.Host.Id == wall.Id`).
  - Elemen `Room` yang dibatasi dinding tersebut dievaluasi ulang batas spasialnya (`GetBoundarySegments`) untuk memastikan tidak terjadi kebocoran ruang pasca-pergeseran dinding.
- **Kondisi Gagal (Fail)**: Dinding digeser namun jendela tertinggal melayang di koordinat lama atau ruang arsitektural menjadi Not Enclosed tanpa terdeteksi.

### TC-43: Validasi Tinggi Bebas Berbasis Tipologi & Fungsi Ruang (*Typology & Room-Function Clearance Matrix*)
- **Deskripsi**: Menguji evaluasi tinggi bebas (*headroom & clearance*) menggunakan matriks fungsi ruang dan tipologi bangunan (`ClearanceRequirementMatrix`), bukan angka seragam.
- **Input 1 (Hunian Type 36 Kamar Mandi)**: Plafon kamar mandi terpasang pada ketinggian $2.15\text{ m}$.
- **Input 2 (Kantor Komersial Open Plan)**: Plafon ruang kerja terpasang pada ketinggian $2.15\text{ m}$.
- **Input 3 (Tangga Darurat)**: Balok melintang menyisakan tinggi bebas vertikal $1.90\text{ m}$ dari nosing tangga.
- **Kriteria Keberhasilan (Pass)**:
  - Input 1 (KM Type 36): Lulus (`PASS`) karena batas minimum ruang servis/KM adalah $2.10\text{ m}$ (Permen PU No. 29/PRT/M/2006 `UNVERIFIED_STANDARD_REFERENCE` [memerlukan konfirmasi Perda/spesifikasi proyek]).
  - Input 2 (Kantor Open Plan): Ditolak (`FAIL`) dengan temuan `INSUFFICIENT_OFFICE_HEADROOM_CLEARANCE` karena batas minimum area kerja kantor adalah $2.70\text{ m}$.
  - Input 3 (Tangga): Ditolak (`FAIL`) dengan temuan `STAIR_HEADROOM_VIOLATION` karena standar IBC 2024 Section 1011.3 mewajibkan minimal $80\text{ in} \approx 2.032\text{ m}$ vertikal dari nosing.
- **Kondisi Gagal (Fail)**: Sistem menerapkan satu angka kaku (misal $2.20\text{ m}$) pada seluruh ruang sehingga menolak kamar mandi yang sah atau meloloskan ruang kantor yang sempit.

### TC-44: Penegakan Gerbang Kualitas 4-Lapisan Mandiri (*4-Layer Quality Gate Enforcement*)
- **Deskripsi**: Menguji kondisi di mana suatu elemen sukses dibuat secara API (Lapisan A dan B lolos), namun gagal pada Lapisan C (estetika/arsitektur) atau Lapisan D (visual live capture).
- **Input**: Pembuatan jendela kaca ber-host dinding sukses menghasilkan `ElementId` valid, namun material kaca yang diterapkan berwarna hijau buram pejal (Transparansi $= 0\%$, menembus batas privasi fasad).
- **Kriteria Keberhasilan (Pass)**:
  - Lapisan A (Semantik) dan Lapisan B (Geometri) mencatat PASS.
  - Lapisan C / D mendeteksi kegagalan transparansi kaca ($< 50\%$) dan memicu status `REJECT_MATERIAL_OPACITY_DEFECT`.
  - Sistem membatalkan pengesahan elemen dan menerbitkan repair plan penggantian material ke `Glass - Clear Transparent`.
- **Kondisi Gagal (Fail)**: Elemen disahkan hanya karena ID elemen berhasil diterbitkan oleh Revit DB kernel.

### TC-45: Seleksi Tahap Dinamis Tanpa Pemaksaan Tahap Irrelevan (*Dynamic Stage Selection*)
- **Deskripsi**: Menguji kemampuan sistem dalam mengeksekusi tugas lingkup terbatas (*partial scope task*) tanpa memaksakan eksekusi seluruh 8 tahap.
- **Input**: Pengguna memberikan instruksi: *"Tambahkan sekat partisi gypsum dan 1 pintu panel di ruang rapat Lantai 2"*.
- **Kriteria Keberhasilan (Pass)**:
  - Sistem mengidentifikasi lingkup tugas dan hanya mengaktifkan **Stage 3 (Bukaan) dan Stage 5 (Interior)**.
  - Sistem memvalidasi dependensi dinding host dan level dari data model eksisting tanpa memicu pembuatan ulang Level, Grid, Pondasi, atau Atap.
  - Tahap 1, 2, 4, dan 6 ditandai status `SKIPPED_NOT_IN_SCOPE` secara teratur.
- **Kondisi Gagal (Fail)**: Sistem memaksakan eksekusi ulang dari Stage 0 hingga Stage 7 sehingga merusak atau menimpa elemen eksisting yang sudah ada di model.

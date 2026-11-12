# AIGRAV Unverified Items & User Decision Checkpoints

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Tujuan Dokumen:** Menginventarisasi asumsi teknis lingkungan runtime yang belum terverifikasi secara statis, mendokumentasikan alternatif arsitektural, serta mencatat secara resmi seluruh keputusan pengguna (*Approved User Decisions*) yang menjadi acuan desain tata kelola menuju Fase 3.

---

## 1. Inventarisasi Item Belum Terverifikasi (*Unverified Items Inventory*)

Berikut adalah aspek teknis lingkungan runtime yang memerlukan verifikasi pada saat uji coba di stasiun kerja aktif:

| ID | Asumsi / Komponen Teknis | Kondisi Saat Ini di Repositori | Kebutuhan Verifikasi Runtime | Risiko Jika Tidak Diverifikasi |
| :---: | :--- | :--- | :--- | :--- |
| **U-01** | **Interaksi Add-In Pihak Ketiga di Revit 2026** | AIGRAV mengimplementasikan antrean tunggal FIFO di `AIGRAVRequestManager.cs`. | Periksa apakah ada Add-In pihak ketiga (Navisworks exporter, Enscape, dll.) yang memasang event handler pada DocumentChanged. | Potensi perlambatan transaksi atau deadlock di luar antrean AIGRAV. |
| **U-02** | **Ketersediaan Family Library di Komputer Klien** | Aturan merujuk ke `C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\`. | Konfirmasi keberadaan folder library default Autodesk pada stasiun kerja aktif. | Pemuatan family otomatis (`doc.LoadFamily`) akan gagal jika paket konten belum diunduh. |
| **U-03** | **Skema Klasifikasi Shared Parameters Proyek** | Parameter `Lokasi` dan `Tipe Sambungan` digunakan pada schedule baja. | Verifikasi apakah file `RevitSharedParameters.txt` resmi proyek telah terikat ke template dokumen aktif. | Kolom schedule akan kosong jika shared parameter belum di-bind ke kategori terkait. |
| **U-04** | **Konfigurasi Hak Akses Direktori Biner (ACL)** | Folder `C:\AIGRAV\Skills\` diasumsikan memiliki izin tulis terbatas. | Verifikasi izin Windows NTFS pada folder produksi. | Risiko keamanan jika folder dapat ditulis oleh akun non-administrator. |

---

## 2. Alternatif Arsitektural & Hasil Keputusan (*Architectural Decisions*)

### Alternatif A: Kebijakan Transaksi Milestone
- **Opsi 1A (DISETUJUI):** Pertahankan transaksi per-tool mandiri dengan *Operation Journal* dan *Compensating Rollback* berbasis Element ID.
  - **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
  - *Catatan Penting:* Compensating rollback **TIDAK BOLEH DISEBUT SEBAGAI ATOMIC ROLLBACK** karena setiap tool meng-commit perubahan secara lokal dan meninggalkan jejak transaksi individual pada riwayat Undo Revit.
- **Opsi 1B (DITUNDA):** Macro TransactionGroup per milestone.
  - *Status:* Ditunda sebagai target pengembangan jangka panjang masa depan (*future milestone development target*), bukan untuk implementasi sekarang.

### Alternatif B: Waktu Penggantian DirectShape pada Modul Sambungan Baja
- **Opsi 2A (DISETUJUI):** Terapkan *Controlled-Exception Policy* pada Fase 3 untuk mempertahankan fungsi sambungan baja aktif pada call sites `ConnectionGeneratorSkill.cs` yang ada, sembari merumuskan peta jalan migrasi ke native family pada Fase 4.
  - **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
- **Opsi 2B (DITOLAK):** Penggantian terburu-buru yang berisiko merusak modul sambungan baja aktif tanpa family pengganti yang teruji.

---

## 3. Catatan Keputusan Resmi Pengguna (*Approved User Decisions*)

Seluruh checkpoint telah mendapatkan persetujuan eksplisit dari Pengguna (*User Approved*):

### 📌 Checkpoint 1: Arsitektur Transaksi
- **Keputusan:** **APPROVED — Opsi 1A**
- **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
- **Ketentuan Implementasi:**
  1. Pertahankan transaksi per-tool mandiri untuk tahap sekarang.
  2. Implementasikan *Operation Journal* yang mencatat seluruh Element ID yang dibuat/dimodifikasi per langkah pemodelan.
  3. Implementasikan *Compensating Rollback* (penghapusan elemen hasil kreasi jika terjadi kegagalan atau pembatalan di tengah proses).
  4. **Larangan Wording:** Dilarang menyebut compensating rollback sebagai *atomic rollback*.
  5. `TransactionGroup` per milestone dicatat murni sebagai target riset/pengembangan masa depan, bukan implementasi sekarang.

### 📌 Checkpoint 2: Kebijakan DirectShape Sambungan Baja
- **Keputusan:** **APPROVED — Opsi 2A**
- **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
- **Ketentuan Implementasi:**
  1. Pertahankan DirectShape **HANYA** pada call sites eksisting di `ConnectionGeneratorSkill.cs` yang telah teridentifikasi (baris 91, 109, 140).
  2. Dilarang keras menambah call site DirectShape baru tanpa persetujuan pengecualian eksplisit (*explicit exception approval*).
  3. Dilarang keras menggunakan Generic Model maupun DirectShape sebagai fallback untuk pintu, jendela, dinding, lantai, atap, serta ruang.
  4. Pertahankan audit QTO solid volume dan logging telemetri provenance untuk seluruh elemen pengecualian terkendali.
  5. Tetapkan roadmap migrasi bertahap menuju native loadable family atau representasi BIM resmi pada Fase 4.

### 📌 Checkpoint 3: Prioritas Implementasi Endpoint Native C# Baru
- **Keputusan:** **APPROVED dengan urutan prioritas:**
  1. **Prioritas 1:** `aigrav_create_door` dan `aigrav_create_window`
  2. **Prioritas 2:** `aigrav_create_roof`
  3. **Prioritas 3:** `aigrav_create_room`
- **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
- **Ketentuan Implementasi:**
  1. Endpoint Door dan Window **WAJIB** menggunakan pemanggilan `FamilyInstance` yang *wall-hosted* (terikat pada dinding host).
  2. Seluruh endpoint wajib memiliki validasi parameter (*parameter validation*), validasi kategori (*category validation*), validasi host (*host validation*), dan respons error terstruktur (*structured error response*).
  3. Dilarang melakukan pengalihan diam-diam (*silent fallback*) ke Generic Model atau DirectShape jika pembuatan native gagal.

### 📌 Checkpoint 4: Kebijakan Keamanan Hot-Reload DLL
- **Keputusan:** **APPROVED — Opsi 4A**
- **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
- **Ketentuan Implementasi:**
  1. Nonaktifkan DLL hot-reload secara permanen pada lingkungan produksi (*Production Mode*).
  2. Pembaruan DLL produksi hanya dilakukan melalui restart aplikasi Autodesk Revit.
  3. Mode pengembangan (*Development Mode*) diperbolehkan mempertahankan hot-reload dengan kontrol keamanan ketat (prompt persetujuan interaktif pengguna, verifikasi hash SHA-256, dan ACL direktori).

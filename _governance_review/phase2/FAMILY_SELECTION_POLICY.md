# AIGRAV Family Selection & Resolution Policy (Revised Specification)

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan kebijakan pemilihan family fungsional (*Functional Family Selection*), 5 tangga keputusan resolusi family (*Family Selection Ladder*), protokol penanganan family tidak ditemukan (*Missing Compatible Families Protocol*), verifikasi kompatibilitas hosting, dan larangan mutlak fallback otomatis ke Generic Model / DirectShape.

---

## 1. Prinsip Pemilihan Family Berbasis Fungsi Nyata (*Functional Family Selection*)

Setiap objek yang dimodelkan oleh AIGRAV di Autodesk Revit **WAJIB** menggunakan kategori Revit, System Family, Loadable Family, atau Adaptive Component yang secara akurat mencerminkan fungsi fisik sebenarnya di dalam bangunan:

- **Dinding Bangunan** $\longrightarrow$ `OST_Walls` (System Family `WallType` berstruktur komposit multi-layer).
- **Kolom Struktur Portal** $\longrightarrow$ `OST_StructuralColumns` (Loadable Family kolom beton bertulang atau baja Wide Flange/H-Beam). Dilarang menggunakan `OST_Columns` (Arsitektural) untuk elemen pemikul beban.
- **Balok & Rangka Struktur** $\longrightarrow$ `OST_StructuralFraming` (Loadable Family balok girder, joist, atau truss).
- **Pelat Lantai & Dak Beton** $\longrightarrow$ `OST_Floors` (System Family `FloorType` multi-loop donut slab).
- **Selubung & Penutup Atap** $\longrightarrow$ `OST_Roofs` (System Family `RoofType` dengan slope ratio rise/run presisi).
- **Pintu Arsitektural** $\longrightarrow$ `OST_Doors` (Loadable Family wall-hosted dengan void cutout dan swing symbol).
- **Jendela Fasad & Kaca** $\longrightarrow$ `OST_Windows` (Loadable Family wall-hosted dengan sill height terukur dan kaca transparan).
- **Ruang & Zonasi Spasial** $\longrightarrow$ `OST_Rooms` (System Spatial Element tertutup berbatas komputasional).
- **Distribusi Udara HVAC** $\longrightarrow$ Kategori HVAC resmi (`OST_DuctCurves`, `OST_DuctTerminal`, `OST_MechanicalEquipment`).
- **Sistem Perpipaan Plumbing** $\longrightarrow$ `OST_PipeCurves` dan `OST_PlumbingFixtures`.

> [!CRITICAL] LARANGAN MUTLAK FALLBACK KE GENERIC MODEL & DIRECTSHAPE
> `OST_GenericModel` dan `DirectShape` **TIDAK BOLEH PERNAH** menjadi jalan pintas otomatis (*automatic fallback*) atas kegagalan pemodelan native. Jika operasi pemodelan native gagal karena ketiadaan family, ketidaksesuaian host, atau kesalahan geometri, sistem **WAJIB GAGAL SECARA TERSTRUKTUR (FAIL-CLOSED)** dan menghentikan eksekusi, bukan menciptakan objek generik tanpa parameter.

---

## 2. Lima Tangga Resolusi Family (*5-Step Selection Ladder*)

Ketika eksekutor AIGRAV membutuhkan tipe elemen untuk pemodelan di Revit, proses pencarian dan resolusi **WAJIB** menaiki tangga 5 tingkat berikut:

```
[Tingkat 1: Native System Family Type]
  └── Cari tipe yang telah terdefinisi di dokumen aktif via FilteredElementCollector.
  └── Jika tipe spesifik belum ada, duplicate dari tipe basis yang kompatibel (misal: "Basic Wall - 150mm").
  └── JIKA ELEMEN MEMBUTUHKAN LOADABLE FAMILY ──────────────────────────┐
                                                                        ▼
[Tingkat 2: Approved Loadable Family Eksisting di Dokumen]
  └── Cari FamilySymbol aktif di memori model dengan BuiltInCategory & Name yang sesuai.
  └── Verifikasi integritas: symbol.Family.FamilyCategory.Id == expectedCategory.
  └── Aktifkan symbol via symbol.Activate() di dalam transaksi.
  └── JIKA BELUM TERLOAD DI DALAM MODEL ────────────────────────────────┐
                                                                        ▼
[Tingkat 3: Penemuan & Pemuatan Otomatis dari Library Resmi Terinstal]
  └── Cari file .rfa pada repositori library resmi Revit terverifikasi:
      C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\...
  └── Eksekusi doc.LoadFamily(filePath, out Family fam).
  └── Pilih FamilySymbol yang dibutuhkan dan aktifkan.
  └── JIKA TIDAK DITEMUKAN PADA LIBRARY RESMI ──────────────────────────┐
                                                                        ▼
[Tingkat 4: Duplikasi & Modifikasi Tipe Parametrik Sah]
  └── Temukan Family serumpun yang memiliki parameter dimensi terhitung (Width, Height, Web, Flange).
  └── Jalankan symbol.Duplicate(newTypeName) dan setel parameter geometris yang sesuai.
  └── JIKA BENTUK ELEMEN MERUPAKAN GEOMETRI NON-STANDAR BEBAS ──────────┐
                                                                        ▼
[Tingkat 5: Controlled Component (Adaptive Component / Conceptual Massing)]
  └── Gunakan template Adaptive Component dengan titik kontrol berpresisi tinggi (P1, P2, ... Pn).
  └── ATAU bangun Conceptual Mass (OST_Mass) dan terapkan sistem Model By Face (Wall/Roof/Curtain).
  └── JIKA SELURUH 5 TINGKATAN TIDAK TERSEDIA ──────────────────────────┐
                                                                        ▼
[STATUS AKHIR: STRUCTURED FAIL-CLOSED (STOP & ESKALASI)]
  └── Hentikan transaksi secara bersih (Rollback).
  └── Terbitkan laporan terstruktur: ERR_FAMILY_SYMBOL_NOT_FOUND.
  └── Minta pengguna menyetujui penambahan file library eksternal.
```

---

## 3. Protokol Penanganan Family yang Tidak Tersedia (*Missing Compatible Families Protocol*)

Ketika family yang kompatibel tidak ditemukan di model maupun di library resmi terinstal, agen wajib mengeksekusi protokol 3 langkah:

```
                  FAMILY KOMPATIBEL TIDAK DITEMUKAN
                                  │
         ┌────────────────────────┼────────────────────────┐
         │                        │                        │
         ▼                        ▼                        ▼
 [1. Family Discovery]    [2. Approved Creation]   [3. Structured Fail-Closed]
 Telusuri pustaka resmi   Duplikasi tipe atau      Jika opsi 1 & 2 tidak ada:
 & registri konten RVT    buat Adaptive Component  Tolak eksekusi, laporkan
 terinstal di mesin host  resmi dengan approval    kebutuhan RFA ke pengguna
```

1. **Automated Family Discovery:**  
   Pencarian komprehensif pada struktur folder library Revit terinstal (`C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\...`) berdasarkan kategori semantik dan kata kunci profil (misal: mencari di subfolder `Doors\`, `Windows\`, `Structural Columns\`, `Structural Framing\`).
2. **Approved Family Creation:**  
   Jika family dasar sejenis tersedia tetapi dimensi spesifik berbeda, agen diizinkan menduplikasi tipe (`Duplicate`) dan menetapkan parameter dimensi baru. Untuk geometri khusus kurva/organik, gunakan komponen adaptif terverifikasi dengan titik kontrol terukur.
3. **Structured Fail-Closed Response:**  
   Jika tidak ada family yang valid di disk, sistem **DILARANG MEREKAYASA** geometri generik. Sistem wajib mengembalikan respons error terstruktur:
   ```json
   {
     "status": "FAILED",
     "error_code": "ERR_FAMILY_SYMBOL_NOT_FOUND",
     "missing_category": "OST_Windows",
     "required_family": "Fixed Window - Triple Bay",
     "searched_paths": [
       "C:\\ProgramData\\Autodesk\\RVT 2026\\Libraries\\English\\US\\Windows\\"
     ],
     "fallback_prohibited": ["OST_GenericModel", "DirectShape"],
     "action_required": "Harap muat family RFA jendela yang sesuai atau sediakan path library resmi."
   }
   ```

---

## 4. Resolusi Berbasis Semantik vs Asumsi Nama File

- **Pemeriksaan Kategori API Tingkat Rendah:**  
  Nama file (misal: `Window_Special.rfa`) **TIDAK DAPAT DIPERCAYA** sebagai bukti kategori. Kategori wajib diverifikasi langsung melalui properti biner Revit API:
  ```csharp
  if (symbol.Family.FamilyCategory.Id.Value != (long)BuiltInCategory.OST_Windows)
  {
      throw new InvalidOperationException(
          $"Family Integrity Defect: Family '{symbol.Family.Name}' terdaftar di bawah kategori " +
          $"'{symbol.Family.FamilyCategory.Name}', BUKAN 'OST_Windows'. Pemodelan dibatalkan.");
  }
  ```

---

## 5. Matriks Kompatibilitas Tipe Perletakan & Host (*Host Compatibility Matrix*)

| Tipe Hosting Family | Properti `FamilyHostingType` | Prasyarat Dependensi Host | Contoh Komponen | Perilaku Jika Host Belum Ada |
| :--- | :--- | :--- | :--- | :--- |
| **Wall-Hosted** | `FamilyHostingType.Wall` | Wajib ada elemen `Wall` valid dan ter-commit pada level yang sama. | Pintu, Jendela, Exhaust Fan Dinding | **FAIL-CLOSED:** Tolak eksekusi. Minta pemodelan dinding perimeter/partisi terlebih dahulu. |
| **Floor-Hosted** | `FamilyHostingType.Floor` | Wajib ada elemen `Floor` valid dan ter-commit. | Floor Drain, Mesin Lantai, Socket Lantai | **FAIL-CLOSED:** Minta pemodelan pelat lantai terlebih dahulu. |
| **Roof-Hosted** | `FamilyHostingType.Roof` | Wajib ada elemen `Roof` valid dan bergeometri closed loop. | Skylight, Talang Atap, Exhaust Atap | **FAIL-CLOSED:** Minta penyelesaian selubung atap terlebih dahulu. |
| **Level-Based (Unhosted)** | `FamilyHostingType.None` | Koordinat $XYZ$ dan `LevelId` aktif. | Kolom Struktur, Balok Struktur, Furnitur | Tempatkan pada elevasi level yang benar. |
| **Face-Hosted** | `FamilyHostingType.Face` | Referensi bidang geometris (`Face`) solid. | Stop Kontak Dinding, Bracket Lampu | Temukan face planar terdekat dari host. |
| **Adaptive Component** | `AdaptivePoints` | Array titik kontrol 3D ($P_1, P_2, \dots, P_n$). | Rangka Atap Lengkung, Fasad Parametrik | Validasi jumlah dan koordinat titik kontrol. |

---

## 6. Konsistensi Arsitektural Komponen Fasad (*Architectural Façade Consistency*)

Pemilihan family komponen fasad dan bukaan wajib selaras dengan tipologi arsitektur yang dibangun:
- **Irama Modul Fasad:** Profil kusen, lebar panel kaca spandrel, dan jenis mullion pada satu bidang fasad wajib konsisten dan berasal dari family symbol yang seragam.
- **Hierarki Bukaan:** Pintu entrance utama memiliki artikulasi lebih dominan (lebar dan tinggi lebih besar) dibanding pintu servis samping; jendela lantai tipikal mempertahankan ritme repetitif yang teratur.

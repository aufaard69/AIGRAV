# AIGRAV Quantity Takeoff (QTO) Standards & Calibration Specification

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Standard Identifier:** `AIGRAV-STD-QTO-REVIT-2026-V2`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION (C-02)` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan tata kelola baku pembuatan, pembacaan, dan audit jadwal kuantitas (*Schedule / QTO*) baja struktural dan elemen arsitektur, menyelesaikan rekonsiliasi kolom pelat sambungan, kalibrasi numerik densitas baja, serta pencegahan hitungan ganda (*anti-double counting*).

---

## 1. Rekonsiliasi Kolom Schedule Pelat Sambungan (Resolution of Conflict C-02)

Berdasarkan audit komprehensif terhadap kode sumber C# aktif di `BimAuditSkill.cs:26240` dan `BimAuditSkill.cs:29471`:
- **Fakta Kode C# Aktif**: Implementasi nyata telah mendefinisikan tepat **5 kolom terlihat** untuk schedule pelat sambungan (`fLok, fType, fThk, fQty, fWeight`).
- **Akar Masalah Inkonsistensi**: Dokumen markdown lama `qto_revit_api_guide.md:22` terlambat diperbarui (*stale documentation*) sehingga hanya mencatat 4 kolom (tanpa kolom `Thickness`).
- **Keputusan Tata Kelola yang Disetujui**:
  - Kode sumber C# aktif dipertahankan sepenuhnya (karena sudah benar dan sesuai kebutuhan lapangan).
  - Standar dokumentasi resmi di-baseline ulang ke format **5 Kolom Baku**.

---

## 2. Template Kolom Baku per Kategori Komponen Baja

Setiap schedule struktural yang dibuat atau diaudit WAJIB mengikuti struktur kolom berikut. **Kolom A (Index 0) WAJIB berupa parameter `Lokasi`** sebagai pemisah zona spasial:

### 2.1. Pelat Sambungan (`OST_StructConnectionPlates`) — Tepat 5 Kolom
```
[Kolom A] Lokasi
[Kolom B] Tipe Sambungan
[Kolom C] Thickness (mm)
[Kolom D] Qty
[Kolom E] Berat (Kg)
```
*Sorting & Grouping:* By `Tipe Sambungan` (Ascending), lalu `Thickness` (Ascending, `ShowFooter = true` untuk subtotal Qty dan Berat).

### 2.2. Baut Sambungan (`OST_StructConnectionBolts`) — Tepat 3 Kolom
```
[Kolom A] Lokasi
[Kolom B] Tipe Sambungan
[Kolom C] Qty
```
*(Catatan: Baut baja dihitung dalam satuan butir/pieces, tanpa kolom berat individual pada tabel sambungan).*

### 2.3. Baseplate Kolom Baja (`OST_StructConnectionPlates`) — Tepat 7 Kolom
```
[Kolom A] Lokasi
[Kolom B] Keterangan (Comments)
[Kolom C] Thickness (mm)
[Kolom D] Width (mm)
[Kolom E] Length (mm)
[Kolom F] Qty
[Kolom G] Berat (Kg)
```
*Sorting & Grouping:* By `Comments`, `Thickness`, `Width`, `Length` (Ascending).

### 2.4. Kolom & Balok Profil (`OST_StructuralColumns` / `OST_StructuralFraming`) — Tepat 6 Kolom
```
[Kolom A] Lokasi
[Kolom B] Family and Type
[Kolom C] Length (mm)
[Kolom D] Comments
[Kolom E] Qty
[Kolom F] Berat (Kg)
```

### 2.5. Angkur Baja (*Anchor Hardware*) — Tepat 5 Kolom
```
[Kolom A] Lokasi
[Kolom B] Comments
[Kolom C] Diameter
[Kolom D] Qty
[Kolom E] Length (mm)
```

### 2.6. Voute Sambungan Baja (`OST_StructConnectionProfiles`) — Tepat 7 Kolom
```
[Kolom A] Lokasi
[Kolom B] Tipe Sambungan
[Kolom C] Type
[Kolom D] Length (mm)
[Kolom E] Volume (m³)
[Kolom F] Qty
[Kolom G] Berat (Kg)
```
*Sorting & Grouping:* By `Profile Type` (Ascending), lalu `Cut Length` (Ascending, `ShowFooter = true`).

---

## 3. Rumus Kalibrasi Berat Baja & Konversi Satuan Internal Revit

### 3.1. Standar Densitas Baja Struktural
- **Konstanta Numerik**: Nilai densitas baja yang disimpan dan dihitung secara terprogram adalah bilangan mengambang baku:
  $$\rho_{\text{steel}} = 7850.0\text{ kg/m}^3$$
- **Notasi Tampilan Regional**: Dalam teks laporan bahasa Indonesia, angka ini disajikan dengan tanda titik ribuan (`7.850 kg/m³`). Dilarang mengurai (*parse*) string berpemisah titik ke dalam tipe data `double` tanpa *CultureInfo.InvariantCulture*.

### 3.2. Konversi Satuan Internal Revit API
Revit API secara internal menyimpan volume dalam satuan **kaki kubik (*cubic feet / ft³*)**. Konversi wajib menggunakan faktor presisi tinggi:
$$1\text{ ft}^3 = (0.3048\text{ m})^3 \approx 0.028316846592\text{ m}^3$$

Rumus perhitungan berat eksak elemen voute dari volume solid Revit:
$$\text{Volume (m}^3\text{)} = \text{Element.Volume (ft}^3\text{)} \times 0.028316846592$$
$$\text{Berat (kg)} = \text{Volume (m}^3\text{)} \times 7850.0$$

> [!IMPORTANT] Larangan Perkalian Panjang Kasar pada Voute
> Voute adalah potongan profil miring (*tapered cutting*). Menghitung berat voute dengan mengalikan panjang terpotong terhadap berat linier penuh balok induk akan menghasilkan surplus volume palsu (*overestimation* hingga 50%). Berat voute **WAJIB dihitung dari volume solid eksak**.

### 3.3. Validasi Database Resmi Pabrikan
Setiap profil balok/kolom wajib divalidasi terhadap database profil baja terkalibrasi:
- **Lokasi File**: `C:\AIGRAV\Revit\AIGRAV.SteelDatabase.csv` (standar SNI dan Gunung Garuda).
- Profil yang tidak terdaftar akan memicu status `UNCALIBRATED_PROFILE` pada log audit kuantitas.

---

## 4. Protokol Anti Double-Counting (Pencegahan Perhitungan Ganda)

Untuk memastikan elemen yang telah dipartisi ke dalam sub-zona spasial tidak terakumulasi dua kali pada rekapitulasi utama proyek:

1. **Schedule Bangunan Utama / Gedung Induk (`B.*`, `C.2.*`)**:
   - **Wajib Memiliki Filter Parameter**: `Lokasi Equal ""` (string kosong).
   - Seluruh elemen yang telah dialokasikan ke sub-area khusus otomatis dikeluarkan dari rekapitulasi umum ini.
2. **Schedule Sub-Zona Spesifik (`C.3.x.y`)**:
   - **Wajib Memiliki Filter Parameter**: `Lokasi Equal "<NamaSubZona>"` (misalnya: `"Carton & Cups"`, `"Power House"`, `"Waterline"`).
3. **Audit Verifikasi Filter**:
   Skrip QA/QC `aigrav_qa_audit_all_schedules` wajib memverifikasi bahwa tidak ada elemen dengan `Lokasi != ""` yang muncul di schedule bangunan utama.

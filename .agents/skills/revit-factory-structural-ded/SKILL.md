---
name: revit-factory-structural-ded
description: >-
  Standard Operating Procedure and architectural/structural drafting specification for
  Industrial Factory Structural Detailing Drawings (DED) in Autodesk Revit via AIGRAV.
  Encodes ISO A3 Landscape bottom-strip titleblock geometry (420x297 mm), 8-series sheet
  taxonomy (000..RF), 5 sheet composition templates (including Hybrid 2D + 3D Shaded
  Isometric details), typical portal grouping, partial plan splitting with Keyplans,
  ISO metric scales, 2-tier dimensioning, steel tagging rules, and 8 QA anti-error guardrails
  learned from NUAS-FT-BU-ST.rvt.
---

# Revit Factory Structural DED Standard (`AIGRAV-FAC-STR-DED-v1.0`)

Standar resmi AIGRAV untuk perancangan, penyusunan sheet, penataan viewport, pendimensian, penandaan (*tagging*), dan pemeriksaan kualitas (*QA/QC*) gambar kerja struktur bangunan pabrik industri (*Industrial Factory Structural DED*) di Autodesk Revit.

> [!NOTE]
> Standar ini dirumuskan dari acuan proyek nyata `NUAS-FT-BU-ST.rvt` (pabrik 438 m × 48 m, 146 sheet A3) dengan penyempurnaan *QA Guardrails* agar AIGRAV tidak mengulangi kesalahan drafting yang terdeteksi pada audit proyek tersebut.

---

## 1. Standar Kertas & Anatomi Kop Gambar (ISO A3 Horizontal Bottom Strip)

Seluruh gambar kerja struktur pabrik menggunakan standar **ISO A3 Landscape (`420 × 297 mm`)** dengan **Kop Gambar Horizontal di Bagian Bawah** (*bottom strip*).

| Zona Paper Space A3 (`420 × 297 mm`) | Dimensi / Posisi Baku | Ketentuan & Fungsi |
| :--- | :--- | :--- |
| **Margin Kiri (Jilid)** | `20 mm` (`X = 0..20 mm`) | Ruang jilid dokumen tender/konstruksi. |
| **Margin Atas & Kanan** | `6 mm` (`Y = 291..297`, `X = 414..420`) | Garis bingkai luar kop. |
| **Strip Kop Horizontal Bawah** | Tinggi `32 mm` (`Y = 5..37 mm`) | Lebar penuh di bagian bawah lembar. |
| **Zona Terlarang Stempel Status** | `48 × 18 mm` (`X = 366..414`, `Y = 37..55`) | Area `FOR TENDER` / `FOR CONSTRUCTION` di pojok kanan-bawah di atas kop; **viewport DILARANG menimpa zona ini**. |
| **Kanvas Gambar Efektif** | **~`390 × 250 mm`** (`X = 20..414`, `Y = 38..291`) | Area bebas gambar di luar kop bawah dan stempel kanan-bawah. |

### Parameter Wajib Kop Gambar
Setiap sheet wajib mengisi metadata konsisten:
- `PEMILIK`, `PROYEK`, `GAMBAR` (judul lembar huruf kapital bersih)
- Kolom pengesahan: `DIRENCANAKAN`, `DIGAMBAR`, `DIPERIKSA`, `DISETUJUI`
- Blok revisi: `REVISI`, `CATATAN`, `TANGGAL`
- Blok teknis kanan bawah: `UNIT` (`mm`), `SKALA` (`As indicated` bila multi-skala, atau `1 : xxx` bila 1 skala), `TGL`, `UKURAN` (`A3`), `NO.` (urutan cetak `1..N`, atau `-` untuk Cover & Daftar Gambar), `KODE GAMBAR` (`STR- <SheetNumber>`).

---

## 2. Taksonomi 8 Seri Sheet & Aturan Estimasi Kebutuhan Gambar

Paket gambar disusun berurutan mengikuti logika konstruksi dari dokumen pengantar → standar umum → rangka utama → pondasi → lantai/mezzanine → atap & sambungan detail:

| Urutan | Seri | Rentang Kode | `NO.` | Isi & Aturan Estimasi Cerdas dari Model 3D |
| :---: | :---: | :--- | :---: | :--- |
| **1** | **000** | `000` | `-` | **Cover Proyek** (1 sheet, view `3D COVER` dengan titleblock proyek). |
| **2** | **DG** | `DG-001..N` | `-` | **Daftar Gambar** (*Sheet List Schedule* dibagi per ±75–80 baris agar muat rapi di A3). |
| **3** | **SD** | `SD-001..N` | `001..` | **Standard Detail Struktur** memakai **Legend View** (mutu beton/baja, tulangan, catatan umum, detail sambungan & profil standar) agar 100% *reusable*. |
| **4** | **DT** | `DT-001..N` | berlanjut | **General Arrangement (GA)**: Denah Keseluruhan → Split Zona Fungsional (misal `PRODUKSI`, `WFG & WRM`) → Denah Atap per zona → 3–4 Tampak (Utuh + Parsial Zoom) → Isometric & Perspektif. |
| **5** | **PT** | `PT-001..N` | berlanjut | **Portal Frame Sections**: *Typical Portal Grouping* untuk grid transversal identik (`AS 2-4`, `AS 5-14`, `AS 42-59`), plus portal longitudinal (`AS A`, `AS B`, `AS C`) yang dipecah per zona parsial. |
| **6** | **PD** | `PD-001..N` | berlanjut | **Substruktur & Pondasi**: Denah Pondasi, Strauss/Tiang Pancang, Kolom Pedestal, Kolom Baja (dipecah parsial + Keyplan bila bentang > 150 m) → 1 sheet Detail per tipe pilecap unik (`PC.xxx`) → Detail Sloof, Ramp Loading Dock, Poer Plat, & Tiang Pancang. |
| **7** | **LT** | `LT-001..N` | berlanjut | **Lantai & Mezzanine**: Denah pengecoran/cutting sealant/stop cor/detail relat → **Pasangan Wajib Mezzanine** (Sheet 1: Denah + 3D Isometric; Sheet 2: Potongan A/B/C) untuk tiap mezzanine terpisah (Fasum, Storage, Walkway, AHU, Office, Power House) → Detail Pit Lift & Railing. |
| **8** | **RF** | `RF-001..N` | berlanjut | **Atap, Rangka Sekunder, Tangga & Joint Baja**: Denah Atap Multi-Layer (`Rafter & Wind Bracing`, `Gording & Tie Rod`, `Klem Plat Strip`, `Insulation`) → Detail Member Sekunder (`2CNP`, `CNP`, `Regel`, `Bracing`, `Tie Rod`, `Talang`, `Cladding`, `Kanopi`, `Cremona`) → Detail Tangga (1 sheet multi-view per tangga) → Matriks Tabel Joint Baja (`Legend View`) → Isometric Baja. |

### Aturan Estimasi Jumlah Sheet Otomatis
1. **Typical Portal Grouping:** Kelompokkan portal transversal berdasarkan kemiripan profil kolom, bentang, rafter, dan keberadaan mezzanine/kanopi menjadi rentang grid (`AS 5-14`, bukan 10 sheet terpisah).
2. **Partial Plan & Longitudinal Split (> 150 m):** Jika panjang bangunan > 150 m atau skala utuh turun di bawah `1:500`, pecah denah menjadi zona fungsional (`PRODUKSI`, `WFG & WRM`) atau `PARSIAL 1` / `PARSIAL 2` dengan **Keyplan wajib**, serta pecah portal longitudinal menjadi `PARSIAL 1&2` dan `PARSIAL 3&4` (2 viewport bertumpuk atas-bawah).
3. **1 Sheet per Tipe Pilecap Unik:** Setiap tipe `PC.xxx` yang memiliki kombinasi kolom/pile berbeda mendapat 1 sheet detail komplit (`Template D`).
4. **Mezzanine Pairing:** Setiap modul mezzanine atau bangunan utilitas bertingkat menghasilkan 1 sheet `DENAH + ISOMETRIC` diikuti 1 sheet `POTONGAN`.

---

## 3. Lima (5) Template Komposisi Sheet Baku (Kanvas A3)

Gunakan koordinat relatif pada kanvas efektif A3 (`X: 20..414 mm`, `Y: 38..291 mm`, hindari zona stempel `X: 366..414, Y: 38..56`):

### Template A — Partial Plan + Keyplan + Legend (`DT-002`, `PD-001`, `RF-001`)
- **Kiri-Atas (`X: 24..140, Y: 225..285`):** Keyplan (`1:3000–1:5000`) dengan highlight zona aktif + label panah.
- **Kanan-Atas (`X: 260..408, Y: 225..285`):** Legend / Keterangan Arsir / Legend Sloof.
- **Area Utama Bawah (`X: 24..408, Y: 44..220`):**
  - Mode 1 Denah: 1 Denah Parsial penuh lebar.
  - Mode 2 Denah Bertumpuk (`RF-001`): `PARSIAL 1` di atas (`Y: 165..285`) dan `PARSIAL 2` di bawah (`Y: 44..160`) dengan Keyplan di kiri-atas.

### Template B — Elevation Overview + 2 Stacked Zoom Partials (`DT-006`)
- **Atas (`X: 24..408, Y: 225..285`):** 1 Tampak Utuh berisi 2 Callout Box putus-putus (`A`, `B`) atau (`1`, `2`).
- **Tengah (`X: 24..408, Y: 135..220`):** Tampak Parsial A (skala `1:200–1:400`).
- **Bawah (`X: 24..408, Y: 44..130`):** Tampak Parsial B (skala `1:200–1:400`, sejajar vertikal dengan Parsial A).

### Template C — Portal Frame Section (`PT-003`, `PT-024`)
- **Transversal (1 Viewport):** Terpusat horizontal (`X: 35..395, Y: 60..275`), Grid di bawah, Level head rata kanan (`+xx.xxx`), dimensi vertikal 2-tier di sisi kanan sebelum Level head.
- **Longitudinal (2 Viewport Bertumpuk):** `PARSIAL 1` di `Y: 165..285` dan `PARSIAL 2` di `Y: 44..158` dengan elevasi level sejajar di sisi kanan.

### Template D — Hybrid 2D Detail + 3D Shaded Isometric (`PD-009`, `RF-010`, `LT-006`)
> **Wajib menjadi standar utama detail struktur AIGRAV.**
- **Kiri / Tengah:**
  - *Pondasi (`PD-009`)*: Potongan vertikal lengkap (`1:25 / 1:20`), Denah Pilecap (`1:25`), dan Callout Section Pedestal (`1:20`).
  - *Member Baja (`RF-010`)*: Elevasi & Tampak Atas batang utama (`1:20`) memanjang di bagian atas, lalu 2–3 Callout Detail Joint (`A1`, `A2`, `B2` @ `1:10`) berjajar di kiri/tengah bawah.
  - *Mezzanine (`LT-006`)*: Denah Mezzanine (`1:200`) di bagian atas penuh, diikuti 3D Shaded Isometric di bagian bawah.
- **Kanan / Pojok Kanan (`X: 280..408, Y: 62..250`):** **1 View 3D Isometric Shaded** memperlihatkan struktur/tulangan/pelat sambungan dalam 3D penuh (memberi kejelasan langsung bagi pelaksana lapangan).
- *Catatan Khusus `RF-041..046`*: Tabel **Matriks Joint Baja** menggunakan **Legend View** penuh di kanvas (`Baris = Profil WF`, `Kolom = Base Plate, Rib, Beam, Girder, End Plate, Rafter, Rafter Joint`).

### Template E — High-Density Multi-View Assembly (`LT-007`, `RF-038`)
- **Multi-Section (`LT-007`):** 1 Potongan Memanjang penuh di baris atas (`Y: 170..285`) + 2 Potongan Melintang sejajar horizontal di baris bawah (`Y: 44..165`).
- **Assembly 6–8 Viewport (`RF-038` Tangga):**
  - Baris Atas Kiri: Denah (`1:50`).
  - Baris Atas Tengah: **3D Isometric Shaded**.
  - Baris Bawah Kiri & Tengah: 2 Potongan Ortogonal (`POT. A` & `POT. B`).
  - Kolom Kanan (2 kolom × 2–3 baris, jauh di atas zona stamp `Y > 60`): Rantai 4–6 Detail Callout (`A1..A6`, `1:10–1:25`).

---

## 4. Standar Grafis, Tangga Skala ISO, Dimensi & Tagging

### A. Tangga Skala ISO Metrik Wajib
Gunakan **hanya** skala metrik standar ISO berikut:
- **Detail Sambungan / Joint / Baseplate / Trap Tangga:** `1:10`, `1:15`, `1:20`, `1:25`
- **Detail Pondasi / Pedestal / Ramp / Pit / Member Baja:** `1:20`, `1:25`, `1:50`
- **Potongan Mezzanine / Tangga / Kanopi / Portal Transversal:** `1:50`, `1:75`, `1:100`, `1:150`, `1:200`
- **Denah Parsial & Portal Longitudinal / Tampak Parsial:** `1:200`, `1:250`, `1:300`, `1:400`, `1:500`
- **Keyplan:** `1:2500` / `1:3000` / `1:5000` (skala referensi diagramatis; bila tersedia pakai `1:2000` atau `1:5000`)
- **Skala Terlarang (Non-ISO):** `1:12`, `1:26`, `1:32`, `1:35`, `1:40`, `1:60`, `1:70`, `1:80`, `1:90`, `1:110`, `1:120`, `1:160`, `1:350`, `1:550`, `1:600`, `1:650`, `1:700`, `1:800`, `1:900`, `1:1200`, `1:1250`, `1:1300`. Jika model > 350 m tetap membutuhkan overview di A3, **pecah zona lebih lanjut** atau minta persetujuan user terlebih dahulu sebelum memakai skala custom-fit.

### B. Hierarki Grafis, Dimensi & Tagging
- **Underlay & Link:** Link Arsitektur/MEP wajib di-override **Halftone** (abu-abu tipis), dan seluruh label/anotasi nama file RVT link wajib disembunyikan di view struktur.
- **Grid & Level:** Grid + Bubble halftone di kedua ujung (Atas-Bawah, Kiri-Kanan); Level Head rata kanan format 3 desimal (`+71.800`).
- **Dimensi 2-Tier (Denah & Portal):**
  1. Tier 1 (dekat model): jarak antar As Grid / elevasi antar lantai.
  2. Tier 2 (luar): dimensi bentang total / tinggi total.
  3. Tambahan Portal: *Aligned Dimension* sepanjang rafter miring, *Spot Slope* (`6°`), dan *Spot Elevation* (`EL +71800`).
- **Dimensi Detail (`1:10–1:25`):** Jarak tepi & pitch baut (`30|120|30`, `60|130|60`), selimut beton (`50|250|50`), dan tebal tiap lapisan pondasi (`lantai kerja`, `base course`, `pilecap`).
- **Strategi Tagging & Keterangan:**
  - Tag profil balok/rafter/kolom/bracing dipasang **sejajar orientasi batang** (`WF 350.175.7.11`, `CWF 350.175.7.11`, `2CNP 150`, `REGEL`, `WB`).
  - Keterangan detail pondasi & joint memakai **leader siku horizontal** rata kiri/kanan di luar batas solid elemen agar teks tidak pernah menimpa arsiran beton atau garis baut.
  - Judul Viewport: `HURUF KAPITAL + GARIS BAWAH`, bubble Detail Number pendek di kiri, skala di bawah (khusus view **3D Isometric: sembunyikan teks skala**).

---

## 5. Delapan (8) Guardrail Anti-Kesalahan (*Lessons Learned* dari Audit `NUAS-FT-BU-ST.rvt`)

Setiap kali AIGRAV menganalisis, merencanakan, atau membuat gambar kerja struktur pabrik, **8 larangan dan pagar QA berikut WAJIB dipatuhi**:

1. **Zero Wrong Cross-Reference Bubble:** DILARANG memakai Family *View Title* yang menampilkan parameter *Referencing Sheet* pada Floor Plan, Keyplan, atau View Independen (mencegah munculnya bubble salah seperti `A / LT-007` atau `B / LT-018` pada denah utama).
2. **Detail Number Wajib Pendek (`<= 4` karakter, misal `1..9`, `A..F`, `A1..A6`):** DILARANG mengisi `Detail Number` dengan kode tipe panjang seperti `PC.200A` karena menabrak garis lingkaran bubble dan judul viewport di bawahnya.
3. **Naming Hygiene Pre-Commit Gate:** Sebelum membuat atau mengesahkan Sheet dan View, validasi bebas dari:
   - Spasi ganda (`"  "`), spasi sebelum/sesudah tanda hubung yang tidak simetris (`AS 72- 73`), atau inkonsistensi titik (`AS.1` vs `AS 2-4`).
   - Kata ganda/typo (`PRODUKSI PRODUKSI`, `LOADICK DONG`, `KEYPLANKOLOM`, `CEMPER`, `DETIL`, `STANDART` -> gunakan **`STANDARD`** atau **`STANDAR`** secara konsisten).
   - Nama default Revit (`Detail 2`, `Section 1`) atau duplikat mentah berakhiran `Copy 1`, `Copy 2` pada sheet.
4. **Disiplin Skala ISO (Prioritaskan Split Zona daripada Skala Ganjil):** Jangan asal menembak skala *custom fit* (`1:650`, `1:160`, `1:12`) hanya supaya gambar muat; pilih skala ISO (`1:500`, `1:200`, `1:15`) dan pecah ke parsial + Keyplan bila melebihi kanvas.
5. **Zero Link-Label Leak & Tag/Dim Anti-Overlap:** Pada View Template denah/portal struktur, matikan *Room Tags*, *Door Tags* link yang padat, dan label nama model link (`NUAS-FT-*-AR`); pastikan *Dimension Text* tidak menumpuk garis profil baja (`L`, `WF`, `UNP`) dan callout bubble tidak saling bertabrakan.
6. **Sinkronisasi Parameter `NO.` Wajib Sebelum Export:** Sebelum menjalankan `MasterExporter`, jalankan `aigrav_get_all_sheets_meta` dan pastikan nilai parameter `NO.` pada seluruh sheet berurutan konsisten `001..N` (Cover & Daftar Gambar = `-`) serta cocok dengan tabel `DAFTAR GAMBAR`.
7. **Kepatuhan Kop Horizontal Bawah & Zona Keep-Out Stamp:** Pada titleblock A3 SMU/factory, kanvas drawable berakhir di `Y >= 38 mm` dengan zona bebas tambahan di pojok kanan-bawah (`48 × 18 mm` di atas kop) untuk stempel `FOR TENDER`.
8. **Paginasi Tool Bridge pada Model Besar (`> 250 MB` atau `> 50 Sheets`):** DILARANG menjalankan inspeksi viewport atau warning seluruh dokumen dalam satu panggilan tunggal (`aigrav_inspect_viewports` penuh berisiko timeout >60 detik). Gunakan `aigrav_get_sheet_paper_space(sheet_id)` per lembar atau batch bertahap per seri sheet.

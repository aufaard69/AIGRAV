---
name: revit-consultative-design
description: Standard Operating Procedure (SOP) dan alur perancangan interaktif-konsultatif (Interactive Consultative Building Design) di Autodesk Revit via AIGRAV. Menggabungkan keanggunan alur kerja Claude (wawancara kebutuhan, discovery family di awal, sekuens konstruksi logis, checkpoint per tahap, dan visual color splash) dengan kekuatan komputasi native C# AIGRAV. Digunakan ketika pengguna meminta mendesain bangunan dari nol, merancang rumah tinggal, kantor, ruko, gedung bertingkat, atau meminta alur kerja yang terstruktur dan rapi.
---

# AIGRAV Consultative BIM Building Design Protocol

Protokol ini dirancang untuk mewujudkan pengalaman perancangan bangunan yang **sangat rapi, komunikatif, transparan, dan terstruktur** di Autodesk Revit. Protokol ini mengadopsi keunggulan interaksi manusiawi (*conversational pacing & human-in-the-loop checkpoints*) yang dipadukan dengan performa mesin native C# AIGRAV.

---

## 🏛️ Prinsip Inti (Core Tenets)

1. **Konsultatif Sebelum Eksekutif (Interview Before Acting)**:
   - Dilarang langsung memborbardir model dengan elemen masif tanpa memahami program ruang dan tapak.
   - Klarifikasi kebutuhan utama: tipologi, dimensi tapak ($P \times L$), jumlah lantai, dan selera material.
2. **Audit Library Sebelum Memilih Tipe (Query Families First)**:
   - Selalu panggil `aigrav_get_project_context()` untuk membaca family dan tipe elemen yang sudah terpasang di template Revit pengguna.
   - Sajikan ringkasan tipe yang tersedia kepada pengguna dan berikan rekomendasi terbaik.
3. **Sekuens Konstruksi Nyata (Real Construction Sequence)**:
   - Ikuti hukum gravitasi dan urutan fisik lapangan: Datum $\to$ Struktur Utama $\to$ Selubung Eksterior $\to$ Partisi & Bukaan $\to$ Lantai & Atap $\to$ Dokumentasi.
4. **Pemberhentian & Konfirmasi Antar-Milestone (Human Checkpoints)**:
   - Berikan jeda konfirmasi di setiap akhir milestone struktural utama. Sertakan screenshot atau minimap dan tanyakan apakah pengguna ingin melakukan penyesuaian sebelum melanjutkan.
5. **Sentuhan Estetika Visual (*Color Splash Visualization*)**:
   - Manfaatkan `aigrav_color_splash` untuk memberikan pewarnaan harmonis (*graphic override*) pada kategori elemen di viewport 3D agar model tampak memukau, hidup, dan mudah dipahami fungsinya.

---

## 📋 Alur 6 Fase Perancangan Interaktif

```
┌────────────────────────────────────────────────────────────────────────┐
│                   ALUR PERANCANGAN KONSULTATIF AIGRAV                  │
├────────────────────────────────────────────────────────────────────────┤
│  FASE 1: Wawancara Program & Kebutuhan Desain (Design Brief Gathering) │
│     ▼                                                                  │
│  FASE 2: Audit Konteks & Discovery Family Template Revit                │
│     ▼                                                                  │
│  FASE 3: Formulasi Blueprint Spasial & Rekomendasi Tipe                │
│     ▼                                                                  │
│  FASE 4: Eksekusi Berbasis Sekuens Bertahap (Human Checkpoints)        │
│          M1: Datum & Kolom/Balok ──> [Checkpoint & Preview]           │
│          M2: Dinding Perimeter & Pelat ──> [Checkpoint & Preview]      │
│          M3: Partisi Ruang, Pintu & Jendela ──> [Checkpoint & Preview] │
│     ▼                                                                  │
│  FASE 5: Visualisasi Estetik (AIGRAV Color Splash & Viewport Capture)   │
│     ▼                                                                  │
│  FASE 6: Rekapitulasi Statistik Model & Quantity Takeoff (QTO)         │
└────────────────────────────────────────────────────────────────────────┘
```

---

### Fase 1: Wawancara Program & Kebutuhan Desain
Kumpulkan parameter esensial dari pengguna:
- **Fungsi Bangunan**: Rumah tinggal, villa, ruko komersial, kantor modern, dsb.
- **Dimensi Tapak & Koefisien**: Panjang $\times$ Lebar (dalam meter), sempadan jalan, dan orientasi arah hadap.
- **Jumlah Lantai & Ketinggian**: Ketinggian antar lantai standar (misal: Lantai 1 = 3.60m, Lantai 2 = 3.20m).
- **Program Ruang Utama**: Jumlah kamar tidur, ruang keluarga, area servis, carport/garasi.

---

### Fase 2: Audit Konteks & Discovery Family Template
Panggil `aigrav_get_project_context()` untuk membaca status proyek aktif:
- Periksa daftar `levels` yang sudah ada (jangan membuat level duplikat jika sudah tersedia).
- Periksa daftar `wall_types` (misal: bata 150mm, hebel, beton, partisi gypsum).
- Periksa daftar `column_types` dan `framing_beam_types`.
- Laporkan secara ringkas kepada pengguna:  
  *"Di dalam template Revit aktif, terdeteksi 3 Level, 8 Tipe Dinding, dan 5 Profil Kolom. Saya merekomendasikan Dinding Eksterior menggunakan Bata 150mm dan Kolom Struktur 200x200mm."*

---

### Fase 3: Formulasi Blueprint Spasial
Sebelum mengeksekusi kode:
1. Tetapkan koordinat grid dasar (Grid A–D sumbu X, Grid 1–4 sumbu Y).
2. Hitung tebal struktur dan titik penempatan pintu/jendela agar tidak menabrak kolom utama.
3. Sajikan ringkasan tabel koordinat secara ringkas.

---

### Fase 4: Eksekusi Sekuens Bertahap (Dengan Checkpoint)
Eksekusi dilakukan per makro-blok logis menggunakan transaksi atomik:
1. **Langkah 1 (Datum & Struktur Pokok)**:
   - Buat Level & Grid (`create_grid_matrix` atau `create_level`).
   - Buat Kolom Struktur & Balok Sloof/Tie Beam.
   - *Checkpoint*: Laporkan jumlah kolom/balok yang terpasang dan minta konfirmasi untuk lanjut ke selubung.
2. **Langkah 2 (Selubung Perimeter & Pelat Lantai)**:
   - Buat Dinding Eksterior Perimeter.
   - Buat Pelat Lantai (wajib mematuhi aturan Donut Boundary Loops jika ada void/void tangga).
   - *Checkpoint*: Sajikan elevasi lantai dan dinding perimeter.
3. **Langkah 3 (Partisi Interior & Bukaan)**:
   - Buat Dinding Partisi Pembagi Ruangan.
   - Pasang Pintu dan Jendela (*hosted directly in walls*) dengan ambang atas serasi (standar 2.10m).
   - Buat Atap (*gable/hip roof*).

---

### Fase 5: Visualisasi Estetik (Color Splash)
Setelah geometri fisik terpasang, panggil tool `aigrav_color_splash`:
- Otomatis menerapkan skema warna harmonis standar BIM:
  - **Dinding (Walls)**: Coral Terracotta (`RGB: 235, 120, 80`)
  - **Lantai (Floors)**: Sky Slate Blue (`RGB: 130, 175, 220`)
  - **Kolom Struktur**: Royal Blue (`RGB: 40, 100, 215`)
  - **Balok Struktur**: Dark Navy Indigo (`RGB: 50, 65, 105`)
  - **Pintu (Doors)**: Emerald Green (`RGB: 45, 175, 100`)
  - **Jendela (Windows)**: Ice Cyan Translucent (`RGB: 110, 210, 245, Transp: 50%`)
  - **Atap (Roofs)**: Crimson Burgundy (`RGB: 180, 45, 60`)
- Panggil `aigrav_capture_revit_viewport` untuk menyajikan tangkapan layar 3D yang hidup dan memukau kepada pengguna.

---

### Fase 6: Rekapitulasi Statistik & Quantity Takeoff (QTO)
Tutup laporan dengan data profesional:
- Rekapitulasi jumlah elemen per kategori (Dinding, Kolom, Pintu, Jendela, Pelat).
- Luas total lantai terbangun ($m^2$).
- Kepatuhan terhadap kaidah teknik sipil (load path terhubung, zero clash, zero overlapping slabs).
- Kotak Verdict Kelayakan BIM Engineer & Arsitek.

# AIGRAV Autonomous Modeling - Phase 1 to 3 Report

## PHASE 1 — UNDERSTAND

**Project Goal**: Membangun 1 portal frame baja simetris dari kondisi project kosong (Empty Project).

**Standard-Derived (Dari Steel Knowledge Base)**:
- **Profil Utama**: WF 396x199x7x11 atau ekuivalen WF 400x200x8x13.
- **Base Plate & Pedestal**: Kolom WF400 membutuhkan base plate $300\times440\times19\text{ mm}$ dengan 4 angkur ⌀22mm. Pedestal yang sesuai dari standar adalah $350\times500\text{ mm}$.
- **Connection**: End plate momen di ridge, base plate terkespos.

**Agent Assumption**:
- Center-to-center / clear span portal ditetapkan $24,000\text{ mm}$ (Grid 1 ke Grid 2). 
- Reference Base (Lantai) berada di elevasi $0.000\text{ mm}$.

**Unknown / Needs Approval**:
- Mutu beton pedestal dan dimensi telapak pondasi (Footing) di bawah $0.000\text{ m}$. (Hanya pedestal $350\times500\text{ mm}$ yang akan dimodelkan sesuai tinggi $2,000\text{ mm}$).


## PHASE 2 — GEOMETRIC PLAN

**Sistem Koordinat (X, Y, Z)**:
- Left Column: $X = 0\text{ mm}, Y = 0\text{ mm}$
- Right Column: $X = 24,000\text{ mm}, Y = 0\text{ mm}$
- Center / Ridge: $X = 12,000\text{ mm}, Y = 0\text{ mm}$

**Elevasi & Level**:
- **Level 1 (Base)**: $+0\text{ mm}$
- **Level 2 (Pedestal Top)**: $+2,000\text{ mm}$
- **Level 3 (Left/Right Eaves)**: $+12,000\text{ mm}$ (Constraint Tepi Atas Rafter)
- **Level 4 (Ridge)**: $+13,900.6\text{ mm}$
  - *Perhitungan*: $\text{Rise} = 12,000 \times \tan(9^\circ) \approx 1,900.6\text{ mm}$.
  - Ridge Top Edge = $12,000 + 1,900.6 = 13,900.6\text{ mm}$.

**Constraint Tepi Atas**:
Karena constraint adalah *Top Edge*, sumbu analitik (Centerline) dari balok WF400 (ketinggian $\approx 400\text{ mm}$) akan di-offset ke bawah sebesar setengah tinggi profil ($\approx 200\text{ mm}$ / $200 / \cos(9^\circ)$), atau menggunakan fitur *Z-Justification = Top* pada elemen Revit.


## PHASE 3 — DISCOVERY REPORT

Hasil inspeksi AIGRAV Bridge terhadap Revit Document aktif:
1. **Levels**: Hanya terdapat `L1 (0 mm)` dan `L2 (3600 mm)`.
2. **Grids**: Kosong (`0` grids).
3. **Families Loaded**:
   - Kolom Beton: `M_Concrete-Rectangular-Column` (Tersedia, perlu diduplikasi menjadi $350\times500$).
   - Kolom Baja: Hanya `UC-Universal Column` dan `M_HSS`. **WF400 Tidak Ditemukan**.
   - Balok Baja: Hanya `M_W Shapes`, `M_C Shapes`. **WF400 Tidak Ditemukan**.
4. **Action Plan (Phase 4)**:
   - Mencari dan me-load `M_W-Wide Flange-Column.rfa` dan `M_W-Wide Flange.rfa` dari `C:\ProgramData\Autodesk\RVT 2026\Libraries\...`
   - Membuat/menduplikasi tipe `WF400` dengan parameter $H=400, B=200, tw=8, tf=13$.

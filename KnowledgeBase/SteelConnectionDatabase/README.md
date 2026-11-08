# AIGRAV Steel Connection Standard Knowledge Base (v1.0)

**Project Reference**: PT. Niagatama Kencana - All DC  
**Standard Drawings Verified**:
- `STD-BJ-01` s/d `STD-BJ-07` (Detail Sambungan Baja)
- `STD-PDS-01` s/d `STD-PDS-15` (Standar Pondasi & Pedestal)

---

## 1. Ikhtisar & Tujuan
Knowledge base ini mendokumentasikan secara terstruktur seluruh aturan detail sambungan dan komponen baja struktural berdasarkan dokumen standar gambar kerja resmi. Database ini dirancang untuk:
1. Menjadi *Single Source of Truth* parameter sambungan baja untuk engine AIGRAV.
2. Mendukung otomatisasi pembuatan model 3D (Family / Structural Connection API di Revit) untuk proyek mendatang.
3. Memastikan kepatuhan dimensi geometri (tebal pelat, jumlah & diameter baut, panjang angkur, dan dimensi pedestal).

---

## 2. Struktur Kategori (11 Standar)

1. **Steel Profile**: Penampang profil WF (150 s/d 450), H-Beam (200 s/d 400), Castellated WF (300 s/d 450), dan Kanal CNP / 2CNP.
2. **Beam Connection**: Sambungan geser balok anak ke induk (clip plate) dan sambungan lapangan rafter (bolted flange + welded web).
3. **Column Connection**: Sambungan kolom (column splice) dengan pelat sayap & badan serta transisi reduksi kolom pada elevasi +24.60.
4. **End Plate**: Detail pelat momen ujung balok / rafter (WF dan CWF) lengkap dengan jumlah lubang baut HTB.
5. **Gusset Plate**: Pelat buhul untuk ikatan angin atap, ikatan vertikal kolom, dan klit gording.
6. **Base Plate**: Pelat landasan kolom baja pada pedestal beton lengkap dengan tebal pelat, jumlah & diameter angkur ($L=700\text{ mm}$ / $1000\text{ mm}$), stiffener rib, dan ukuran pedestal.
7. **Stiffener**: Pelat pengaku vertikal dan rib segitiga untuk tumpuan base plate, transisi kolom, dan web post castellated.
8. **Bolt**: Spesifikasi baut HTB (M12, M16, M19, M20, M22, M25) dan angkur pondasi dengan double nut.
9. **Weld**: Standar pengelasan las sudut (fillet weld "DILAS KELILING") dan las tumpul sambungan badan ("JOINT DILAS").
10. **Bracing**: Ikatan angin atap (rod + jarumpas), ikatan vertikal kolom (double angle), dan sagrod/trackstang.
11. **Other Steel Details**: Shear connector stud bondek (⌀19 $L=100$, spasi $150-200\text{ mm}$), tumpuan baut oval (slotted holes STD-BJ-07), dan spesi non-shrink grout.

---

## 3. Lokasi File Database
- **Master Knowledge Base**:  
  `C:\AIGRAV\KnowledgeBase\SteelConnectionDatabase\AIGRAV_SteelConnectionDatabase_v1.json`
- **Revit Bridge / Skills Runtime Lookup**:  
  `C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills\SteelDatabase\AIGRAV_SteelConnectionDatabase_v1.json`

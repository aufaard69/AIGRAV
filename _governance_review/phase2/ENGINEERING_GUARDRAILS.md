# AIGRAV Engineering Guardrails & Architectural Sanity Criteria

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan batasan penapisan awal (*preliminary screening heuristics*) untuk kelayakan struktural, ergonomi arsitektural, dan perlindungan cuaca dalam proses pemodelan BIM otomatis, serta menegaskan batasan tanggung jawab hukum profesional.

---

## 1. Status Hukum & Batasan Tanggung Jawab (*Disclaimer of Engineering Liability*)

> [!CAUTION] PERINGATAN HUKUM & BATASAN KELAYAKAN TEKNIK
> Parameter, rasio bentang, dimensi, dan aturan batas dalam dokumen ini berstatus murni sebagai **HEURISTIK PENAPISAN AWAL (PRELIMINARY SCREENING HEURISTICS)**.  
> 1. Heuristik ini berfungsi mencegah kesalahan pemodelan geometris yang nyata (*gross modeling blunders*) seperti balok terlampau tipis, ruang tanpa kepala bebas, atau atap terbalik.  
> 2. Dokumen ini **BUKAN** merupakan sertifikasi kelaikan struktur (*structural certification*), **BUKAN** analisis elemen hingga (*FEA/structural analysis*), dan **TIDAK MENGGANTIKAN** perhitungan resmi oleh Insinyur Struktur Berlisensi (*Licensed Structural Professional Engineer / Ahli Teknik Bangunan Gedung*).  
> 3. Gambar kerja dan data fabrikasi wajib divalidasi dengan kalkulasi struktural resmi berbasis SNI/AISC/ACI sebelum konstruksi lapangan dimulai.

---

## 2. Heuristik Penapisan Struktur (Structural Screening Heuristics)

### 2.1. Integritas Jalur Beban (*Continuous Load Path*)
- **Larangan Kolom Melayang (*Floating Columns*)**:
  Setiap elemen `OST_StructuralColumns` wajib memiliki tumpuan vertikal menerus ke bawah (bertumpu langsung pada sloof, pondasi telapak/pile cap, atau balok transfer terhitung).
- **Verifikasi Elevasi Dasar Kolom**:
  Jika kolom tidak bertumpu pada elemen struktur lain atau tanah dasar, sistem wajib memunculkan peringatan kritis:
  `WARNING_DISCONTINUOUS_COLUMN_LOAD_PATH`.

### 2.2. Rasio Pra-Dimensi Tinggi Balok terhadap Bentang ($h / L$)
Heuristik penapisan awal untuk mengidentifikasi profil balok yang berpotensi melendut secara ekstrem:

| Material Balok | Rasio Tinggi terhadap Bentang ($h / L$) | Contoh Bentang ($L = 6.00\text{ m}$) |
| :--- | :---: | :---: |
| **Beton Bertulang (RC Beam)** | $h \approx \frac{1}{10} L \text{ s/d } \frac{1}{14} L$ | $h \approx 450\text{ mm s/d } 600\text{ mm}$ |
| **Baja Profil I-Beam / WF** | $h \approx \frac{1}{20} L \text{ s/d } \frac{1}{24} L$ | $h \approx 250\text{ mm s/d } 300\text{ mm}$ |
| **Gording Baja Ringan / CNP** | $h \approx \frac{1}{30} L \text{ s/d } \frac{1}{35} L$ | $h \approx 100\text{ mm s/d } 125\text{ mm}$ (bentang 3–4 m) |

*Tindakan Audit:* Jika model menggunakan balok beton bentang 8 meter dengan tebal hanya 200 mm, validator akan menandai sebagai *Extreme Aspect Ratio Defect*.

---

## 3. Heuristik Ergonomi & Arsitektural (Architectural Usability Screening)

### 3.1. Ruang Bebas Vertikal (*Headroom Clearance*)
- **Area Hunian / Perkantoran**: Tinggi bebas vertikal bersih di bawah dasar balok terendah, pelat kantilever, atau bordes tangga **minimal $2.20\text{ meter}$**.
- **Area Gudang / Workshop / Pabrik / Industri**: Tinggi bebas vertikal bersih di bawah struktur atap/portal portal **minimal $3.50\text{ s/d } 4.50\text{ meter}$** (menyesuaikan pergerakan forklift dan instalasi MEP overhead).

### 3.2. Ergonomi Sirkulasi Vertikal (Tangga)
Pemodelan tangga arsitektural wajib memenuhi rumus kenyamanan langkah Blondel:
$$2 \times \text{Optrede (O)} + \text{Antrede (A)} = 600 \text{ s/d } 640\text{ mm}$$
- **Tinggi Tanjakan (*Optrede / Riser*)**: $150\text{ mm} \le O \le 185\text{ mm}$.
- **Lebar Pijakan (*Antrede / Tread*)**: $280\text{ mm} \le A \le 300\text{ mm}$.
- **Bordes (*Landing*)**: Setiap perbedaan elevasi $> 3.50\text{ m}$ atau setelah 16–18 tanjakan wajib disediakan bordes dengan panjang minimal setara lebar bersih tangga.

### 3.3. Penurunan Muka Lantai Area Basah (*Wet Area Floor Drop*)
- Kamar mandi, toilet, tempat wudhu, dan area cuci wajib memiliki elevasi pelat lantai lebih rendah **$-30\text{ mm s/d } -50\text{ mm}$** dibanding lantai bersih ruangan sekitarnya untuk mencegah luapan air dan memudahkan kemiringan pipa pembuangan (*floor drain*).

---

## 4. Heuristik Perlindungan Cuaca & Drainase (Weatherproofing & Drainage Screening)

### 4.1. Kemiringan Minimum Bidang Atap (*Roof Pitch*)
Untuk mencegah terjadinya kebocoran dan genangan air (*water ponding*):

| Jenis Penutup Atap | Kemiringan Minimum ($\theta$) | Keterangan Teknis |
| :--- | :---: | :--- |
| **Metal Sheet / Spandek Zincalume** | $\ge 5^\circ \text{ s/d } 10^\circ$ | Overlap sambungan memanjang minimal 200 mm. |
| **Genteng Beton / Keramik** | $\ge 25^\circ \text{ s/d } 35^\circ$ | Di bawah $25^\circ$ berisiko tampias akibat angin kencang. |
| **Dak Beton (*Flat Concrete Roof*)** | $\ge 1\% \text{ s/d } 2\%$ ($1^\circ$) | Wajib dibuat screed kemiringan ke arah roof drain. |

> [!WARNING] Larangan Kemiringan Terbalik
> Dilarang keras memodelkan bidang atap dengan kemiringan yang mengalirkan air menuju dinding dalam tanpa talang (*negative back-fall drainage*).

### 4.2. Overhang Teritisan Atap (*Eaves / Canopy Projection*)
- Pada iklim tropis dengan curah hujan tinggi, lebar teritisan atap atau pelindung kanopi minimal **$800\text{ mm s/d } 1200\text{ mm}$** dari as dinding terluar untuk melindungi kusen dan dinding bata dari degradasi akibat tampias air hujan.

---

## 5. Ringkasan Verdict Kelayakan

Setiap eksekusi pemodelan menyeluruh menghasilkan status audit berdasarkan kriteria di atas:

```markdown
### 🏛️ Verdict Kelayakan BIM Engineer & Arsitek
- [x] Integritas Jalur Beban (Tumpuan Kolom Menerus & Dimensi Balok): 🟢 LULUS PENAPISAN
- [x] Headroom Clearance (Tinggi Bersih >= 2.20 m): 🟢 LULUS PENAPISAN
- [x] Ergonomi Sirkulasi & Tangga (Formula 2O + A): 🟢 LULUS PENAPISAN
- [x] Perlindungan Cuaca & Drainase (Kemiringan Atap & Teritisan): 🟢 LULUS PENAPISAN
```
*(Catatan: Penapisan ini tidak menggantikan pengesahan gambar struktural oleh Tenaga Ahli Terlisensi).*

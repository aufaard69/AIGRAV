# AIGRAV Rules Precedence & Loading Architecture

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION (C-05)` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan hierarki otoritas aturan yang mengikat dan memetakan mekanisme pemuatan (*loading mechanism*) seluruh dokumen instruksional di lingkungan Antigravity/AIGRAV.

---

## 1. Model Hierarki Otoritas Aturan (8 Tingkat)

Ketika terjadi perbedaan atau pertentangan instruksi antar-dokumen, agen AI dan sistem runtime **WAJIB** menyelesaikan konflik berdasarkan urutan hierarki otoritas berikut:

```
[Level 1: Platform & Security Constraints] (OS, Sandbox, .NET Runtime, Revit API Boundaries)
       │
       ▼
[Level 2: Approved Project Requirements] (Gambar Kerja Resmi, Spesifikasi Kontrak, SNI, Instruksi User)
       │
       ▼
[Level 3: AIGRAV Core Governance] (AGENTS.md: Single Writer, Fail-Closed, Native BIM, No Silent Fallback)
       │
       ▼
[Level 4: Domain Standards] (ELEMENT_REGISTRY_SPEC, QTO_STANDARDS, CLASH_AND_WARNING_POLICY)
       │
       ▼
[Level 5: Domain Skills / SOPs] (.agents/skills/*/SKILL.md: Prosedur Eksekusi Pemodelan & Audit)
       │
       ▼
[Level 6: Technical API References] (.agents/skills/*/references/*.md: Panduan C# & Python)
       │
       ▼
[Level 7: Project Plans & Working Notes] (Catatan Sementara, Blueprint Staging)
       │
       ▼
[Level 8: Historical / Archived Material] (archive/*: Murni Referensi Historis, Tidak Mengikat)
```

**Kaidah Baku:** Aturan pada tingkat yang lebih rendah tidak boleh mengesampingkan atau melanggar aturan pada tingkat yang lebih tinggi.

---

## 2. Bukti Mekanisme Pemuatan Runtime (*Runtime Loading Proof*)

Berdasarkan analisis arsitektur Antigravity CLI dan plugin loader:

### A. Pemuatan Otomatis (*Always Loaded at Session Start*)
Hanya berkas-berkas berikut yang dimuat otomatis ke dalam konteks master agent pada setiap sesi:
1. `C:\AIGRAV\AGENTS.md` (Aturan kerja root).
2. Peraturan terkonfigurasi pada `~/.gemini/config/plugins/` (aturan global Antigravity).

### B. Pemuatan Berbasis Kebutuhan (*On-Demand / Skill Invocation*)
Berkas-berkas berikut **TIDAK DIMUAT OTOMATIS** untuk menjaga efisiensi token dan mencegah polusi konteks:
1. Seluruh berkas `SKILL.md` di folder `c:\AIGRAV\.agents\skills\*`: Hanya dimuat ketika agen mendeteksi pemicu skill (*intent match*) atau dipanggil via slash command.
2. Seluruh berkas di subfolder `references/`: Hanya dibaca saat agen mengeksekusi implementasi teknis tertentu.
3. Berkas-berkas di folder `archive/` dan `KnowledgeBase/`: Tidak dimuat ke memori aktif kecuali dibaca secara eksplisit menggunakan tool pembaca berkas.

---

## 3. Strategi Ramping Master AGENTS.md

Untuk mencegah batas konteks (*context window exhaustion*) dan kebingungan instruksi:
- Master `AGENTS.md` dirampingkan menjadi **piagam inti yang ringkas** (< 120 baris), memuat:
  1. Mandat Peran & Otoritas 8 Tingkat.
  2. 6 Invarian Mutlak (Single Writer, Native First, Fail Closed, Controlled DirectShape, Zero Overlap, Anti Silent Failure).
  3. Peta penunjuk ke domain skills (*Skills Index*).
- Seluruh prosedur operasional detail didelegasikan ke domain skills yang dimuat secara dinamis saat dibutuhkan.

# AIGRAV Governance Conflict Decision Log

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Tujuan Dokumen:** Mencatat dan mendokumentasikan secara formal resolusi atas seluruh konflik, inkonsistensi, dan kesenjangan kemampuan (*capability gaps*) antara aturan tata kelola (dokumen `.md`) dengan kode sumber nyata (C# dan Python) di repositori AIGRAV, diperbarui dengan keputusan resmi yang telah disetujui Pengguna (*User Approved Decisions*).

---

## 1. Taksonomi Klasifikasi & Status Keputusan

Setiap konflik dikategorikan ke dalam kelas keputusan teknis dan diberi status siklus rilis:
- **Kelas Klasifikasi:** `WORDING`, `DOCUMENT_CONFLICT`, `RUNTIME_CAPABILITY_GAP`, `CODE_CHANGE_REQUIRED`, `USER_DECISION`.
- **Status Penerapan:**
  * `APPROVED — DESIGN DECISION`: Solusi dan spesifikasi arsitektur telah disetujui secara resmi oleh Pengguna.
  * `NOT YET IMPLEMENTED`: Belum diterapkan ke basis kode atau berkas aktif (menanti Fase 3/4).

---

## 2. Tabel Rekonsiliasi & Log Keputusan Formal

| ID | Topik Konflik | Sumber Masalah & Redaksi Asli | Fakta Kode Sumber Nyata | Klasifikasi | Keputusan Tata Kelola yang Disetujui | Status Keputusan |
| :---: | :--- | :--- | :--- | :---: | :--- | :---: |
| **C-01** | **Single Writer vs Multi-Agent Writes** | `AGENTS.md` & log sesi lama mengizinkan multi-subagent menulis model Revit secara paralel. | `AIGRAVRequestManager.cs` mengantrekan request ke `RevitExternalEventHandler.cs` di main UI thread. | `DOCUMENT_CONFLICT` & `WORDING` | **Single-Writer Invariant Terpilih**: Menulis ke Revit API dibatasi mutlak hanya untuk 1 Executor. Sub-agen hanya boleh membaca, menganalisis, atau merencanakan. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |
| **C-02** | **Kolom Schedule Pelat Sambungan (4 vs 5 Kolom)** | `qto_revit_api_guide.md:22` mencatat 4 kolom (tanpa tebal pelat). | `BimAuditSkill.cs:26240, 29471` telah mengimplementasikan tepat 5 kolom (`Lokasi, Tipe, Thickness, Qty, Berat`). | `DOCUMENT_CONFLICT` | **Baseline 5 Kolom Baku**: Kode sumber C# dipertahankan. Dokumen panduan lama diperbarui agar seragam dengan standar 5 kolom. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |
| **C-03** | **Larangan Mutlak DirectShape vs Sambungan Baja** | `AGENTS.md` Bab 1.3 melarang mutlak seluruh penggunaan DirectShape tanpa kecuali. | `ConnectionGeneratorSkill.cs:91,109,140` menggunakan DirectShape untuk pelat simpul baja aktif. | `CODE_CHANGE_REQUIRED` & `USER_DECISION` | **Approved Opsi 2A (Controlled Exception)**: Pertahankan DirectShape HANYA pada call sites eksisting di `ConnectionGeneratorSkill.cs`. Larang penambahan call site baru tanpa izin eksplisit. Larang Generic Model & DirectShape sebagai fallback pintu/jendela/dinding/lantai/atap/ruang. Pertahankan QTO solid & logging. Migrasi ke native family di Fase 4. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |
| **C-04** | **Macro TransactionGroup vs Atomic Per-Tool Tx** | Aturan Milestone menuntut 1 Undo menyeluruh (*macro TransactionGroup*) per milestone. | Setiap tool C# membuka dan menutup `Transaction` lokalnya sendiri. Belum ada API macro batch runner. | `RUNTIME_CAPABILITY_GAP` & `USER_DECISION` | **Approved Opsi 1A (Per-Tool + Operation Journal)**: Pertahankan transaksi per-tool mandiri saat ini. Implementasikan *Operation Journal* dan *Compensating Rollback* berbasis Element ID. Dilarang menyebut compensating rollback sebagai atomic rollback. TransactionGroup per milestone dicatat sebagai target masa depan. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |
| **C-05** | **Hierarki Otoritas & Rantai Pemuatan Aturan** | Berkas aturan tersebar di root, subfolder `.agents`, dan plugin tanpa urutan preseden yang jelas. | Sistem Antigravity memuat instruksi berdasarkan konteks injeksi dinamis. | `WORDING` & `DOCUMENT_CONFLICT` | **8-Tier Precedence Hierarchy**: Formalisasikan urutan preseden mengikat pada `RULES_PRECEDENCE_AND_LOADING.md` dengan instruksi pengguna sebagai Tier 1. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |
| **C-06** | **Ketiadaan Tool Pembuat Pintu, Jendela, Atap, Ruang** | Dokumentasi menyebut adanya `create_door` dan `create_window`. | Kode C# di `AIGRAVToolExecutor.cs:2153` gagal memasang hosted family dan tidak memiliki tool native pintu/jendela/atap/ruang. | `RUNTIME_CAPABILITY_GAP` & `USER_DECISION` | **Approved Urutan Prioritas Implementasi**: 1. `aigrav_create_door` & `aigrav_create_window` (wall-hosted), 2. `aigrav_create_roof`, 3. `aigrav_create_room`. Validasi parameter, kategori, host, respons error terstruktur, dan larangan silent fallback ke Generic Model. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |
| **C-07** | **Pembersihan Warning Buta (`DeleteAllWarnings`)** | `BimAuditSkill.cs:14493` menghapus seluruh warning yang muncul di transaksi failure context. | Pemanggilan tanpa seleksi dapat menutupi kegagalan nyata (*silent failure*). | `CODE_CHANGE_REQUIRED` | **Failure Allowlist & Baseline Diff**: Hentikan pembersihan buta. Ganti dengan penyaringan ID kegagalan spesifik dan pencatatan delta warning sebelum/sesudah transaksi. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |
| **C-08** | **Heuristik Struktur & Arsitektur sebagai Keharusan** | `AGENTS.md` Bab 4 menyatakan rasio $h/L$ dan headroom sebagai syarat mutlak tanpa klausul hukum. | Aturan ini adalah pedoman empiris (*rules of thumb*), bukan kode bangunan resmi atau sertifikasi struktur. | `WORDING` | **Preliminary Screening Heuristics**: Ubah status menjadi filter penapisan awal untuk mendeteksi *gross blunders*, disertai *Engineering Disclaimer* resmi. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |
| **C-09** | **Keamanan Pemuatan Dinamis DLL Hot-Reload** | `AIGRAVSkillLoader.cs` dapat memuat DLL dari folder disk ke memori Revit tanpa verifikasi hash. | Potensi eksekusi biner tidak terotorisasi di konteks proses Revit.exe. | `USER_DECISION` & `CODE_CHANGE_REQUIRED` | **Approved Opsi 4A (Production Lockdown)**: Nonaktifkan DLL hot-reload di produksi; pembaruan biner produksi wajib melalui restart Revit. Mode development boleh mempertahankan hot-reload dengan prompt persetujuan, SHA-256 hash, dan izin ACL. | `APPROVED — DESIGN DECISION` \| `NOT YET IMPLEMENTED` |

---

## 3. Matriks Tindak Lanjut Penerapan (Action Matrix)

| ID Konflik | Tindakan Staging (Fase 2) | Tindakan Implementasi Kode (Fase 3/4) | Status Eksekusi |
| :---: | :--- | :--- | :---: |
| **C-01** | Kodifikasi di `EXECUTION_AND_TRANSACTION_POLICY.md` & `AGENTS.md`. | Tambahkan pengunci konkurensi di Bridge jika multi-klien dideteksi. | `NOT YET IMPLEMENTED` |
| **C-02** | Kodifikasi di `QTO_STANDARDS.md`. | Buat acceptance test untuk memverifikasi 5 kolom schedule secara terprogram. | `NOT YET IMPLEMENTED` |
| **C-03** | Kodifikasi di `DIRECTSHAPE_EXCEPTION_POLICY.md` (Opsi 2A). | Pertahankan call sites aktif di Fase 3; migrasikan ke native loadable family di Fase 4. | `NOT YET IMPLEMENTED` |
| **C-04** | Kodifikasi di `EXECUTION_AND_TRANSACTION_POLICY.md` (Opsi 1A). | Bangun modul *Operation Journal* dan fungsi *Compensating Rollback* berbasis Element ID. | `NOT YET IMPLEMENTED` |
| **C-05** | Kodifikasi di `RULES_PRECEDENCE_AND_LOADING.md`. | Bersihkan redundant markdown files pada repositori produksi. | `NOT YET IMPLEMENTED` |
| **C-06** | Kodifikasi di `TOOL_CAPABILITY_REGISTRY_SPEC.md` & `ELEMENT_REGISTRY_SPEC.md`. | Buat implementasi C# `aigrav_create_door` dan `aigrav_create_window` (Prioritas 1). | `NOT YET IMPLEMENTED` |
| **C-07** | Kodifikasi di `CLASH_AND_WARNING_POLICY.md`. | Ganti `DeleteAllWarnings()` di `BimAuditSkill.cs` dengan ID allowlist. | `NOT YET IMPLEMENTED` |
| **C-08** | Kodifikasi di `ENGINEERING_GUARDRAILS.md`. | Perbarui pesan verdict audit pada seluruh respons agen. | `NOT YET IMPLEMENTED` |
| **C-09** | Kodifikasi di `HOT_RELOAD_SECURITY_POLICY.md` (Opsi 4A). | Nonaktifkan endpoint reload di mode produksi; tambahkan prompt & hash di mode dev. | `NOT YET IMPLEMENTED` |

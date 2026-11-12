# AIGRAV Source-to-Target Governance Migration Map

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Memetakan setiap dokumen, bab, standar, dan berkas aturan lama (*legacy rules*) di repositori AIGRAV ke dalam arsitektur tata kelola baru (Fase 2 Staging), mencantumkan perlakuan yang disetujui (*action item*), klasifikasi perubahan, serta *migration checklist* untuk dieksekusi secara resmi pada Fase 3 & 4.

---

## 1. Peta Migrasi Dokumen Sumber ke Target (Source-to-Target Mapping)

| Berkas Sumber Lama | Bagian / Klausul Spesifik | Dokumen Target Baru (Staging Phase 2) | Perlakuan yang Disetujui (*Action*) | Alasan Teknis & Pertimbangan | Status Keputusan |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **`C:\AIGRAV\AGENTS.md`** | **Bab 0: Mandat Peran Profesional** | `AGENTS.md` (Draft Staging) & `ENGINEERING_GUARDRAILS.md` | **Dipertahankan & Diperjelas** | Menjaga integritas profesional arsitek & insinyur tanpa menimbulkan klaim kepatuhan hukum semu. | `APPROVED — DESIGN DECISION` |
| **`C:\AIGRAV\AGENTS.md`** | **Bab 1.1: Single-Threaded Write** | `EXECUTION_AND_TRANSACTION_POLICY.md` & `AGENTS.md` | **Diperketat sebagai Invariant** | Menjamin tidak ada *race condition* pada akses Revit API dengan antrean tunggal. | `APPROVED — DESIGN DECISION` |
| **`C:\AIGRAV\AGENTS.md`** | **Bab 1.3: Larangan DirectShape** | `DIRECTSHAPE_EXCEPTION_POLICY.md` & `MODELING_HIERARCHY.md` | **Approved Opsi 2A (Controlled Exception)** | Pertahankan DirectShape HANYA pada call sites eksisting di `ConnectionGeneratorSkill.cs`; larang penambahan baru; larang untuk elemen primer; migrasi ke native di Fase 4. | `APPROVED — DESIGN DECISION` |
| **`C:\AIGRAV\AGENTS.md`** | **Bab 1.4 & Bab 7: Zero-Overlap Slabs** | `MODELING_HIERARCHY.md` & `CLASH_AND_WARNING_POLICY.md` | **Dipertahankan Mutlak** | Mencegah Z-fighting dan pembengkakan volume QTO beton. | `APPROVED — DESIGN DECISION` |
| **`C:\AIGRAV\AGENTS.md`** | **Bab 3: 4 Makro-Milestone Wajib** | `DEPENDENCY_AND_TASK_SCOPE.md` & `EXECUTION_AND_TRANSACTION_POLICY.md` | **Approved Opsi 1A (Per-Tool + Journal)** | Pertahankan transaksi mandiri saat ini dengan Operation Journal dan Compensating Rollback. TransactionGroup per milestone ditunda sebagai target masa depan. | `APPROVED — DESIGN DECISION` |
| **`C:\AIGRAV\AGENTS.md`** | **Bab 4: Triple-Layer Validation** | `BIM_SEMANTIC_VALIDATION.md` & `CLASH_AND_WARNING_POLICY.md` | **Dipisahkan menjadi Pre-Commit vs Post-Commit** | Mengatasi batasan teknis di mana fungsi audit read-only tidak dapat membatalkan transaksi yang telah di-commit. | `APPROVED — DESIGN DECISION` |
| **`C:\AIGRAV\AGENTS.md`** | **Bab 4.3: Engineering Sanity Gate** | `ENGINEERING_GUARDRAILS.md` | **Diubah Menjadi Heuristik Penapisan Awal** | Mengklarifikasi batasan hukum bahwa heuristik bukan sertifikasi struktur resmi Insinyur Berlisensi. | `APPROVED — DESIGN DECISION` |
| **`C:\AIGRAV\AGENTS.md`** | **Bab 6: Standar Schedule Baja & Kolom A** | `QTO_STANDARDS.md` | **Dipertahankan & Dikodifikasi Ulang** | Format 5 kolom pelat, 3 baut, 7 baseplate, 6 balok/kolom, 5 angkur, 7 voute dijadikan baseline resmi `AIGRAV-STD-QTO-REVIT-2026-V2`. | `APPROVED — DESIGN DECISION` |
| **`KnowledgeBase\qto_revit_api_guide.md`** | Klausul 4 Kolom Pelat Sambungan | `QTO_STANDARDS.md` | **Ditandai Superseded / Diperbarui** | Menyelesaikan konflik dengan kode C# aktif yang menggunakan 5 kolom (`fLok, fType, fThk, fQty, fWeight`). | `APPROVED — DESIGN DECISION` |
| **`KnowledgeBase\revit_api_reference.md`** | Daftar Endpoint Alat & Method Revit API | `TOOL_CAPABILITY_REGISTRY_SPEC.md` | **Approved Prioritas Pengembangan C#** | Menginventarisasi gap alat; menetapkan prioritas: 1. Door & Window, 2. Roof, 3. Room (tanpa silent fallback ke Generic Model). | `APPROVED — DESIGN DECISION` |
| **`AIGRAVSkillLoader.cs`** | Pemuatan Biner DLL Hot-Reload | `HOT_RELOAD_SECURITY_POLICY.md` | **Approved Opsi 4A (Production Lockdown)** | Nonaktifkan hot-reload DLL di produksi; pembaruan biner wajib restart Revit; mode development boleh mempertahankan hot-reload terkontrol. | `APPROVED — DESIGN DECISION` |
| **`.agents\skills\revit-milestone-modeling\`** | Seluruh Berkas `SKILL.md` | `DEPENDENCY_AND_TASK_SCOPE.md` & `EXECUTION_AND_TRANSACTION_POLICY.md` | **Dipertahankan sebagai On-Demand Skill** | Dihubungkan dengan task classifier sehingga hanya dipanggil saat pemodelan gedung penuh diminta. | `APPROVED — DESIGN DECISION` |
| **`.agents\skills\cad-to-revit-drafting-views\`** | Seluruh Berkas `SKILL.md` | `MODELING_HIERARCHY.md` (Disiplin 5) | **Dipertahankan sebagai On-Demand Skill** | Menangani alur kerja drafting CAD ke Revit secara terisolasi. | `APPROVED — DESIGN DECISION` |
| **`.agents\skills\revit-slanted-wall-modeling\`** | Seluruh Berkas `SKILL.md` | `MODELING_HIERARCHY.md` (Disiplin 2) | **Dipertahankan sebagai On-Demand Skill** | Standar dinding miring dan perpotongan bukaan. | `APPROVED — DESIGN DECISION` |
| **`.agents\skills\revit-adaptive-massing-modeling\`** | Seluruh Berkas `SKILL.md` | `MODELING_HIERARCHY.md` (Disiplin 4) | **Dipertahankan sebagai On-Demand Skill** | Alternatif profesional terhadap DirectShape untuk geometri bebas. | `APPROVED — DESIGN DECISION` |
| **`.agents\skills\revit-qto-schedule-audit\`** | Seluruh Berkas `SKILL.md` | `QTO_STANDARDS.md` | **Dipertahankan sebagai On-Demand Skill** | Standar audit schedule otomatis berbasis kode C#. | `APPROVED — DESIGN DECISION` |
| **`.agents\skills\revit-voute-connection-qto\`** | Seluruh Berkas `SKILL.md` | `QTO_STANDARDS.md` | **Dipertahankan sebagai On-Demand Skill** | Ekstraksi volume solid eksak voute baja. | `APPROVED — DESIGN DECISION` |

---

## 2. Checklist Migrasi Masa Depan (*Future Migration Checklist - Phase 3 & 4 Execution*)

> [!NOTE] PEMBERITAHUAN PELAKSANAAN MIGRASI
> Checklist berikut dirancang untuk dieksekusi secara bertahap pada **Fase 3 & Fase 4** setelah persetujuan rilis resmi diberikan.  
> **JANGAN MENGEKSEKUSI CHECKLIST INI PADA FASE 2 STAGING.**

- [ ] **Langkah 1: Backup & Snapshot Arsip (Pra-Fase 3)**
  - Buat arsip snapshot `.zip` dari `C:\AIGRAV\AGENTS.md` dan folder `.agents\skills\` sebelum modifikasi diterapkan.
- [ ] **Langkah 2: Promosi Master AGENTS.md (Awal Fase 3)**
  - Salin `C:\AIGRAV\_governance_review\phase2\AGENTS.md` ke `C:\AIGRAV\AGENTS.md` untuk merampingkan master rule menjadi deklarasi otoritas, 6 invarian inti, dan penunjuk skill on-demand.
- [ ] **Langkah 3: Pembaruan Dokumentasi KnowledgeBase (Fase 3)**
  - Perbarui `C:\AIGRAV\KnowledgeBase\qto_revit_api_guide.md` ke format 5 kolom pelat baku.
  - Perbarui `C:\AIGRAV\KnowledgeBase\revit_api_reference.md` dengan status kesenjangan kapabilitas alat.
- [ ] **Langkah 4: Implementasi Endpoint C# Prioritas 1 (Fase 3)**
  - Bangun implementasi native wall-hosted `aigrav_create_door` dan `aigrav_create_window` dengan validasi parameter, host, dan kategori.
  - Terapkan mekanisme *Operation Journal* dan *Compensating Rollback* (Opsi 1A).
- [ ] **Langkah 5: Penguncian Hot-Reload Produksi (Fase 3)**
  - Terapkan Opsi 4A di `AIGRAVSkillLoader.cs`: blokir reload biner pada mode produksi.
- [ ] **Langkah 6: Migrasi Sambungan Baja ke Native Family (Fase 4)**
  - Buat library RFA parametrik pelat simpul baja dan refactor `ConnectionGeneratorSkill.cs` untuk menghentikan pemanggilan DirectShape (Opsi 2A).
- [ ] **Langkah 7: Verifikasi Eksekusi & Uji Regresi (Fase 4 Akhir)**
  - Jalankan seluruh 15 pengujian pada acceptance test matrix (`ACCEPTANCE_TEST_MATRIX.md`).

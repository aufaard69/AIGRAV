# AIGRAV Governance Reconciliation (Phase 2 Staging Pack)

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tanggal:** 12 November 2026  
**Auditor / Rekonsiliator:** Lead BIM Engineer & Antigravity Autonomous Orchestrator  
**Direktori Staging:** `C:\AIGRAV\_governance_review\phase2\`  
**Dasar Rekonsiliasi:** 
1. Laporan Audit Fisik Kode: `C:\AIGRAV\AUDIT\AIGRAV_RULES_CODE_VALIDATION.md`
2. Tinjauan Kritis Reviewer: `AIGRAV_CODE_VALIDATION_REVIEW.md`
3. Instruksi Kerja: `ANTIGRAVITY_PHASE2_PROMPT_ADDENDUM.md`
4. Mandat Addendum: `AIGRAV_LIVE_CAPTURE_AND_VISUAL_FEEDBACK_ADDENDUM.md`
5. Keputusan Arsitektur Pengguna: Persetujuan Checkpoint 1 s/d 4 serta Koreksi Wajib 1 & 2 (12 November 2026)

---

## 1. Pernyataan Batasan Keamanan & Kepatuhan (*Safety Boundaries*)

1. **Zero Active File Modification:** Seluruh dokumen dalam paket ini berada dalam folder staging perancangan tata kelola. **Tidak ada satu pun file kode sumber (C#/Python), file konfigurasi, add-in DLL, file aturan aktif (`C:\AIGRAV\AGENTS.md`), maupun dokumen Revit aktif yang diubah, ditimpa, atau dihapus.**
2. **Status Non-Aktif:** Seluruh dokumen dalam folder ini berstatus proposal desain yang telah disetujui pengguna (`APPROVED — DESIGN DECISION`) namun **belum diimplementasikan ke dalam basis kode produksi** (`NOT YET IMPLEMENTED`). Implementasi kode baru akan dijalankan pada Fase 3 setelah persetujuan rilis resmi diberikan.
3. **Pemisahan Disiplin:** Memisahkan secara tegas antara apa yang telah berjalan di kode produksi saat ini dengan apa yang menjadi target pengembangan Fase 3 & 4.

---

## 2. Struktur Paket Deliverables Fase 2 (21 Dokumen Termasuk Final Report)

Paket staging ini terdiri dari 20 dokumen standar tata kelola terstruktur dan 1 laporan penutupan final:

| No | Nama File Dokumen | Fungsi & Cakupan Utama | Status Keputusan |
| :---: | :---|:---|:---:|
| 1 | [`README_PHASE2.md`](file:///C:/AIGRAV/_governance_review/phase2/README_PHASE2.md) | Ikhtisar paket staging, status keselamatan, dan peta navigasi dokumen. | `APPROVED` |
| 2 | [`AGENTS.md`](file:///C:/AIGRAV/_governance_review/phase2/AGENTS.md) | Draf ringkas aturan induk (*Master Entry Point*): 8-tier hierarchy, 7 Core Invariants. | `APPROVED` |
| 3 | [`RULES_PRECEDENCE_AND_LOADING.md`](file:///C:/AIGRAV/_governance_review/phase2/RULES_PRECEDENCE_AND_LOADING.md) | Model 8 tingkat hierarki otoritas aturan dan mekanisme pemuatannya. | `APPROVED` |
| 4 | [`MODELING_HIERARCHY.md`](file:///C:/AIGRAV/_governance_review/phase2/MODELING_HIERARCHY.md) | Taksonomi 5 disiplin model (Arsitektur, Struktur, MEP, Site, Detail Drafting). | `APPROVED` |
| 5 | [`ELEMENT_REGISTRY_SPEC.md`](file:///C:/AIGRAV/_governance_review/phase2/ELEMENT_REGISTRY_SPEC.md) | Spesifikasi lengkap kategori Revit, metode native, host, dan parameter wajib. | `APPROVED` |
| 6 | [`FAMILY_SELECTION_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/FAMILY_SELECTION_POLICY.md) | 5 tangga seleksi family, kompatibilitas host, dan larangan Generic Model. | `APPROVED` |
| 7 | [`TOOL_CAPABILITY_REGISTRY_SPEC.md`](file:///C:/AIGRAV/_governance_review/phase2/TOOL_CAPABILITY_REGISTRY_SPEC.md) | Matriks 3 lapis: MCP Tool $\rightarrow$ HTTP Route $\rightarrow$ C# Method & Prioritas Gap. | `APPROVED` |
| 8 | [`DEPENDENCY_AND_TASK_SCOPE.md`](file:///C:/AIGRAV/_governance_review/phase2/DEPENDENCY_AND_TASK_SCOPE.md) | Task Scope Classifier (Level 1–4), Dependency DAG, dan View Coverage per-task. | `APPROVED` |
| 9 | [`EXECUTION_AND_TRANSACTION_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/EXECUTION_AND_TRANSACTION_POLICY.md) | Invarian Single-Writer, Transaksi Opsi 1A, Journal 3-Status, dan Compensating Rollback. | `APPROVED` |
| 10 | [`BIM_SEMANTIC_VALIDATION.md`](file:///C:/AIGRAV/_governance_review/phase2/BIM_SEMANTIC_VALIDATION.md) | Pre-Commit Deterministic Gate vs Post-Commit Audit + Siklus Visual 5 Tahap. | `APPROVED` |
| 11 | [`LIVE_CAPTURE_AND_VISUAL_FEEDBACK_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/LIVE_CAPTURE_AND_VISUAL_FEEDBACK_POLICY.md) | Siklus Observe-Execute-Inspect-Correct-Verify, View Coverage Manifest, dan Repair Plan. | `APPROVED` |
| 12 | [`CLASH_AND_WARNING_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/CLASH_AND_WARNING_POLICY.md) | 2-fase clash (BBox + Solid), allowlist kontak sah, dan audit warning baseline. | `APPROVED` |
| 13 | [`ENGINEERING_GUARDRAILS.md`](file:///C:/AIGRAV/_governance_review/phase2/ENGINEERING_GUARDRAILS.md) | Klasifikasi batas heuristik awal vs sertifikasi kalkulasi rekayasa struktur. | `APPROVED` |
| 14 | [`QTO_STANDARDS.md`](file:///C:/AIGRAV/_governance_review/phase2/QTO_STANDARDS.md) | Standarisasi baku 5 kolom pelat, kalibrasi voute volume, dan numerik `7850.0`. | `APPROVED` |
| 15 | [`DIRECTSHAPE_EXCEPTION_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/DIRECTSHAPE_EXCEPTION_POLICY.md) | Kebijakan berpagar Opsi 2A, audit call-site, dan roadmap native Fase 4. | `APPROVED` |
| 16 | [`HOT_RELOAD_SECURITY_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/HOT_RELOAD_SECURITY_POLICY.md) | Penguncian Opsi 4A (Production lockdown & restart requirement). | `APPROVED` |
| 17 | [`CONFLICT_DECISION_LOG.md`](file:///C:/AIGRAV/_governance_review/phase2/CONFLICT_DECISION_LOG.md) | Catatan resmi resolusi seluruh konflik dengan status keputusan pengguna. | `APPROVED` |
| 18 | [`SOURCE_TO_TARGET_MIGRATION_MAP.md`](file:///C:/AIGRAV/_governance_review/phase2/SOURCE_TO_TARGET_MIGRATION_MAP.md) | Pemetaan migrasi seluruh file sumber lama ke target arsitektur modular baru. | `APPROVED` |
| 19 | [`ACCEPTANCE_TEST_MATRIX.md`](file:///C:/AIGRAV/_governance_review/phase2/ACCEPTANCE_TEST_MATRIX.md) | 27 skenario uji penerimaan (TC-01 s/d TC-15 umum + TC-16 s/d TC-27 Live Capture). | `APPROVED` |
| 20 | [`UNVERIFIED_ITEMS_AND_USER_DECISIONS.md`](file:///C:/AIGRAV/_governance_review/phase2/UNVERIFIED_ITEMS_AND_USER_DECISIONS.md) | Inventarisasi item belum terverifikasi dan keputusan resmi pengguna. | `APPROVED` |
| 21 | [`FINAL_REVIEW_REPORT.md`](file:///C:/AIGRAV/_governance_review/phase2/FINAL_REVIEW_REPORT.md) | Laporan Penutupan & Hasil Finalisasi Rekonsiliasi Tata Kelola Fase 2. | `APPROVED` |

---

## 3. Rangkuman Integrasi Dua Koreksi Wajib

### A. Koreksi 1: Live Capture & Visual Feedback Loop
1. **Masuk sebagai Core Invariant #7 di AGENTS.md:** Validasi visual langsung menjadi prinsip wajib tata kelola AIGRAV.
2. **Siklus Tertutup 5 Tahap:** $\text{Observe} \rightarrow \text{Execute} \rightarrow \text{Inspect} \rightarrow \text{Correct} \rightarrow \text{Verify}$.
3. **View Coverage Manifest:** Dilarang menganggap 1 sudut pandang 3D cukup untuk validasi gedung. Wajib memeriksa Tampak Depan, Belakang, Samping Kiri/Kanan, Atas, Denah, Potongan, 3D Isometrik, dan Interior relevan.
4. **Protokol Uncovered View:** Sudut pandang yang tidak dapat diakses wajib dicatat sebagai `UNCOVERED`, bukan `PASS`. Batch tidak boleh disahkan jika coverage belum lengkap.
5. **Repair Plan & Batas 2 Iterasi:** Cacat visual menghasilkan repair plan terstruktur, wajib re-capture seluruh view terdampak, dan dibatasi maksimal 2 iterasi otomatis sebelum eskalasi ke Pengguna.
6. **Audit Realitas Kode Eksisting:** `aigrav_capture_revit_viewport` (1 view aktif) dan `aigrav_inspect_model_visually` (3-4 view dokumen). Ketiadaan generator orientasi kamera otomatis diakui secara jujur sebagai target Fase 3.
7. **12 Kasus Uji Penerimaan Khusus:** TC-16 s/d TC-27 ditambahkan ke `ACCEPTANCE_TEST_MATRIX.md`.

### B. Koreksi 2: Operation Journal & Compensating Rollback
1. **Diferensiasi Status Operasi:** Jurnal operasi wajib membedakan dengan tegas:
   - `CREATED`: Elemen baru yang aman dihapus via `doc.Delete()` saat kompensasi.
   - `MODIFIED`: Elemen eksisting yang diubah; **dilarang di-delete**, wajib dikembalikan ke parameter awal melalui *before-state snapshot* dan *inverse operation*.
   - `DELETED`: Elemen eksisting yang terhapus; membutuhkan strategi pemulihan khusus (*recreate from snapshot*).
2. **Larangan Blind Delete:** Dilarang menjalankan `doc.Delete()` terhadap seluruh Element ID tanpa membedakan status entri.
3. **Operasi NON_COMPENSABLE:** Modifikasi destruktif yang belum memiliki skrip pemulihan aman berstatus `NON_COMPENSABLE` dan memerlukan eskalasi darurat.
4. **Penegasan Terminologi:** Compensating Rollback **TIDAK BOLEH DISEBUT SEBAGAI ATOMIC ROLLBACK**.

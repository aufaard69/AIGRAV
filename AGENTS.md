# AIGRAV Revit Standard Operating Protocol (Mandatory Workflow)

Setiap perintah yang berkaitan dengan pemodelan, audit, atau modifikasi di Autodesk Revit melalui AIGRAV WAJIB mengikuti protokol 2-fase berikut:

---

## 1. Fase 1: Pre-Execution Review (Wajib Sebelum Eksekusi)
Sebelum melakukan modifikasi atau pembuatan elemen di Revit, AIGRAV TIDAK BOLEH langsung mengeksekusi script/transaksi tanpa verifikasi:
1. **Verifikasi Gambar Standar & Spesifikasi Acuan**:
   - Lakukan cross-check langsung ke gambar kerja standar resmi (`STD-BJ-01 s/d STD-BJ-07`, `STD-PDS-01 s/d STD-PDS-15`, atau dokumen referensi proyek terkait).
   - Pastikan nilai dimensi (tebal, panjang, lebar, diameter baut/angkur, pitch, gauge) sesuai gambar detail asli dan bebas dari *typo / data-entry error*.
2. **Audit Data Model Eksisting**:
   - Periksa tipe, kategori, jumlah elemen target, elevasi, dan parameter aktif di Revit.
   - Perhatikan format angka desimal lokal (misal koma `,` vs titik `.` pada Advance Steel / Revit API).
3. **Formulasi Rencana Eksekusi (Action Plan)**:
   - Buat matriks aksi yang jelas sebelum transaksi dijalankan.

---

## 2. Fase 2: Dekomposisi 3 Sub-Agent Paralel
Setelah tahapan Pre-Execution Review selesai dan disetujui, pekerjaan dipecah menjadi **3 Sub-Agent** yang bekerja secara terkoordinasi untuk memastikan kecepatan dan kualitas tinggi:

### Model A: Functional Specialization (Task Verifikasi, Sambungan & QA/QC)
* **Sub-Agent 1 (Standard & QA/QC Reviewer)**:
  - Memverifikasi kepatuhan terhadap standar teknis, engineering rules, dan drawing references.
  - Memvalidasi parameter input sebelum dan sesudah eksekusi.
* **Sub-Agent 2 (Revit Bridge Executor)**:
  - Fokus mengeksekusi transaksi modifikasi / pemodelan API di Revit secara batch.
  - Mengelola transaction rollback, locking safety, dan regenerasi model.
* **Sub-Agent 3 (Validation & Inspection Auditor)**:
  - Memverifikasi geometri 3D pasca-eksekusi (Bounding Box, Parameter Values, Warnings).
  - Menyusun rekapitulasi / laporan audit yang presisi untuk BIM Coordinator.

### Model B: Partitioning Skala Besar (Pemodelan Elemen / Editing Masif)
* **Sub-Agent 1 (Zone/Batch A)**: Menangani Substruktur & Kolom / Grid 1 s/d 25.
* **Sub-Agent 2 (Zone/Batch B)**: Menangani Rafter & Primary Framing / Grid 26 s/d 50.
* **Sub-Agent 3 (Zone/Batch C)**: Menangani Secondary Framing, Bracing & Sambungan Khusus.

---

## 3. Post-Execution Safety Gate
- Lakukan regenerasi model (`Regenerate`).
- Pastikan tidak ada *error* atau *orphan elements*.
- Sajikan laporan ringkas berupa tabel perbandingan (Sebelum vs Sesudah) kepada pengguna.

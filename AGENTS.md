# AIGRAV Revit Standard Operating Protocol (Mandatory Workflow)

Setiap perintah yang berkaitan dengan pemodelan, audit, atau modifikasi di Autodesk Revit melalui AIGRAV WAJIB mengikuti protokol presisi berikut:

---

## 1. Prinsip Fundamental Arsitektur Revit & Single-Threaded Execution
1. **Revit API adalah Single-Threaded untuk Transaksi Tulis (Write Transactions)**:
   - DILARANG membagi beberapa sub-agent untuk menulis/membuat elemen ke Revit secara paralel (karena akan memicu *transaction deadlock*, antrean lambat, atau *race condition*).
   - **Hanya ada SATU (1) Revit Bridge Executor** yang memegang kontrol transaksi tulis.
   - Analisis, pembacaan data, *planning blueprint*, dan validasi visual BOLEH berjalan paralel.
2. **Kejujuran Laporan & Larangan Silent Failure**:
   - DILARANG menelan eror dengan blok `catch { }` kosong.
   - DILARANG mencetak klaim palsu seperti *"100% SUKSES DENGAN ZERO ERROR"* tanpa verifikasi independen.
   - Setiap panggilan eksekusi wajib mengembalikan JSON terstruktur: `total_created`, `total_failed`, `failed_items` (pesan eror spesifik), dan daftar ID elemen baru.

---

## 2. Fase 1: Pre-Execution Review & Spatial Blueprinting
Sebelum memodifikasi atau membuat elemen di Revit:
1. **Verifikasi Gambar Acuan & Standar Resmi**:
   - Cross-check standar teknis (`STD-BJ-01 s/d 07`, `STD-PDS-01 s/d 15`, atau referensi proyek).
   - Pastikan dimensi (mm), tebal pelat, diameter baut, dan elevasi bebas dari salah ketik.
2. **Audit Datum & Konteks Model**:
   - Panggil `aigrav_get_project_context()` untuk memeriksa Level aktif, sistem Grid, dan ketersediaan FamilySymbol.
3. **Formulasi Rencana Eksekusi (Action Plan)**:
   - Tuliskan tabel koordinat $(X, Y, Z)$ dalam milimeter dan jangkar relatif (`anchor points`) sebelum eksekusi dimulai.

---

## 3. Fase 2: Eksekusi Berbasis Relative Helper (1 Executor)
1. **Gunakan Relative Helpers**:
   - Utamakan pemanggilan fungsi relatif (`place_on_top_of`, `connect_elements`, `create_grid_matrix`) daripada menembak koordinat absolut di ruang hampa.
2. **Safe Transaction Sandbox**:
   - Setiap batch operasi dijalankan di dalam `TransactionGroup` dengan pengembalian *Auto-Spatial Telemetry* (`spatial_bounds_mm`, `is_floating`, dan `unjoined_bbox_clashes`).

---

## 4. Fase 3: Dual-Layer Independent Validation & Hak Veto Validator
Validator bertindak secara independen dan **MEMILIKI HAK VETO** penuh untuk menolak hasil Executor:

### Layer 1: Validasi Deterministik Berbasis Kode (Angka & Geometri Pasti)
Validator wajib memanggil tool validasi independen:
1. **Deteksi Benturan Solid (`aigrav_detect_spatial_clashes`)**:
   - Memastikan furnitur tidak menembus dinding (`OST_Furniture` vs `OST_Walls`), dan pelat lantai tidak menembus balok.
2. **Audit Material & Transparansi (`aigrav_audit_material_transparency`)**:
   - Kaca jendela/curtain wall **wajib transparan** (`Transparency >= 50%`). Jika transparan $< 50\%$ (abu-abu solid), status ditolak (*REJECT*).
   - Fasia dan atap wajib memiliki material resmi (tidak boleh material default hijau neon).
3. **Audit Peringatan Revit (`aigrav_audit_unresolved_warnings`)**:
   - Memastikan tidak ada *duplicate elements* atau *overlapping instances*.

### Layer 2: Validasi Visual (Checklist Gambar PNG)
Validator memanggil `aigrav_inspect_model_visually()` atau `aigrav_capture_revit_viewport()`:
* Cek visual **khusus untuk tampilan dan proporsi estetika**, bukan untuk angka dimensi:
  - [ ] Apakah warna render material tampak natural?
  - [ ] Apakah kaca tembus pandang?
  - [ ] Apakah teks anotasi denah saling bertumpuk?
  - [ ] Apakah ada objek lansekap (pohon) yang menutupi tampak depan bangunan?

### Protokol Hak Veto:
* Jika salah satu syarat Layer 1 atau Layer 2 gagal:
  * Validator **REJECT** hasil eksekusi dan mengirimkan daftar poin koreksi spesifik ke Executor.
  * Executor melakukan revisi (maksimal 2 iterasi).
  * Jika setelah 2 iterasi masih gagal, eskalasikan langsung ke pengguna beserta laporannya.

---

## 5. Post-Execution Safety Gate
- Lakukan regenerasi model (`Regenerate`).
- Pastikan tidak ada *error* atau *orphan elements*.
- Sajikan laporan ringkas berupa tabel perbandingan (Sebelum vs Sesudah) kepada pengguna.

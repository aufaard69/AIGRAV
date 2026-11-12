# AIGRAV Execution & Transaction Management Policy

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION (Opsi 1A)` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan kontrak arsitektur kepemilikan transaksi (*Transaction Ownership*), invarian antrean eksekusi serial (*Single-Writer Queue*), spesifikasi rinci *Operation Journal* (CREATED, MODIFIED, DELETED), kebijakan *Compensating Rollback*, serta kewajiban *Post-Batch Live Capture*.

---

## 1. Invarian Single-Writer Dispatcher

Revit API merupakan subsistem *single-threaded* untuk transaksi tulis (*write operations*). AIGRAV menerapkan kebijakan single-writer yang terbukti secara teknis di kode C#:

1. **Jalur Antrean Tunggal:**  
   Seluruh permintaan tulis yang dikirim dari klien FastMCP atau HTTP Bridge **WAJIB** dimasukkan ke dalam antrean aman `ConcurrentQueue<AIGRAVRequest>` di `AIGRAVRequestManager.cs`.
2. **Eksekusi Serial pada UI Thread:**  
   Metode `RevitExternalEventHandler.Execute(UIApplication app)` mendeku antrean satu per satu secara berurutan dan menjalankannya langsung di Revit Main UI Thread.
3. **Pernyataan Garansi:**  
   > *"Seluruh permintaan modifikasi dokumen yang melewati antrean AIGRAV diproses secara serial pada thread utama Revit API, sehingga tidak ada dua operasi tulis yang berjalan bersamaan di dokumen aktif."*  
   *(Catatan arsitektur: Garansi ini berlaku untuk alur resmi AIGRAV Bridge; sistem tetap harus waspada terhadap add-in eksternal pihak ketiga yang mungkin memodifikasi dokumen di luar antrean AIGRAV).*

---

## 2. Kontrak Kepemilikan Transaksi (*Transaction Ownership Contract*)

Berdasarkan audit fisik kode (57 kemunculan `new Transaction` di C#), saat ini setiap tool C# mengelola dan meng-commit transaksinya sendiri secara atomik (*Tool-Level Isolated Transaction*).

### A. Aturan Transaksi Lokal (Kondisi Produksi Terpilih — Opsi 1A)
- **Status Keputusan:** `APPROVED — DESIGN DECISION (Opsi 1A)` | `NOT YET IMPLEMENTED`
- Setiap tool C# bertanggung jawab penuh membuka, memvalidasi pre-commit, meregenerasi model, dan menutup transaksinya sendiri:
```csharp
using Transaction tx = new(doc, "AIGRAV - [Nama Operasi]");
tx.Start();
// ... modifikasi elemen Revit ...
doc.Regenerate(); // Wajib di dalam transaksi sebelum commit
tx.Commit();
```

### B. Larangan Keras Transaksi Bersarang (*Nested Transaction Prohibition*)
Revit API melarang keras membuka transaksi baru ketika ada transaksi yang sedang aktif:
- Tool pembantu (*helper functions*) **DILARANG** membuka `tx.Start()` jika dipanggil dari dalam fungsi lain yang sudah membuka transaksi.
- Setiap metode helper wajib menerima konteks: apakah metode ini adalah pemilik transaksi (`isTransactionOwner: true`) atau bergabung dalam transaksi yang sudah dibuka caller (`isTransactionOwner: false`).

---

## 3. Desain Rollback: Operation Journal & Compensating Rollback (Opsi 1A Approved)

Sesuai keputusan arsitektur yang disetujui pengguna (**Opsi 1A**), mekanisme pemulihan kegagalan pemodelan diatur secara ketat dan deterministik:

### 3.1. Spesifikasi Tiga Kategori Status Elemen dalam Operation Journal
Jurnal operasi **WAJIB MEMBEDAKAN** dengan tegas antara elemen baru, elemen terubah, dan elemen terhapus:

```json
{
  "operation_id": "op_20261112_001",
  "task_id": "task_wall_segment_A",
  "tool_name": "create_wall",
  "timestamp": "2026-11-12T05:15:00Z",
  "compensability": "COMPENSABLE",
  "entries": [
    {
      "element_id": 128490,
      "mutation_type": "CREATED",
      "category": "OST_Walls",
      "description": "Dinding bata baru as 39-44"
    },
    {
      "element_id": 128100,
      "mutation_type": "MODIFIED",
      "category": "OST_StructuralColumns",
      "before_state": {
        "parameters": {
          "INSTANCE_TOP_OFFSET_PARAM": 0.0,
          "ALL_MODEL_INSTANCE_COMMENTS": "Original"
        }
      },
      "inverse_operation": "restore_parameters"
    },
    {
      "element_id": 127950,
      "mutation_type": "DELETED",
      "category": "OST_GenericModel",
      "before_state_snapshot": { ... },
      "recovery_strategy": "recreate_from_snapshot"
    }
  ]
}
```

### 3.2. Prosedur Pembatalan Kompensasi (*Compensating Rollback*)
Jika terjadi kegagalan pada langkah berikutnya atau pengguna membatalkan eksekusi, sistem menjalankan prosedur pembatalan berbasis status entri:

> [!CAUTION] LARANGAN BLIND DELETE PADA SELURUH ELEMENT ID
> **DILARANG MENJALANKAN `doc.Delete()` SECARA BUTA TERHADAP SELURUH ELEMENT ID TANPA MEMBEDAKAN STATUS OPERASI.**  
> Menghapus ID dari elemen yang berstatus `MODIFIED` akan melenyapkan elemen eksisting milik proyek!

1. **Untuk Elemen `CREATED`:**  
   Elemen baru ini **aman dihapus** secara langsung menggunakan `doc.Delete(createdElementIds)` dalam transaksi kompensasi baru.
2. **Untuk Elemen `MODIFIED`:**  
   Elemen eksisting yang diubah **TIDAK BOLEH DIHAPUS**. Sistem wajib menjalankan *inverse operation* dengan memulihkan parameter dan koordinat dari `before_state` snapshot.
3. **Untuk Elemen `DELETED`:**  
   Elemen eksisting yang telah terhapus memerlukan strategi pemulihan khusus (*recreate from snapshot*).
4. **Operasi `NON_COMPENSABLE`:**  
   Jika suatu operasi modifikasi destruktif belum memiliki skrip *inverse operation* yang aman, operasi tersebut wajib ditandai `compensability: "NON_COMPENSABLE"`. Jika operasi ini gagal di tengah jalan, sistem **DILARANG MENCOBA ROLLBACK OTOMATIS SECARA BUTA** dan wajib melakukan eskalasi darurat kepada Pengguna.

> [!WARNING] Batasan Terminologi Penting (Bukan Atomic Rollback)
> *Compensating Rollback* **TIDAK BOLEH DISEBUT SEBAGAI ATOMIC ROLLBACK**.  
> Pada compensating rollback, elemen perantara telah sempat eksis dan ter-commit ke dalam basis data Revit, dan riwayat Undo Revit mencatat transaksi-transaksi per-tool ditambah transaksi kompensasi baru. Keuntungan Opsi 1A adalah stabilitas tinggi dan zero-risk transaksi menggantung (*no stale locks*).

### 3.3. Status TransactionGroup per Milestone
- Pembungkusan transaksi multi-step ke dalam satu `TransactionGroup` makro (yang melebur seluruh langkah menjadi 1 item Undo via `tg.Assimilate()`) **BUKAN UNTUK IMPLEMENTASI SEKARANG**.
- TransactionGroup dicatat secara resmi sebagai **target riset & pengembangan masa depan (*future milestone development target*)** setelah infrastruktur batch Bridge selesai dibangun dan diuji ketahanannya.

---

## 4. Kewajiban Penangkapan Visual Pasca-Batch (*Mandatory Post-Batch Live Capture*)

Sesuai mandat [`LIVE_CAPTURE_AND_VISUAL_FEEDBACK_POLICY.md`](file:///C:/AIGRAV/_governance_review/phase2/LIVE_CAPTURE_AND_VISUAL_FEEDBACK_POLICY.md):
1. **Wajib Capture Setelah Commit:**  
   Segera setelah suatu batch transaksi selesai di-commit dan `doc.Regenerate()` dipanggil, sistem **WAJIB MENGAMBIL TANGKAPAN LAYAR VIEWPORT** sesuai cakupan yang ditetapkan oleh *View Coverage Manifest*.
2. **Larangan Selesai Tanpa Bukti Visual:**  
   Batch transaksi **DILARANG DINYATAKAN SUKSES/SELESAI** jika tangkapan layar belum berhasil diekspor dan diverifikasi.

---

## 5. Protokol Regenerasi Model (*Model Regeneration Protocol*)

1. **Lokasi Regenerate:** Panggilan `doc.Regenerate()` **WAJIB** berada di dalam blok `using Transaction`, tepat sebelum pemanggilan `tx.Commit()`.
2. **Pencegahan InvalidOperationException:** Memanggil `doc.Regenerate()` di luar transaksi saat model sedang dimodifikasi akan memicu exception fatal dari Revit API: *"Modification of the document is forbidden"*.
3. **Inspeksi ID Elemen Baru:** ID elemen hasil (`instance.Id`) hanya valid untuk dibaca bounding box dan geometrinya setelah `doc.Regenerate()` dieksekusi.

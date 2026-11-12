# AIGRAV — Decision & Review Engine (Phase 3.0 Report)

**Tanggal Eksekusi:** 2026-10-09  
**Target Lingkungan:** Autodesk Revit 2026 (.NET 10.0-windows) & Python 3.14  
**Status Keseluruhan:** ALL 8 TESTS PASSED (Zero Production C# Code Modified)

---

## 1. Audit Arsitektur Sebelum Modifikasi

Berdasarkan audit pra-implementasi:
1. **Titik Pemanggilan AI / LLM Saat Ini**: 
   AIGRAV saat ini beroperasi secara *headless* sebagai tool server via FastMCP (`MCP/TestServer/server.py`) dan HTTP bridge (`localhost:8765`). Pemanggilan model AI terjadi pada *upstream client/agent* (seperti Claude/Gemini/Cursor) yang berinteraksi dengan tool catalog AIGRAV.
2. **Pemrosesan User Prompt**:
   Sebelum Phase 3.0, prompt yang dikirim ke orchestrator dieksekusi secara imperatif langkah-demi-langkah tanpa tahap telaah niat (*intent review*), estimasi risiko, atau deteksi ambiguitas pra-eksekusi.
3. **Seleksi dan Pemanggilan MCP Tool**:
   MCP tool dipilih langsung oleh upstream agent dan diteruskan sebagai payload JSON ke `http://127.0.0.1:8765/`.
4. **Status Komponen yang Ada**:
   * *Planning & Verification*: Telah dibuat di Phase 2 (`TaskExecutionEngine`, `Verifier`, `TaskStore`).
   * *Review, Recommendation & Approval Gates*: **Belum ada** sebelum Phase 3.0.
5. **Titik Integrasi Minimal**:
   Modul baru `decision_engine.py` dan `decision_models.py` diintegrasikan langsung di dalam sub-paket `MCP/TestServer/orchestrator/`. Modul ini bertindak sebagai pintu gerbang (*front-gate*) sebelum `Task` diserahkan kepada `TaskExecutionEngine`.

---

## 2. Alur Keputusan (Decision Flow)

```
[User Prompt]
      │
      ▼
[Intent Understanding] ──► Ekstraksi target, operasi, batasan, risiko
      │
      ▼
[Context Inspection] ──► Probe live Revit bridge jika konteks diperlukan
      │
      ▼
[Decision Gate Evaluation]
      ├─► UNSUPPORTED              ──► Laporkan gap kemampuan, hentikan eksekusi
      ├─► CLARIFICATION_REQUIRED   ──► Ajukan pertanyaan klarifikasi esensial
      ├─► RECOMMENDATION_REQUIRED  ──► Sajikan opsi rekomendasi & trade-offs
      ├─► APPROVAL_REQUIRED        ──► Minta persetujuan eksplisit user
      ├─► BLOCKED                  ──► Laporkan konteks/bridge tidak tersedia
      └─► READY
            │
            ▼
      [Plan Synthesis] ──────────► Buat Task & Step dengan kriteria verifikasi
            │
            ▼
      [TaskExecutionEngine] ─────► Eksekusi via HTTP Bridge :8765
            │
            ▼
      [Result Verification] ─────► Semantic read-back check (Anti-false success)
            │
            ▼
      [TaskStore Persistence] ───► Simpan checkpoint atomik di TaskHistory/
```

### 6 Decision Gate Outcomes:
1. **`READY`**: Permintaan jelas, didukung tools aktif, read-only, dan aman untuk dieksekusi langsung.
2. **`CLARIFICATION_REQUIRED`**: Informasi esensial (seperti tipe elemen, parameter target, atau koordinat) belum didefinisikan secara spesifik.
3. **`RECOMMENDATION_REQUIRED`**: Terdapat lebih dari satu pendekatan teknik yang valid dengan trade-offs (contoh: kalkulasi volume via QTO engine vs. query per instance).
4. **`APPROVAL_REQUIRED`**: Permintaan melibatkan mutasi atau penghapusan elemen model yang bersifat permanen/konsekuensial.
5. **`UNSUPPORTED`**: Permintaan memerlukan software eksternal (seperti analisis FEM SAP2000/ETABS atau rendering Lumion) yang tidak didukung Revit Bridge.
6. **`BLOCKED`**: Konteks model atau koneksi HTTP bridge ke Revit tidak dapat diakses.

---

## 3. Hasil Pengujian (8 Canonical Tests)

Test suite dijalankan melalui:
```powershell
& "c:\AIGRAV\MCP\TestServer\venv\Scripts\python.exe" "c:\AIGRAV\MCP\TestServer\run_phase_3_tests.py"
```

| No | Kasus Uji | Skenario Pengujian | Hasil Evaluasi | Status | Lingkungan |
| :---: | :--- | :--- | :--- | :---: | :---: |
| 1 | **Clear Read-Only Request** | *"Inspect active project information and current view"* | Outcome: `READY`. Rencana 2 langkah langsung dibentuk tanpa pertanyaan tidak perlu. | **PASS** | Mocked |
| 2 | **Ambiguous Request** | *"ubah model"* | Outcome: `CLARIFICATION_REQUIRED`. Meminta target elemen & nilai parameter yang jelas. | **PASS** | Mocked |
| 3 | **Multiple Approaches** | *"Hitung volume kolom struktural dan bandingkan cara perhitungannya"* | Outcome: `RECOMMENDATION_REQUIRED`. Menyajikan opsi QTO teragregasi vs. query manual per elemen. | **PASS** | Mocked |
| 4 | **Unsupported Operation** | *"Lakukan FEM analysis dan export model ke SAP2000"* | Outcome: `UNSUPPORTED`. Menjelaskan keterbatasan tanpa pura-pura mengeksekusi. | **PASS** | Mocked |
| 5 | **Consequential Operation** | *"Delete element 330753 from Level 1"* | Outcome: `APPROVAL_REQUIRED`. Menahan eksekusi dan menyajikan approval prompt eksplisit. | **PASS** | Mocked |
| 6 | **Missing Context / Offline** | Deteksi bridge offline saat butuh inspeksi model | Outcome: `BLOCKED`. Melaporkan keterbatasan koneksi tanpa menciptakan data fiktif. | **PASS** | Mocked |
| 7 | **Execution Result Mismatch** | Query awal sukses, namun read-back verifikasi tidak cocok | Step ditandai `FAILED` karena verifikasi gagal (Anti-false success policy). | **PASS** | Mocked |
| 8 | **End-to-End Live Task** | *"Inspect active project information and current view"* | Outcome `READY` $\rightarrow$ Dispatched ke Revit 2026 aktif (port 8765) $\rightarrow$ Hasil terverifikasi. | **PASS** | **Live Revit 2026** |

### Output Ringkasan Harness:
```text
===========================================================================
PHASE 3.0 TEST RESULTS SUMMARY
===========================================================================
  Test 1: Clear Read-Only Request                   : PASS (Mocked)
  Test 2: Ambiguous Request                         : PASS (Mocked)
  Test 3: Multiple Meaningful Approaches            : PASS (Mocked)
  Test 4: Unsupported Operation                     : PASS (Mocked)
  Test 5: Consequential Modification / Deletion     : PASS (Mocked)
  Test 6: Missing Context / Offline Bridge          : PASS (Mocked)
  Test 7: Result Mismatch Fails Verification        : PASS (Mocked)
  Test 8: End-to-End Live Decision & Execution      : PASS (Live Revit)
===========================================================================
OVERALL PHASE 3.0 RESULT: ALL TESTS PASSED.
```

---

## 4. File yang Dibuat dan Dimodifikasi

1. **Dibuat**:
   * `C:\AIGRAV\MCP\TestServer\orchestrator\decision_models.py`: Data contracts (`DecisionOutcome`, `IntentAnalysis`, `RecommendationOption`, `DecisionReviewResult`).
   * `C:\AIGRAV\MCP\TestServer\orchestrator\decision_engine.py`: Kelas `DecisionReviewEngine` yang mengimplementasikan 6 Decision Gates, ekstraksi intent, rekomendasi, dan translasi ke `Task`.
   * `C:\AIGRAV\MCP\TestServer\run_phase_3_tests.py`: Test runner untuk memvalidasi seluruh 8 skenario keputusan.
   * `C:\AIGRAV\AIGRAV_DecisionReview_Phase3_Report.md`: Laporan resmi Phase 3.0 ini.
2. **Dimodifikasi**:
   * `C:\AIGRAV\MCP\TestServer\orchestrator\__init__.py`: Mengekspos models dan engine baru.
3. **Invarian Produksi**:
   * **NOL (0) file produksi C# dimodifikasi**. Jalur `AIGRAVBridge.cs`, `RevitExternalEventHandler.cs`, dan modul Steel Connection tetap utuh tanpa perubahan.

---

## 5. Keterbatasan yang Diketahui (Known Limitations)

1. **Rule-Based Intent Classifier (Pre-LLM)**:
   * Klasifikasi intent pada tahap ini menggunakan deterministic pattern-matching dan keyword extraction. Tahap berikutnya dapat menghubungkan LLM adapter (misalnya Gemini/Claude) untuk menangani variasi bahasa alami yang lebih bebas sambil tetap mematuhi schema contract `IntentAnalysis`.
2. **Approval Gate Interaction**:
   * Pada level Python engine, status `APPROVAL_REQUIRED` menghasilkan string konfirmasi dan menahan pembentukan `Task`. Integrasi UI modal di Revit atau chat interface klien diperlukan agar pengguna dapat menekan tombol "Approve".

---

## 6. Prosedur Rollback

Jika diperlukan pembatalan perubahan Phase 3.0:
1. Hapus file modul Decision Engine:
   ```powershell
   Remove-Item -Force "C:\AIGRAV\MCP\TestServer\orchestrator\decision_models.py"
   Remove-Item -Force "C:\AIGRAV\MCP\TestServer\orchestrator\decision_engine.py"
   Remove-Item -Force "C:\AIGRAV\MCP\TestServer\run_phase_3_tests.py"
   ```
2. Kembalikan `__init__.py` ke versi Phase 2.
3. **Dampak**: Nol dampak terhadap fungsionalitas inti Revit Bridge dan tool execution Phase 2.

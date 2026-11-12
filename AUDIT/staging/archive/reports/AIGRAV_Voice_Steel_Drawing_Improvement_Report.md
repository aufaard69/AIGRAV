# LAPORAN TEKNIS PENINGKATAN AIGRAV 2.0: VOICE INTELLIGENCE, STEEL AUTOMATION & TEKLA-LIKE DRAWING

**Lokasi Berkas:** `C:\AIGRAV\AIGRAV_Voice_Steel_Drawing_Improvement_Report.md`  
**Sistem:** AIGRAV Autonomous BIM Intelligence & Voice HUD  
**Peran & Pelaksana:** Senior AI Agent Architect, Revit API Engineer, MCP Developer & BIM Automation Specialist  
**Tanggal Eksekusi:** 11 Oktober 2026  
**Status Pengujian:** 31/31 Uji Lulus 100% (Zero Regression)  

---

## 1. TEMUAN ROOT CAUSE (AKAR MASALAH)

Berdasarkan audit mendalam terhadap source code aktual dan penelusuran runtime path, ditemukan beberapa akar masalah teknis:

### A. Masalah Voice Endpointing & Pengambilan Teks STT
1. **Penyebab Endpointing Terputus:**  
   Pada `aigrav_voice_hud.html` sebelumnya, objek `SpeechRecognition` diset dengan `continuous = false`. Pada konfigurasi ini, mesin pengenal suara bawaan peramban (*browser native silence detector*) secara otomatis menghentikan pengenalan setelah jeda bicara sangat singkat (hanya 500 ms – 1.000 ms). Pengguna tidak memiliki kesempatan untuk berpikir atau mengambil jeda di tengah kalimat tanpa memicu pengiriman prematur.
2. **Penyalahgunaan Elemen DOM Subtitle sebagai Sumber Transkrip:**  
   Pada fungsi `onend` lama, kode membaca nilai string dari `speechSubtitle.textContent.replace(...)`. Membaca teks tampilan DOM (*UI subtitle scraping*) berisiko tinggi mengambil teks lama, placeholder default, atau teks yang terpotong saat render. Tidak ada pemisahan antara buffer hasil sementara (*interim results*) dan hasil final (*finalized transcript*).
3. **Ketiadaan Countdown Timer 4.000 ms:**  
   Tidak ada mekanisme penundaan 4 detik (*speech delay timer*) sejak suara terakhir terdeteksi yang dapat di-reset kembali ketika pengguna melanjutkan berbicara.
4. **Kebocoran Auto-restart saat Mic Bisu (Muted):**  
   Saat fungsi `toggleMicMute()` dipanggil, timer atau callback `onend`/`onerror` yang sedang berjalan masih berpotensi memanggil `startListening()`, sehingga mikrofon aktif kembali secara tidak diinginkan.

### B. Masalah Respons Monoton *"Pemeriksaan selesai. Project: ..."*
1. **Unconditional Injection pada Perencana:**  
   Pada `MCP/TestServer/orchestrator/decision_engine.py`, setiap kali rencana aksi (`plan_steps`) kosong, sistem menyuntikkan perintah `get_project_info` sebagai langkah default. Ini mengklasifikasikan ucapan santai ke dalam gate `READY`, yang kemudian memicu `TaskExecutionEngine` mengeksekusi panggilan bridge ke Autodesk Revit (port 8765).
2. **Filter Intensitas BIM yang Terlalu Luas:**  
   Kata-kata umum seperti *"proyek"*, *"model"*, atau *"pekerjaan"* langsung memicu filter BIM lama, meskipun pengguna hanya curhat (*"Hari ini capek sekali di proyek"*) atau menanyakan ekonomi (*"Kenapa harga besi bisa naik?"*).

### C. Keterbatasan Steel Connection & Drawing Sebelumnya
1. **Steel Connection:** Sebelumnya hanya berupa pencarian kamus pasif (*read-only CSV/JSON search*) tanpa mesin inferensi profil, validasi jarak tepi/baut (AISC/SNI), opsi rekomendasi alternatif, dan eksekusi DirectShape 3D yang terkontrol.
2. **Drawing Automation:** Belum terhubungnya `SheetManagerSkill` dengan hierarki drawing modular terstandarisasi (GA Plan, Elevation/Section, 3D Axonometric, Detail Sambungan, dan matriks koordinat penempatan viewport bebas tumpang tindih).

---

## 2. ARSITEKTUR SEBELUM & SESUDAH PERUBAHAN

### Sebelum Perubahan:
```
[ User Mic ] ──> [ Browser STT (continuous=false) ]
                        │ (<1s pause langsung cut)
                        ▼
               [ Scrape UI Subtitle DOM ]
                        │
                        ▼
         [ Decision Engine: Default get_project_info ] ──> [ Revit Bridge :8765 ]
                        │
                        ▼
          "Pemeriksaan selesai. Project: ..." (Monoton)
```

### Sesudah Perubahan (AIGRAV 2.0 Shared Pipeline):
```
[ User Mic ] ──> [ Browser STT (continuous=true, interim=true) ]
                        │
                        ├─> Suara Baru Terdeteksi ──> [ Reset Timer 4.000 ms ]
                        ├─> Memisahkan Buffer: [accumulatedFinal] & [currentInterim]
                        │
                        ▼ (Setelah 4.000 ms Hening)
              [ Finalize dari Memory Buffer ]
                        │
                        ▼
           [ Shared VoiceInterfaceAdapter ]
                        │
       ┌────────────────┴────────────────────────┐
       ▼                                         ▼
[ Mode Percakapan Umum ]                 [ Mode Kerja BIM ]
- Empati Curhat Lapangan                 - Quantity Takeoff (QTO)
- Makroekonomi Harga Besi                - Steel Connection Automation
- Refleksi & Karier Engineer             - Tekla-like Drawing Automation
- Wawasan AI & Humor Teknik              - Inspeksi Model Revit
(Zero Revit Calls, Natural Dialogue)     (6 Decision Gates & Safety Gate)
       │                                         │
       └────────────────┬────────────────────────┘
                        │
                        ▼
       [ Validasi & Persetujuan "Oke Eksekusi" ]
                        │
                        ▼
     [ Eksekusi Revit Bridge via ExternalEvent ]
                        │
                        ▼
  [ TaskStore Sinkron + Visual Markdown + Edge Neural TTS ]
```

---

## 3. BERKAS YANG DIBUAT & DIMODIFIKASI

| No | Berkas | Status | Deskripsi Perubahan |
|---|---|:---:|---|
| 1 | `C:\AIGRAV\aigrav_voice_hud.html` | **Dimodifikasi** | Menerapkan delay endpointing 4.000 ms, pemisahan buffer `accumulatedFinalTranscript` dan `currentInterimTranscript`, countdown UI interaktif, penghapusan ketergantungan DOM scraping, serta isolasi mic mute ketat. |
| 2 | `C:\AIGRAV\voice_adapter.py` | **Dimodifikasi** | Menambahkan negative-signal filtering pada `_is_bim_intent`, memperkaya `_handle_general_conversation` (curhat lapangan, harga besi, karier, humor), menambahkan routing otomasi baja dan drawing berpagar persetujuan (*"oke eksekusi"*). |
| 3 | `C:\AIGRAV\steel_service.py` | **Dimodifikasi** | Meningkatkan layanan dari pencarian pasif menjadi `SteelConnectionAutomationEngine`: inferensi profil (WF250 s/d WF500), validasi jarak tepi/pitch baut AISC/SNI, perumusan opsi primer vs alternatif, dan eksekusi DirectShape 3D bergaransi via bridge. |
| 4 | `C:\AIGRAV\drawing_service.py` | **Dibuat Baru** | Mengimplementasikan `TeklaDrawingAutomationEngine`: perumusan hierarki gambar (GA Plan 1:100, Elevation 1:50, 3D 1:150, Detail 1:15), integrasi Title Block proyek (`KOP GAMBAR A3 - NTS`), penomoran sheet (`ST-101`, `ST-102`), dan matriks penempatan viewport tanpa benturan. |
| 5 | `C:\AIGRAV\server.py` | **Dimodifikasi** | Memperbarui endpoint telemetri `/api/status` untuk melaporkan status mesin otomasi baja dan drawing secara transparan. |
| 6 | `C:\AIGRAV\MCP\TestServer\orchestrator\decision_engine.py` | **Dimodifikasi** | Mengeliminasi injeksi default `get_project_info` dan menyempurnakan pemfilteran perintah ambigu agar tidak memicu eksekusi tanpa izin. |
| 7 | `C:\AIGRAV\test_voice_steel_drawing_suite.py` | **Dibuat Baru** | Suite uji verifikasi 11 skenario untuk endpointing 4 detik, percakapan natural, otomasi baja, otomasi drawing, dan isolasi mic. |

---

## 4. PERUBAHAN PERILAKU VOICE & ENDPOINTING (4 DETIK)

1. **Aturan 4.000 ms Speech Delay:**
   - Setelah pengguna berhenti bersuara, sistem menghitung mundur **4.000 ms** secara presisi.
   - Indikator UI menampilkan status real-time: `MENDENGARKAN... (JEDA: 4.0s)` turun hingga `0.0s`.
2. **Dynamic Timer Reset:**
   - Jika pengguna kembali berbicara pada detik ke-1, 2, atau 3, timer secara otomatis dibatalkan (*cleared*) dan diatur ulang kembali ke 4.000 ms dari suku kata terakhir. Transkrip tidak akan dikirim selama pengguna masih bersuara.
3. **Pemisahan Buffer Sementara & Final:**
   - Suku kata sementara dialirkan ke `currentInterimTranscript`.
   - Kalimat yang telah difinalisasi oleh engine STT diakumulasikan ke `accumulatedFinalTranscript`.
   - Prompt ke AI dibentuk murni dari penggabungan buffer memori ini, tanpa membaca atribut `.textContent` dari elemen HTML subtitle.
4. **Isolasi Mic Mute:**
   - Mengklik tombol **BISUKAN (MUTED)** seketika mematikan timer countdown, mengosongkan seluruh buffer memori, dan menghentikan `SpeechRecognition`.
   - Tidak ada auto-restart yang dapat dipicu oleh event `onend`, `onerror`, callback audio, atau tombol test preview.

---

## 5. IMPLEMENTASI AUTOMATION STEEL CONNECTION

Mengikuti workflow terstandarisasi:  
`User Request → Understand Requirements → Inspect Context → Identify Members → Validate Geometry → Generate Proposal → User Approval ("Oke Eksekusi") → Execute via Revit Bridge → Verify Result → Report.`

1. **Identifikasi Profil & Aturan:**
   - Mendeteksi profil baja dari masukan suara (contoh: `WF 400x200x8x13`, `WF 350x175`, `WF 250x125`).
   - Mencocokkan standar PT. Niagatama Kencana:
     - **Base Plate:** `STD-BJ-01 / STD-PDS-12` (Pelat 300x440x19 mm, 4 Angkur M22 L=700mm, 4 Stiffener 145x200x19 mm, Grout 30mm).
     - **Moment End Plate:** `STD-EP-04` (Pelat 200x700x19 mm, 14 Baut HTB A325 M22, Continuity Plates).
     - **Shear Tab:** `STD-BC-02` (Pelat 100x300x10 mm, 4 Baut HTB A325 M20).
2. **Validasi Geometri & Standar:**
   - Jarak tepi minimum $e \ge 1.5 d_{baut}$ (Memenuhi AISC 360 / SNI 1729).
   - Jarak spasi baut $s \ge 3.0 d_{baut}$ (Bebas tabrakan alat torsi).
3. **Persetujuan Eksplisit (*Safety Gate*):**
   - Menghasilkan rekomendasi primer dan alternatif serta meminta konfirmasi lisan: *"Katakan 'oke, eksekusi' jika Anda setuju saya modelkan di Revit."*
4. **Eksekusi & Verifikasi Revit:**
   - Memanggil `aigrav_connection_generate` pada Revit Bridge untuk memodelkan geometri 3D DirectShape presisi tinggi.
   - Hasil disimpan sebagai task berstatus `COMPLETED` di `TaskStore`.

---

## 6. IMPLEMENTASI DRAWING AUTOMATION (TEKLA-LIKE WORKFLOW)

Mengadopsi efisiensi hierarki drawing Tekla Structures:
`User Request → Inspect Model → Scope Selection → Determine View Sets → Apply Standards → Setup Sheets & Title Blocks → Place Viewports → Quality Control Layout → Approval → Verify Output.`

1. **Formulasi Paket Lembar Gambar:**
   - **GA Drawing (Sheet `ST-101`):** Rencana Struktur Lantai 1 (Skala 1:100) + 3D Isometrik Struktur (Skala 1:150).
   - **Assembly Drawing (Sheet `ST-102`):** Elevasi Portal As A (Skala 1:50) + Potongan Melintang As 1 (Skala 1:50).
   - **Detail Drawing (Sheet `ST-103`):** Detail Base Plate Kolom WF400 (Skala 1:15) + Detail Moment End Plate (Skala 1:15).
2. **Standar Proyek & Title Block:**
   - Menggunakan Title Block resmi proyek: `KOP GAMBAR A3 - NTS` tanpa membuat title block fiktif.
3. **Matriks Koordinat Viewport:**
   - Menghitung koordinat penempatan viewport yang dinormalisasi pada lembar gambar sehingga seluruh view terdistribusi rapi tanpa tumpang tindih.
4. **Validasi Margin & Quality Control:**
   - Melakukan pengecekan koordinat batas ($0.10 \le X, Y \le 0.95$) untuk memastikan viewport tidak meluap keluar dari garis tepi sheet.
5. **Eksekusi Gated:**
   - Memerlukan persetujuan lisan (*"oke, eksekusi"*), mengeksekusi pembuatan sheet dan penataan viewport via `SheetManagerSkill`, serta memverifikasi hasilnya di Revit.

---

## 7. DAFTAR MCP TOOLS & SKILLS YANG DIGUNAKAN

| Nama Tool / Skill | Komponen Sumber | Fungsi dalam Workflow |
|---|---|---|
| `aigrav_get_revit_info` | Revit Bridge / InfoTool | Membaca dokumen proyek aktif (`ADHI_STR RUSUN MBR...`). |
| `aigrav_get_active_view` | Revit Bridge / ViewTool | Membaca tampilan aktif saat ini. |
| `get_structural_columns` | Revit Bridge / ModelQuery | Membaca daftar dan profil kolom struktur aktif. |
| `aigrav_connection_inspect` | `AIGRAV.Skills.ConnectionGenerator` | Memeriksa geometri dan elemen sambungan baja di model. |
| `aigrav_connection_generate` | `AIGRAV.Skills.ConnectionGenerator` | Memodelkan elemen 3D DirectShape sambungan (pelat, baut, stiffener). |
| `aigrav_setup_sheets` | `AIGRAV.Skills.SheetManager` | Membuat sheet gambar resmi dengan Title Block proyek. |
| `aigrav_inspect_views` | `AIGRAV.Skills.SheetManager` | Memeriksa ketersediaan dan skala view struktur. |
| `aigrav_place_viewport` | `AIGRAV.Skills.SheetManager` | Menempatkan view pada lembar gambar di koordinat $(X, Y)$ tertentu. |
| `aigrav_adjust_viewports` | `AIGRAV.Skills.SheetManager` | Menyesuaikan posisi dan label garis viewport. |
| `qto_engine.execute_live_qto` | `qto_engine.py` | Ekstraksi kubikasi beton native dan ekspor Excel 2-sheet tender. |

---

## 8. BUKTI & HASIL PENGUJIAN REGRESI (31/31 SUITES PASS)

Pengujian diverifikasi melalui 3 rangkaian uji otomatis independen:

### A. Integrated Suite: Voice, Steel & Drawing (`test_voice_steel_drawing_suite.py`)
- **Hasil:** **11/11 PASSED (100%)**
  1. *Voice Endpointing (4s Delay & Buffers):* **PASS** (Konfigurasi 4000ms, buffer terpisah, reset timer tervalidasi).
  2. *General Chat - Site Fatigue:* **PASS** (Curhat proyek direspons hangat, 0 panggilan bridge Revit).
  3. *General Chat - Steel Economics:* **PASS** (Penjelasan makroekonomi bijih besi, kurs, dan energi tervalidasi).
  4. *General Chat - Career Advice:* **PASS** (Saran karier berimbang, tanpa respons pemeriksaan proyek).
  5. *General Chat - Multi-turn Reflection:* **PASS** (Menjawab *"Menurut kamu bagaimana?"* secara kontekstual).
  6. *General Chat - Engineering Humor:* **PASS** (Lelucon revisi arsitek & beton disampaikan santai).
  7. *Steel Connection Automation (WF400):* **PASS** (STD-BJ-01 diplanning, disetujui, dieksekusi, dan dicatat di TaskStore).
  8. *Tekla Drawing Automation (ST-101/ST-102):* **PASS** (Sheet GA & Elevasi diplanning, disetujui, dan diverifikasi di Revit).
  9. *Mixed Conversation (Single Session):* **PASS** (Transisi lancar obrolan santai $\leftrightarrow$ kerja BIM tanpa kebocoran state).
  10. *Microphone Mute Isolation:* **PASS** (Mic bisu menghentikan pengenalan tanpa auto-restart).
  11. *Male/Female Indonesian Voices:* **PASS** (Ardi & Gadis menghasilkan stream audio berbeda dan allowlist aktif).

### B. Conversation Intelligence & Voice Selection (`test_conversation_intelligence_and_voices.py`)
- **Hasil:** **11/11 PASSED (100%)**

### C. Shared Voice Adapter & Orchestrator Compatibility (`test_shared_voice_adapter.py`)
- **Hasil:** **9/9 PASSED (100%)** (Mencakup QTO Excel #1B365D, Steel DB, Pause/Resume, TaskStore sharing).

**Total Keseluruhan:** **31 dari 31 Pengujian Lulus (Tingkat Keberhasilan 100%)**.

---

## 9. FITUR YANG BELUM SELESAI BESERTA ALASANNYA

1. **Detail Sambungan Baut Berulir Tingkat Tinggi (Parametric Thread Geometry):**
   - Baut saat ini dimodelkan sebagai DirectShape silinder solid dengan kepala baut heksagonal, bukan geometri ulir fisik 3D mikroskopis.
   - *Alasan:* Pemodelan ulir beresiko tinggi membebani performa grafis Revit (*file bloat*) dan tidak disarankan dalam standar praktik BIM LOD 350–400.
2. **Auto-Dimensioning Otomatis pada Semua Viewport Tekla:**
   - Anotasi dimensi otomatis saat ini aktif pada grid dan level. Penempatan dimensi otomatis pada setiap lubang baut masih memerlukan acuan referensi tepi geometri DirectShape yang stabil di Revit API.

---

## 10. RISIKO, DEPENDENCY & BATASAN SISTEM

1. **Dependensi Revit UI Thread:**  
   Seluruh operasi mutasi elemen (pemodelan baja dan pembuatan sheet) wajib melewati `ExternalEvent` Revit Bridge port 8765. Jika Autodesk Revit ditutup atau mengalami dialog modal pembuka, permintaan mutasi akan menunggu hingga antrean UI thread bebas.
2. **Dependensi Browser Web Speech API:**  
   Perekaman suara bergantung pada dukungan peramban (disarankan Google Chrome atau Microsoft Edge) yang mendukung antarmuka `webkitSpeechRecognition` dan mikrofon terpasang.
3. **Batasan Keinsinyuran:**  
   Otomatisasi ini merupakan alat bantu permodelan (*modeling co-pilot*), bukan pengganti kalkulasi kekuatan elemen oleh software FEA terakreditasi (seperti ETABS/SAP2000) dan pengesahan oleh Ahli Teknik Struktur berizin.

---

## 11. LANGKAH PENGUJIAN MANUAL PENGGUNA (MANUAL TESTING GUIDE)

1. **Buka Antarmuka Voice HUD:**
   - Akses peramban di URL: `http://localhost:8080/`
2. **Uji Endpointing 4 Detik:**
   - Bicaralah: *"AIGRAV... (berhenti bicara 2 detik)... hitung volume beton sekarang"*
   - Amati indikator: Countdown `JEDA: 4.0s` akan ter-reset saat Anda melanjutkan berbicara di detik ke-2, dan prompt baru dikirim setelah hening genap 4 detik.
3. **Uji Percakapan Santai / Curhat:**
   - Katakan: *"Hari ini capek sekali di proyek"* $\rightarrow$ AIGRAV akan merespons dengan empati tanpa membuka Revit.
   - Katakan: *"Kenapa harga besi bisa naik?"* $\rightarrow$ AIGRAV menjelaskan faktor ekonomi material.
   - Katakan: *"Menurut kamu bagaimana kalau saya pindah pekerjaan?"* $\rightarrow$ AIGRAV memberikan masukan karier yang bijak.
4. **Uji Otomasi Steel Connection:**
   - Katakan: *"AIGRAV, buatkan sambungan base plate untuk kolom WF400"*
   - Amati rekomendasi teknis STD-BJ-01 di workspace $\rightarrow$ Katakan: *"Oke, eksekusi"* $\rightarrow$ Elemen dimodelkan di Revit.
5. **Uji Otomasi Drawing Tekla:**
   - Katakan: *"AIGRAV, buatkan drawing struktur denah lantai 1 dan elevasi"*
   - Amati paket lembar ST-101/ST-102 di workspace $\rightarrow$ Katakan: *"Oke, eksekusi"* $\rightarrow$ Lembar gambar resmi disiapkan di Revit.

---

## 12. REKOMENDASI TAHAP BERIKUTNYA

1. Mengintegrasikan pembacaan otomatis beban reaksi perletakan ($Pu, Vu, Mu$) dari output analisis struktur untuk auto-sizing tebal pelat base plate secara adaptif.
2. Mengembangkan kemampuan ekspor drawing sheet langsung ke format PDF dan DWG secara otomatis melalui Revit Bridge.
3. Menambahkan visualisasi mini 3D WebGL preview pada Voice HUD untuk menampilkan pratinjau bentuk sambungan baja sebelum disetujui.

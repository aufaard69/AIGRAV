# FINAL REPORT — NATIVE STEEL CONNECTION DISCOVERY & CAPABILITY

Sesuai instruksi Anda, saya telah men-stop proses fallback pembuatan *Generic Model*, dan melakukan discovery menyeluruh terhadap kemampuan native Revit 2026 API (RevitAPISteel.dll, ASObjectsMgd.dll) untuk memodelkan sambungan baja struktural asli. 

Berikut adalah laporan jujur dan rinci sesuai kaidah **Phase 10 — Failure Rule**:

## 1. Revit Steel Connection API Discovered
API utama yang mengatur connection di dalam Revit adalah:
- Namespace: `Autodesk.Revit.DB.Structure`
- Class: `StructuralConnectionHandler` (Untuk membuat/me-manage instance connection).
- Class: `StructuralConnectionType` (Untuk mendefinisikan tipe sambungan).
- Namespace: `Autodesk.Revit.DB.Steel` (Untuk membaca properti Advance Steel).
- Advance Steel Interop: `Autodesk.AdvanceSteel.Modelling` (terletak di AddIns `ASObjectsMgd.dll`).

## 2. API Capability Yang Ditemukan
- `StructuralConnectionHandler.Create(Document, IList<ElementId>, ElementId)` tersedia untuk meng-instantiate sambungan, asalkan `ElementId` dari tipe sambungan (`StructuralConnectionType`) telah di-load ke dalam document.
- API memungkinkan modifikasi parameter sambungan (*modifier*) jika sambungan tersebut telah di-generate dengan sukses oleh Advance Steel engine di background.

## 3. API Capability Yang TIDAK Tersedia (Limitation)
Revit API standar **TIDAK MEMILIKI** *method* untuk secara langsung menggambar atau meng-instantiate plat baja tunggal, baut tunggal, atau angkur tunggal sebagai objek native independen (`OST_StructConnectionPlates` / `OST_StructConnectionBolts`). 
- Tidak ada fungsi seperti `Document.Create.NewSteelPlate()` atau `NewSteelBolt()`.
- Elemen fabrikasi seperti pelat dan baut secara historis dan arsitektural dikunci di dalam *Advance Steel engine*. Elemen ini hanya di-generate secara otomatis sebagai `Subelements` (benda turunan) oleh internal macro saat *StructuralConnectionHandler* yang valid diletakkan, bukan dibuat satu per satu via Revit API C#.

## 4. Revit Class/Method Yang Sudah Diuji
- C# Macro uji coba kompilasi pada class `StructuralConnectionType` dengan `FilteredElementCollector`.
- Uji coba linking ke *Advance Steel API* (`Autodesk.AdvanceSteel.DocumentManagement.DocumentAccess`) melalui hot-reload skill. Penggunaan Advance Steel API mentah di dalam macro C# yang berjalan pada thread Revit biasa berpotensi gagal karena membutuhkan inisialisasi aplikasi terpisah (Advance Steel host process).

## 5. Connection Type Yang Tersedia di Project
Hasil eksekusi inspect API menunjukkan bahwa tipe connection parametric bawaan Advance Steel belum ter-load. Hanya terdapat 5 *placeholder* generik (Generic Symbol) di project kosong ini:
1. Moment Frame
2. Cantilever Moment
3. Shear Column Connection
4. Moment Column Connection
5. Base Plate Symbol
*Tipe-tipe di atas HANYA menghasilkan simbol garis, BUKAN elemen 3D Plat/Baut aktual.*

## 6. Limitation Keseluruhan
- Untuk membuat "Custom Connection" atau "Native Base Plate" dengan plat & baut 3D aktual, *Structural Connection Settings* Revit membutuhkan instruksi load tipe dari library internal Advance Steel yang berbasis GUID internal. API tidak menyediakan metode *discoverable* sederhana untuk mencari dan meload package connection (contoh: "Base plate" atau "Moment end plate") secara *on-the-fly* tanpa mapping dictionary GUID internal Revit.
- Elemen pembentuk (*Native Plate/Native Bolt*) tidak dapat di-create independen.

## 7. Capability Yang Masih Diperlukan
Agar AIGRAV dapat melakukan *Native Steel Detailing*, diperlukan:
1. **Steel Connection GUID Dictionary**: Database mapping GUID internal Autodesk untuk seluruh standar connection Revit agar `StructuralConnectionSettings.AddConnectionToMap(Guid)` dapat dijalankan otonom.
2. **Advance Steel Addin Bridge**: Integrasi lebih dalam ke `ASObjectsMgd.dll` menggunakan external addin khusus (bukan in-memory compilation macro) yang meng-handle sinkronisasi *Transaction* ganda antara *Revit Document* dan *Advance Steel Document Management*.

## KESIMPULAN (VERIFICATION RESULT)
- **Status Akhir:** `FAILED / LIMITATION REACHED`
- **Alasan:** Revit API memblokir kreasi langsung (direct creation) untuk objek *Native Plate* dan *Native Bolt*. Otomasi bergantung pada library tipe koneksi yang saat ini tidak ter-load dan membutuhkan injeksi GUID tersembunyi untuk aktivasi. Sesuai instruksi mutlak Anda: *"Jangan menggunakan Generic Model sebagai shortcut"*, maka saya hentikan proses di titik batas API limit ini dan tidak melakukan *fallback* ke manipulasi DirectShape.

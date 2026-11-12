# AIGRAV DirectShape Controlled-Exception Policy & Replacement Roadmap

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION (Opsi 2A)` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan kebijakan pengecualian terkendali (*controlled-exception policy*) untuk penggunaan `DirectShape` di Revit API, memetakan seluruh titik pemanggilan (*call sites*) aktif di repositori, melarang keras penggunaan DirectShape sebagai pengganti elemen BIM primer, mempertahankan fungsionalitas sambungan baja aktif, dan menetapkan peta jalan migrasi ke native family pada Fase 4.

---

## 1. Prinsip Dasar & Larangan Penggantian Elemen Primer

> [!CAUTION] LARANGAN MUTLAK PENGGANTIAN ELEMEN PRIMER
> `DirectShape.CreateElement()` dan `OST_GenericModel` **DILARANG KERAS** digunakan sebagai pengganti atau *fallback* otomatis untuk elemen-elemen primer berikut:
> - **Dinding (`OST_Walls`)**
> - **Pelat Lantai (`OST_Floors`)**
> - **Atap (`OST_Roofs`)**
> - **Kolom Struktural (`OST_StructuralColumns`)**
> - **Balok Struktural (`OST_StructuralFraming`)**
> - **Pintu (`OST_Doors`)**
> - **Jendela (`OST_Windows`)**
> - **Ruang (`OST_Rooms`)**
>
> Penggunaan DirectShape maupun Generic Model untuk elemen di atas menghasilkan geometri bisu (*dumb solids*) tanpa parameter tipe terstandar, merusak lapisan material (*compound structures*), mematikan kemampuan host bukaan, dan menggagalkan audit kuantitas resmi. Jika pembuatan native gagal, sistem wajib berhenti dengan *blocking error* (*fail-closed*).

---

## 2. Inventarisasi Call Sites Aktif di Repositori C# & Status Pengecualian

Berdasarkan audit kode sumber mendalam dan persetujuan pengguna atas **Opsi 2A**:

| File Sumber & Nomor Baris | Modul / Konteks | Tujuan Pemanggilan Saat Ini | Status Keputusan | Ketentuan Pengecualian |
| :--- | :--- | :--- | :---: | :--- |
| **`ConnectionGeneratorSkill.cs:91`** | Steel Connection Generator | Pembuatan geometri pelat simpul khusus (*custom gusset plates*) | `APPROVED — DESIGN DECISION` | **EXCEPTION TERBATAS SAH** (Dipertahankan pada Fase 3, migrasi ke native di Fase 4) |
| **`ConnectionGeneratorSkill.cs:109`** | Steel Connection Generator | Pembuatan profil siku penyiku (*stiffener cleats*) | `APPROVED — DESIGN DECISION` | **EXCEPTION TERBATAS SAH** (Dipertahankan pada Fase 3, migrasi ke native di Fase 4) |
| **`ConnectionGeneratorSkill.cs:140`** | Steel Connection Generator | Penggabungan solid sambungan kompleks | `APPROVED — DESIGN DECISION` | **EXCEPTION TERBATAS SAH** (Dipertahankan pada Fase 3, migrasi ke native di Fase 4) |
| **`HouseType36Builder.cs:180`** | Prototype Demo Script | Pembuatan kotak massa prototipe tipe 36 (`CreateDirectShapeBox`) | `APPROVED — DESIGN DECISION` | **DEPRECATED** (Hapus saat refactoring builder pada Fase 3) |
| **`HouseType36Builder.cs:215`** | Prototype Demo Script | Geometri bantuan visual sementara | `APPROVED — DESIGN DECISION` | **DEPRECATED** (Hapus saat refactoring builder pada Fase 3) |
| **`BimAuditSkill.VisualAndPython.cs:149, 212`** | Audit Visual & Debugging | Penandaan visual batas ruang benturan (*clash boundary markers*) | `APPROVED — DESIGN DECISION` | **DISETUJUI SEBAGAI DIAGNOSTIK** (Transient display only) |

> [!IMPORTANT] Larangan Penambahan Call Site Baru
> Penambahan titik pemanggilan `DirectShape` baru di luar 3 baris aktif pada `ConnectionGeneratorSkill.cs` di atas **DILARANG KERAS** tanpa persetujuan pengecualian tertulis dari Pengguna.

---

## 3. Ketentuan Pengecualian Terkendali (*Controlled-Exception Requirements*)

Untuk elemen yang memanfaatkan pengecualian terkendali di `ConnectionGeneratorSkill.cs`, seluruh persyaratan berikut **WAJIB** dipenuhi:

1. **Declared Category Invariant:**
   Wajib menetapkan `BuiltInCategory` yang valid (misalnya `BuiltInCategory.OST_StructConnectionPlates`). Dilarang membiarkan category null atau unassigned.
2. **QTO Preservation (Volume Solid Eksak):**
   Kuantitas berat pelat simpul DirectShape wajib dihitung dari volume solid eksak dikalikan densitas baja:
   $$\text{Berat (kg)} = \text{Solid.Volume (m}^3\text{)} \times 7850.0$$
3. **Explicit Provenance & Logging:**
   Elemen DirectShape wajib mencatat parameter komentar: `Comments = "AIGRAV_CONTROLLED_EXCEPTION: ConnectionGeneratorSkill"`. Setiap pembentukan DirectShape wajib dicatat pada log telemetri Bridge dengan tingkat informasi jelas.
4. **No Fallback for Openings / Architectural Shell:**
   DirectShape tidak boleh diizinkan menjadi cadangan (*fallback*) untuk bukaan atau selubung arsitektural dalam kondisi apa pun.

---

## 4. Peta Jalan Migrasi ke Native Family (*Migration Roadmap to Native BIM*)

Sesuai persetujuan Opsi 2A, roadmap transisi menuju representasi BIM native diatur bertahap:

```
[FASE 2: KONSOLIDASI TATA KELOLA (Saat Ini)]
   └── Tetapkan Kebijakan Pengecualian Opsi 2A & Bekukan Penambahan Call Site Baru.
[FASE 3: PENGEMBANGAN FITUR NATIVE ARSITEKTUR]
   ├── Implementasikan host-based NewFamilyInstance untuk Pintu & Jendela.
   ├── Implementasikan tool pembuatan Atap dan Ruang native.
   ├── Hapus CreateDirectShapeBox dari skrip demonstrasi HouseType36Builder.
   └── Pertahankan ConnectionGeneratorSkill dengan audit QTO solid & logging ketat.
[FASE 4: MIGRASI PENUH SAMBUNGAN BAJA KE NATIVE FAMILY]
   ├── Bangun library Loadable Family Parametrik resmi untuk Pelat Simpul & Siku Cleat.
   ├── Refactoring ConnectionGeneratorSkill.cs untuk memanggil doc.Create.NewFamilyInstance().
   └── Jalankan uji kalibrasi QTO agar hasil berat identik 100% dengan hasil solid lama.
[FASE 5: PENGHAPUSAN LENGKAP DIRECTSHAPE]
   └── Cabut seluruh izin DirectShape dari repositori produksi (kecuali penanda diagnostik transien).
```

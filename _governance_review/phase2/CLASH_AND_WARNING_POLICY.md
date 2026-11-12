# AIGRAV Clash & Warning Policy

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan protokol deteksi benturan spasial (*spatial clash detection*) dua tahap yang presisi serta tata kelola penanganan peringatan (*warning suppression and handling*) di Revit API, mencegah kegagalan silent suppression dan memisahkan kontak fisik yang sah dari benturan terlarang.

---

## 1. Arsitektur Deteksi Benturan Spasial (Two-Phase Clash Detection)

Pemeriksaan benturan solid di Revit API memerlukan komputasi intensif (*O(N²)*). Untuk mencegah *performance degradation* dan *timeout*, deteksi benturan WAJIB menggunakan pendekatan dua tahap:

```
[Kumpulan Elemen Target]
         │
         ▼
[FASE 1: BROAD-PHASE FILTERING (BoundingBox Intersect)]
         │   Membandingkan BoundingBoxXYZ dengan toleransi ekspansi (±2 mm)
         │   Elemen tanpa perpotongan BoundingBox langsung diabaikan (O(1))
         ▼
[KANDIDAT PASANGAN BENTURAN]
         │
         ▼
[PENYARINGAN KONTAK SAH / ALLOWED CONTACT MATRIX]
         │   Apakah pasangan termasuk interaksi legal (Host-Guest, Joined, Bearing)?
         ├── YA ──> [ABAIKAN / CATAT SEBAGAI KONTAK SAH]
         └── TIDAK
               │
               ▼
[FASE 2: NARROW-PHASE COMPUTATION (Solid Boolean Intersect)]
         │   BooleanOperationsUtils.ExecuteBooleanOperation(
         │       SolidA, SolidB, BooleanOperationsType.Intersect)
         ▼
[HITUNG VOLUME INTERSEKSI (V_intersect)]
         │
         ├── V_intersect <= Toleransi Spasial (1e-6 m³) ──> [ABAIKAN / TOLERANSI NUMERIK]
         └── V_intersect > Toleransi Spasial ────────────> [VERDICT: SPATIAL CLASH TERBUKTI]
```

---

## 2. Pembedaan Sambungan BIM Sah vs Interseksi Tak Disengaja (*Intentional Joins vs Unintended Intersections*)

Kebijakan deteksi benturan AIGRAV **TIDAK MENGGUNAKAN** larangan buta *"Zero Solid Overlap"*. Sebaliknya, sistem secara tegas membedakan antara **Sambungan BIM Sah (*Intentional BIM Connections & Monolithic Joins*)** dengan **Interseksi Tak Disengaja (*Unintended Geometric Intersections & Clashes*)**:

### A. Matriks Sambungan BIM Sah yang DISETUJUI (*Approved Intentional Connections*):
Irisan geometri berikut merupakan praktik pemodelan standar di Autodesk Revit dan **DIKECUALIKAN** dari klasifikasi benturan:

| Pasangan Kategori | Karakteristik Kontak / Sambungan | Status | Mekanisme Pemodelan Revit & Validasi Anti-Double Counting |
| :--- | :--- | :---: | :--- |
| **`OST_Doors` / `OST_Windows` vs `OST_Walls`** | Hosted Wall Opening | **DISETUJUI** | Bukaan kusen memotong dinding host melalui void otomatis Revit; volume bukaan mengurangi volume dinding host. |
| **`OST_Floors` vs `OST_Walls` (Joined)** | Slab Bearing / Monolithic Wall-Floor Joint | **DISETUJUI** | Sambungan aktif via `JoinGeometryUtils.JoinGeometry()`. Prioritas material core memotong lapisan pelat; tidak terjadi double counting volume. |
| **`OST_StructuralFraming` vs `OST_StructuralColumns`** | Beam-to-Column Core Node | **DISETUJUI** | Hubungan balok-kolom cor di tempat (*cast-in-place*). Volume join diatur oleh urutan join priority (`JoinGeometryUtils.SwitchJoinOrder()`). |
| **`OST_StructuralFoundation` vs `OST_StructuralColumns`** | Column Base Pedestal Bearing | **DISETUJUI** | Kolom bertumpu di atas *pedestal* / *pile cap* / *footplate* dengan join kontak sah. |
| **`OST_StructuralFraming` vs `OST_StructConnectionPlates`** | Steel Connection Assembly | **DISETUJUI** | Pelat simpul/baseplate baja menempel atau tertanam pada sayap profil/grout pedestal. |
| **`OST_Walls` vs `OST_Roofs` (Attached)** | Wall-to-Roof Soffit Attachment | **DISETUJUI** | Dinding di-attach ke bagian bawah pelat atap (`Wall.Attach()`); profil dinding terpotong kemiringan atap. |
| **`OST_Ceilings` vs `OST_Walls`** | Perimeter Ceiling Interface | **DISETUJUI** | Rangka plafon bertumpu pada bidang batas dinding partisi. |

### B. Interseksi Geometri Tak Disengaja yang DILARANG KERAS (*Unintended Intersections / Prohibited Clashes*):
Interseksi berikut merupakan kecacatan model (*modeling defect*) yang wajib ditolak atau diperbaiki:

1. **Coplanar Overlapping Slabs (Pelat Sebidang Tumpang Tindih):**  
   Dua pelat pejal atau lebih yang diletakkan pada elevasi bidang horizontal yang sama tanpa pembagian opening loop. Hal ini memicu artefak visual Z-fighting (flicker hitam saat render) dan pelipatgandaan luasan/volume beton pada schedule kuantitas QTO.  
   *Kaidah Wajib:* Gunakan multi-loop `CurveLoop` (donut slabs: Loop 0 = outer boundary, Loop 1..N = inner openings).
2. **Duplicate Collinear Walls (Dinding Ganda Sejajar):**  
   Dua dinding yang dimodelkan tumpang tindih di garis as yang sama (*double drafting*), memicu warning kritis Revit `WallsOverlap`.
3. **Hard Clashes MEP/Furniture vs Struktur:**  
   Pipa atau ducting menembus balok struktural tanpa opening sleeve resmi; atau furnitur menembus dinding/kolom pejal (> 5 mm penetrasi).
4. **Structural Framing Clashes Bukaan:**  
   Balok struktural melintang memotong void pintu atau jendela sehingga merusak tinggi bebas (*lintel clash*).

---

## 3. Tata Kelola Peringatan Revit (*Warning Handling & Suppression Policy*)

### Analisis Kritis Call Site: `ClashWarningSwallower`
Pada `BimAuditSkill.cs:14493`, implementasi `IFailuresPreprocessor` memanggil:
```csharp
failuresAccessor.DeleteAllWarnings();
```

> [!WARNING] Koreksi Pemahaman Audit
> `DeleteAllWarnings()` **TIDAK MENGHAPUS** seluruh riwayat warning yang telah tersimpan di dokumen Revit (*document-level historical warnings*).  
> Fungsi tersebut **hanya membuang (menelan) peringatan baru yang dipicu dalam konteks transaksi yang sedang diproses oleh `FailuresAccessor` tersebut**.  
> Kendati demikian, pemanggilan tanpa filter (*unconditional suppression*) sangat berbahaya karena menutupi kegagalan pemodelan nyata.

### Aturan Penanganan Warning yang Benar:
1. **Dilarang Menelan Warning Secara Buta (*No Blind Swallowing*)**:
   - Penghapusan warning hanya diizinkan untuk `FailureDefinitionId` spesifik yang terdaftar dalam *Warning Allowlist*.
2. **Warning Allowlist Resmi**:
   Hanya peringatan berikut yang boleh disupresi pada transaksi otomatis jika telah diantisipasi secara terprogram:
   - `BuiltInFailures.OverlapFailures.DuplicateInstances`: Hanya jika elemen sengaja digandakan dalam tahap *temporary staging* dan akan segera dihapus/di-join.
   - `BuiltInFailures.CreationFailures.CannotDrawWallsError`: Hanya jika penanganan alternatif (*fallback handler*) segera dipanggil.
   - `BuiltInFailures.JoinElementsFailures.CannotJoinElementsWarning`: Hanya jika unjoined geometry dicatat dalam log QA/QC.
3. **Peringatan yang DILARANG KERAS Ditekan (*Fatal Prohibited Warnings*)**:
   - `BuiltInFailures.OverlapFailures.WallsOverlap`: Mengindikasikan dinding bertumpuk pejal (*double drafting*).
   - `BuiltInFailures.RoomFailures.RoomNotEnclosed`: Ruang bocor/tidak terdefinisi batasnya.
   - `BuiltInFailures.InaccurateFailures.LineSlightlyOffAxis`: Sumbu miring tidak disengaja yang merusak ortogonalitas model.

---

## 4. Pelacakan Baseline Peringatan (*Warning Baseline Diff Tracking*)

Setiap modul pemodelan dan skrip otomatis wajib menerapkan mekanisme pelacakan perubahan warning (*Baseline Warning Diff*):

```csharp
// 1. Tangkap baseline warning sebelum transaksi
IList<FailureMessage> preWarnings = doc.GetWarnings();
int preCount = preWarnings.Count;

// 2. Jalankan transaksi pemodelan
using (Transaction tx = new Transaction(doc, "Model Operation"))
{
    tx.Start();
    // Operasi pembuatan elemen...
    tx.Commit();
}

// 3. Tangkap warning setelah commit
IList<FailureMessage> postWarnings = doc.GetWarnings();
int postCount = postWarnings.Count;

// 4. Hitung delta dan lakukan audit identitas
int deltaWarnings = postCount - preCount;
if (deltaWarnings > 0)
{
    var newWarnings = postWarnings.Skip(preCount).ToList();
    foreach (var w in newWarnings)
    {
        LogWarningToTelemetry(w.GetDescriptionText(), w.GetSeverity(), w.GetFailingElementIds());
    }
}
```

---

## 5. Standar Output Telemetri Clash & Warning

Fungsi audit spasial dan warning wajib mengembalikan payload terstruktur:

```json
{
  "clash_audit_status": "COMPLETED",
  "total_candidate_pairs_evaluated": 142,
  "allowed_contacts_filtered": 138,
  "hard_clashes_detected": 4,
  "clash_details": [
    {
      "element_id_a": 128490,
      "category_a": "OST_StructuralFraming",
      "element_id_b": 128955,
      "category_b": "OST_Windows",
      "intersection_volume_m3": 0.042,
      "severity": "CRITICAL",
      "description": "Lintel clash: Balok WF memotong kusen jendela"
    }
  ],
  "warning_delta": {
    "pre_tx_count": 12,
    "post_tx_count": 12,
    "new_warnings_introduced": 0
  }
}
```

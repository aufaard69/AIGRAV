# AIGRAV Tool & Capability Registry Specification

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan matriks registri kapabilitas alat (*Tool Registry*) secara end-to-end melalui 3 lapisan arsitektur: (1) FastMCP Python, (2) HTTP Bridge Request, dan (3) C# Revit Bridge Executor, serta mencatat prioritas pengembangan endpoint native baru yang telah disetujui Pengguna.

---

## 1. Arsitektur Pemanggilan Alat 3 Lapis

Suatu kapabilitas hanya dapat dieksekusi secara sukses oleh agen AI jika ketiga lapisan ini saling terhubung secara konsisten:

```
[Lapisan 1: Agent-Facing FastMCP] (C:\AIGRAV\MCP\TestServer\server.py)
       │
       ▼ (HTTP POST http://localhost:8765/ dengan JSON payload: { tool, argument })
[Lapisan 2: HTTP Bridge Dispatcher] (AIGRAVHttpServer.cs & AIGRAVRequestManager.cs)
       │
       ▼ (Revit ExternalEvent on Main UI Thread)
[Lapisan 3: C# Skill & Executor Implementation] (BimAuditSkill, QTOSkill, AIGRAVToolExecutor)
```

**Kaidah Penting:** Keberadaan suatu metode di kode C# **TIDAK MEMBUKTIKAN** bahwa agen dapat memanggilnya jika metode tersebut tidak didaftarkan pada Lapisan 1 atau Lapisan 2. Sebaliknya, pendaftaran tool di Lapisan 1 yang tidak memiliki target eksekutor di C# adalah **tool palsu / rusak (*phantom tool*)**.

---

## 2. Matriks Pemetaan Alat End-to-End

| Nama Alat (MCP Interface) | Status di `server.py` | HTTP Payload / Command Name | Target Metode & Kelas C# di Revit Bridge | Kategori Revit | Sifat (R/W) | Pemilik Transaksi | Status Terverifikasi |
| :--- | :---: | :--- | :--- | :---: | :---: | :---: | :---: |
| `aigrav_ping` | ❌ Internal | `POST /ping` | `AIGRAVHttpServer.cs` | System | Read | Tanpa Tx | **VERIFIED IMPLEMENTED** |
| `aigrav_get_project_context` | Baris 106 | `{"tool":"aigrav_get_project_context"}` | `BimAuditSkill.SpatialTelemetry.cs:17` | Datum/Model | Read | Tanpa Tx | **VERIFIED IMPLEMENTED** |
| `aigrav_get_active_view` | ❌ Bridge | `{"tool":"get_active_view"}` | `AIGRAVToolExecutor.cs:141` | Views | Read | Tanpa Tx | **VERIFIED IMPLEMENTED** |
| `aigrav_get_selection` | ❌ Bridge | `{"tool":"get_selection"}` | `AIGRAVToolExecutor.cs:170` | Elements | Read | Tanpa Tx | **VERIFIED IMPLEMENTED** |
| `aigrav_get_element` | ❌ Bridge | `{"tool":"get_element"}` | `AIGRAVToolExecutor.cs:188` | Generic | Read | Tanpa Tx | **VERIFIED IMPLEMENTED** |
| `aigrav_create_level` | ❌ Bridge | `{"tool":"create_level"}` | `AIGRAVToolExecutor.cs:1725` (`CreateLevel`) | `OST_Levels` | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_create_grid` | ❌ Bridge | `{"tool":"create_grid"}` | `AIGRAVToolExecutor.cs:1771` (`CreateGrid`) | `OST_Grids` | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_create_grid_matrix` | Baris 169 | `{"tool":"aigrav_create_grid_matrix"}` | `BimAuditSkill.RelativeHelpers.cs:14` | `OST_Grids` | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_create_wall` | ❌ Bridge | `{"tool":"create_wall"}` | `BimAuditSkill.SpatialCreators.cs:122` | `OST_Walls` | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_create_floor` | ❌ Bridge | `{"tool":"create_floor"}` | `BimAuditSkill.SpatialCreators.cs:220` | `OST_Floors` | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_create_column` | ❌ Bridge | `{"tool":"create_column"}` | `BimAuditSkill.SpatialCreators.cs:150` | `OST_StructuralColumns` | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_create_beam` | ❌ Bridge | `{"tool":"create_beam"}` | `BimAuditSkill.SpatialCreators.cs:185` | `OST_StructuralFraming` | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_create_door` | ❌ Bridge | `{"tool":"create_door"}` | ❌ **TIDAK ADA METODE DI C#** | `OST_Doors` | Write | — | 🔴 **TARGET FASE 3 (PRIORITAS 1)** |
| `aigrav_create_window` | ❌ Bridge | `{"tool":"create_window"}` | ❌ **TIDAK ADA METODE DI C#** | `OST_Windows` | Write | — | 🔴 **TARGET FASE 3 (PRIORITAS 1)** |
| `aigrav_create_roof` | ❌ Bridge | `{"tool":"create_roof"}` | ❌ **TIDAK ADA METODE DI C#** | `OST_Roofs` | Write | — | 🔴 **TARGET FASE 3 (PRIORITAS 2)** |
| `aigrav_create_room` | ❌ Bridge | `{"tool":"create_room"}` | ❌ **TIDAK ADA METODE DI C#** | `OST_Rooms` | Write | — | 🔴 **TARGET FASE 3 (PRIORITAS 3)** |
| `place_family_instance` | ❌ Bridge | `{"tool":"place_family_instance"}` | `AIGRAVToolExecutor.cs:2074` | Generic Unhosted | Write | Tool `Transaction` | **PARTIAL (Unhosted Only)** |
| `create_generic_model` | ❌ Bridge | `{"tool":"create_generic_model"}` | `AIGRAVToolExecutor.cs:522` | `OST_GenericModel` | Write | Tool `Transaction` | **IMPLEMENTED (Restricted)** |
| `aigrav_place_on_top_of` | Baris 233 | `{"tool":"aigrav_place_on_top_of"}` | `BimAuditSkill.RelativeHelpers.cs:120` | Dynamic | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_connect_elements` | Baris 291 | `{"tool":"aigrav_connect_elements"}` | `BimAuditSkill.RelativeHelpers.cs:240` | Framing | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_detect_spatial_clashes` | Baris 477 | `{"tool":"aigrav_detect_spatial_clashes"}` | `BimAuditSkill.DeterministicValidator.cs:110` | Furniture vs Wall | Read | Tanpa Tx | **PARTIAL (BBox Only)** |
| `aigrav_audit_material_transparency` | Baris 452 | `{"tool":"aigrav_audit_material_transparency"}` | `BimAuditSkill.DeterministicValidator.cs:18` | Materials | Read | Tanpa Tx | **VERIFIED IMPLEMENTED** |
| `aigrav_audit_unresolved_warnings` | Baris 502 | `{"tool":"aigrav_audit_unresolved_warnings"}` | `BimAuditSkill.DeterministicValidator.cs:150` | Warnings | Read | Tanpa Tx | **PARTIAL (No Baseline)** |
| `aigrav_capture_revit_viewport` | Baris 378 | `{"tool":"aigrav_capture_revit_viewport"}` | `BimAuditSkill.VisualAndPython.cs:18` | Views (PNG) | Read/Write | Temp `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_inspect_model_visually` | Baris 402 | `{"tool":"aigrav_inspect_model_visually"}` | `BimAuditSkill.VisualAndPython.cs:91` | Views (4-PNG) | Read/Write | Temp `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_check_model_health` | ❌ Bridge | `{"tool":"check_model_health"}` | ❌ **TIDAK ADA SEBAGAI TOOL MANDIRI** | Warnings | Read | — | 🔴 **MISSING RUNTIME TOOL** |
| `aigrav_reload_skills` | ❌ Bridge | `{"tool":"aigrav_reload_skills"}` | `AIGRAVSkillManagementSkill.cs:40` | System | System | Tanpa Tx | **VERIFIED (Production Locked)** |
| `aigrav_execute_hide_portal_links` | ❌ Bridge | `{"tool":"aigrav_execute_hide_portal_links"}` | `BimAuditSkill.cs:11030` | `OST_RvtLinks` | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_inspect_portal_sheets` | ❌ Bridge | `{"tool":"aigrav_inspect_portal_sheets"}` | `BimAuditSkill.cs:10950` | Views/Sheets | Read | Tanpa Tx | **VERIFIED IMPLEMENTED** |
| `aigrav_generate_voute_schedules` | ❌ Bridge | `{"tool":"aigrav_generate_voute_schedules"}` | `VouteProfileQTOSkill.cs:115` | ConnectionProfiles | Write | Tool `Transaction` | **VERIFIED IMPLEMENTED** |
| `aigrav_qa_audit_all_schedules` | ❌ Bridge | `{"tool":"aigrav_qa_audit_all_schedules"}` | `BimAuditSkill.cs:33200` | Schedules | Read | Tanpa Tx | **VERIFIED IMPLEMENTED** |

---

## 3. Prioritas Implementasi Endpoint Native Baru (Approved Checkpoint 3)

Sesuai persetujuan resmi pengguna, urutan pengembangan dan penambahan endpoint native C# Bridge diatur secara ketat:

### 3.1. Prioritas 1: `aigrav_create_door` dan `aigrav_create_window` (Wall-Hosted)
- **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
- **Metode Revit API Wajib:**
  ```csharp
  // Overload resmi untuk hosted family instance:
  doc.Create.NewFamilyInstance(
      locationXYZ, 
      familySymbol, 
      hostWall, 
      hostLevel, 
      StructuralType.NonStructural
  );
  ```
- **Ketentuan Wajib:**
  * Wajib memvalidasi host wall sebelum pemanggilan (`hostWall != null && hostWall.IsValidObject`).
  * Wajib menyetel parameter `INSTANCE_SILL_HEIGHT_PARAM` untuk jendela.
  * Wajib melakukan validasi kategori semantik (`OST_Doors` atau `OST_Windows`).
  * **DILARANG KERAS** melakukan silent fallback ke Generic Model atau DirectShape.

### 3.2. Prioritas 2: `aigrav_create_roof` (Native Roof)
- **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
- **Metode Revit API Wajib:** `doc.Create.NewFootPrintRoof(modelCurveArray, level, roofType, out modelCurveMapping)` atau `doc.Create.NewExtrusionRoof(...)`.
- **Ketentuan Wajib:** Menerapkan slope per sisi dan overhang presisi; dilarang membuat prisma DirectShape.

### 3.3. Prioritas 3: `aigrav_create_room` (Architecture Room)
- **Status:** `APPROVED — DESIGN DECISION` | `NOT YET IMPLEMENTED`
- **Metode Revit API Wajib:** `doc.Create.NewRoom(level, uvPoint)`.
- **Ketentuan Wajib:** Memvalidasi batas ruang tertutup (*room-bounding boundaries*) sebelum membuat elemen untuk mencegah ruang hantu (*unclosed/unplaced room*).

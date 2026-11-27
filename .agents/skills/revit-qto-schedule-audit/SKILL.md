---
name: revit-qto-schedule-audit
description: >-
  Standard Operating Procedure and automation engine for AIGRAV QTO v4.0 Universal
  Multi-Discipline Quantity Takeoff (Structure, Architecture, Siteplan), 12-Gate QA/QC Audit,
  Dual-Key Filtering (Type of Connection vs Comments), Custom Connection Parameter Injection,
  and Smart Excel BOQ Compilation.
---

# Revit QTO v4.0 Universal Multi-Discipline & 12-Gate QA/QC Skill

This skill encodes the official **AIGRAV QTO v4.0** workflow across **Structure (`A–N`)**, **Architecture (`AR-A..AR-K`)**, and **Siteplan/Civil (`SP-A..SP-H`, `KDB`)** disciplines using clean unified databases (`C:\AIGRAV\Databases\QTO\steel_profiles_sni_jis.json`, `materials_hardware_rebar.json`, `qto_master_templates.json`).

## Dual-Key Standard, Connection Injection & Mandatory 12-Gate Audit

1. **Dual-Key Filter Standard**:
   - **ALL Steel Connections** (Custom Advance Steel + Manual/Exploded + Voute/`OST_StructConnectionProfiles`) MUST filter by **`Lokasi` + `Type of Connection`**. Always run `aigrav_qto_sync_connection_params` (`mode: "plan"` first, then `"execute"`) to auto-bind all 10 Advance Steel categories and inject parent `Type of Connection` & `Lokasi` into 100% of subelements (Plates, Bolts, Anchors, Voutes, Welds).
   - **ALL Non-Connection Elements** MUST filter by **`Lokasi` + `Comments`** (+ `Material: Name` for Material Takeoffs).
   - **Bolts & Anchors** MUST schedule `Number` (`Totals`) instead of `Count`.
2. **Mandatory Plan & Report + 12-Gate Audit (`aigrav_qto_full_audit` + `aigrav_qto_plan_wbs_schedules`)**:
   - Check: (1) unscheduled elements, (2) double counting, (3) spatial duplicates at same location, (4) empty schedules + typo diagnosis, (5) filter/cross-contamination errors, (6) unit & heading mismatches, (7) calculation errors (`ShowGrandTotal`, `Totals`, Bolt `Number` vs `Count`, `0.00 kg`), (8) steel table calibration vs cleaned SNI/JIS DB (268 profiles), (9) connection param injection & Voute check, (10) WBS numbering hygiene, (11) zero-geometry/missing params, (12) engineering sanity (`kg/m³` rebar).
   - **Zero `(Blank Comments)` & Zero Empty Handover Rule**: Blank-comment crosschecks are internal-only (`untagged_crosscheck_report`). Never leave `(Blank Comments)` or empty (`0 rows`) schedules in final Revit Project Browser deliverables; auto-tag unambiguous blanks (`AutoTagUnambiguousBlanks=true`) and prune empty schedules (`PruneEmptySchedulesAfterCreate=true`).
   - Export multi-tab Smart Excel BOQ via `deliverable_orchestrator.py build-qto-workbook` (`Executive BOQ Summary` tab with live cross-sheet references + numeric cells + `=SUM()` formulas).

---

## 1. Naming & Numbering Hierarchy Standard

All location-based schedules must follow the strict hierarchical convention:
`[Prefix].[Zone].[ItemNo] [Location] - [Category Item] [Specification / Profile / Connection Type]`

### Hierarchy Structure
```
Lokasi (Location)
  ├── Item Kategori (Kolom / Balok / Sambungan / Baseplate / Angkur)
        └── Profil Baja / Tipe Sambungan (WF 350, Endplate WF350, t=16, d=19mm)
```

### Approved Prefix & Numbering Scheme:
- `C.3.1.x` : **Waterline** (Balok, Pelat Sambungan, Baut Sambungan)
- `C.3.2.x` : **Power House** (Kolom, Balok, Pelat, Baut, Baseplate, Angkur)
- `C.3.3.x` : **Carton & Cups Storage** (Kolom, Balok, Pelat, Baut, Baseplate, Angkur)
- `C.3.4.x` : **Production Office** (Kolom, Balok, Pelat, Baut, Baseplate, Angkur)
- `C.3.5.x` : **Chiller Area 1** (Kolom, Balok, Pelat, Baut, Baseplate, Angkur)
- `C.3.6.x` : **Chiller Area 2** (Kolom, Balok, Pelat, Baut, Baseplate, Angkur)

---

## 2. Standard Column Layouts & Formats

> [!IMPORTANT]
> **Column A (Column 0 / Index 0)** MUST ALWAYS BE the `Lokasi` column, unhidden and positioned as the very first column across ALL schedules as the primary location marker.

### A. Pelat Sambungan (`OST_StructConnectionPlates`)
- **Format**: Exactly **5 Columns**
  `Lokasi | Tipe Sambungan | Thickness (mm) | Qty | Berat (Kg)`
- **Category**: `BuiltInCategory.OST_StructConnectionPlates`
- **Hidden Fields**: `Width`, `Length`, `Comments`, `Tipe Detail Sambungan`
- **Sorting/Grouping**:
  1. `Type of Connection` (Ascending, ShowHeader=False, ShowFooter=False)
  2. `Thickness` (Ascending, ShowHeader=False, **ShowFooter=True** - Subtotal Qty & Berat per ketebalan pelat)
  - Itemize every instance = False
  - Grand totals = True (Title: "Total", Count=True, Totals=True)
- **Filters**:
  1. `Lokasi` = `<LocationName>`
  2. `Type of Connection` Contains `<ConnectionTypeName>` (e.g. `WF350-Endplate`)

### B. Baut Sambungan (`OST_StructConnectionBolts`)
- **Format**: Exactly **3 Columns** (Only up to Qty, no weight)
  `Lokasi | Tipe Sambungan | Qty`
- **Category**: `BuiltInCategory.OST_StructConnectionBolts`
- **Hidden Fields**: `Comments`, `Standard`, `Diameter`
- **Sorting/Grouping**: By `Type of Connection` (Ascending), Itemize every instance = False, Grand totals = True.
- **Filters**:
  1. `Lokasi` = `<LocationName>`
  2. `Type of Connection` Contains `<ConnectionTypeName>` (e.g. `WF350-Endplate`)

### C. Baseplate Kolom (`OST_StructConnectionPlates`)
- **Format**: Exactly **7 Columns**
  `Lokasi | Keterangan | Thickness (mm) | Width (mm) | Length (mm) | Qty | Berat (Kg)`
- **Category**: `BuiltInCategory.OST_StructConnectionPlates`
- **Hidden Fields**: `Type of Connection` (to prevent blank column A)
- **Sorting/Grouping**: By `Comments`, `Thickness`, `Width`, `Length` (Ascending).
- **Filters**:
  1. `Lokasi` = `<LocationName>`
  2. `Comments` Contains `Baseplate` (or thickness specific `t=12` / `t=19`)

### D. Kolom & Balok Baja (`OST_StructuralColumns` / `OST_StructuralFraming`)
- **Format**: Exactly **6 Columns**
  `Lokasi | Family and Type | Length | Comments | Qty | Berat (Kg)`
- **Filters**:
  1. `Lokasi` = `<LocationName>`
  2. `Type` Contains `<ProfileName>` (e.g. `WF 350`, `H 200`, `RCWF`)

### E. Angkur Kolom (`OST_StructuralFraming` / Anchor Hardware)
- **Format**: Exactly **5 Columns**
  `Lokasi | Comments | Diameter | Count | Length`
- **Filters**:
  1. `Lokasi` = `<LocationName>`

### F. Voute & Profil Sambungan (`OST_StructConnectionProfiles`)
- **Format**: Exactly **7 Columns**
  `Lokasi | Tipe Sambungan | Type | Length (mm) | Volume | Qty | Berat (Kg)`
- **Category**: `BuiltInCategory.OST_StructConnectionProfiles`
- **Weight Calibration**: Strictly calculated from solid volume ($\text{Exact Weight} = \text{Volume} \times 7.850\text{ kg/m}^3$) for tapered / half-cut haunches.
- **Sorting/Grouping**:
  1. `Profile Type` (Ascending)
  2. `Cut Length` (Ascending, ShowFooter=True for subtotal per length)
  - Itemize every instance = False
  - Grand totals = True (Title: "Total", Count=True, Totals=True)
- **Filters**:
  1. `Lokasi` = `<LocationName>`
  2. `Type of Connection` Contains `<ConnectionTypeName>` (e.g. `WF350-Endplate`)

---

## 3. Anti Double-Counting Protocol

To guarantee that elements partitioned into location-specific schedules are NOT counted twice in General or Main Building schedules:

1. **Bangunan Utama / General Schedules** (`B.1.8`, `B.2.1`, `C.2.3`, `C.2.5`, `C.2.9` s/d `C.2.23`):
   - Add field `Lokasi` (can be hidden).
   - Add filter: `Lokasi Equal ""` (string empty).
   - **Effect**: Any element assigned a location string is automatically excluded from the main building schedule.

2. **Location Schedules** (`C.3.X.Y`):
   - Field `Lokasi` is Column 0 (visible).
   - Filter: `Lokasi Equal "<LocationName>"`.

---

## 4. Execution Tools & Automation Bridge

Use `powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool <ToolName> -Argument <Arg>` to invoke the Revit Bridge:

| Tool Name | Purpose | Example Command |
|:---|:---|:---|
| `aigrav_exec_subagent1_batch1` | Generate Batch 1 (Carton & Cups + Production Office) | `call_revit.ps1 -Tool aigrav_exec_subagent1_batch1` |
| `aigrav_exec_subagent2_batch2` | Generate Batch 2 (Waterline + Power House) | `call_revit.ps1 -Tool aigrav_exec_subagent2_batch2` |
| `aigrav_exec_subagent3_batch3` | Generate Batch 3 (Chiller Area 1 & 2) | `call_revit.ps1 -Tool aigrav_exec_subagent3_batch3` |
| `aigrav_cleanup_orphaned_copies` | Delete obsolete duplicate `Copy` schedules | `call_revit.ps1 -Tool aigrav_cleanup_orphaned_copies` |
| `aigrav_apply_anti_double_counting` | Apply `Lokasi = ""` filter to Bangunan Utama | `call_revit.ps1 -Tool aigrav_apply_anti_double_counting` |
| `aigrav_qa_audit_all_schedules` | In-memory verification of all 89 schedules | `call_revit.ps1 -Tool aigrav_qa_audit_all_schedules` |
| `aigrav_inspect_schedule_def` | Deep inspection of specific schedule definition | `call_revit.ps1 -Tool aigrav_inspect_schedule_def -Argument "<ScheduleName>"` |

---

## 5. Sub-Agent Coordination Roles

Follow the mandatory 3-Sub-Agent orchestration defined in `c:\AIGRAV\AGENTS.md`:
1. **Sub-Agent 1 (Reviewer & Architect Lead)**: Validates standard drawings (`STD-BJ-*`, `STD-PDS-*`), formulates the schedule breakdown matrix, and cross-checks parameter formats.
2. **Sub-Agent 2 (Revit Bridge Executor)**: Executes batch transactions in partitioned groups (Batch 1, 2, 3) to prevent Revit API timeouts.
3. **Sub-Agent 3 (QA/QC Auditor & Inspector)**: Verifies in-memory `ScheduleDefinition`, field orders, visibility flags, zero copy clutter, and certifies 100% compliance.

# AIGRAV — DRAWING AUTOMATION EXISTING CAPABILITY AUDIT
**Document ID:** AIGRAV-GOV-DD0-01  
**Status:** AUDIT_COMPLETE — READ-ONLY BASELINE  
**Date:** 2026-11-12  
**Target Platform:** Autodesk Revit 2026 / .NET 10 / C# Revit Bridge / FastMCP Python  
**Location:** `C:\AIGRAV\_governance_review\drawing_documentation\DRAWING_AUTOMATION_EXISTING_CAPABILITY_AUDIT.md`

---

## 1. Executive Summary & Audit Scope

This audit provides an exhaustive, line-by-line inspection of the drawing generation, annotation, sheet layout, and documentation capabilities currently present in the AIGRAV codebase. The audit inspected:
- C# Revit Bridge solution: `C:\AIGRAV\Revit\AIGRAV.RevitBridge\` (including `AIGRAVToolExecutor.cs`, `SheetManagerSkill.cs`, `BimAuditSkill.CadDetailImporter.cs`, `HouseType36Builder.cs`, `HouseType36Perspectives.cs`).
- FastMCP Python Server: `C:\AIGRAV\MCP\TestServer\server.py` and tool schemas in `C:\Users\sapuser\.gemini\antigravity\mcp\aigrav-test\`.
- Active Agent Skills: `C:\AIGRAV\.agents\skills\` (especially `cad-to-revit-drafting-views`).
- Installed Autodesk Revit 2026 official libraries: `C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\`.

### Core Audit Verdict:
1. **Critical Architectural Void in Native Detailing:** AIGRAV has **zero** implementation of native 2D construction detailing (`FilledRegion`, `DetailComponent`, `RepeatingDetail`, and `Dimension`). All existing detail drawings are either imported flat CAD DWG files (`CadDetailImporter.cs`) or raw 2D line primitives (`doc.Create.NewDetailCurve` in `HouseType36Builder.cs`).
2. **Missing C# Handlers for Registered MCP Tools:** Tools declared in FastMCP `server.py` and the MCP tool schema registry (`aigrav_create_dimension`, `aigrav_create_text_note`, `aigrav_create_detail_curve`, `aigrav_create_model_curve`, `aigrav_create_room`, `aigrav_create_area`, `aigrav_create_view`) are **not routed** in `AIGRAVToolExecutor.cs` and return a fallback string: `"AIGRAV tool '{op}' is registered, but its specialized Revit API implementation is currently pending."`
3. **Partial Sheet Management & Missing Schedule Placement:** `SheetManagerSkill.cs` provides robust TitleBlock boundary extraction (`ExtractTitleBlockGeometry`) and basic viewport collision checking (`PaperRectMm.Intersects`), but lacks adaptive scale calculation, true 2D bin-packing layout, and schedule placement (`ScheduleSheetInstance.Create` is 0% implemented).
4. **Missing PDF Export Engine:** AIGRAV has no PDF export capability (`doc.Export(folder, names, new PDFExportOptions())`). View export is restricted to PNG raster images via `doc.ExportImage()`.

---

## 2. Comprehensive Capability Matrix (18 Technical Areas)

| # | Capability Area | Status | Source File | Class / Method | Line Range | Caller / Invoker | Dependency | Key Limitation & Evidence |
|---|---|---|---|---|---|---|---|---|
| 1 | **DetailCurve** | `PARTIALLY_IMPLEMENTED` | `HouseType36Builder.cs` | `HouseType36Builder.Create2DDoorSwing` | L1120–L1139 | `HouseType36Builder.Execute` | Revit DB API (`NewDetailCurve`) | Only used to draw 2D door swing arcs in a single hardcoded script. No generalized drafting tool exists. |
| 2 | **ModelCurve** | `PARTIALLY_IMPLEMENTED` | `HouseType36Builder.cs` | `HouseType36Builder.CreateSoffitFascia` | L251–L260 | `HouseType36Builder.Execute` | Revit DB API (`ModelCurveArray`) | Used solely as boundary input for roof fascia generation. `aigrav_create_model_curve` tool is unrouted in Bridge. |
| 3 | **FilledRegion & Masking** | `MISSING` | N/A | None | N/A | None | Revit DB API (`FilledRegion.Create`) | Zero references in entire C# codebase. Cannot generate material hatching (concrete, brick, soil, insulation). |
| 4 | **FillPattern & LineStyles** | `PARTIALLY_IMPLEMENTED` | `BimAuditSkill.cs` | `BimAuditSkill.AuditMaterials` | L6857–L6875 | `BimAuditSkill.Execute` | `FilteredElementCollector(doc).OfClass(typeof(FillPatternElement))` | Read-only collector used to audit material transparency. Cannot assign or create custom line styles or fill patterns for drafting views. |
| 5 | **Detail Component Families** | `MISSING` | `BimAuditSkill.CrossDocumentAndFamily.cs` | Filter query only | L1108 | Audit only | `BuiltInCategory.OST_DetailComponents` | Only queried in a linq expression checking for element types. Never loaded, placed, or configured in views. |
| 6 | **TextNote** | `PARTIALLY_IMPLEMENTED` | `HouseType36Builder.cs` | Hardcoded room label loop | L676–L725 | `HouseType36Builder.Execute` | `TextNote.Create(doc, plan.Id, pt, text, tnt.Id)` | Only executed in Type 36 builder with hardcoded 2.0 mm font. `aigrav_create_text_note` in MCP server is unrouted in `AIGRAVToolExecutor.cs`. |
| 7 | **IndependentTag** | `MISSING` | N/A | None | N/A | None | Revit DB API (`IndependentTag.Create`) | Zero occurrences in codebase. Cannot place door, window, wall, or room tags on views. |
| 8 | **Dimension Creation** | `MISSING` | `AIGRAVToolExecutor.cs` / `server.py` | `server.py:aigrav_create_dimension` | `server.py:433`, `ToolExecutor:743` | MCP client | `doc.Create.NewDimension` | Tool is declared in Python schema but completely missing from `AIGRAVToolExecutor.cs` switch block. Returns pending message. |
| 9 | **ViewSection & Detail** | `MISSING` | N/A | None | N/A | None | Revit DB API (`ViewSection.CreateSection`, `ViewSection.CreateDetail`) | Zero occurrences in codebase. True building sections or enlarged detail views cannot currently be generated. |
| 10 | **ElevationMarker** | `MISSING` | `HouseType36Builder.cs` | Workaround via `View3D.CreateIsometric` | L572–L587 | `HouseType36Builder.Execute` | `View3D.CreateIsometric` | True Revit elevation markers (`ElevationMarker.CreateElevationMarker`) are missing. Elevations are faked using 3D isometric views. |
| 11 | **View.Scale Management** | `CODE_EXISTS` | `AIGRAVToolExecutor.cs` & `CadDetailImporter.cs` | `AIGRAVToolExecutor.SetViewScale` | `ToolExecutor:1514`, `CadDetail:147` | MCP `set_view_scale` | `view.Scale = scale` | Raw integer setter with zero intelligence: no check against titleblock usable area, no font legibility validation, no relayout. |
| 12 | **CropBox & AnnotationCrop** | `PARTIALLY_IMPLEMENTED` | `CadDetailImporter.cs` | `CadDetailImporter.CreateDraftingViewsAndPlace` | L179–L186 | CAD detail importer | `dv.CropBoxActive`, `dv.CropBox` | Only configures `CropBox` for CAD imports. `AnnotationCropActive` is never enabled, causing annotation markers to spill out. |
| 13 | **ViewRange & Far Clip** | `MISSING` | N/A | None | N/A | None | `PlanViewRange`, `view.get_Parameter(VIEW_FAR_CLIP_ACTIVE)` | View range is never programmatically queried or adjusted. Far clipping is never set on sections/elevations, causing visual background noise. |
| 14 | **ViewTemplate Management** | `PARTIALLY_IMPLEMENTED` | `BimAuditSkill.cs` | `BimAuditSkill.AuditViewTemplates` | L6797–L6948 | `BimAuditSkill.Execute` | `view.ViewTemplateId` | Can inspect existing view templates and report overrides. Cannot create, assign, or standardize view templates programmatically. |
| 15 | **View.HideElements** | `CODE_EXISTS` | `AIGRAVToolExecutor.cs` & `HouseType36Builder.cs` | `AIGRAVToolExecutor.HideElements` | `ToolExecutor:1616`, `HouseBuilder:652` | MCP `hide_elements` | `view.HideElements(ids)` | Crude element ID hiding or blanket category hiding (`OST_Sections`). Lacks semantic awareness of marker ownership. |
| 16 | **ViewSheet Management** | `CODE_EXISTS` | `AIGRAVToolExecutor.cs` | `AIGRAVToolExecutor.CreateSheet` | L2218–L2235 | MCP `create_sheet` | `ViewSheet.Create(doc, titleBlockTypeId)` | Creates empty sheets. Requires valid TitleBlock FamilySymbol ID. |
| 17 | **Viewport Placement** | `CODE_EXISTS` | `SheetManagerSkill.cs` | `SheetManagerSkill.PlaceViewOnSheetSmart` | L443–L620 | MCP `place_view_on_sheet` | `Viewport.Create(doc, sheet.Id, view.Id, center)` | Creates viewport and positions via `targetVp.SetBoxCenter()`. Computes BoxOutline delta. |
| 18 | **Viewport Labels & Collision** | `PARTIALLY_IMPLEMENTED` | `SheetManagerSkill.cs` | `SheetManagerSkill.GetSheetPaperSpace` | L358–L390, L553 | SheetManager | `vp.GetBoxOutline()`, `vp.GetLabelOutline()` | Measures label outline, but excludes label from placement candidate bounding box, resulting in label collisions. |
| 19 | **ScheduleSheetInstance** | `MISSING` | N/A | None | N/A | None | `ScheduleSheetInstance.Create(doc, sheetId, schedId, pt)` | Zero occurrences in codebase. `ViewSchedule` can be created, but CANNOT be placed onto sheets. |
| 20 | **PDF Export Engine** | `MISSING` | N/A | None | N/A | None | `doc.Export(folder, names, new PDFExportOptions())` | Zero occurrences. System only exports PNG raster images via `doc.ExportImage()`. No vector PDF export exists. |

---

## 3. Deep Dive: Existing Modules & Architecture

### 3.1 `SheetManagerSkill.cs` (1,168 Lines)
- **Strengths:**
  - Implements `PaperRectMm` struct with clean millimetric math (`FtToMmFactor = 304.8`).
  - `ExtractTitleBlockGeometry()` dynamically queries `BuiltInCategory.OST_TitleBlocks` to find title block bounding box on the sheet and classifies paper size (A0, A1, A2, A3, A4, or Custom).
  - Calculates `drawableRect` by deducting standard border margins (left 15mm, top/bottom 12mm, right stamp strip 14% of width).
  - Inspects existing viewports on sheet (`vp.GetBoxOutline()`, `vp.GetBoxCenter()`), reporting center, size, and collision status.
  - Implements ASCII paper map generator (`BuildSheetAsciiMap`) for telemetry output.
- **Critical Architectural Deficiencies:**
  1. **Slot Candidate Brittleness:** `GenerateLayoutCandidates` tests only 5 hardcoded positions (Center, TopLeft, TopRight, BottomLeft, BottomRight). If all 5 slots have any collision, it falls back to placing at sheet center, causing guaranteed overlap!
  2. **Viewport Label Neglect:** While `vp.GetLabelOutline()` is called for telemetry, the candidate collision check (`candidateRect.Intersects(eb.box, paddingMm)`) tests *only* `BoxOutline`. The view title and scale label hanging below the viewport are ignored, causing titles to collide with lower viewports or sheet border lines.
  3. **Zero Scale Calculation:** It assumes the incoming `View` already has an appropriate scale. It does not calculate whether the view fits inside the drawable canvas before attempting placement.
  4. **No Schedule Support:** Completely ignores `ScheduleSheetInstance`.

### 3.2 `BimAuditSkill.CadDetailImporter.cs` (309 Lines)
- **Strengths:**
  - Accurately demonstrates how to create a `ViewDrafting` (`ViewDrafting.Create(doc, draftingVft.Id)`).
  - Correctly calculates imperial crop box coordinates from millimetric paper dimensions:
    `halfWFt = (item.WidthMm * 0.5 / 304.8) * item.Scale`.
  - Sets viewport type to "No Title" (`BuiltInCategory.OST_Viewports`) to prevent default Revit title conflict.
- **Critical Architectural Deficiencies:**
  1. **Purely Dependent on External CAD Files:** Entire logic relies on `doc.Import(item.DwgPath, dwgOpts, dv, out impId)`. If DWG does not exist, nothing is drawn.
  2. **Silent Failure & Warning Swallowing:** Uses `fOpts.SetFailuresPreprocessor(new ClashWarningSwallower())` to suppress all Revit transaction warnings.
  3. **Hardcoded Coordinates:** Relies on pre-extracted paper space coordinates (`PaperXMm`, `PaperYMm`) mapped from AutoCAD DWGs.

### 3.3 `HouseType36Builder.cs` (Drawing & View Generation Section)
- **Strengths:**
  - Demonstrates native `TextNote.Create` with custom 2.0 mm `TextNoteType`.
  - Demonstrates `doc.Create.NewDetailCurve` for 2D door leaf and swing arc generation.
  - Generates 3D perspectives (`View3D.CreatePerspective`) and axonometric views (`View3D.CreateIsometric`).
- **Critical Architectural Deficiencies:**
  1. **Pseudo-Elevations:** Generates front elevation as an isometric 3D view (`View3D.CreateIsometric`) oriented to the front plane (`eyeF = new XYZ(0, -50, 2)`), instead of creating a true native Revit `ElevationMarker`.
  2. **Blind Marker Hiding:** Hides all section markers unconditionally via `plan.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Sections), true)`.
  3. **Absence of Detail Assemblies:** Wall sections and foundation junctions are completely absent. Details cannot be produced from the model.

---

## 4. Revit 2026 Library Asset Inventory

An inspection of the official installed Autodesk Revit 2026 library (`C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\`) reveals extensive native assets available for immediate utilization:

1. **Detail Items (`OST_DetailComponents`) — 653 RFA Files Available:**
   - `Concrete`: 55 RFAs (cast-in-place concrete joints, precast connections, rebar sections).
   - `Masonry`: 87 RFAs (concrete masonry units, brick ties, mortar joints, lintels).
   - `Metals`: 207 RFAs (structural steel shapes, plate washers, cold-formed framing, fasteners).
   - `Thermal and Moisture Protection`: 33 RFAs (batt insulation, rigid board, flashing, vapor barriers, sealants).
   - `Doors and Windows`: 98 RFAs (aluminum storefront profiles, wood frame jambs, head details, sills).
   - `Finishes`: 76 RFAs (gypsum wallboard, ceramic tile, suspended ceiling runners).
   - `Wood and Plastic`: 60 RFAs (dimensional lumber, plywood, architectural trim).
2. **Titleblocks (`OST_TitleBlocks`) — 5 Official Metric Templates:**
   - `A0 metric.rfa` ($1189 \times 841\text{ mm}$)
   - `A1 metric.rfa` ($841 \times 594\text{ mm}$)
   - `A2 metric.rfa` ($594 \times 420\text{ mm}$)
   - `A3 metric.rfa` ($420 \times 297\text{ mm}$)
   - `A4 metric.rfa` ($297 \times 210\text{ mm}$)
3. **Annotations (`OST_GenericAnnotation`, `OST_Tags`) — 321 RFA Files Available:**
   - Door, window, wall, room, and material tags across Architectural, Structural, and MEP disciplines.

---

## 5. Architectural Preservation vs Modification Strategy

| Component | Status | Strategy | Rationale |
|---|---|---|---|
| `SheetManagerSkill.cs` | **PRESERVE & ENHANCE** | Modify | `ExtractTitleBlockGeometry`, `PaperRectMm`, and viewport outline inspection are rock-solid. Needs 2D guillotine packing, label boundary inclusion, and schedule placement. |
| `BimAuditSkill.CadDetailImporter.cs` | **PRESERVE AS SPECIALIZED MODULE** | Retain | Serves a valid purpose for legacy CAD DWG detail ingestion. Keep separate from native Revit BIM detail generator. |
| `AIGRAVToolExecutor.cs` | **MODIFY DISPATCH** | Modify | Route pending tools (`create_dimension`, `create_text_note`, `create_detail_curve`, `create_sheet`, `create_schedule`). |
| `DrawingDetailEngine` | **GENUINELY NEW** | Create | Missing engine required to interrogate Revit compound structures, load native `OST_DetailComponents`, and generate `FilledRegion` material hatching. |
| `IntelligentSheetComposer` | **ENHANCED REFACTOR** | Refactor | Upgrade `SheetManagerSkill` placement engine from static candidate slots to dynamic 2D guillotine bin-packing with zoning hierarchy. |
| `ScheduleSheetLayoutManager`| **GENUINELY NEW** | Create | Implement `ScheduleSheetInstance.Create` to place, format, and fit schedules on sheets. |
| `PDFDrawingInspector` | **GENUINELY NEW** | Create | Native Revit 2022+ PDF export engine (`PDFExportOptions`) with headless PDF verification. |

---

## 6. Audit Conclusion & Governance Sign-off

The codebase possesses excellent foundation logic for sheet space coordinate mathematics and basic viewport interaction, but is completely devoid of native Revit 2D detailing, dimensioning, scheduling on sheets, and PDF output. All necessary Revit 2026 APIs and family libraries are fully present and supported. No implementation may begin until this audit is formally reviewed.

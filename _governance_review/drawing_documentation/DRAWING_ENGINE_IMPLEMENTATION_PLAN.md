# AIGRAV — DRAWING ENGINE IMPLEMENTATION PLAN
**Document ID:** AIGRAV-GOV-DD0-07  
**Status:** DRAFT IMPLEMENTATION ROADMAP — SUBJECT TO FORMAL APPROVAL  
**Date:** 2026-11-12  
**Target Platform:** Autodesk Revit 2026 / .NET 10 / C# Revit Bridge / FastMCP Python  
**Location:** `C:\AIGRAV\_governance_review\drawing_documentation\DRAWING_ENGINE_IMPLEMENTATION_PLAN.md`

---

## 1. Executive Strategy: Refactor Existing vs Build New

Rather than over-engineering 10 disconnected micro-classes from scratch, the system architecture leverages the solid mathematical foundations already present in `SheetManagerSkill.cs` while building the missing native detailing, annotation, and PDF export engines.

```
┌────────────────────────────────────────────────────────────────────────┐
│ EXISTING REFACTORED MODULES                                            │
│ 1. IntelligentSheetComposer (Refactored SheetManagerSkill.cs)          │
│ 2. ViewportCollisionDetector (Extracted from PaperRectMm logic)        │
│ 3. AIGRAVToolExecutor.cs (Add dispatch routes for pending tools)       │
├────────────────────────────────────────────────────────────────────────┤
│ GENUINELY NEW MODULES                                                  │
│ 4. DrawingDetailEngine (Assembly interrogation & FilledRegion)         │
│ 5. DrawingGraphicProfileManager (Discipline graphic standards & pens)  │
│ 6. AnnotationAndDimensionEngine (NewDimension & IndependentTag)        │
│ 7. ViewScaleAndVisibilityEngine (Adaptive scale, FarClip, ViewRange)   │
│ 8. ReferenceAnnotationVisibilityManager (Semantic marker filter)       │
│ 9. ScheduleSheetLayoutManager (ScheduleSheetInstance placement)        │
│ 10. PDFDrawingInspector (Revit 2026 native vector PDF export & audit)  │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Evaluation & Specification of the 10 System Modules

### Module 1: `IntelligentSheetComposer`
- **Architecture Decision:** Refactor and upgrade `SheetManagerSkill.cs`.
- **Responsibilities:** Dynamic TitleBlock bounds interrogation, drawable canvas extraction, 2D zoned guillotine bin-packing, multi-view orchestration, automated multi-sheet splitting.
- **Dependencies:** `ViewportCollisionDetector`, `ScheduleSheetLayoutManager`.
- **Required Revit API:** `ViewSheet`, `OST_TitleBlocks`, `Viewport.Create`, `Viewport.SetBoxCenter`.
- **Input / Output:** Input: List of view IDs, schedule IDs, target sheet number. Output: JSON layout telemetry, viewport IDs, paper coordinates, collision status.
- **Transaction:** Enclosed in atomic `TransactionGroup`.
- **Acceptance Criteria:** Zero viewport-to-viewport or viewport-to-sheet border collisions.

### Module 2: `ViewportCollisionDetector`
- **Architecture Decision:** Preserve & enhance `PaperRectMm` struct.
- **Responsibilities:** Compute `UnifiedViewportEnvelope` (`BoxOutline` $\cup$ `LabelOutline` $+ \text{padding}$), test 2D intersections, verify sheet boundary containment.
- **Required Revit API:** `Viewport.GetBoxOutline()`, `Viewport.GetLabelOutline()`.
- **Acceptance Criteria:** Accurately detects collisions including view title labels.

### Module 3: `ScheduleSheetLayoutManager`
- **Architecture Decision:** Genuinely New Module (`ScheduleSheetLayoutManager.cs`).
- **Responsibilities:** Calculate schedule paper height based on row count, place `ScheduleSheetInstance` in Zone 4 (top-right abutting titleblock), perform multi-segment splitting if schedule height exceeds available canvas.
- **Required Revit API:** `ScheduleSheetInstance.Create(doc, sheetId, scheduleId, point)`, `scheduleSheetInstance.Segment()`.
- **Acceptance Criteria:** Schedule placed cleanly on sheet without colliding with floor plans or title block stamps.

### Module 4: `DrawingDetailEngine`
- **Architecture Decision:** Genuinely New Module (`DrawingDetailEngine.cs`).
- **Responsibilities:** Interrogate 3D wall/floor/roof compound layers (`CompoundStructureLayer`), map construction materials to native `OST_DetailComponents` and `FilledRegion` drafting patterns, generate waterproof membranes and ground earth lines.
- **Required Revit API:** `WallType.GetCompoundStructure()`, `FilledRegion.Create()`, `doc.Create.NewFamilyInstance()`, `doc.Create.NewDetailCurve()`.
- **Acceptance Criteria:** Generates 2D cross-sections displaying real physical layer thicknesses and distinct pen weight contrast.

### Module 5: `DrawingGraphicProfileManager`
- **Architecture Decision:** Genuinely New Module (`DrawingGraphicProfileManager.cs`).
- **Responsibilities:** Manage pen weights (Pens 1–8), standard metric line weights per scale, discipline-specific line styles, and fill pattern element lookup.
- **Required Revit API:** `FillPatternElement`, `GraphicsStyle`, `Category.GetLineWeight()`.
- **Acceptance Criteria:** Enforces ISO 128 / SNI lineweight hierarchy across all views.

### Module 6: `AnnotationAndDimensionEngine`
- **Architecture Decision:** Genuinely New Module (`AnnotationAndDimensionEngine.cs`).
- **Responsibilities:** Extract stable geometric `Reference` handles from wall exterior faces, column centerlines, and grid lines; place continuous dimension strings via `doc.Create.NewDimension`; place tags via `IndependentTag.Create`.
- **Required Revit API:** `doc.Create.NewDimension(view, line, referenceArray)`, `IndependentTag.Create(doc, viewId, elementId, ...)`.
- **Acceptance Criteria:** Valid dimension strings placed with $2.0\text{ mm}$ printed text height referencing real BIM geometry.

### Module 7: `ViewScaleAndVisibilityEngine`
- **Architecture Decision:** Genuinely New Module (`ViewScaleAndVisibilityEngine.cs`).
- **Responsibilities:** Calculate deterministic adaptive scale ($S_{\text{chosen}} = \min \{ s \in \mathbb{S} \mid s \ge S_{\text{req}} \}$), configure `PlanViewRange` offsets, enforce `CropBoxActive` / `AnnotationCropActive`, configure elevation/section `FarClipOffset`.
- **Required Revit API:** `view.Scale`, `PlanViewRange`, `VIEW_FAR_CLIP_ACTIVE_PARAM`, `VIEWER_ANNOTATION_CROP_ACTIVE`.
- **Acceptance Criteria:** Views sized proportionally to sheet canvas; far clipping eliminates background noise.

### Module 8: `ReferenceAnnotationVisibilityManager`
- **Architecture Decision:** Genuinely New Module (`ReferenceAnnotationVisibilityManager.cs`).
- **Responsibilities:** Semantic filtering of section, elevation, and callout markers; identify unplaced working views vs public sheet-placed views; hide markers on a per-element basis (`view.HideElements`) without globally hiding categories.
- **Required Revit API:** `view.HideElements(ICollection<ElementId>)`, `view.UnhideElements()`.
- **Acceptance Criteria:** Irrelevant working section markers hidden; essential cross-reference markers preserved.

### Module 9: `DrawingQualityValidator`
- **Architecture Decision:** Genuinely New Module (`DrawingQualityValidator.cs`).
- **Responsibilities:** Run in-memory pre-publish audit against Master Test Matrix (TC-DRW-01 through TC-DRW-25), verify non-fabrication invariant (`DESIGN_INPUT_REQUIRED`), generate JSON audit report.
- **Acceptance Criteria:** Emits explicit PASS / FAIL / NOT_APPLICABLE verdicts for every test.

### Module 10: `PDFDrawingInspector`
- **Architecture Decision:** Genuinely New Module (`PDFDrawingInspector.cs`).
- **Responsibilities:** Native vector PDF export using Revit 2026 `doc.Export()` with `PDFExportOptions`; perform headless inspection of generated PDF files (file size, page dimensions matching title block, text layer readability).
- **Required Revit API:** `doc.Export(folder, sheetIds, new PDFExportOptions())`.
- **Acceptance Criteria:** Produces crisp vector PDF matching paper sheet size (e.g., exactly $420 \times 297\text{ mm}$ for A3).

---

## 3. Discipline-Specific Drawing Intelligence & Safeguards

```
┌────────────────────────────────────────────────────────────────────────┐
│ ARCHITECTURAL PROFILE                                                  │
│ • Floor Plans (1:100 / 1:50): Cut at +1200mm, door tags, room names   │
│ • Elevations (1:100): True ElevationMarkers, far clipped, shading on  │
│ • Building Sections (1:100): Cut concrete Pen 7, brick Pen 6           │
│ • Construction Details (1:20 / 1:10): Assemblies, flashing, DPC        │
├────────────────────────────────────────────────────────────────────────┤
│ STRUCTURAL PROFILE                                                     │
│ • Sloof / Foundation Plans (1:100): Slices plinth, shows footings      │
│ • Column & Beam Framing Plans (1:100): Structural framing tags         │
│ • Structural Junction Details (1:20): Baseplates, anchors, gussets     │
│ ⚠️ SAFEGUARD: If rebar diameter/spacing is missing in model brief,     │
│    EMIT [DESIGN_INPUT_REQUIRED]. NEVER FABRICATE REBAR SIZES.          │
├────────────────────────────────────────────────────────────────────────┤
│ MEP PROFILE                                                            │
│ • Plumbing & Drainage Layouts: Pipe slopes, invert levels              │
│ • Electrical Lighting & Power Plans: Circuit tags, fixture symbols     │
│ ⚠️ SAFEGUARD: If invert levels or pipe slopes are uncalculated,         │
│    EMIT [DESIGN_INPUT_REQUIRED]. NEVER FABRICATE SEWER SLOPES.         │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Pilot Scenario Specification — Residential Type 36

### Drawing Package Overview:
A single, complete, professionally coordinated construction document package for a Type 36 residential unit placed onto an ISO standard A3 or A1 title block sheet:

| # | Drawing Title | View Type | Recommended Scale | View Settings & Visibility | Key Detail Components / Annotations | Assigned Sheet Zone |
|---|---|---|---|---|---|---|
| 1 | **Denah Lantai 1** | `ViewPlan` (FloorPlan) | **1:100** (or 1:50 on A1) | Cut Plane $+1200\text{ mm}$, Fine, Arch, FarClip Off, Crop Active | Wall dimension string, Room Names, Door/Window Tags (`P1`, `J1`) | **Zone 1** (Bottom-Left) |
| 2 | **Tampak Depan** | `ViewSection` (Elevation) | **1:100** | Fine, Arch, Shadows On, FarClip Active ($+8000\text{ mm}$) | Level elevation markers ($+0.00$, $+3.60$, $+6.20$), Grid labels | **Zone 2** (Top-Left) |
| 3 | **Tampak Belakang**| `ViewSection` (Elevation) | **1:100** | Fine, Arch, Shadows On, FarClip Active ($+8000\text{ mm}$) | Level elevation markers, roof ridge height callout | **Zone 2** (Top-Center) |
| 4 | **Potongan A-A** | `ViewSection` (Section) | **1:100** | Fine, Arch, FarClip Active ($+4000\text{ mm}$), Cut Pen 7/6 | Floor-to-ceiling headroom clearance ($3.45\text{ m}$), roof pitch ($30^\circ$) | **Zone 2** (Center) |
| 5 | **Detail Pertemuan Pondasi & Dinding** | `ViewDrafting` / `ViewSection` | **1:20** | Fine, Pen 7/6/1 contrast, AnnotationCrop Active | Batu kali / footplate, sloof $150\times 200$, DPC membrane, tanah urug, ground line Pen 8 | **Zone 3** (Bottom-Center) |
| 6 | **Jadwal Pintu (Door Schedule)** | `ViewSchedule` | Schedule | Text 2.0 mm, Total Count, Mark, Width, Height | Formatted table placed via `ScheduleSheetInstance` | **Zone 4** (Top-Right) |
| 7 | **Jadwal Jendela (Window Schedule)** | `ViewSchedule` | Schedule | Text 2.0 mm, Total Count, Mark, Width, Height | Formatted table placed directly beneath Door Schedule | **Zone 4** (Middle-Right) |

### Sheet Composition Coordinates (ISO A3 Metric: $420 \times 297\text{ mm}$):
- `drawableRect`: Min $(20.0, 12.0)$, Max $(355.0, 285.0)$ [Right stamp strip occupies $65\text{ mm}$].
- **Denah Lantai 1 (1:100):** Model footprint $6.0 \times 6.0\text{ m} \rightarrow 60 \times 60\text{ mm}$ paper box. Placed at Center $(85.0, 85.0)$.
- **Tampak Depan (1:100):** Footprint $6.0 \times 6.2\text{ m} \rightarrow 60 \times 62\text{ mm}$ paper box. Placed at Center $(85.0, 200.0)$.
- **Tampak Belakang (1:100):** Placed at Center $(170.0, 200.0)$.
- **Potongan A-A (1:100):** Placed at Center $(170.0, 85.0)$.
- **Detail Pondasi (1:20):** Detail size $1.2 \times 1.6\text{ m} \rightarrow 60 \times 80\text{ mm}$ paper box. Placed at Center $(255.0, 85.0)$.
- **Door & Window Schedules:** Width $80\text{ mm}$, Height $70\text{ mm}$. Placed at Center $(295.0, 220.0)$ in Zone 4.
- **Verification:** All viewports maintain $\ge 15\text{ mm}$ edge-to-edge separation; zero collisions detected.

---

## 4. Multi-Batch Implementation Roadmap

```
BATCH D1: SHEET LAYOUT & ADAPTIVE SCALE ENGINE
├── 1. Refactor SheetManagerSkill into IntelligentSheetComposer
├── 2. Implement 2D Zoned Guillotine Bin-Packing Algorithm
├── 3. Implement Unified Viewport Envelope (BoxOutline ∪ LabelOutline)
├── 4. Implement ScheduleSheetInstance.Create engine
└── Verification: TC-DRW-08, 09, 16, 17, 18, 19, 20 + Live Viewport Capture

BATCH D2: VIEW SETUP, CROP & SEMANTIC VISIBILITY ENGINE
├── 1. Implement true ElevationMarker.CreateElevationMarker routines
├── 2. Implement true ViewSection.CreateSection and CreateDetail routines
├── 3. Implement PlanViewRange configuration engine
├── 4. Implement FarClip and AnnotationCrop activation
└── 5. Implement Semantic Reference Annotation Filter (anti-blanket hiding)
└── Verification: TC-DRW-10, 11, 12, 13, 14, 15 + Live Viewport Capture

BATCH D3: ANNOTATION & DIMENSION ENGINE
├── 1. Route aigrav_create_dimension in AIGRAVToolExecutor
├── 2. Implement doc.Create.NewDimension on stable geometric references
├── 3. Implement IndependentTag.Create for doors, windows, and rooms
└── 4. Implement standard 2.0mm printed TextNoteType generator
└── Verification: TC-DRW-06, 07 + Live Viewport Capture

BATCH D4: ASSEMBLY DETAILING & PDF EXPORT ENGINE
├── 1. Implement WallType.GetCompoundStructure layer interrogation
├── 2. Implement FilledRegion material hatch generator
├── 3. Load and place native OST_DetailComponents (insulation, studs, ties)
├── 4. Implement Revit 2026 doc.Export with native PDFExportOptions
└── 5. Implement headless PDF inspection and text readability audit
└── Verification: TC-DRW-01, 02, 03, 04, 05, 24, 25 + Final PDF Inspection
```

### Live Capture Requirement for Every Batch:
Every batch execution MUST generate high-resolution viewport captures (`aigrav_capture_revit_viewport`) and return quantitative spatial telemetry before human-in-the-loop sign-off.

# AIGRAV — DRAWING QA ACCEPTANCE MATRIX
**Document ID:** AIGRAV-GOV-DD0-06  
**Status:** DRAFT ACCEPTANCE TEST SUITE — SUBJECT TO FORMAL APPROVAL  
**Date:** 2026-11-12  
**Target Platform:** Autodesk Revit 2026 / .NET 10 / C# Revit Bridge / FastMCP Python  
**Location:** `C:\AIGRAV\_governance_review\drawing_documentation\DRAWING_QA_ACCEPTANCE_MATRIX.md`

---

## 1. Quality Assurance Framework & Result States

Every automated drawing production run in AIGRAV must pass an objective, deterministic test suite before being marked publication-ready.

### Deterministic Result States:
- `PASS`: The test criterion was fully satisfied within defined numerical tolerances.
- `FAIL`: The test criterion was violated; execution must roll back or trigger compensating layout logic.
- `NOT_APPLICABLE`: The test does not apply to the active drawing type (e.g., rebar tests on architectural plans).

---

## 2. Master Test Matrix (TC-DRW-01 through TC-DRW-25)

| Test ID | Category | Objective / Capability | Test Model & Conditions | Execution Step | Expected Telemetry / Pass Criteria | Result States |
|---|---|---|---|---|---|---|
| **TC-DRW-01** | **Detail Quality** | Cut vs projection lineweight contrast | Wall-foundation detail at 1:20 | Inspect lineweight table in generated view | Cut structural concrete = Pen 7 ($0.70\text{ mm}$); projection = Pen 2 ($0.13\text{ mm}$); contrast $\ge 0.50\text{ mm}$. | `PASS` / `FAIL` |
| **TC-DRW-02** | **Detail Quality** | Material hatching via `FilledRegion` | Brick wall section | Verify `FilledRegion` instances in view | At least 1 `FilledRegion` with `Brick / Masonry` drafting pattern; zero raw line infills. | `PASS` / `FAIL` |
| **TC-DRW-03** | **Detail Quality** | Native Detail Components | Wall footing detail | Check for `OST_DetailComponents` | Native family loaded (e.g., `Reinforcing Steel` or `Anchor Bolt`); no raw CAD DWG import. | `PASS` / `FAIL` / `N_A` |
| **TC-DRW-04** | **Detail Quality** | Compound assembly interrogation | Multi-layer wall (bata + plester) | Extract layers via `GetCompoundStructure()` | Number of 2D detail layers matches 3D wall compound layer count ($N \ge 3$). | `PASS` / `FAIL` |
| **TC-DRW-05** | **Detail Quality** | Moisture barrier presence | Roof eave / foundation plinth | Query line style `Waterproofing_Membrane` | Continuous membrane polyline present at exterior masonry/concrete junction. | `PASS` / `FAIL` |
| **TC-DRW-06** | **Dimensions** | Dimension geometric reference integrity | Exterior wall grid dimension string | Call `doc.Create.NewDimension` on wall faces | Dimension element created with valid non-null `ReferenceArray`; references real geometry faces. | `PASS` / `FAIL` |
| **TC-DRW-07** | **Dimensions** | Dimension text printed height | Floor plan at 1:100 | Measure `DimensionType.TEXT_SIZE` | Printed text size equals standard $2.0\text{ mm} \pm 0.2\text{ mm}$ in paper space. | `PASS` / `FAIL` |
| **TC-DRW-08** | **Adaptive Scale** | Scale calculation accuracy | Model bounds $6.0 \times 6.0\text{ m}$; Canvas $250 \times 180\text{ mm}$ | Run `CalculateAdaptiveScale` | Snaps mathematically to $1:50$ (calculated $40.0 \rightarrow$ snaps to $50$). | `PASS` / `FAIL` |
| **TC-DRW-09** | **Adaptive Scale** | Relayout trigger upon scale change | Modify view scale $1:100 \rightarrow 1:50$ | Observe viewport box outline | Viewport outline expands by factor $2.0\text{x}$; layout re-checks sheet containment. | `PASS` / `FAIL` |
| **TC-DRW-10** | **View Range** | Architectural floor plan cut plane | Residential Level 1 plan | Query `PlanViewRange` parameters | Cut plane $= +1200\text{ mm} \pm 50\text{ mm}$; Top $= +2300\text{ mm}$; Bottom $= 0\text{ mm}$. | `PASS` / `FAIL` |
| **TC-DRW-11** | **Far Clipping** | Elevation far clipping activation | Front facade elevation | Inspect `VIEW_FAR_CLIP_ACTIVE_PARAM` | Far clip is active (`= 1`); offset $\le \text{BuildingDepth} + 3000\text{ mm}$. | `PASS` / `FAIL` |
| **TC-DRW-12** | **View Crop** | Model crop active and invisible | All placed sheet views | Query `CropBoxActive` and `CropBoxVisible` | `CropBoxActive == true` AND `CropBoxVisible == false`. | `PASS` / `FAIL` |
| **TC-DRW-13** | **View Crop** | Annotation crop activation | Floor plan with distant grids | Query `VIEWER_ANNOTATION_CROP_ACTIVE` | Parameter `= 1`; grid heads terminate within $15\text{ mm}$ of model crop boundary. | `PASS` / `FAIL` |
| **TC-DRW-14** | **Annotation Filter**| Semantic section marker visibility | Floor plan with 1 placed section and 1 working section | Query section marker visibility | Placed section marker remains VISIBLE; unplaced working section marker is HIDDEN. | `PASS` / `FAIL` |
| **TC-DRW-15** | **Annotation Filter**| Anti-blanket category hiding | Floor plan verification | Check category visibility of `OST_Sections` | Category `OST_Sections` is NOT globally hidden; specific irrelevant instances hidden individually. | `PASS` / `FAIL` |
| **TC-DRW-16** | **Sheet Containment**| Viewport boundary within titleblock canvas | Viewport placed on A3 sheet | Compare `UnifiedEnvelope` vs `drawableRect` | `drawableRect.Contains(UnifiedEnvelope) == true`; clearance to titleblock border $\ge 10\text{ mm}$. | `PASS` / `FAIL` |
| **TC-DRW-17** | **Sheet Collision** | Zero viewport-to-viewport collision | Sheet with 3 viewports | Run `PaperRectMm.Intersects` on all pairs | Intersection count $== 0$; minimum edge-to-edge separation $\ge 12\text{ mm}$. | `PASS` / `FAIL` |
| **TC-DRW-18** | **Sheet Collision** | Viewport title / label protection | Viewport placed above another viewport | Check `GetLabelOutline()` overlap | View title box does not intersect lower viewport geometry ($0\text{ mm}$ overlap). | `PASS` / `FAIL` |
| **TC-DRW-19** | **Schedules** | Schedule sheet instance creation | Door schedule on sheet | Call `ScheduleSheetInstance.Create` | Element of class `ScheduleSheetInstance` created on target sheet without error. | `PASS` / `FAIL` |
| **TC-DRW-20** | **Schedules** | Schedule placement within Zone 4 | Sheet with schedule | Inspect schedule bounding box | Schedule placed in top-right quadrant abutting titleblock stamp; zero viewport collision. | `PASS` / `FAIL` |
| **TC-DRW-21** | **Multi-Sheet** | Automated multi-sheet splitting | Overflow drawing package on A3 | Execute layout engine | Spawns Sheet 2 (`A-102`) cleanly; moves details/schedules to Sheet 2; zero overflows. | `PASS` / `FAIL` |
| **TC-DRW-22** | **Design Integrity**| Non-fabrication invariant | Structural detail without rebar brief | Interrogate generated annotation text | Emits `[DESIGN_INPUT_REQUIRED: Structural Rebar Specs Needed]`; zero fabricated rebar. | `PASS` / `FAIL` |
| **TC-DRW-23** | **User Protection** | Immutable user-authored views | Document with user view `ORIGINAL_PLAN` | Run drawing generation suite | User view ID, name, scale, and visibility overrides remain 100% unchanged. | `PASS` / `FAIL` |
| **TC-DRW-24** | **PDF Export** | Vector PDF export execution | Completed sheet | Call `doc.Export` with `PDFExportOptions` | Valid vector PDF file generated on disk; file size $> 50\text{ KB}$; zero raster pixelation. | `PASS` / `FAIL` |
| **TC-DRW-25** | **PDF Legibility** | PDF text readability inspection | Exported PDF document | Run headless text density / bounding audit | Text annotations legible at 100% zoom; no overlapping text blocks detected. | `PASS` / `FAIL` |

---

## 3. Failure Isolation & Compensating Action Protocol

Whenever a test fails during automated execution:
1. **Layout Collisions (TC-DRW-16, 17, 18):**
   - Step 1: Attempt compaction using smaller padding ($12\text{ mm} \rightarrow 8\text{ mm}$).
   - Step 2: Attempt scale reduction for details ($1:10 \rightarrow 1:20$).
   - Step 3: Trigger automated multi-sheet partition (TC-DRW-21).
2. **Dimension Reference Invalidation (TC-DRW-06):**
   - If a model face reference cannot be resolved, fallback to model analytical curves or grid line references. Under no circumstance place floating unattached dimension text.
3. **Engineering Fabrication Violation (TC-DRW-22):**
   - Immediate execution HALT on that component; revert to `DESIGN_INPUT_REQUIRED` callout box and notify user.

# AIGRAV — VIEW SCALE & VISIBILITY POLICY
**Document ID:** AIGRAV-GOV-DD0-04  
**Status:** DRAFT GOVERNANCE POLICY — SUBJECT TO FORMAL APPROVAL  
**Date:** 2026-11-12  
**Target Platform:** Autodesk Revit 2026 / .NET 10 / C# Revit Bridge / FastMCP Python  
**Location:** `C:\AIGRAV\_governance_review\drawing_documentation\VIEW_SCALE_AND_VISIBILITY_POLICY.md`

---

## 1. Introduction & Problem Statement

Current drawing automation in AIGRAV exhibits two crippling visibility and scaling failures:
1. **Blind Scale Selection:** Scales are either hardcoded (e.g., $1:100$ in `HouseType36Builder.cs` or $1:20$ in `CadDetailImporter.cs`) or manually pushed via `view.Scale = scale` without verifying whether the resulting viewport fits onto the sheet or leaves sufficient margin for annotations and dimensions.
2. **Brute-Force Annotation Hiding:** When section or elevation markers clutter a plan, the existing code executes crude global overrides such as `plan.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Sections), true)`. This globally obliterates all section callouts from the drawing, destroying cross-referencing and violating basic drafting standards.

This policy defines a deterministic algorithm for adaptive scale calculation and establishes a semantic visibility framework for reference annotations, view range, far clipping, and crop regions.

---

## 2. Deterministic Adaptive Scale Selection Algorithm

Scale selection is a purely mathematical constraint satisfaction problem based on the physical size of the building element in model space and the usable canvas area allocated on the drawing sheet.

### Mathematical Formulation:
Let:
- $W_{\text{model}}$ and $H_{\text{model}}$ be the bounding dimensions (in millimeters) of the model elements visible within the view's crop box.
- $W_{\text{canvas}}$ and $H_{\text{canvas}}$ be the available millimetric dimensions of the partitioned sheet zone assigned to this view.
- $\alpha_{\text{margin}}$ be the annotation reserve factor ($0.20 \le \alpha_{\text{margin}} \le 0.35$), reserving $20\%\text{ to }35\%$ of the canvas area for dimension strings, room tags, keynote leaders, and the viewport title bar.

The effective available canvas is:
$$W_{\text{eff}} = W_{\text{canvas}} \times (1 - \alpha_{\text{margin}}), \quad H_{\text{eff}} = H_{\text{canvas}} \times (1 - \alpha_{\text{margin}})$$

The minimum reduction scale ratio $S_{\text{req}}$ is:
$$S_{\text{req}} = \max\left(\frac{W_{\text{model}}}{W_{\text{eff}}}, \frac{H_{\text{model}}}{H_{\text{eff}}}\right)$$

### Standard Architectural Scale Snapping Table:
The calculated scale ratio $S_{\text{req}}$ must be snapped upwards to the nearest standardized architectural/engineering scale $S_{\text{std}}$ from the approved series:

$$\mathbb{S} = \{1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000\}$$

$$S_{\text{chosen}} = \min \{ s \in \mathbb{S} \mid s \ge S_{\text{req}} \}$$

```csharp
public static int CalculateAdaptiveScale(double modelWidthMm, double modelHeightMm, 
                                        double canvasWidthMm, double canvasHeightMm, 
                                        double marginFactor = 0.25)
{
    int[] standardScales = { 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000 };
    double effWidth = canvasWidthMm * (1.0 - marginFactor);
    double effHeight = canvasHeightMm * (1.0 - marginFactor);
    
    double reqScale = Math.Max(modelWidthMm / effWidth, modelHeightMm / effHeight);
    
    foreach (int scale in standardScales)
    {
        if (scale >= reqScale) return scale;
    }
    return 1000; // Fallback for massive site plans
}
```

### Relayout Trigger Lifecycle:
Whenever a view's scale is modified via `view.Scale = S_chosen`:
1. The viewport's physical paper footprint changes proportionally:
   $$\text{ViewportSize}_{\text{paper}} \propto \frac{\text{ModelSize}}{S_{\text{chosen}}}$$
2. The sheet composer MUST immediately trigger a layout recalculation.
3. It is strictly forbidden to change a view's scale without recalculating sheet collision and margin containment.

---

## 3. View Range & Cut Plane Management (`PlanViewRange`)

For plan views (Floor Plans, Reflected Ceiling Plans, Foundation Plans), the Revit `PlanViewRange` must be explicitly configured according to the drawing's architectural discipline:

### Discipline View Range Standards

| Plan Type | Top Plane Offset | Cut Plane Offset | Bottom Plane Offset | View Depth Offset | Purpose |
|---|---|---|---|---|---|
| **Floor Plan (Architectural)** | $+2300\text{ mm}$ | $+1200\text{ mm}$ | $0\text{ mm}$ (Level) | $0\text{ mm}$ (Level) | Slices windows and doors; excludes ceiling |
| **Reflected Ceiling Plan (RCP)** | $+2300\text{ mm}$ | $+2100\text{ mm}$ | $0\text{ mm}$ (Look Up) | $+3000\text{ mm}$ | Inspects lighting fixtures and diffusers looking up |
| **Foundation / Sloof Plan** | $+600\text{ mm}$ | $+300\text{ mm}$ | $-1500\text{ mm}$ | $-2000\text{ mm}$ | Slices plinth/sloof and views underground footings |
| **Roof / Site Plan** | $+5000\text{ mm}$ | $+4000\text{ mm}$ | $0\text{ mm}$ | $0\text{ mm}$ | Captures entire roof pitch, ridges, and gutters |

---

## 4. Far Clipping & View Depth Policy

In section and elevation views, failing to activate far clipping causes objects located hundreds of meters behind the target facade (interior furniture, distant terrain, back walls) to be drawn, creating an unreadable web of overlapping lines.

### Elevation & Section Far Clipping Rules:
1. **Elevations:**
   - Far clipping MUST be enabled:
     ```csharp
     Parameter farClipParam = view.get_Parameter(BuiltInParameter.VIEW_FAR_CLIP_ACTIVE_PARAM);
     farClipParam?.Set(1); // Enable Far Clip
     Parameter farClipOffset = view.get_Parameter(BuiltInParameter.VIEW_FAR_CLIP_OFFSET_PARAM);
     // Set depth equal to building facade depth plus 2000 mm clearance
     farClipOffset?.Set(buildingDepthFt + (2000.0 / 304.8));
     ```
2. **Building Sections:**
   - Far clipping MUST terminate just beyond the opposite exterior wall or interior partition of interest.
   - Far clip depth must NOT extend across the entire building unless an "unclipped architectural cross-section" is explicitly requested.

---

## 5. Crop Regions & Annotation Crop Mandate

Every placed view on a drawing sheet MUST have active crop boundary management:

1. **Model Crop Box:**
   - `view.CropBoxActive = true` (enforces model boundary).
   - `view.CropBoxVisible = false` (the black rectangular boundary box must NOT print on the final sheet).
2. **Annotation Crop (`AnnotationCropActive = true`):**
   - Revit allows annotation markers (grids, levels, section lines) to extend beyond the model crop box unless Annotation Crop is explicitly engaged.
   - For every view placed on a sheet, the engine MUST set:
     ```csharp
     Parameter annCropParam = view.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
     annCropParam?.Set(1);
     ```
   - This ensures that grid lines and section markers are clipped cleanly at the designated annotation boundary margin, eliminating horizontal and vertical spillover onto adjacent viewports.

---

## 6. Semantic Reference Annotation Visibility Policy

Instead of blindly hiding entire categories (`OST_Sections`, `OST_Elevations`, `OST_Grids`), the system must employ a **Semantic Annotation Filter**:

### The Hiding Rules:
1. **Discipline Alignment:**
   - In Architectural Floor Plans: Hide MEP section markers and Structural rebar callouts.
   - In Structural Framing Plans: Hide interior architectural detail callouts.
2. **Sheet Reference Relevance:**
   - An architectural section marker should remain VISIBLE on a floor plan if and only if that section view is placed on the drawing set and provides a valid sheet reference callout (e.g., `1 / A-102`).
   - If a section view is an internal working view (`IsWorkingView = true`) and is NOT placed on any sheet, its marker MUST be hidden on public presentation views:
     ```csharp
     if (sectionView.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER)?.AsString() == null)
     {
         // Working view not placed on any sheet: hide its marker in presentation view
         view.HideElements(new List<ElementId> { markerElementId });
     }
     ```
3. **Orthogonal Relevance in Sections/Elevations:**
   - In a Front Elevation view, longitudinal section markers running parallel to the line of sight must be hidden; only perpendicular cross-reference markers should be displayed.
4. **Prohibition Against Global Category Hiding:**
   - `view.SetCategoryHidden(OST_Sections, true)` is strictly banned on primary floor plans and building sections, as it destroys the sheet cross-referencing system required by professional building codes.

---

## 7. View Template Standardization & Protection

1. **Standard AI View Templates:**
   - AIGRAV must maintain standardized, version-controlled View Templates:
     * `AIGRAV_Arch_FloorPlan_1-100`
     * `AIGRAV_Arch_Elevation_1-100`
     * `AIGRAV_Arch_Section_1-100`
     * `AIGRAV_Arch_Detail_1-20`
     * `AIGRAV_Struct_Framing_1-100`
     * `AIGRAV_Struct_Detail_1-20`
2. **Protection of User Templates:**
   - If an existing project already uses custom office view templates (e.g., `STD_ARCH_PLAN`), AIGRAV must respect and assign that template rather than forcing its own.
   - AIGRAV is strictly forbidden from editing the visibility/graphics overrides of user-owned view templates.

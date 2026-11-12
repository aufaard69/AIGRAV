# AIGRAV — DETAIL DRAWING GRAPHIC STANDARDS
**Document ID:** AIGRAV-GOV-DD0-03  
**Status:** DRAFT GOVERNANCE STANDARD — SUBJECT TO FORMAL APPROVAL  
**Date:** 2026-11-12  
**Target Platform:** Autodesk Revit 2026 / .NET 10 / C# Revit Bridge / FastMCP Python  
**Location:** `C:\AIGRAV\_governance_review\drawing_documentation\DETAIL_DRAWING_GRAPHIC_STANDARDS.md`

---

## 1. Introduction & Graphic Philosophy

Professional architectural and engineering detail drawings communicate construction reality through an established visual grammar: contrast between cut elements and projected background elements, precise material hatching, and standardized component profiles.

The observed failure in current AIGRAV drawings—where details appear as "disconnected, rigid straight lines"—stems directly from treating detail graphics as raw 2D line primitives (`DetailCurve`) without pen hierarchy, material hatching, or native Revit detail families.

This standard establishes the mandatory graphic rules, pen weight hierarchy, hatch pattern mapping, and native component integration for all automated detail generation.

---

## 2. Lineweight Hierarchy & Pen Weights (ISO 128 & SNI Standard)

Detail views must establish immediate visual depth by separating **Cut Elements** (elements physically sliced by the detail cut plane) from **Projection Elements** (elements visible in the background beyond the cut plane).

### Standard Revit Lineweight Table (Metric mm at Printed Scale)

| Pen # | Usage Classification | Printed Width (mm) at 1:10 / 1:20 | Printed Width (mm) at 1:50 / 1:100 | Examples |
|---|---|---|---|---|
| **Pen 1** | Hairline / Hatching | 0.08 mm | 0.08 mm | Material hatch lines, insulation stipples, dimension extension lines |
| **Pen 2** | Thin Projection | 0.13 mm | 0.13 mm | Secondary projection lines, door swings, floor tile joints |
| **Pen 3** | Medium Projection | 0.18 mm | 0.18 mm | Main building projection edges, window frame outlines, furniture |
| **Pen 4** | Heavy Projection / Hidden | 0.25 mm | 0.25 mm | Hidden structural lines, slab edges viewed from above |
| **Pen 5** | Light Cut | 0.35 mm | 0.35 mm | Cut finishes (plaster, gypsum board, thermal insulation boundary) |
| **Pen 6** | Medium Cut | 0.50 mm | 0.50 mm | Cut masonry walls, lightweight steel framing, timber studs |
| **Pen 7** | Heavy Cut / Structural Cut | 0.70 mm | 0.70 mm | Cut reinforced concrete (columns, beams, slabs, foundations), heavy structural steel |
| **Pen 8** | Extra Heavy Boundary | 1.00 mm | 1.00 mm | Sheet border lines, ground boundary line (*earth line / grade line*) |

### Mandatory Drafting Invariants:
1. **The Ground Line Invariant:** The exterior ground plane in foundation and wall section details must ALWAYS be drawn with Pen 8 ($1.00\text{ mm}$) to anchor the building visually to the earth.
2. **Cut vs Projection Contrast:** The primary cut boundary must be at least two pen weights heavier than interior material hatch lines (e.g., concrete boundary at Pen 7 $0.70\text{ mm}$ vs concrete gravel hatch at Pen 1 $0.08\text{ mm}$).

---

## 3. Material Hatching & `FilledRegion` Specifications

Material boundaries in cut details must NEVER be left empty or filled with arbitrary CAD linework. They must use native Revit `FilledRegion` instances bound to standard `FillPatternElement` patterns:

### Material Hatching Standard Mapping

| Material Class | Revit Fill Pattern Name | Hatch Type | Pattern Geometry | Boundary Pen | Fill Color |
|---|---|---|---|---|---|
| **Cast-in-Place Concrete** | `Concrete` | Drafting | Random triangles and stipple dots | Pen 7 (0.70 mm) | Black (`RGB 0,0,0`) |
| **Concrete Masonry / Bata Merah** | `Brick / Masonry` | Drafting | Parallel $45^\circ$ diagonal lines at 2.5 mm spacing | Pen 6 (0.50 mm) | Black (`RGB 0,0,0`) |
| **Plaster / Acian** | `Sand / Plaster` | Drafting | Fine random stipple dots | Pen 5 (0.35 mm) | 50% Gray (`RGB 128,128,128`) |
| **Natural Earth / Tanah Asli** | `Earth` | Drafting | Groups of 3 horizontal lines followed by 3 vertical lines | Pen 8 (1.00 mm) | Black (`RGB 0,0,0`) |
| **Compacted Sand / Pasir Urug** | `Sand - Dense` | Drafting | Dense uniform stipple dots | Pen 2 (0.13 mm) | Black (`RGB 0,0,0`) |
| **Crushed Stone / Gravel** | `Gravel` | Drafting | Irregular interlocking angular pebbles | Pen 6 (0.50 mm) | Black (`RGB 0,0,0`) |
| **Rigid Insulation (XPS/EPS)** | `Rigid Insulation` | Drafting | Cross-hatched $45^\circ$ diamond grid | Pen 5 (0.35 mm) | Black (`RGB 0,0,0`) |
| **Vapor Barrier / Membrane** | Solid Line (Pen 6) | Vector Line | Heavy continuous polyline ($0.50\text{ mm}$) | Pen 6 (0.50 mm) | Black (`RGB 0,0,0`) |

### Drafting Pattern vs Model Pattern Rule:
- Detail views (scales 1:5, 1:10, 1:20) MUST use **Drafting Patterns** (`FillPatternTarget.Drafting`), where line spacing is fixed relative to paper space (e.g., lines remain 2 mm apart on paper regardless of whether the view is printed at 1:10 or 1:20).
- Model Patterns (`FillPatternTarget.Model`) must NOT be used for 2D cut detail hatching.

---

## 4. Integration of Native Revit Detail Components (`OST_DetailComponents`)

Rather than redrawing standard construction parts from scratch using line primitives, the engine must leverage the **653 official Detail Component families** pre-installed in `C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Detail Items\`:

```
C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Detail Items\
├── Concrete\
│   └── Reinforcing Steel Sections, Expansion Joints, Waterstops
├── Masonry\
│   └── Brick Sections, CMU Sections, Mortar Joints, Wall Ties
├── Metals\
│   └── WF Sections, Hollow Structural Sections, Cold-Formed C-Studs, Anchor Bolts, Baseplates
├── Thermal and Moisture Protection\
│   └── Batt Insulation, Rigid Insulation, Flashing Profiles, Roof Drains, Sealants
├── Doors and Windows\
│   └── Aluminum Frame Extrusions, Wood Jambs, Sill Pans, Weatherstripping
└── Wood and Plastic\
    └── Dimensional Lumber (2x4, 2x6, 2x8), Plywood Subflooring, Blocking
```

### Component Placement Hierarchy:
1. **Primary Structural & Framing Items:** Loaded directly as parametric Detail Components (`doc.Create.NewFamilyInstance(point, familySymbol, view)`).
2. **Repetitive Linear Items (Insulation & Studs):** Use Revit native Repeating Detail Families (`OST_RepeatingDetailLines`) for continuous batt insulation and wall studs.
3. **Continuous Membranes & Sealants:** Use Detail Curves (`doc.Create.NewDetailCurve`) styled with dedicated LineStyles (e.g., `AIGRAV_Waterproofing_Membrane`).
4. **Material Infill Areas:** Use `FilledRegion.Create(doc, typeId, viewId, curveLoops)`.

---

## 5. Compound Assembly Interrogation Engine

To automatically produce intelligent cross-sections from existing 3D BIM elements (Walls, Floors, Roofs), the engine must interrogate their compound layer definitions via Revit API:

```csharp
// Programmatic Inspection of Compound Layers
WallType wallType = wall.WallType;
CompoundStructure cs = wallType.GetCompoundStructure();
if (cs != null)
{
    IList<CompoundStructureLayer> layers = cs.GetLayers();
    foreach (CompoundStructureLayer layer in layers)
    {
        MaterialFunctionAssignment func = layer.Function; // Structure, Substrate, Thermal/Air, Finish1, Finish2
        double widthFt = layer.Width;                      // Layer thickness in decimal feet
        ElementId matId = layer.MaterialId;                // Material reference
        // Map function and material to corresponding Detail Component or FilledRegion
    }
}
```

### Layer Translation Table for Automated 2D Detailing:

| Layer Function | Construction Layer | Detail Generation Mechanism | Graphic Representation |
|---|---|---|---|
| `MaterialFunctionAssignment.Structure` | Concrete Core / Brick Core | `FilledRegion` (Concrete or Brick pattern) | Heavy boundary (Pen 7), standard hatch |
| `MaterialFunctionAssignment.Substrate` | Plywood / Cement Board | Standard Detail Component or `FilledRegion` | Medium boundary (Pen 5), wood grain/cross |
| `MaterialFunctionAssignment.ThermalOrAir` | Mineral Wool / XPS Batt | Native Repeating Detail: `Batt Insulation` | Sine-wave loop batting |
| `MaterialFunctionAssignment.Membrane` | Damp-Proof Course (DPC) / Flashing | Heavy Polyline Curve with dedicated LineStyle | Pen 6 solid continuous line |
| `MaterialFunctionAssignment.Finish1` | Exterior Render / Cladding | Thin `FilledRegion` with stipple hatch | Pen 5 boundary |
| `MaterialFunctionAssignment.Finish2` | Interior Plaster / Paint | Thin line ($0.15\text{ mm}$) with plaster stipple | Pen 5 boundary |

---

## 6. Standard Construction Junction Blueprint (Pilot Case: Residential Type 36)

Every automated detail package for low-rise residential structures must include these 4 standardized junction details:

### 1. Foundation & Plinth Junction (Scale 1:20 / 1:10)
- **Components:** Strauss pile / batu kali foundation footing, Sloof ($150 \times 200\text{ mm}$), rabat beton ($50\text{ mm}$), pasir urug ($100\text{ mm}$), tanah asli, trasraam waterproofing layer ($150\text{ mm}$ above floor).
- **Key Details:** DPC bitumen membrane between sloof and brick wall to prevent rising dampness; exterior ground line with Pen 8.

### 2. Wall-to-Window Head & Sill Junction (Scale 1:10)
- **Components:** Cast concrete lintel beam (*balok latai*) above opening, aluminum / PVC frame profile, silicone sealant backer rod, sloped exterior sill (*plesteran miring* with drip edge/*tali air*), interior tile/timber sill board.

### 3. Wall-to-Roof Eave & Gutter Junction (Scale 1:20)
- **Components:** Ring balk ($150 \times 200\text{ mm}$), timber wall plate (*murplat*) or cold-formed steel top plate, anchor bolt, roof truss bottom chord, ceiling joist, gypsum ceiling ($9\text{ mm}$), fascia board (*lisplank*), metal gutter with bracket, flashing under corrugated roof sheet.

### 4. Wet Area Floor Drop & Waterproofing Junction (Scale 1:10)
- **Components:** Main floor slab, bathroom depressed slab ($-50\text{ mm}$ floor drop), cementitious liquid waterproofing upturn ($200\text{ mm}$ up wall), screed to falls ($1:50$), floor tile with waterproof grout, floor drain with puddle flange.

---

## 7. Visual Quality & Acceptance Criteria

A detail drawing is verified as **GRAPHICALLY ACCEPTABLE** if and only if:
1. **Zero Raw 1-Weight Linework:** The drawing contains distinct visual depth across Pens 1 through 8.
2. **Standard Material Hatching:** All cut solids have appropriate `FilledRegion` drafting patterns.
3. **Real Construction Layers:** Assemblies display true physical thickness (e.g., wall shows brick + plaster + skim coat, not a single hollow rectangle).
4. **Moisture Protection Explicit:** Waterproofing membranes, flashings, and drip edges are explicitly depicted at every exterior junction.
5. **Legible Annotation Clearances:** Leader lines for keynotes and dimensions maintain a minimum $5\text{ mm}$ printed clearance from component geometry.

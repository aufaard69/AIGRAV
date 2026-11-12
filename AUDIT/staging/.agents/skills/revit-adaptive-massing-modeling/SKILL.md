---
name: revit-adaptive-massing-modeling
description: >-
  Standard Operating Procedure and automation engine for Revit Adaptive Component Modeling
  and Conceptual Massing (with Model By Face integration). Serves as the official, professional
  BIM alternative to DirectShape for complex, curved, organic, panelized, and freeform geometries,
  ensuring full parameter editing, category classification, family identity, and QTO schedulability.
---

# Revit Adaptive Component & Conceptual Massing Skill

This skill provides the mandatory guidelines, technical architecture, and execution scripts for modeling non-standard, curved, panelized, organic, and complex geometrical elements in Autodesk Revit **without ever resorting to DirectShape**.

---

## 1. The BIM Imperative: Why DirectShape is Banned & Why Adaptive / Massing Wins

### The Failure of DirectShape in Professional BIM
In amateur automation scripts, `DirectShape.CreateElement` is often abused because it is easy to dump a raw boundary representation (B-Rep) solid into Revit. However, in production engineering:
1. **Dumb Solid (`TypeId == -1`)**: DirectShapes have no Family or Type. They cannot be swapped, typed, or inherited.
2. **Non-Hostable**: You cannot insert doors, windows, curtain systems, or rebar into a DirectShape.
3. **Non-Parametric**: Engineers cannot modify dimensions, control points, or thicknesses inside the Revit UI.
4. **Poor Section Graphics**: DirectShapes often fail to respect section cut styles, coarse/fine detail levels, and composite hatch patterns.
5. **QTO / Schedule Corruption**: They bypass compound layer material takeoff and standard quantity schedules.

### The Two Official BIM Alternatives
| Requirement | Solution 1: **Adaptive Components** | Solution 2: **Conceptual Massing + By-Face** |
| :--- | :--- | :--- |
| **Typical Use Case** | Panelized facades, space trusses, canopies, curved framing, complex brackets, stepped foundations, haunch cuts. | Freeform building envelopes, double-curved roofs, irregular tower massing, organic walls, mass floors. |
| **Revit Mechanism** | `AdaptiveComponentInstanceUtils` with $N$ placement `ReferencePoint`s. | `FormUtils` / `FreeFormElement` in Mass Family $\rightarrow$ `Wall.Create`, `Roof.Create`, `CurtainSystem.Create` By-Face. |
| **Parameter Control** | Full instance & type parameters (material, profile, offset). | Native Revit System Families applied to mass faces. |
| **Editability in Revit** | Control points can be dragged or driven by coordinates. | Mass can be edited; system families update via "Update to Face". |
| **BIM Level** | LOD 300 - 400 (Fabrication / Detailed Component). | LOD 200 - 350 (Architectural & Structural Envelope). |

---

## 2. Adaptive Component Modeling Protocol

### Core Architecture
Adaptive components rely on loadable families created with the **Curtain Panel by Pattern** or **Generic Model Adaptive** templates.
Each instance is placed by specifying an ordered list of 3D control points ($P_1, P_2, \dots, P_n$).

```
[Target 3D Coordinates (mm)]
       │
       ▼ (Unit Conversion: mm -> Feet)
[XYZ Coordinates in Revit Space]
       │
       ▼ AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance()
[FamilyInstance Created]
       │
       ▼ GetInstancePlacementPointElementRefIds()
[List of ReferencePoint ElementIds]
       │
       ▼ pt.Position = new XYZ(x, y, z)
[Adaptive Points Locked to Target Geometry]
```

### Standard Control Point Topologies
1. **2-Point Adaptive (Linear / Structural)**:
   - Point 1: Start node (e.g. column head / connection anchor).
   - Point 2: End node (e.g. rafter joint / tie rod end).
   - Application: Tapered haunches, sagging cables, angled struts, customized purlin cleats.
2. **3-Point Adaptive (Triangular Panel / Arch)**:
   - Points 1, 2, 3: Triangle vertices or 3-point circular arc control.
   - Application: Corner canopies, triangulated geodesic domes, gusset plates.
3. **4-Point Adaptive (Quadrilateral Panel / Ramp Slab)**:
   - Points 1, 2, 3, 4: Quad vertices in cyclic order (counter-clockwise).
   - Application: Warped curtain wall glass panels, curved retaining wall segments, irregular slab in-fills.

### Execution Script (Python / Revit Bridge)
```python
import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitServices')
from Autodesk.Revit.DB import XYZ, ElementId, Transaction
from Autodesk.Revit.DB import AdaptiveComponentInstanceUtils, ReferencePoint

def place_adaptive_component(doc, symbol, points_mm):
    """
    symbol: FamilySymbol (Adaptive)
    points_mm: List of tuples [(x, y, z), ...] in millimeters
    """
    if not symbol.IsActive:
        symbol.Activate()
        doc.Regenerate()

    # 1. Create instance
    instance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, symbol)
    
    # 2. Get placement point IDs
    point_ids = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(instance)
    
    if len(point_ids) != len(points_mm):
        raise ValueError(f"Symbol expects {len(point_ids)} points, but {len(points_mm)} were provided.")

    # 3. Position each point (Convert mm to feet: 1 ft = 304.8 mm)
    MM_TO_FT = 1.0 / 304.8
    for i, pt_id in enumerate(point_ids):
        ref_pt = doc.GetElement(pt_id)
        if isinstance(ref_pt, ReferencePoint):
            x, y, z = points_mm[i]
            ref_pt.Position = XYZ(x * MM_TO_FT, y * MM_TO_FT, z * MM_TO_FT)
            
    doc.Regenerate()
    return instance
```

---

## 3. Conceptual Massing & "Model By Face" Protocol

When geometry represents an entire building skin, a complex curved roof, or non-vertical walls:

### Step 1: Mass Form Creation
1. Generate the conceptual mass either via an In-Place Mass family (`OST_Mass`) or a loaded `.rfa` mass family.
2. Build 3D reference curves / splines defining profile slices.
3. Call `FormUtils.CreateForm` or `doc.FamilyCreate.NewLoftForm` to create the solid/void loft.

### Step 2: "Model By Face" Conversion to Native BIM
Once the mass form is committed in the document:
1. **Curtain Wall by Face**:
   ```csharp
   CurtainSystem.Create(doc, faceArray, curtainSystemTypeId);
   ```
2. **Wall by Face**:
   ```csharp
   FaceWall.Create(doc, wallTypeId, WallLocationLine.CoreCenterline, faceReference);
   ```
3. **Roof by Face**:
   ```csharp
   Roof.Create(doc, roofTypeId, faceArray);
   ```
4. **Mass Floors (Slabs)**:
   ```csharp
   MassInstanceUtils.AddMassLevelDataToMassInstance(doc, massInstanceId, levelId);
   ```

### Why this is 100% BIM Compliant:
- When the architect or engineer updates the conceptual mass, clicking **"Update to Face"** in Revit regenerates all composite walls, mullions, and roofs automatically.
- No `DirectShape` is created. All materials, U-values, thicknesses, and schedule parameters function perfectly.

---

## 4. Decision Matrix for AI Modeling

Whenever the user or agent needs to create an element in Revit:

```
Is the geometry standard rectilinear/planar?
 ├─► YES: Use Native System Families (Wall.Create, Floor.Create, Column, Beam).
 └─► NO (Curved, sloped, irregular, non-standard):
       │
       ├─► Is it a localized building component, panel, or bracket?
       │     └─► USE: Adaptive Component (2-pt, 3-pt, 4-pt).
       │
       └─► Is it a large building envelope, canopy skin, or faceted facade?
             └─► USE: Conceptual Massing -> By-Face Systems (FaceWall, Roof, CurtainSystem).
```

---

## 5. QA/QC & Validation Gate

Before marking any adaptive or massing task as complete:
1. **Family Symbol Verification**: Check that `AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(symbol)` is `True`.
2. **Point Position Audit**: Verify that all `ReferencePoint.Position` coordinates match target CAD/blueprint coordinates within $\pm 1\text{ mm}$.
3. **Host Attachment Check**: Verify that By-Face walls/roofs have valid face references and report non-zero area/volume in Revit schedules.
4. **Zero DirectShape Audit**: Confirm that `FilteredElementCollector(doc).OfClass(typeof(DirectShape))` contains no new entries generated for architectural or structural components.

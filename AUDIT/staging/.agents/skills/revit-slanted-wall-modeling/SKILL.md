---
name: revit-slanted-wall-modeling
description: >-
  Standard Operating Procedure and automation engine for Revit Slanted Wall Modeling
  (WallCrossSection.SingleSlanted) and Zero-Overlap Donut Slabs (CurveLoop Multi-Loop Openings).
  Eliminates stepped traps on obelisks, spires, and tapering monuments while enforcing zero
  Z-fighting, zero spatial clash, and mathematically pure Quantity Takeoff (QTO) calculation.
---

# Revit Slanted Wall & Zero-Overlap Slab Modeling Skill

This skill provides the mandatory guidelines, mathematical formulas, and automation scripts for modeling tapering towers, obelisks, angled chalices, and concentric stepped slabs in Autodesk Revit **with 100% Native BIM fidelity, zero stepped traps, zero overlapping floors, and zero DirectShape**.

---

## 1. The BIM Imperatives

### Imperative 1: Why Stepped Segment Walls Fail on Tapered Structures
In legacy workflows or amateur scripts, tapering structures (such as obelisks, church spires, minarets, or pylons) are often modeled by stacking multiple vertical wall segments on different levels.
This causes severe architectural defects:
1. **Stepped "Traps" & Unsightly Ledges**: Horizontal gaps and steps appear where the building profile should be continuous.
2. **Artificial Seams & Joints**: Horizontal lines divide what is physically a single continuous monolithic facade.
3. **Improper Floor Plan Cutting**: Section cuts at intermediate heights show jagged wall intersections rather than true continuous cross-sections.

**The Solution: Native Slanted Wall (`WallCrossSection.SingleSlanted`)**  
Modern Revit (2021+) supports native slanted basic walls. One single continuous wall can be drawn from base level to top level ($H = 98\text{ m}$ or more) and tilted at an exact angle $\theta$, creating a seamless monolithic facade with automatic mitered corner joins.

---

### Imperative 2: The Zero-Overlap Slab Mandate (Donut Boundary Loops)
Modeling concentric stepped terraces or plazas by stacking solid slabs directly on top of each other is a **critical BIM violation**:
1. **Quantity Takeoff (QTO) Corruption**: Stacking a $160 \times 160\text{ m}$ plaza on top of a solid $300 \times 300\text{ m}$ lawn causes the lawn area schedule to report $90,000\text{ m}^2$ instead of the true net lawn of $64,400\text{ m}^2$, causing huge over-ordering of material.
2. **Z-Fighting Graphic Artefacts**: Overlapping surfaces at the same elevation flicker with black and green artifacts during 3D rendering.
3. **Spatial Clashes**: Solid collision checkers flag interpenetrating floor volumes as clashes.

**The Solution: Multi-Loop `CurveLoop` (Donut Slabs)**  
In Revit API, `Floor.Create(doc, curveLoops, floorTypeId, levelId)` accepts a list of `CurveLoop` boundaries:
- **Loop 0 (Outer Boundary)**: Counter-Clockwise (CCW) loop defining the outer perimeter.
- **Loop 1..N (Inner Void Loops)**: Clockwise (CW) loops defining openings and cutouts.
Any inner plaza, podium structure, or elevator/stair shaft must be cut out cleanly from the surrounding slab.

---

## 2. Mathematical Foundation of Slanted Walls

### Inclination Angle Formula
For a wall of height $H$ whose top shifts laterally by $\Delta w$ relative to its base:

$$\theta = \arctan\left(\frac{\Delta w}{H}\right)$$

### Revit API Sign Convention (Orientation)
When a wall is drawn along a horizontal vector $\vec{V}$ in a **Counter-Clockwise (CCW)** loop around a building:
- The **exterior face** of the wall is to the **right** of the drawing direction (outward).
- The **interior face** is to the **left** (inward).

| Slant Direction | Parameter Value (`WALL_SINGLE_SLANT_ANGLE_FROM_VERTICAL`) | Practical Application |
| :--- | :--- | :--- |
| **Inward (Meramping ke Dalam)** | **Negative** ($-\theta$) | Obelisk shaft, tapering towers, spires, pyramids. |
| **Outward (Melebar ke Luar)** | **Positive** ($+\theta$) | Inverted chalices, splayed cawan/yoni, cantilevers, overhanging podiums. |

### Revit API Parameter Setup (C#)
```csharp
// 1. Create native basic wall
Wall wall = Wall.Create(doc, line, wallTypeId, levelId, heightFt, baseOffsetFt, false, false);

// 2. Set Cross-Section to SingleSlanted (enum value 0)
wall.CrossSection = WallCrossSection.SingleSlanted;
doc.Regenerate(); // Mandatory to instantiate slant angle parameter

// 3. Set Slant Angle from vertical (in radians)
Parameter pAngle = wall.get_Parameter(BuiltInParameter.WALL_SINGLE_SLANT_ANGLE_FROM_VERTICAL);
if (pAngle != null && !pAngle.IsReadOnly)
{
    pAngle.Set(slantAngleRad); // negative for inward, positive for outward
}
```

---

## 3. Donut Boundary Loop Implementation

### Helper Implementation (C#)
```csharp
public static CurveLoop CreateRectLoop(double halfW, double halfH, double zMeters, bool ccw = true)
{
    CurveLoop loop = new CurveLoop();
    XYZ p1 = Pt(-halfW, -halfH, zMeters);
    XYZ p2 = Pt(halfW, -halfH, zMeters);
    XYZ p3 = Pt(halfW, halfH, zMeters);
    XYZ p4 = Pt(-halfW, halfH, zMeters);

    if (ccw) // Outer Boundary: CCW
    {
        loop.Append(Line.CreateBound(p1, p2));
        loop.Append(Line.CreateBound(p2, p3));
        loop.Append(Line.CreateBound(p3, p4));
        loop.Append(Line.CreateBound(p4, p1));
    }
    else // Inner Void Opening: CW
    {
        loop.Append(Line.CreateBound(p1, p4));
        loop.Append(Line.CreateBound(p4, p3));
        loop.Append(Line.CreateBound(p3, p2));
        loop.Append(Line.CreateBound(p2, p1));
    }
    return loop;
}

// Creating a Donut Floor:
CurveLoop outer = CreateRectLoop(150.0, 150.0, 0.0, true);
CurveLoop inner = CreateRectLoop(80.0, 80.0, 0.0, false);
Floor floor = Floor.Create(doc, new List<CurveLoop> { outer, inner }, floorTypeId, levelId);
```

---

## 4. Reference Implementation: Monumen Nasional (Monas)

| Component | Dimensions / Elevation | Slant Angle | Floor Loop Configuration |
| :--- | :--- | :--- | :--- |
| **Rumput Lanskap** | $300 \times 300\text{ m}$ (Elev $0.00\text{ m}$) | - | Outer: $150\text{m}$, Inner Void: $80\text{m} \implies \text{Net } 64,400\text{ m}^2$ |
| **Plaza Andesit Luar** | $160 \times 160\text{ m}$ (Elev $+0.30\text{ m}$) | - | Outer: $80\text{m}$, Inner Void: $60\text{m} \implies \text{Net } 11,200\text{ m}^2$ |
| **Plaza Granit Menengah**| $120 \times 120\text{ m}$ (Elev $+0.65\text{ m}$) | - | Outer: $60\text{m}$, Inner Void: $50\text{m} \implies \text{Net } 4,400\text{ m}^2$ |
| **Platform Museum** | $100 \times 100\text{ m}$ (Elev $+1.00\text{ m}$) | - | Outer: $50\text{m}$, Inner Void: $40\text{m} \implies \text{Net } 3,600\text{ m}^2$ |
| **Teras Museum** | $80 \times 80\text{ m}$ (Elev $+5.00\text{ m}$) | - | Outer: $40\text{m}$, Inner Void: $12\text{m} \implies \text{Net } 5,824\text{ m}^2$ |
| **Cawan Splay (Yoni)**| $24\text{m} \to 45\text{m}$ ($+11\text{m} \to +17\text{m}$) | $\mathbf{+60.255^\circ}$ (Outward) | 4 Slanted Walls $H = 6\text{m}$ ($\Delta = 10.5\text{m}$) |
| **Pelataran Cawan Top**| $45 \times 45\text{ m}$ (Elev $+17.00\text{ m}$) | - | Outer: $22.5\text{m}$, Inner Void: $4\text{m} \implies \text{Net } 1,961\text{ m}^2$ |
| **Batang Obelisk** | $8\text{m} \to 3.6\text{m}$ ($+17\text{m} \to +115\text{m}$) | $\mathbf{-1.286^\circ}$ (Inward) | 4 Slanted Walls $H = 98\text{m}$ ($\Delta = 2.2\text{m}$) |
| **Viewing Deck Floor** | $11 \times 11\text{ m}$ (Elev $+115.00\text{ m}$) | - | Outer: $5.5\text{m}$, Inner Void: $1.8\text{m} \implies \text{Net } 108.04\text{ m}^2$ |

---

## 5. QA/QC & Validation Checklist
- [ ] No `DirectShape` elements used (`TypeId != -1`).
- [ ] Slanted walls use `WallCrossSection.SingleSlanted` with correct negative (inward) or positive (outward) angle.
- [ ] Slabs with interior structures have inner void opening loops (Zero Overlap).
- [ ] QTO schedule verified: Material net area equals outer boundary minus interior voids.
- [ ] 3D viewport capture confirms absence of Z-fighting visual flickering.

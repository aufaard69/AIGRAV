# AIGRAV — SHEET LAYOUT COMPOSITION POLICY
**Document ID:** AIGRAV-GOV-DD0-05  
**Status:** DRAFT GOVERNANCE POLICY — SUBJECT TO FORMAL APPROVAL  
**Date:** 2026-11-12  
**Target Platform:** Autodesk Revit 2026 / .NET 10 / C# Revit Bridge / FastMCP Python  
**Location:** `C:\AIGRAV\_governance_review\drawing_documentation\SHEET_LAYOUT_COMPOSITION_POLICY.md`

---

## 1. Introduction & Composition Goals

Sheet composition is the final synthesis of architectural and engineering documentation. A disorganized sheet—where viewports collide, schedules overlap drawing titles, and views are scattered randomly—destroys professional credibility regardless of the underlying BIM model's quality.

This policy defines the mathematical rules, dynamic canvas partitioning algorithms, collision detection envelopes, and schedule placement mechanisms that govern automated sheet composition in AIGRAV.

---

## 2. Dynamic Titleblock Geometry & Usable Canvas Extraction

Rather than assuming hardcoded sheet dimensions or fixed margins, the engine dynamically interrogates the Titleblock instance (`OST_TitleBlocks`) placed on the `ViewSheet`:

```
┌─────────────────────────────────────────────────────────── tbRect.MaxY
│ ┌───────────────────────────────────────┐ ┌─────────────┐ │
│ │                                       │ │ TITLEBLOCK  │ │
│ │                                       │ │ STAMP STRIP │ │
│ │          USABLE DRAWING               │ │ (PROJECT,   │ │
│ │              CANVAS                   │ │  SHEET NO,  │ │
│ │          (drawableRect)               │ │  REVISIONS, │ │
│ │                                       │ │  CONSULTANT)│ │
│ │                                       │ │             │ │
│ └───────────────────────────────────────┘ └─────────────┘ │
└─────────────────────────────────────────────────────────── tbRect.MinY
tbRect.MinX                                       tbRect.MaxX
```

### Canvas Extraction Algorithm:
1. **Titleblock Bounding Box:**
   ```csharp
   Element tb = new FilteredElementCollector(doc, sheet.Id)
       .OfCategory(BuiltInCategory.OST_TitleBlocks)
       .WhereElementIsNotElementType()
       .FirstOrDefault();
   
   BoundingBoxXYZ bb = tb.get_BoundingBox(sheet) ?? tb.get_BoundingBox(null);
   PaperRectMm tbRect = new()
   {
       MinX = FtToMm(bb.Min.X), MinY = FtToMm(bb.Min.Y),
       MaxX = FtToMm(bb.Max.X), MaxY = FtToMm(bb.Max.Y)
   };
   ```
2. **Titleblock Family Type Classification:**
   - **Type A (Right Vertical Column):** Standard European/American architectural titleblock (stamp column takes rightmost $12\%\text{ to }16\%$ of paper width).
   - **Type B (Bottom Horizontal Banner):** ISO civil/mechanical titleblock (stamp banner occupies bottom $45\text{ mm to }65\text{ mm}$ of paper height).
   - **Type C (Compact Corner Block):** Small $180 \times 65\text{ mm}$ corner stamp at bottom-right.
3. **Drawable Canvas Calculation (`drawableRect`):**
   - Left Binding Margin: $20.0\text{ mm}$ (reserves space for sheet binding/stapling).
   - Top Margin: $12.0\text{ mm}$.
   - Bottom Margin: $12.0\text{ mm}$ (or bottom stamp offset for Type B).
   - Right Margin: $12.0\text{ mm} + \text{StampWidth}$ (for Type A).

---

## 3. Viewport Boundary Envelopes & View Title Safety Margins

### The Root Cause of Viewport Title Collisions:
In Revit API, a `Viewport` has two separate bounding outlines:
1. `vp.GetBoxOutline()`: Bounding box of the actual model/drafting geometry visible through the crop region.
2. `vp.GetLabelOutline()`: Bounding box of the view title line, view name, scale text, and detail bubble hanging beneath the view.

**The Fatal Flaw in Existing Code:**  
Existing implementations (including `SheetManagerSkill.cs:553`) test collisions against `BoxOutline` *only*, ignoring `GetLabelOutline()`. Consequently, view titles routinely collide with the viewports positioned below them or overlap bottom sheet margins.

### The Unified Collision Envelope Mandate:
For every viewport, the engine MUST construct a **Unified Viewport Boundary Envelope** ($R_{\text{unified}}$):

$$R_{\text{unified}} = \text{Envelope}\Big(\text{BoxOutline} \cup \text{LabelOutline}\Big) \oplus \text{Padding}(12\text{ mm})$$

```csharp
public static PaperRectMm GetUnifiedViewportEnvelope(Viewport vp, double paddingMm = 12.0)
{
    Outline box = vp.GetBoxOutline();
    Outline lbl = vp.GetLabelOutline();
    
    double minX = Math.Min(FtToMm(box.MinimumPoint.X), FtToMm(lbl.MinimumPoint.X)) - paddingMm;
    double minY = Math.Min(FtToMm(box.MinimumPoint.Y), FtToMm(lbl.MinimumPoint.Y)) - paddingMm;
    double maxX = Math.Max(FtToMm(box.MaximumPoint.X), FtToMm(lbl.MaximumPoint.X)) + paddingMm;
    double maxY = Math.Max(FtToMm(box.MaximumPoint.Y), FtToMm(lbl.MaximumPoint.Y)) + paddingMm;
    
    return new PaperRectMm { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
}
```

---

## 4. Intelligent Canvas Partitioning Algorithm (2D Guillotine Bin-Packing)

Instead of testing a static list of 5 hardcoded candidate slots, the engine implements a **Zoned Guillotine 2D Partitioning Algorithm**:

### Architectural Drawing Hierarchy & Zoning:
The usable canvas is partitioned into 4 functional zones based on professional drafting conventions:

```
┌──────────────────────────────────────┬──────────────────────────────┐
│ ZONE 2: ELEVATIONS & SECTIONS        │ ZONE 4: SCHEDULES & LEGENDS  │
│ Front / Rear / Side Elevations       │ Door/Window Schedule, Notes  │
│ Building Cross-Sections              │ Keynotes, Abbreviation Table │
├──────────────────────────────────────┼──────────────────────────────┤
│ ZONE 1: PRIMARY PLAN VIEWS           │ ZONE 3: ENLARGED DETAILS     │
│ Ground Floor Plan                    │ Foundation, Wall, Eave, Sill │
│ Foundation / Sloof Plan              │ Details (1:20 / 1:10)        │
└──────────────────────────────────────┴──────────────────────────────┘
```

### Deterministic Placement Sequence:
1. **Sort Views by Importance & Area:**
   - Floor Plans $\rightarrow$ Elevations $\rightarrow$ Sections $\rightarrow$ Details $\rightarrow$ Schedules.
2. **Zone Assignment:**
   - Floor Plan placed first in Zone 1 (anchored to lower-left with $15\text{ mm}$ margin).
   - Elevations placed in Zone 2 (aligned horizontally or vertically with plans where feasible).
   - Sections placed in Zone 2 adjacent to elevations.
   - Enlarged Details packed into Zone 3 using best-fit decreasing guillotine packing.
   - Schedules placed in Zone 4 (top-right, directly abutting the titleblock stamp strip).
3. **Collision & Margin Verification:**
   - Every candidate placement must satisfy:
     $$\forall i \neq j, \quad R_{\text{unified}}(i) \cap R_{\text{unified}}(j) = \emptyset$$
     $$R_{\text{unified}}(i) \subset \text{drawableRect}$$

---

## 5. Schedule Sheet Placement Engine (`ScheduleSheetInstance`)

Placing schedules on sheets is completely absent from the current codebase. AIGRAV must implement a native schedule placement engine:

### Revit API Placement Routine:
```csharp
public static ScheduleSheetInstance PlaceScheduleOnSheet(Document doc, ViewSheet sheet, 
                                                         ViewSchedule schedule, XYZ paperPoint)
{
    // Verify schedule is not already on this sheet
    bool alreadyPlaced = new FilteredElementCollector(doc, sheet.Id)
        .OfClass(typeof(ScheduleSheetInstance))
        .Cast<ScheduleSheetInstance>()
        .Any(ssi => ssi.ScheduleId == schedule.Id);
        
    if (alreadyPlaced) throw new InvalidOperationException($"Schedule {schedule.Name} already placed on sheet {sheet.SheetNumber}");

    ScheduleSheetInstance instance = ScheduleSheetInstance.Create(doc, sheet.Id, schedule.Id, paperPoint);
    return instance;
}
```

### Schedule Height Measurement & Multi-Column Splitting:
1. A schedule's physical height on paper is proportional to its row count:
   $$H_{\text{sched}} \approx H_{\text{title}} + H_{\text{header}} + (\text{RowCount} \times H_{\text{row}})$$
2. If $H_{\text{sched}}$ exceeds available vertical canvas height in Zone 4, the engine must split the schedule into multiple adjacent segments:
   - Call `scheduleSheetInstance.Segment(segmentIndex)`.
   - Place segment 2 immediately to the left of segment 1 with a $15\text{ mm}$ horizontal gap.

---

## 6. Multi-Sheet Spilling & Escalation Policy

If the requested drawing package cannot fit onto a single sheet at acceptable standard scales without collision:

1. **Iteration 1 — Adaptive Scale Down:**
   - Test if reducing detail views from $1:10$ to $1:20$, or floor plan from $1:50$ to $1:100$, resolves the collision.
   - If minimum legible scale is reached (e.g., floor plan cannot be smaller than $1:100$ on A3), scale down is halted.
2. **Iteration 2 — Multi-Sheet Splitting (Automated Package Partitioning):**
   - Automatically spawn Sheet 2 in the project series:
     * Sheet 1 (`A-101`): Floor Plans & Elevations.
     * Sheet 2 (`A-102`): Building Sections, Construction Details & Schedules.
   - The engine logs the sheet split event with full telemetry:
     ```json
     {
       "action": "AUTOMATED_SHEET_SPLIT",
       "reason": "Drawable canvas capacity exceeded at scale 1:100",
       "sheets_created": ["A-101", "A-102"]
     }
     ```
3. **Escalation to User:**
   - If sheet size is locked by user (e.g., strictly A3 only) and sheet splitting is disallowed by project parameters, emit a `LAYOUT_OVERFLOW_BLOCKED` exception and request user direction.

---

## 7. Sheet Layout Quality Checkpoints

A sheet layout is signed off as **PASSED** if and only if:
- [ ] Zero collisions detected between all viewports and schedules ($0\text{ mm}$ overlap tolerance).
- [ ] Viewport titles fully contained within sheet margins (minimum $10\text{ mm}$ clearance from sheet border).
- [ ] Minimum $12\text{ mm}$ breathing room between adjacent viewports.
- [ ] Drawing hierarchy preserved (plans lower-left, schedules upper-right).
- [ ] Complete sheet telemetry recorded in JSON log.

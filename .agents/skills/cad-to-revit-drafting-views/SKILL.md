---
name: cad-to-revit-drafting-views
description: >-
  Standard Operating Procedure and automation engine for importing CAD details and layouts
  into Revit Drafting Views and placing them accurately onto Sheets matching the original
  CAD layout. Handles ModelSpace viewport extraction, DWG splitting, Black & White vector
  import, crop box sizing, title block coordinate transformation, and multi-viewport placement.
---

# CAD to Revit Drafting Views & Sheet Placement Skill

This skill provides the standard workflow, mathematical coordinate transformations, and C# / Python automation routines for extracting detail views from multi-layout CAD drawings (AutoCAD / ZWCAD) and faithfully reconstructing them as native Revit Drafting Views placed onto Revit Sheets.

---

## 1. High-Level Architecture & Workflow

```
[CAD Multi-Detail Drawing]
         │
         ├── 1. Viewport & Layout Inspection (PaperSpace Viewports & Center/Scale)
         ├── 2. ModelSpace Detail Extraction (WBLOCK / Selection to individual DWG)
         └── 3. PaperSpace Coordinate Mapping (Titleblock Anchor Points in mm)
                      │
                      ▼
[Clean Individual DWGs] (C:\AIGRAV\Temp\DWG_Views\*.dwg)
                      │
                      ▼
[Revit Bridge C# Engine] (BimAuditSkill.CadDetailImporter.cs)
         │
         ├── Create / Retrieve ViewDrafting with designated Scale
         ├── Import DWG (Unit: Millimeter, ColorMode: BlackAndWhite, Placement: Origin)
         ├── Configure Active CropBox:
         │     halfWidthFt  = (WidthMm  * 0.5 / 304.8) * Scale
         │     halfHeightFt = (HeightMm * 0.5 / 304.8) * Scale
         ├── Assign View Description ("Title on Sheet")
         ├── Titleblock BoundingBox Coordinate Mapping:
         │     xFt = tb.Min.X + (PaperXMm / 304.8)
         │     yFt = tb.Min.Y + (PaperYMm / 304.8)
         └── Viewport Placement on Target ViewSheet (with "No Title" type)
                      │
                      ▼
[Dual-Layer Visual & Geometric Verification] (ExportComparisonSheets vs CAD Plot)
```

---

## 2. Mathematical Principles & Coordinate Transformation

### A. ModelSpace to DWG Origin Alignment
When isolating a detail from ModelSpace into a standalone DWG, the center of the detail $(X_c, Y_c)$ should be mapped to the origin $(0, 0, 0)$ of the new DWG. This ensures that Revit's `ImportPlacement.Origin` places the drawing center exactly at $(0,0,0)$ in the `ViewDrafting`.

### B. Drafting View Crop Box Sizing
In Revit, Drafting Views use Imperial Feet ($1\text{ ft} = 304.8\text{ mm}$). For a detail with real-world dimensions $W_{\text{mm}} \times H_{\text{mm}}$ displayed at scale $1 : S$ (e.g. $S = 20$):
$$\text{halfWidthFt} = \left(\frac{W_{\text{mm}}}{2 \times 304.8}\right) \times S$$
$$\text{halfHeightFt} = \left(\frac{H_{\text{mm}}}{2 \times 304.8}\right) \times S$$
The `CropBox` bounding box is set to:
$$\text{Min} = (-\text{halfWidthFt}, -\text{halfHeightFt}, 0), \quad \text{Max} = (\text{halfWidthFt}, \text{halfHeightFt}, 0)$$

### C. PaperSpace to Revit Sheet Positioning
The Titleblock (`OST_TitleBlocks`) defines the origin on the sheet:
$$\text{Position}_X = \text{tb.Min.X} + \frac{\text{PaperX}_{\text{mm}}}{304.8}$$
$$\text{Position}_Y = \text{tb.Min.Y} + \frac{\text{PaperY}_{\text{mm}}}{304.8}$$
`centerPoint = new XYZ(Position_X, Position_Y, 0)`

---

## 3. Revit Bridge Tool Specification

### Tool Name: `aigrav_create_drafting_views_and_place`
Payload format (JSON):
```json
{
  "views": [
    {
      "sheet_number": "JL-048",
      "view_name": "DETAIL PONDASI - PETIR T1",
      "title_on_sheet": "DETAIL PONDASI",
      "scale": 20,
      "dwg_path": "C:\\AIGRAV\\Temp\\DWG_Views\\JL048_V1_DetailPondasi.dwg",
      "paper_x_mm": 84.9,
      "paper_y_mm": 144.9,
      "width_mm": 144.2,
      "height_mm": 258.2
    }
  ]
}
```

### Safety & Concurrency Rules:
1. **Single-Threaded Transactions**: Always wrap view creation and viewport placement in a Revit transaction with `ClashWarningSwallower` failure preprocessor.
2. **Duplicate Viewport Prevention**: Before creating a viewport, query all existing viewports in the document referencing `ViewDrafting.Id`. If a viewport exists on another sheet or is orphaned, delete it before adding to the target sheet.
3. **No-Title Viewport Type**: Change viewport type to "No Title" (or "Tanpa Judul") so that CAD PaperSpace titles do not conflict with Revit default viewport title blocks.

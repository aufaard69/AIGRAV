# AIGRAV — DRAWING PRODUCTION & DOCUMENTATION POLICY
**Document ID:** AIGRAV-GOV-DD0-02  
**Status:** DRAFT GOVERNANCE POLICY — SUBJECT TO FORMAL APPROVAL  
**Date:** 2026-11-12  
**Target Platform:** Autodesk Revit 2026 / .NET 10 / C# Revit Bridge / FastMCP Python  
**Location:** `C:\AIGRAV\_governance_review\drawing_documentation\DRAWING_PRODUCTION_AND_DOCUMENTATION_POLICY.md`

---

## 1. Purpose & Professional BIM Mission

The primary mission of the AIGRAV Drawing Production & Documentation Engine is to automatically generate professional, publication-ready architectural and engineering construction documentation in Autodesk Revit that:
1. Adheres strictly to established architectural and structural drafting standards (SNI, ISO 128, AIA CAD/BIM standards).
2. Produces fully editable, native Revit elements (Viewports, Drafting Views, Detail Components, Dimensions, TextNotes, Schedules) rather than "dumb" imported geometry or frozen raster blocks.
3. Enables downstream BIM Engineers, Architects, and Draftspersons to review, adjust, coordinate, and develop the documentation further without having to recreate drawing sheets from scratch.

---

## 2. Separation of Concerns Architecture

To eliminate the recurring flaw where model geometry is confused with 2D drafting graphics, the system enforces a strict 4-layer architectural hierarchy:

```
┌─────────────────────────────────────────────────────────────┐
│ LAYER 4: SHEET COMPOSITION & PRODUCTION                     │
│ ViewSheets, TitleBlocks, Viewports, ScheduleSheetInstances   │
└──────────────────────────────┬──────────────────────────────┘
                               │ references
┌──────────────────────────────▼──────────────────────────────┐
│ LAYER 3: VIEW-SPECIFIC ANNOTATION & DIMENSIONS              │
│ Dimensions, IndependentTags, TextNotes, Keynotes            │
└──────────────────────────────┬──────────────────────────────┘
                               │ annotates
┌──────────────────────────────▼──────────────────────────────┐
│ LAYER 2: 2D DETAIL GRAPHICS & DRAFTING OVERLAYS             │
│ DetailComponents, RepeatingDetails, FilledRegions, Masking │
└──────────────────────────────┬──────────────────────────────┘
                               │ interrogates / cuts
┌──────────────────────────────▼──────────────────────────────┐
│ LAYER 1: 3D MODEL GEOMETRY (THE BIM TRUTH)                  │
│ Walls, Floors, Roofs, Columns, Beams, Openings, Rebar       │
└─────────────────────────────────────────────────────────────┘
```

### Policy Rules:
1. **Model Geometry Integrity:** 3D model elements must NEVER be sliced, distorted, or modified purely to make a 2D drawing "look right."
2. **Detail Specificity:** 2D construction details (such as waterproofing membranes, flashing, acoustic sealants, rebar ties, and thermal breaks) belong in Layer 2 as view-specific `DetailComponents` (`OST_DetailComponents`), `FilledRegions`, or within a dedicated `ViewDrafting` / `ViewSection`, not as modeled 3D solids.
3. **Parametric Association:** Dimensions (Layer 3) must reference genuine geometric references (`Reference`) of model elements or detail components, ensuring that any subsequent design changes automatically reflect in the dimension values.

---

## 3. The Non-Fabrication Invariant (`DESIGN_INPUT_REQUIRED`)

AIGRAV acts as a professional BIM Engineer and Draftsperson. Under no circumstances may the system fabricate, guess, or extrapolate critical structural or MEP engineering information:

### Explicit Prohibition Against Engineering Invention:
1. **Structural Rebar & Reinforcement:** Do not invent bar diameters, spacing, hook lengths, lap splice lengths, or rebar grades (e.g., BjTS 420B vs BjTP 280) unless specified in the project engineering brief or validated calculation note.
2. **Structural Steel Connections:** Do not invent plate thicknesses, bolt grades (e.g., A325, Grade 8.8), bolt pitches, or weld sizes (e.g., 6 mm fillet weld).
3. **Concrete Specifications:** Do not assign structural concrete grades ($f'_c$) or exposure classes without design input.
4. **MEP Slopes & Equipment:** Do not fabricate sewer pipe gradients, drainage invert levels, duct friction losses, or circuit breaker ratings.

### Protocol for Missing Engineering Information:
When generating detail drawings where engineering details are incomplete or unspecified:
1. The engine MUST emit a standardized `DESIGN_INPUT_REQUIRED` annotation placeholder.
2. The drafting component must use a designated graphic callout:  
   `[DESIGN_INPUT_REQUIRED: Structural Engineer Confirmation Needed for Rebar Diam / Spacing]`.
3. The system log and execution JSON response must explicitly report:
   ```json
   {
     "status": "PARTIAL_DOCUMENTATION_GENERATED",
     "pending_design_inputs": [
       {
         "detail_id": "DET-WALL-FOOTING-01",
         "missing_parameter": "rebar_diameter_and_pitch",
         "action_required": "Provide structural calculation note or standard reinforcement table"
       }
     ]
   }
   ```

---

## 4. Protection of User-Authored Elements & Templates

In real-world project environments, projects already contain views, templates, sheets, and titleblocks authored by humans. AIGRAV must behave as a disciplined guest in the Revit document:

1. **AI-Ownership Identification:**
   - Every view, sheet, and schedule created by AIGRAV must follow strict prefix conventions:  
     * Sheets: `AIGRAV_<Discipline>_<Number>` (e.g., `AIGRAV_A-101`) or user-configured sheet numbering.
     * Views: `AIGRAV_<ViewName>` (e.g., `AIGRAV_Plan_Level_1`).
   - Alternatively, a shared/project parameter `AIGRAV_Managed = 1` must be applied.
2. **Immutable User Elements:**
   - AIGRAV is strictly prohibited from modifying, renaming, overriding, or deleting any view, sheet, or view template that is NOT flagged as AI-managed.
   - If a user-authored view template is assigned to an AI-managed view, AIGRAV must NOT alter the view template parameters globally. It must create a duplicate template: `AIGRAV_<OriginalTemplateName>`.
3. **Non-Destructive Sheet Operations:**
   - If a target sheet already contains user-placed viewports, AIGRAV must respect their bounding boxes as locked obstacles (`is_locked = true`) and place AI viewports only in remaining free canvas areas.

---

## 5. Single-Threaded Write Transaction & Error Handling Mandate

Revit API is strictly single-threaded for all document write operations:

1. **One Write Transaction Controller:**
   - Exactly one Revit Bridge executor handles write transactions (`Transaction`, `TransactionGroup`, `SubTransaction`).
   - Multiple agents or background threads may parse CAD files, calculate bin-packing coordinates, or perform OCR on exported PDFs in parallel, but must send serialized execution payloads to the single Revit Bridge.
2. **Zero Warning Swallowing:**
   - The use of blind warning swallowers (such as `ClashWarningSwallower` with empty handlers) is strictly prohibited.
   - All transaction warnings generated by Revit (e.g., "Highlighted elements overlap", "Line is slightly off axis", "Room tags outside room") must be harvested from the `FailuresAccessor`, logged, and classified into:
     * `IGNORABLE_COSMETIC` (logged for transparency).
     * `DOCUMENTATION_FAULT` (triggers automated compensating transaction or roll-back).
3. **Atomic Multi-View Placement:**
   - The placement of a complete sheet drawing package (views, viewports, titles, schedules) must be enclosed within an atomic `TransactionGroup`.
   - If a catastrophic collision or boundary overflow occurs that cannot be resolved within 2 automated layout iterations, the entire `TransactionGroup` must roll back cleanly, leaving the user's document in its original pristine state.

---

## 6. Policy Verification & Compliance Checkpoints

Before any drawing sheet is presented as "COMPLETE", it must satisfy:
- [ ] Layer separation verified (no 3D geometry mutilated for drafting).
- [ ] Zero unverified engineering assumptions (all unknowns marked `DESIGN_INPUT_REQUIRED`).
- [ ] User elements verified untampered.
- [ ] Clean transaction log with zero swallowed errors.
- [ ] Viewport boundaries and titles verified within titleblock drawable canvas.

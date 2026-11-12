---
name: revit-voute-connection-qto
description: >-
  Standard Operating Procedure and automation engine for Revit Voute & Steel Connection
  Profile Quantity Takeoff (QTO). Resolves the limitation where structural framing bundled
  inside custom steel connections cannot be scheduled via OST_StructuralFraming, by
  extracting them via native OST_StructConnectionProfiles with solid volume calibration
  (Exact Weight = Volume * 7850 kg/m3) and strict 7-column layout.
---

# Revit Voute & Steel Connection Profile QTO Skill

This skill provides the mandatory guidelines, technical architecture, and automation scripts for extracting, calibrating, and scheduling structural steel voutes (haunches) and connection profile cuts embedded within Autodesk Revit Custom Steel Connections.

---

## 1. Problem Statement & Root Cause

### The Revit Bundling Limitation
When structural framing (such as tapered haunches, split WF beams, or voute cuts) is bundled into a **Custom Steel Connection** (*User-Defined Connection / StructuralConnectionHandler*):
* Revit treats the embedded framing as an internal macro subelement.
* It does **NOT** expose the cut as an independent instance of `OST_StructuralFraming`.
* Consequently, standard Structural Framing schedules (`OST_StructuralFraming`) will return **empty** results when filtered for connection voutes.

### The Engineering Solution
Revit provides a dedicated native subelement category:
> **`BuiltInCategory.OST_StructConnectionProfiles` (Structural Connection Profiles)**

By targeting `OST_StructConnectionProfiles`, all embedded connection cuts are fully accessible, schedulable, and report accurate geometry.

---

## 2. Solid Volume Weight Calibration Protocol

### The Half-Cut / Tapered Geometry Principle
Voute beams are longitudinally or diagonally split/tapered from standard wide-flange beams (e.g. WF 300, WF 350, WF 400). Calculating weight using linear profile weight ($\text{Length} \times \text{Weight per Meter}$) causes **significant overestimation** (almost double the actual tonnage).

### Calibration Formula
Weight must be derived strictly from the **net solid volume**:

$$\text{Berat (Kg)} = \text{Volume (m}^3) \times \rho_{\text{steel}}$$

Where:
* $\rho_{\text{steel}} = 7.850\text{ kg/m}^3$ (Standar SNI & AISC).
* Parameter `Exact Weight` (`BuiltInParameter.STRUCTURAL_CONNECTION_EXACT_WEIGHT`) matches this exact volume-based weight.

---

## 3. Mandatory Column Layout & Standards

In strict accordance with `AGENTS.md` (Lokasi in Column A / Index 0):

### Layout: Exactly 7 Columns
$$\text{Lokasi} \mid \text{Tipe Sambungan} \mid \text{Type} \mid \text{Length (mm)} \mid \text{Volume} \mid \text{Qty} \mid \text{Berat (Kg)}$$

| Col Index | Field Name | Revit Parameter | Column Heading | Format / Visibility |
| :---: | :--- | :--- | :--- | :--- |
| **0** | `Lokasi` | Shared Parameter `Lokasi` | `Lokasi` | Visible (Unhidden) |
| **1** | `Type of Connection` | Built-in / Shared Parameter | `Tipe Sambungan` | Visible |
| **2** | `Profile Type` | Built-in Parameter | `Type` | Visible |
| **3** | `Cut Length` | Built-in Parameter | `Length (mm)` | Visible, Subtotal Footer |
| **4** | `Volume` | Built-in Parameter | `Volume` | Visible, Calculate Totals |
| **5** | `Count` | Built-in Field | `Qty` | Visible, Calculate Totals |
| **6** | `Exact Weight` | Built-in Parameter | `Berat (Kg)` | Visible, Calculate Totals |

### Sorting & Grouping Rules
1. **Sort 1**: `Profile Type` (Ascending)
2. **Sort 2**: `Cut Length` (Ascending, `ShowFooter = True` for subtotal per length)
3. **Itemize**: `IsItemized = False` (aggregates identical cuts into rows)
4. **Grand Totals**: `ShowGrandTotal = True`, `ShowGrandTotalTitle = True`, `ShowGrandTotalCount = True`

---

## 4. Family & Type Unification Standard

Custom connection profiles must belong to the standard Revit Advance Steel profile family:
* Family: **`AS_EXT_BEAM_I`**
* Standard Types:
  * `WF 250`
  * `WF 300` (Never name with temporary tags such as `WF 300 - KALIBRASI VOLUME`)
  * `WF 350`
  * `WF 400`
  * `WF 588`

---

## 5. Naming Convention & Matrix of Schedules

Location-specific schedules must follow:
`C.3.[Zone].[Index] [Zone Name] - Voute & CT [Profile]`

| Zone | Schedule Name | Location Filter | Connection Filter |
| :--- | :--- | :--- | :--- |
| **Waterline** | `C.3.1.22 Waterline - Voute & CT WF 350` | `Waterline` | `WF350-Endplate` |
| **Waterline** | `C.3.1.23 Waterline - Voute & CT WF 300` | `Waterline` | `WF300-Endplate` |
| **Waterline** | `C.3.1.24 Waterline - Voute & CT WF 200` | `Waterline` | `WF200-Endplate` |
| **Waterline** | `C.3.1.25 Waterline - Voute & CT WF 400` | `Waterline` | `WF-400 Endplate` |
| **Waterline** | `C.3.1.26 Waterline - Voute & CT WF 588` | `Waterline` | `WF588-Endplate` |
| **Carton & Cups** | `C.3.3.20 Carton & Cups Storage - Voute & CT WF 350` | `Carton & Cups Storage` | `WF350-Endplate` |
| **Carton & Cups** | `C.3.3.21 Carton & Cups Storage - Voute & CT WF 300` | `Carton & Cups Storage` | `WF300-Endplate` |
| **Carton & Cups** | `C.3.3.22 Carton & Cups Storage - Voute & CT WF 400` | `Carton & Cups Storage` | `WF-400 Endplate` |
| **Carton & Cups** | `C.3.3.23 Carton & Cups Storage - Voute & CT WF 200` | `Carton & Cups Storage` | `WF200-Endplate` |
| **Power House** | `C.3.2.10 Power House - Voute & CT WF 350` | `Power House` | `WF350-Endplate` |
| **Power House** | `C.3.2.11 Power House - Voute & CT WF 200` | `Power House` | `WF200-Endplate` |
| **Production Office** | `C.3.4.12 Production Office - Voute & CT WF 350` | `Production Office` | `WF350-Endplate` |
| **Chiller Area 1** | `C.3.5.10 Chiller Area 1 - Voute & CT WF 350` | `Chiller Area 1` | `WF350-Endplate` |
| **Chiller Area 1** | `C.3.5.11 Chiller Area 1 - Voute & CT WF 300` | `Chiller Area 1` | `WF300-Endplate` |
| **Chiller Area 2** | `C.3.6.10 Chiller Area 2 - Voute & CT WF 350` | `Chiller Area 2` | `WF350-Endplate` |
| **Chiller Area 2** | `C.3.6.11 Chiller Area 2 - Voute & CT WF 300` | `Chiller Area 2` | `WF300-Endplate` |

---

## 6. Revit API C# Creation Script

```csharp
ViewSchedule vs = ViewSchedule.CreateSchedule(doc, new ElementId((int)BuiltInCategory.OST_StructConnectionProfiles));
vs.Name = scheduleName;
ScheduleDefinition def = vs.Definition;

// Add Fields
ScheduleField fLok = AddField(doc, def, "Lokasi", "Lokasi");
ScheduleField fConn = AddField(doc, def, "Type of Connection", "Tipe Sambungan");
ScheduleField fProf = AddField(doc, def, "Profile Type", "Type");
ScheduleField fCut = AddField(doc, def, "Cut Length", "Length (mm)");
ScheduleField fVol = AddField(doc, def, "Volume", "Volume");
ScheduleField fQty = AddField(doc, def, "Count", "Qty");
ScheduleField fWt = AddField(doc, def, "Exact Weight", "Berat (Kg)");

// Filters
def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, lokasi));
def.AddFilter(new ScheduleFilter(fConn.FieldId, ScheduleFilterType.Contains, connFilter));

// Sorting & Totals
ScheduleSortGroupField sProf = new ScheduleSortGroupField(fProf.FieldId, ScheduleSortOrder.Ascending);
def.AddSortGroupField(sProf);
ScheduleSortGroupField sCut = new ScheduleSortGroupField(fCut.FieldId, ScheduleSortOrder.Ascending);
sCut.ShowFooter = true;
def.AddSortGroupField(sCut);

def.IsItemized = false;
def.ShowGrandTotal = true;
def.ShowGrandTotalTitle = true;
def.ShowGrandTotalCount = true;

fQty.DisplayType = ScheduleFieldDisplayType.Totals;
fVol.DisplayType = ScheduleFieldDisplayType.Totals;
fWt.DisplayType = ScheduleFieldDisplayType.Totals;
```

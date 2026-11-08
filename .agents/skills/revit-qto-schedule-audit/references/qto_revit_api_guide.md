# Revit API Implementation Guide for QTO Schedule Automation

This reference document outlines the key C# Revit API patterns used in the AIGRAV QTO automation engine (`BimAuditSkill.cs`).

---

## 1. Field Addition & Ordering (`SetFieldOrder`)

In Revit API, newly added fields (`def.AddField(sf)`) are placed at the very end of the schedule. To ensure `Lokasi` appears as Column A (Column 0) and visible columns appear before hidden columns:

```csharp
private static void ReorderVisibleFields(ScheduleDefinition d, List<ScheduleField?> visibleFields)
{
    var allFields = d.GetFieldOrder();
    var newOrder = new List<ScheduleFieldId>();

    // 1. Place explicitly defined visible fields first (e.g. Lokasi at Index 0)
    foreach (var vf in visibleFields)
    {
        if (vf != null && allFields.Contains(vf.FieldId) && !newOrder.Contains(vf.FieldId))
        {
            newOrder.Add(vf.FieldId);
        }
    }

    // 2. Append remaining (hidden / background filter) fields at the end
    foreach (var fid in allFields)
    {
        if (!newOrder.Contains(fid))
        {
            newOrder.Add(fid);
        }
    }

    // 3. Apply the new order
    d.SetFieldOrder(newOrder);
}
```

---

## 2. Dynamic Field Lookup (`GetOrAddField`)

```csharp
private static ScheduleField? GetOrAddField(ScheduleDefinition d, Document doc, string name)
{
    // Check if field already exists in the schedule definition
    for (int i = 0; i < d.GetFieldCount(); i++)
    {
        var f = d.GetField(i);
        if (f.GetName().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
            return f;
    }

    // Search available schedulable fields in the category
    var sf = d.GetSchedulableFields()
        .FirstOrDefault(x => x.GetName(doc).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

    return sf != null ? d.AddField(sf) : null;
}
```

---

## 3. Formatting Rules Implementation

### A. Pelat Sambungan (4 Kolom)
```csharp
fLok.IsHidden = false; // Kolom A
fType.IsHidden = false; // Kolom B
fCount.IsHidden = false; // Kolom C
fWeight.IsHidden = false; // Kolom D

// Sembunyikan parameter geometri internal Advance Steel
if (fComments != null) fComments.IsHidden = true;
if (fThick != null) fThick.IsHidden = true;
if (fWidth != null) fWidth.IsHidden = true;
if (fLength != null) fLength.IsHidden = true;

ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fCount, fWeight });
```

### B. Baut Sambungan (3 Kolom)
```csharp
fLok.IsHidden = false; // Kolom A
fType.IsHidden = false; // Kolom B
fNumber.IsHidden = false; // Kolom C (Number / Qty Baut)

if (fComments != null) fComments.IsHidden = true;
if (fStd != null) fStd.IsHidden = true;
if (fDia != null) fDia.IsHidden = true;

ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fNumber });
```

### C. Baseplate (7 Kolom)
```csharp
fLok.IsHidden = false; // Kolom A
fComm.IsHidden = false; // Kolom B (Keterangan)
fThick.IsHidden = false; // Kolom C
fWidth.IsHidden = false; // Kolom D
fLength.IsHidden = false; // Kolom E
fCount.IsHidden = false; // Kolom F
fWeight.IsHidden = false; // Kolom G

// Sembunyikan Type of Connection (karena kosong pada baseplate)
if (fType != null) fType.IsHidden = true;

ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fComm, fThick, fWidth, fLength, fCount, fWeight });
```

---

## 4. In-Memory QA Audit Pattern (Zero Timeout)

> [!CAUTION]
> Never call `FilteredElementCollector(doc, schedule.Id)` or `schedule.GetTableData()` sequentially across dozens of schedules in a single turn. Doing so forces Revit to recalculate graphics across all schedules, leading to 30-second API timeouts.
> 
> Instead, inspect the `ScheduleDefinition` directly in memory:

```csharp
var def = s.Definition;
int filterCount = def.GetFilterCount();
var order = def.GetFieldOrder();

bool isLokasiCol0 = (order.Count > 0 && fLok != null && order[0] == fLok.FieldId);
bool isLokasiVisible = (fLok != null && !fLok.IsHidden);
```
This in-memory inspection completes across 100+ schedules in under **1.5 seconds**.

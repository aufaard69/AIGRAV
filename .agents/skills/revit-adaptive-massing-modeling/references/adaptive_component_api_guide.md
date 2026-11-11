# Technical Reference: Revit Adaptive Component API Guide

This document provides in-depth technical implementation patterns for developers and AI agents orchestrating Revit Adaptive Components via C# (.NET) and Python (RevitPythonShell / pyRevit / AIGRAV Bridge).

---

## 1. Fundamentals of Adaptive Components

An Adaptive Component is a `FamilyInstance` whose geometry is parametrically governed by one or more `ReferencePoint` elements categorized as Placement Points.

### Key API Classes
- `Autodesk.Revit.DB.AdaptiveComponentInstanceUtils`: Provides static methods to test, instantiate, and query adaptive components.
- `Autodesk.Revit.DB.ReferencePoint`: The datum point element that determines the location of each adaptive node.
- `Autodesk.Revit.DB.FamilySymbol`: The loaded type of the adaptive family.

---

## 2. Step-by-Step API Implementation (C#)

```csharp
using Autodesk.Revit.DB;
using System.Collections.Generic;

public static class AdaptivePlacementEngine
{
    private const double MmToFt = 1.0 / 304.8;

    public static FamilyInstance PlaceAdaptiveComponent(
        Document doc, 
        FamilySymbol symbol, 
        IList<XYZ> targetPointsFeet)
    {
        // 1. Ensure symbol is valid and active
        if (!symbol.IsActive)
        {
            symbol.Activate();
            doc.Regenerate();
        }

        // 2. Verify adaptive capability
        if (!AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(symbol))
        {
            throw new InvalidOperationException($"FamilySymbol '{symbol.Name}' is not an adaptive family.");
        }

        // 3. Create instance in transaction
        using Transaction tx = new Transaction(doc, "AIGRAV - Place Adaptive Component");
        tx.Start();

        FamilyInstance instance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, symbol);

        // 4. Retrieve placement point reference IDs
        IList<ElementId> pointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(instance);

        if (pointIds.Count != targetPointsFeet.Count)
        {
            tx.RollBack();
            throw new ArgumentException(
                $"Point count mismatch: Symbol requires {pointIds.Count} points, but {targetPointsFeet.Count} were provided.");
        }

        // 5. Reposition each reference point
        for (int i = 0; i < pointIds.Count; i++)
        {
            if (doc.GetElement(pointIds[i]) is ReferencePoint refPoint)
            {
                refPoint.Position = targetPointsFeet[i];
            }
        }

        doc.Regenerate();
        tx.Commit();

        return instance;
    }
}
```

---

## 3. Dynamic Python Implementation for AIGRAV Sandbox

When executed dynamically inside `aigrav_execute_revit_python`:

```python
import clr
clr.AddReference('RevitAPI')
from Autodesk.Revit.DB import (
    XYZ, ElementId, Transaction, AdaptiveComponentInstanceUtils, 
    ReferencePoint, FilteredElementCollector, FamilySymbol
)

def find_adaptive_symbol(doc, family_name, type_name=None):
    collector = FilteredElementCollector(doc).OfClass(FamilySymbol)
    for fs in collector:
        if fs.FamilyName.lower() == family_name.lower():
            if type_name is None or fs.Name.lower() == type_name.lower():
                if AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(fs):
                    return fs
    return None

def batch_place_adaptive(doc, family_name, panels_points_mm):
    """
    panels_points_mm: List of list of tuples, e.g.:
    [
       [(0, 0, 0), (6000, 0, 500), (6000, 4000, 800), (0, 4000, 300)], # Panel 1
       ...
    ]
    """
    symbol = find_adaptive_symbol(doc, family_name)
    if not symbol:
        raise ValueError(f"Adaptive family symbol '{family_name}' not found in active document.")

    created_ids = []
    MM_TO_FT = 1.0 / 304.8

    tx = Transaction(doc, "AIGRAV Batch Place Adaptive")
    tx.Start()
    try:
        if not symbol.IsActive:
            symbol.Activate()
            doc.Regenerate()

        for pts in panels_points_mm:
            inst = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, symbol)
            pt_ids = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(inst)
            for i, pid in enumerate(pt_ids):
                ref_pt = doc.GetElement(pid)
                x, y, z = pts[i]
                ref_pt.Position = XYZ(x * MM_TO_FT, y * MM_TO_FT, z * MM_TO_FT)
            created_ids.append(inst.Id.Value)

        doc.Regenerate()
        tx.Commit()
        return created_ids
    except Exception as e:
        tx.RollBack()
        raise e
```

# Technical Reference: Conceptual Massing & "Model By Face" Guide

This guide describes how to generate organic, curved, and complex mass forms and convert them directly into native Revit System Families (Walls, Roofs, Floors, Curtain Systems) using the Revit API.

---

## 1. Conceptual Massing Architecture

Revit supports two distinct massing environments:
1. **In-Place Mass (`OST_Mass`)**: Modeled directly in the project document.
2. **Mass Family (`.rfa`)**: Authored in the Mass Conceptual Design environment and loaded as a `FamilyInstance`.

### The "Model By Face" Philosophy
A conceptual mass serves as the geometrical host. Real building elements are then assigned to its faces:
- **`FaceWall`**: Applies composite architectural or structural wall types to vertical or sloped mass faces.
- **`Roof` (Roof By Face)**: Applies roof construction layers to horizontal, sloped, or multi-curved faces.
- **`CurtainSystem`**: Generates a parametric grid of mullions and panels over single or double-curved surfaces.
- **Mass Floors**: Creates sliced horizontal reference planes at each project level to report gross floor area (GFA).

---

## 2. API Implementation Patterns

### A. Creating a Curtain System by Face
```csharp
using Autodesk.Revit.DB;

public static CurtainSystem CreateCurtainSystemOnFace(
    Document doc, 
    Face massFace, 
    ElementId curtainSystemTypeId)
{
    using Transaction tx = new Transaction(doc, "AIGRAV - Create Curtain System By Face");
    tx.Start();

    FaceArray faceArray = new FaceArray();
    faceArray.Append(massFace);

    CurtainSystem cs = CurtainSystem.Create(doc, faceArray, curtainSystemTypeId);
    
    doc.Regenerate();
    tx.Commit();
    return cs;
}
```

### B. Creating FaceWall on Mass Face
```csharp
public static FaceWall CreateWallOnFace(
    Document doc, 
    Reference faceReference, 
    ElementId wallTypeId, 
    WallLocationLine locationLine)
{
    using Transaction tx = new Transaction(doc, "AIGRAV - Create Face Wall");
    tx.Start();

    FaceWall fw = FaceWall.Create(doc, wallTypeId, locationLine, faceReference);
    
    doc.Regenerate();
    tx.Commit();
    return fw;
}
```

### C. Mass Floor Generation
```csharp
public static void AddMassFloors(
    Document doc, 
    ElementId massInstanceId, 
    IList<ElementId> levelIds)
{
    using Transaction tx = new Transaction(doc, "AIGRAV - Generate Mass Floors");
    tx.Start();

    foreach (ElementId lvlId in levelIds)
    {
        MassInstanceUtils.AddMassLevelDataToMassInstance(doc, massInstanceId, lvlId);
    }

    doc.Regenerate();
    tx.Commit();
}
```

---

## 3. FreeFormElement (The True Alternative to DirectShape in Family Context)

When a unique 3D solid geometry is strictly required (such as an imported mechanical component, custom carved cornice, or complex bridge pylon):
- **DO NOT** create a `DirectShape` in the project.
- **DO** create a `FreeFormElement` inside an in-memory or temporary `.rfa` family file (`OST_GenericModel` or `OST_Mass`), and load it into the project!

```csharp
public static FamilyInstance CreateComponentFromSolid(
    Application app, 
    Document projectDoc, 
    Solid solidGeometry, 
    string familyName)
{
    // 1. Open Generic Model family template in memory
    string templatePath = app.FamilyTemplatePath + "\\Metric Generic Model.rft";
    Document famDoc = app.NewFamilyDocument(templatePath);

    using (Transaction famTx = new Transaction(famDoc, "Create FreeForm Solid"))
    {
        famTx.Start();
        FreeFormElement.Create(famDoc, solidGeometry);
        famTx.Commit();
    }

    // 2. Load into project
    Family loadedFamily = famDoc.LoadFamily(projectDoc, new FamilyOptionOverride());
    famDoc.Close(false);

    // 3. Place instance in project document
    FamilySymbol symbol = projectDoc.GetElement(loadedFamily.GetFamilySymbolIds().First()) as FamilySymbol;
    if (!symbol.IsActive) symbol.Activate();

    return projectDoc.Create.NewFamilyInstance(XYZ.Zero, symbol, StructuralType.NonStructural);
}
```

### Advantages Over DirectShape:
- Assignable parameter values (Mark, Comments, Lokasi, Material).
- Valid `TypeId` and `FamilyName`.
- Cuttable in sections and plan views.
- Fully compatible with QTO and IFC Export.

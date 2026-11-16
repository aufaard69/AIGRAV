using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.RevitBridge;

/// <summary>
/// Next-Generation Precision Modeling Engine adopted from RevitCortex.
/// Features:
/// 1. TransactionFailureHandling with warning suppression (zero UI freeze / modal dialogs).
/// 2. Automatic Level snapping (FindNearestLevel) and fallback family/wall/floor types.
/// 3. Arc / Curved wall support via optional pMid parameter.
/// 4. Zero-Overlap floor creation via Room Boundary (roomId) or Polygon with multi-loop holes.
/// 5. Auto FamilySymbol activation and rotation for structural columns and furniture.
/// 6. Dual compatibility: accepts both Cortex structured JSON (mm) and legacy AIGRAV meters.
/// </summary>
public static class CortexModelingToolkit
{
    public const double MmPerFoot = 304.8;
    public const double MeterPerFoot = 0.3048;

    #region Helper Utilities

    public static Level? FindNearestLevel(Document doc, double elevationFeet)
    {
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => Math.Abs(l.Elevation - elevationFeet))
            .ToList();

        return levels.FirstOrDefault();
    }

    public static Level? FindLevelByName(Document doc, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public static XYZ ParseXYZ(JsonElement elem, double defaultScale = 1.0 / MmPerFoot)
    {
        double x = 0, y = 0, z = 0;
        if (elem.TryGetProperty("x", out var px)) x = px.GetDouble();
        else if (elem.TryGetProperty("X", out var pX)) x = pX.GetDouble();

        if (elem.TryGetProperty("y", out var py)) y = py.GetDouble();
        else if (elem.TryGetProperty("Y", out var pY)) y = pY.GetDouble();

        if (elem.TryGetProperty("z", out var pz)) z = pz.GetDouble();
        else if (elem.TryGetProperty("Z", out var pZ)) z = pZ.GetDouble();

        return new XYZ(x * defaultScale, y * defaultScale, z * defaultScale);
    }

    #endregion

    #region 1. Smart Create Line-Based Elements (Walls, Beams)

    public static string CreateLineBasedElement(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { success = false, error = "Argument JSON is required." });

        try
        {
            using var docJson = JsonDocument.Parse(argument);
            var root = docJson.RootElement;

            List<JsonElement> items = new();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray()) items.Add(el);
            }
            else if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in dataProp.EnumerateArray()) items.Add(el);
            }
            else
            {
                items.Add(root);
            }

            var createdIds = new List<long>();
            var warnings = new List<string>();

            foreach (var item in items)
            {
                ProcessLineItem(doc, item, createdIds, warnings);
            }

            return JsonSerializer.Serialize(new
            {
                success = createdIds.Count > 0,
                createdElementIds = createdIds,
                count = createdIds.Count,
                warnings = warnings,
                message = $"Successfully created {createdIds.Count} line-based element(s)."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }

    private static void ProcessLineItem(Document doc, JsonElement item, List<long> createdIds, List<string> warnings)
    {
        // 1. Detect Category
        BuiltInCategory builtInCategory = BuiltInCategory.OST_Walls;
        if (item.TryGetProperty("category", out var catProp))
        {
            string catStr = catProp.GetString() ?? "";
            if (Enum.TryParse(catStr.Replace(".", ""), true, out BuiltInCategory bic))
                builtInCategory = bic;
        }

        // 2. Parse Start/End Points
        XYZ p0, p1;
        XYZ? pMid = null;

        // Legacy format: x1M, y1M, x2M, y2M (meters)
        if (item.TryGetProperty("x1M", out var x1Prop) || item.TryGetProperty("x1m", out x1Prop))
        {
            double x1 = x1Prop.GetDouble() / MeterPerFoot;
            double y1 = (item.TryGetProperty("y1M", out var y1P) ? y1P.GetDouble() : item.GetProperty("y1m").GetDouble()) / MeterPerFoot;
            double x2 = (item.TryGetProperty("x2M", out var x2P) ? x2P.GetDouble() : item.GetProperty("x2m").GetDouble()) / MeterPerFoot;
            double y2 = (item.TryGetProperty("y2M", out var y2P) ? y2P.GetDouble() : item.GetProperty("y2m").GetDouble()) / MeterPerFoot;
            p0 = new XYZ(x1, y1, 0);
            p1 = new XYZ(x2, y2, 0);
        }
        else if (item.TryGetProperty("locationLine", out var locProp))
        {
            p0 = ParseXYZ(locProp.GetProperty("p0"));
            p1 = ParseXYZ(locProp.GetProperty("p1"));
            if (locProp.TryGetProperty("pMid", out var pmProp))
                pMid = ParseXYZ(pmProp);
        }
        else if (item.TryGetProperty("p0", out var p0Prop) && item.TryGetProperty("p1", out var p1Prop))
        {
            p0 = ParseXYZ(p0Prop);
            p1 = ParseXYZ(p1Prop);
            if (item.TryGetProperty("pMid", out var pmProp2))
                pMid = ParseXYZ(pmProp2);
        }
        else
        {
            warnings.Add("Missing line location points (p0/p1 or x1M/y1M).");
            return;
        }

        if (p0.IsAlmostEqualTo(p1))
        {
            warnings.Add("Start and end points are too close — line has zero length.");
            return;
        }

        Curve locationLine;
        try
        {
            if (pMid != null)
                locationLine = Arc.Create(p0, p1, pMid);
            else
                locationLine = Line.CreateBound(p0, p1);
        }
        catch (Exception ex)
        {
            warnings.Add($"Cannot create curve: {ex.Message}");
            return;
        }

        // 3. Resolve Height & Level
        double heightFt = 3000.0 / MmPerFoot;
        if (item.TryGetProperty("heightM", out var hmProp)) heightFt = hmProp.GetDouble() / MeterPerFoot;
        else if (item.TryGetProperty("height", out var hProp)) heightFt = hProp.GetDouble() / MmPerFoot;
        else if (item.TryGetProperty("heightMm", out var hmmProp)) heightFt = hmmProp.GetDouble() / MmPerFoot;

        double baseElevationFt = p0.Z;
        if (item.TryGetProperty("baseLevel", out var blProp)) baseElevationFt = blProp.GetDouble() / MmPerFoot;

        Level? level = null;
        if (item.TryGetProperty("levelId", out var lidProp))
        {
            long lid = lidProp.GetInt64();
            if (lid > 0) level = doc.GetElement(new ElementId(lid)) as Level;
        }
        if (level == null && item.TryGetProperty("levelName", out var lnProp))
            level = FindLevelByName(doc, lnProp.GetString() ?? "");

        level ??= FindNearestLevel(doc, baseElevationFt);
        if (level == null)
        {
            warnings.Add("No levels found in document.");
            return;
        }

        double baseOffset = baseElevationFt - level.Elevation;

        // 4. Resolve Type
        string? requestedTypeName = null;
        if (item.TryGetProperty("typeName", out var tnProp)) requestedTypeName = tnProp.GetString();
        else if (item.TryGetProperty("wallType", out var wtProp)) requestedTypeName = wtProp.GetString();

        if (builtInCategory == BuiltInCategory.OST_Walls)
        {
            WallType? wallType = null;
            if (!string.IsNullOrWhiteSpace(requestedTypeName))
            {
                wallType = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .FirstOrDefault(wt => wt.Name.Equals(requestedTypeName, StringComparison.OrdinalIgnoreCase));
            }

            if (wallType == null)
            {
                wallType = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

                if (wallType == null)
                {
                    warnings.Add("No WallType available in project.");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(requestedTypeName))
                    warnings.Add($"Wall type '{requestedTypeName}' not found. Defaulted to '{wallType.Name}'.");
            }

            using var tx = new Transaction(doc, "AIGRAV Cortex: Create Wall");
            var capture = TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            try
            {
                var wall = Wall.Create(doc, locationLine, wallType.Id, level.Id, heightFt, baseOffset, false, false);
                if (wall != null)
                {
                    double slantRad = 0.0;
                    if (item.TryGetProperty("slantAngleDeg", out var saDeg)) slantRad = saDeg.GetDouble() * Math.PI / 180.0;
                    else if (item.TryGetProperty("slantAngleRad", out var saRad)) slantRad = saRad.GetDouble();
                    else if (item.TryGetProperty("slantAngle", out var saGen))
                    {
                        double val = saGen.GetDouble();
                        slantRad = Math.Abs(val) > 1.5 ? val * Math.PI / 180.0 : val;
                    }

                    if (Math.Abs(slantRad) > 0.0001)
                    {
                        try
                        {
                            wall.CrossSection = WallCrossSection.SingleSlanted;
                            doc.Regenerate();
                            var pAngle = wall.get_Parameter(BuiltInParameter.WALL_SINGLE_SLANT_ANGLE_FROM_VERTICAL);
                            if (pAngle != null && !pAngle.IsReadOnly)
                            {
                                pAngle.Set(slantRad);
                            }
                        }
                        catch (Exception slantEx)
                        {
                            warnings.Add($"Could not set slant angle: {slantEx.Message}");
                        }
                    }

                    createdIds.Add(wall.Id.Value);
                }
                if (tx.Commit() != TransactionStatus.Committed)
                {
                    warnings.Add($"Wall transaction rolled back: {TransactionFailureHandling.Describe(capture)}");
                }
            }
            catch (Exception ex)
            {
                if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                warnings.Add($"Failed to create wall: {ex.Message}");
            }
        }
        else // Structural Framing (Beams)
        {
            FamilySymbol? symbol = null;
            if (!string.IsNullOrWhiteSpace(requestedTypeName))
            {
                symbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(builtInCategory)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.Name.Equals(requestedTypeName, StringComparison.OrdinalIgnoreCase)
                                      || $"{s.FamilyName}: {s.Name}".Equals(requestedTypeName, StringComparison.OrdinalIgnoreCase));
            }

            symbol ??= new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(builtInCategory)
                .Cast<FamilySymbol>()
                .FirstOrDefault();

            if (symbol == null)
            {
                warnings.Add($"No FamilySymbol available for category {builtInCategory}.");
                return;
            }

            using var tx = new Transaction(doc, "AIGRAV Cortex: Create Beam");
            var capture = TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            try
            {
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                var inst = doc.Create.NewFamilyInstance(locationLine, symbol, level, StructuralType.Beam);
                if (inst != null)
                {
                    createdIds.Add(inst.Id.Value);
                }
                if (tx.Commit() != TransactionStatus.Committed)
                {
                    warnings.Add($"Beam transaction rolled back: {TransactionFailureHandling.Describe(capture)}");
                }
            }
            catch (Exception ex)
            {
                if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                warnings.Add($"Failed to create beam: {ex.Message}");
            }
        }
    }

    #endregion

    #region 2. Smart Create Floor (Room-Based & Donut Slabs with Holes)

    public static string CreateFloor(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { success = false, error = "Argument JSON is required." });

        try
        {
            using var docJson = JsonDocument.Parse(argument);
            var root = docJson.RootElement;

            long roomId = 0;
            if (root.TryGetProperty("roomId", out var rProp)) roomId = rProp.GetInt64();

            string? floorTypeName = null;
            if (root.TryGetProperty("typeName", out var tnProp)) floorTypeName = tnProp.GetString();
            else if (root.TryGetProperty("floorTypeName", out var ftnProp)) floorTypeName = ftnProp.GetString();

            // Resolve FloorType
            FloorType? floorType = null;
            if (!string.IsNullOrWhiteSpace(floorTypeName))
            {
                floorType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .FirstOrDefault(ft => ft.Name.Equals(floorTypeName, StringComparison.OrdinalIgnoreCase));
            }

            string? typeWarning = null;
            if (floorType == null)
            {
                floorType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .OfCategory(BuiltInCategory.OST_Floors)
                    .Cast<FloorType>()
                    .FirstOrDefault();

                if (floorType == null)
                    return JsonSerializer.Serialize(new { success = false, error = "No FloorType available in project." });

                if (!string.IsNullOrWhiteSpace(floorTypeName))
                    typeWarning = $"Floor type '{floorTypeName}' not found. Defaulted to '{floorType.Name}'.";
            }

            // Build CurveLoops (Outer + optional Inner Holes)
            var curveLoops = new List<CurveLoop>();
            Level? targetLevel = null;

            if (roomId > 0)
            {
                var room = doc.GetElement(new ElementId(roomId)) as Room;
                if (room == null)
                    return JsonSerializer.Serialize(new { success = false, error = $"Room with ID {roomId} not found." });

                targetLevel = doc.GetElement(room.LevelId) as Level;

                var opt = new SpatialElementBoundaryOptions();
                var segments = room.GetBoundarySegments(opt);
                if (segments == null || segments.Count == 0)
                    return JsonSerializer.Serialize(new { success = false, error = $"Room {roomId} has no closed boundary." });

                // Outer loop
                var outerLoop = new CurveLoop();
                foreach (var seg in segments[0])
                    outerLoop.Append(seg.GetCurve());
                curveLoops.Add(outerLoop);

                // Additional inner boundary loops (room internal voids)
                for (int i = 1; i < segments.Count; i++)
                {
                    var innerLoop = new CurveLoop();
                    foreach (var seg in segments[i])
                        innerLoop.Append(seg.GetCurve());
                    curveLoops.Add(innerLoop);
                }
            }
            else // From boundary points
            {
                var points = new List<XYZ>();

                // Check points array
                JsonElement ptsArray = default;
                if (root.TryGetProperty("boundaryPoints", out var bp)) ptsArray = bp;
                else if (root.TryGetProperty("points", out var p)) ptsArray = p;

                if (ptsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pt in ptsArray.EnumerateArray())
                    {
                        // Supports {x, y} or [x, y]
                        if (pt.ValueKind == JsonValueKind.Object)
                        {
                            points.Add(ParseXYZ(pt));
                        }
                        else if (pt.ValueKind == JsonValueKind.Array)
                        {
                            var arr = pt.EnumerateArray().ToList();
                            double x = arr[0].GetDouble() / MeterPerFoot;
                            double y = arr[1].GetDouble() / MeterPerFoot;
                            points.Add(new XYZ(x, y, 0));
                        }
                    }
                }

                if (points.Count < 3)
                    return JsonSerializer.Serialize(new { success = false, error = "At least 3 boundary points are required." });

                var outerLoop = new CurveLoop();
                for (int i = 0; i < points.Count; i++)
                {
                    outerLoop.Append(Line.CreateBound(points[i], points[(i + 1) % points.Count]));
                }
                curveLoops.Add(outerLoop);

                // Process Holes (Donut loops for zero-overlap slabs)
                if (root.TryGetProperty("holes", out var holesProp) && holesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var hole in holesProp.EnumerateArray())
                    {
                        if (hole.ValueKind != JsonValueKind.Array) continue;
                        var hpts = new List<XYZ>();
                        foreach (var hp in hole.EnumerateArray())
                            hpts.Add(ParseXYZ(hp));

                        if (hpts.Count >= 3)
                        {
                            var hloop = new CurveLoop();
                            for (int j = 0; j < hpts.Count; j++)
                                hloop.Append(Line.CreateBound(hpts[j], hpts[(j + 1) % hpts.Count]));
                            curveLoops.Add(hloop);
                        }
                    }
                }
            }

            // Resolve Level
            if (targetLevel == null)
            {
                if (root.TryGetProperty("levelId", out var lidProp))
                    targetLevel = doc.GetElement(new ElementId(lidProp.GetInt64())) as Level;
                else if (root.TryGetProperty("levelElevation", out var leProp))
                    targetLevel = FindNearestLevel(doc, leProp.GetDouble() / MmPerFoot);

                targetLevel ??= FindNearestLevel(doc, 0);
            }

            if (targetLevel == null)
                return JsonSerializer.Serialize(new { success = false, error = "No Level found in project." });

            long createdFloorId = 0;
            var warnings = new List<string>();
            if (typeWarning != null) warnings.Add(typeWarning);

            using var tx = new Transaction(doc, "AIGRAV Cortex: Create Floor");
            var capture = TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            try
            {
                var floor = Floor.Create(doc, curveLoops, floorType.Id, targetLevel.Id);
                if (floor != null)
                {
                    createdFloorId = floor.Id.Value;
                }
                if (tx.Commit() != TransactionStatus.Committed)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Floor transaction rolled back: {TransactionFailureHandling.Describe(capture)}"
                    });
                }
            }
            catch (Exception ex)
            {
                if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }

            return JsonSerializer.Serialize(new
            {
                success = createdFloorId > 0,
                createdElementIds = new[] { createdFloorId },
                floorId = createdFloorId,
                warnings = warnings,
                message = $"Successfully created floor with {curveLoops.Count} boundary loop(s) (1 outer, {curveLoops.Count - 1} void holes)."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }

    #endregion

    #region 3. Smart Create Point-Based Elements (Columns, Furniture)

    public static string CreatePointBasedElement(Document doc, string? argument, BuiltInCategory defaultCategory = BuiltInCategory.OST_StructuralColumns)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { success = false, error = "Argument JSON is required." });

        try
        {
            using var docJson = JsonDocument.Parse(argument);
            var root = docJson.RootElement;

            List<JsonElement> items = new();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray()) items.Add(el);
            }
            else if (root.TryGetProperty("data", out var dProp) && dProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in dProp.EnumerateArray()) items.Add(el);
            }
            else
            {
                items.Add(root);
            }

            var createdIds = new List<long>();
            var warnings = new List<string>();

            foreach (var item in items)
            {
                ProcessPointItem(doc, item, defaultCategory, createdIds, warnings);
            }

            return JsonSerializer.Serialize(new
            {
                success = createdIds.Count > 0,
                createdElementIds = createdIds,
                count = createdIds.Count,
                warnings = warnings,
                message = $"Successfully placed {createdIds.Count} point-based instance(s)."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }

    private static void ProcessPointItem(Document doc, JsonElement item, BuiltInCategory defaultCat, List<long> createdIds, List<string> warnings)
    {
        BuiltInCategory category = defaultCat;
        if (item.TryGetProperty("category", out var catProp))
        {
            string catStr = catProp.GetString() ?? "";
            if (Enum.TryParse(catStr.Replace(".", ""), true, out BuiltInCategory bic))
                category = bic;
        }

        // Location
        XYZ location;
        if (item.TryGetProperty("xM", out var xProp) || item.TryGetProperty("xm", out xProp))
        {
            double x = xProp.GetDouble() / MeterPerFoot;
            double y = (item.TryGetProperty("yM", out var yP) ? yP.GetDouble() : item.GetProperty("ym").GetDouble()) / MeterPerFoot;
            double z = (item.TryGetProperty("zM", out var zP) ? zP.GetDouble() : 0) / MeterPerFoot;
            location = new XYZ(x, y, z);
        }
        else if (item.TryGetProperty("location", out var locProp))
        {
            location = ParseXYZ(locProp);
        }
        else if (item.TryGetProperty("point", out var ptProp))
        {
            location = ParseXYZ(ptProp);
        }
        else
        {
            warnings.Add("Missing placement location point.");
            return;
        }

        // Level
        Level? level = null;
        if (item.TryGetProperty("levelId", out var lidProp))
            level = doc.GetElement(new ElementId(lidProp.GetInt64())) as Level;
        else if (item.TryGetProperty("levelName", out var lnProp))
            level = FindLevelByName(doc, lnProp.GetString() ?? "");

        level ??= FindNearestLevel(doc, location.Z);
        if (level == null)
        {
            warnings.Add("No Level found in project.");
            return;
        }

        // Family Symbol
        string? typeName = null;
        if (item.TryGetProperty("typeName", out var tnProp)) typeName = tnProp.GetString();
        else if (item.TryGetProperty("symbolName", out var snProp)) typeName = snProp.GetString();

        FamilySymbol? symbol = null;
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            symbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(category)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                                  || $"{s.FamilyName}: {s.Name}".Equals(typeName, StringComparison.OrdinalIgnoreCase));
        }

        symbol ??= new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(category)
            .Cast<FamilySymbol>()
            .FirstOrDefault();

        if (symbol == null)
        {
            warnings.Add($"No FamilySymbol found for category {category}.");
            return;
        }

        StructuralType structType = category == BuiltInCategory.OST_StructuralColumns
            ? StructuralType.Column
            : (category == BuiltInCategory.OST_StructuralFraming ? StructuralType.Beam : StructuralType.NonStructural);

        using var tx = new Transaction(doc, "AIGRAV Cortex: Create Point Instance");
        var capture = TransactionFailureHandling.SuppressWarnings(tx);
        tx.Start();
        try
        {
            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            var inst = doc.Create.NewFamilyInstance(location, symbol, level, structType);
            if (inst != null)
            {
                // Optional rotation
                if (item.TryGetProperty("rotationDeg", out var rotProp) || item.TryGetProperty("rotation", out rotProp))
                {
                    double rad = rotProp.GetDouble() * Math.PI / 180.0;
                    if (Math.Abs(rad) > 1e-6)
                    {
                        var axis = Line.CreateBound(location, location + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, inst.Id, axis, rad);
                    }
                }

                createdIds.Add(inst.Id.Value);
            }

            if (tx.Commit() != TransactionStatus.Committed)
            {
                warnings.Add($"Transaction rolled back: {TransactionFailureHandling.Describe(capture)}");
            }
        }
        catch (Exception ex)
        {
            if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
            warnings.Add($"Failed to place family instance: {ex.Message}");
        }
    }

    #endregion

    #region 4. Element Manipulation (Change Type, Move, Rotate, Copy)

    public static string ChangeElementType(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { success = false, error = "Argument JSON required." });

        try
        {
            using var docJson = JsonDocument.Parse(argument);
            var root = docJson.RootElement;

            var elementIds = new List<ElementId>();
            if (root.TryGetProperty("elementIds", out var idsProp) && idsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var id in idsProp.EnumerateArray())
                    elementIds.Add(new ElementId(id.GetInt64()));
            }

            string? targetTypeName = null;
            if (root.TryGetProperty("targetTypeName", out var ttnProp)) targetTypeName = ttnProp.GetString();

            ElementId? targetTypeId = null;
            if (root.TryGetProperty("targetTypeId", out var ttidProp)) targetTypeId = new ElementId(ttidProp.GetInt64());

            if (targetTypeId == null && !string.IsNullOrWhiteSpace(targetTypeName))
            {
                var typeElem = new FilteredElementCollector(doc)
                    .WhereElementIsElementType()
                    .FirstOrDefault(e => e.Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase));
                if (typeElem != null) targetTypeId = typeElem.Id;
            }

            if (targetTypeId == null)
                return JsonSerializer.Serialize(new { success = false, error = "Target type could not be resolved." });

            int modifiedCount = 0;
            using var tx = new Transaction(doc, "AIGRAV Cortex: Change Element Type");
            var capture = TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            foreach (var eid in elementIds)
            {
                var elem = doc.GetElement(eid);
                if (elem != null && elem.CanHaveTypeAssigned())
                {
                    elem.ChangeTypeId(targetTypeId);
                    modifiedCount++;
                }
            }
            tx.Commit();

            return JsonSerializer.Serialize(new
            {
                success = modifiedCount > 0,
                modifiedCount,
                message = $"Successfully updated type on {modifiedCount} element(s)."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }

    public static string ModifyElement(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { success = false, error = "Argument JSON required." });

        try
        {
            using var docJson = JsonDocument.Parse(argument);
            var root = docJson.RootElement;

            long id = root.GetProperty("elementId").GetInt64();
            string action = root.GetProperty("action").GetString()?.ToLowerInvariant() ?? "";

            var elem = doc.GetElement(new ElementId(id));
            if (elem == null)
                return JsonSerializer.Serialize(new { success = false, error = $"Element {id} not found." });

            using var tx = new Transaction(doc, $"AIGRAV Cortex: Modify {action}");
            var capture = TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();

            if (action == "move" && root.TryGetProperty("translation", out var trProp))
            {
                var vec = ParseXYZ(trProp);
                ElementTransformUtils.MoveElement(doc, elem.Id, vec);
            }
            else if (action == "rotate" && root.TryGetProperty("angleDeg", out var angProp))
            {
                double rad = angProp.GetDouble() * Math.PI / 180.0;
                var bb = elem.get_BoundingBox(null);
                XYZ center = bb != null ? (bb.Min + bb.Max) * 0.5 : XYZ.Zero;
                var axis = Line.CreateBound(center, center + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, elem.Id, axis, rad);
            }
            tx.Commit();

            return JsonSerializer.Serialize(new { success = true, elementId = id, action = action });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }

    public static string CopyElements(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { success = false, error = "Argument JSON required." });

        try
        {
            using var docJson = JsonDocument.Parse(argument);
            var root = docJson.RootElement;

            var elementIds = new List<ElementId>();
            if (root.TryGetProperty("elementIds", out var idsProp) && idsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var id in idsProp.EnumerateArray())
                    elementIds.Add(new ElementId(id.GetInt64()));
            }

            XYZ offset = XYZ.Zero;
            if (root.TryGetProperty("offset", out var offProp))
                offset = ParseXYZ(offProp);

            using var tx = new Transaction(doc, "AIGRAV Cortex: Copy Elements");
            var capture = TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();

            var newIds = ElementTransformUtils.CopyElements(doc, elementIds, offset);
            tx.Commit();

            return JsonSerializer.Serialize(new
            {
                success = newIds.Count > 0,
                createdElementIds = newIds.Select(i => i.Value).ToList(),
                count = newIds.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }

    #endregion
}

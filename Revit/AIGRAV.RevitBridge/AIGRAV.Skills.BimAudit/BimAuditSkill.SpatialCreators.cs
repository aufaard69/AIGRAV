using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class SpatialAgenticSandboxSkill
{
    public static string BuildPlanAsciiMinimap(Document doc, HashSet<ElementId>? highlightIds = null)
    {
        const int cols = 42;
        const int rows = 16;
        char[,] map = new char[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                map[r, c] = '.';

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.Category != null && (
                e.Category.Id.Value == (long)BuiltInCategory.OST_Grids ||
                e.Category.Id.Value == (long)BuiltInCategory.OST_Walls ||
                e.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns ||
                e.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming))
            .Take(250)
            .ToList();

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        List<(Element el, BoundingBoxXYZ bb)> valid = new();
        foreach (var e in elements)
        {
            var bb = e.get_BoundingBox(null);
            if (bb == null) continue;
            valid.Add((e, bb));
            if (bb.Min.X < minX) minX = bb.Min.X;
            if (bb.Min.Y < minY) minY = bb.Min.Y;
            if (bb.Max.X > maxX) maxX = bb.Max.X;
            if (bb.Max.Y > maxY) maxY = bb.Max.Y;
        }

        if (valid.Count == 0) return "ASCII Minimap: (Empty model)";

        double spanX = Math.Max(1.0, maxX - minX);
        double spanY = Math.Max(1.0, maxY - minY);

        int ToCol(double x) => Math.Clamp((int)Math.Round(((x - minX) / spanX) * (cols - 1)), 0, cols - 1);
        int ToRow(double y) => Math.Clamp(rows - 1 - (int)Math.Round(((y - minY) / spanY) * (rows - 1)), 0, rows - 1);

        foreach (var (el, bb) in valid)
        {
            bool isTarget = highlightIds != null && highlightIds.Contains(el.Id);
            long catId = el.Category?.Id.Value ?? 0;
            char ch = isTarget ? '@' : catId switch
            {
                (long)BuiltInCategory.OST_StructuralColumns => 'C',
                (long)BuiltInCategory.OST_Walls => '#',
                (long)BuiltInCategory.OST_StructuralFraming => '=',
                _ => '+'
            };

            int c1 = ToCol(bb.Min.X), c2 = ToCol(bb.Max.X);
            int r1 = ToRow(bb.Max.Y), r2 = ToRow(bb.Min.Y);
            for (int r = Math.Min(r1, r2); r <= Math.Max(r1, r2); r++)
                for (int c = Math.Min(c1, c2); c <= Math.Max(c1, c2); c++)
                    if (isTarget || map[r, c] == '.' || map[r, c] == '+')
                        map[r, c] = ch;
        }

        StringBuilder sb = new();
        sb.AppendLine($"Plan Bounds MM: X[{FtToMm(minX)}..{FtToMm(maxX)}], Y[{FtToMm(minY)}..{FtToMm(maxY)}] ('@'=New/Modified, 'C'=Col, '#'=Wall, '='=Beam):");
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++) sb.Append(map[r, c]);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static double ReadCoordFt(JsonElement data, string mmKey, string mKey, double defaultMm = 0.0)
    {
        if (data.TryGetProperty(mmKey, out JsonElement mmEl) && mmEl.ValueKind == JsonValueKind.Number)
            return MmToFt(mmEl.GetDouble());
        if (data.TryGetProperty(mKey, out JsonElement mEl) && mEl.ValueKind == JsonValueKind.Number)
            return mEl.GetDouble() / 0.3048;
        return MmToFt(defaultMm);
    }

    private static Level? ResolveLevel(Document doc, JsonElement data)
    {
        if (data.TryGetProperty("levelId", out JsonElement lid) && lid.ValueKind == JsonValueKind.Number)
            return doc.GetElement(new ElementId(lid.GetInt64())) as Level;
        if (data.TryGetProperty("level_id", out JsonElement lid2) && lid2.ValueKind == JsonValueKind.Number)
            return doc.GetElement(new ElementId(lid2.GetInt64())) as Level;
        if (data.TryGetProperty("levelName", out JsonElement ln) && ln.ValueKind == JsonValueKind.String)
        {
            string name = ln.GetString() ?? "";
            return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(l => l.Elevation).FirstOrDefault();
    }

    private static string GetElementTelemetryByIds(Document doc, string? argument)
    {
        List<ElementId> ids = new();
        if (!string.IsNullOrWhiteSpace(argument))
        {
            if (argument.TrimStart().StartsWith("{"))
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                if (jd.RootElement.TryGetProperty("elementIds", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var item in arr.EnumerateArray()) ids.Add(new ElementId(item.GetInt64()));
                else if (jd.RootElement.TryGetProperty("elementId", out JsonElement single))
                    ids.Add(new ElementId(single.GetInt64()));
            }
            else
            {
                foreach (var part in argument.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (long.TryParse(part.Trim(), out long v)) ids.Add(new ElementId(v));
            }
        }
        return BuildSpatialTelemetryJson(doc, "inspect_element_telemetry", ids);
    }

    private static string CreateWallWithTelemetry(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement data = jd.RootElement;

        string typeName = data.TryGetProperty("typeName", out JsonElement tn) ? tn.GetString() ?? "" : "";
        Level? level = ResolveLevel(doc, data);
        if (level == null) return JsonSerializer.Serialize(new { error = "No valid Level found." });

        WallType? wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>()
            .FirstOrDefault(x => string.IsNullOrEmpty(typeName) || x.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            ?? new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault();
        if (wallType == null) return JsonSerializer.Serialize(new { error = $"WallType '{typeName}' not found." });

        double x1 = ReadCoordFt(data, "x1_mm", "x1M");
        double y1 = ReadCoordFt(data, "y1_mm", "y1M");
        double x2 = ReadCoordFt(data, "x2_mm", "x2M");
        double y2 = ReadCoordFt(data, "y2_mm", "y2M");
        double heightFt = ReadCoordFt(data, "height_mm", "heightM", 3000.0);

        using Transaction tx = new(doc, "AIGRAV - Create Wall (Telemetry)");
        tx.Start();
        Wall wall = Wall.Create(doc, Line.CreateBound(new XYZ(x1, y1, level.Elevation), new XYZ(x2, y2, level.Elevation)),
            wallType.Id, level.Id, heightFt, 0.0, false, false);
        doc.Regenerate();
        tx.Commit();

        return BuildSpatialTelemetryJson(doc, "create_wall", new[] { wall.Id });
    }

    private static string CreateColumnWithTelemetry(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement data = jd.RootElement;

        string typeName = data.TryGetProperty("typeName", out JsonElement tn) ? tn.GetString() ?? "" : "";
        Level? level = ResolveLevel(doc, data);
        if (level == null) return JsonSerializer.Serialize(new { error = "No valid Level found." });

        FamilySymbol? symbol = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .FirstOrDefault(s => string.IsNullOrEmpty(typeName) || s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            ?? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();
        if (symbol == null) return JsonSerializer.Serialize(new { error = $"Column type '{typeName}' not found." });

        double x = ReadCoordFt(data, "x_mm", "xM");
        double y = ReadCoordFt(data, "y_mm", "yM");
        double z = ReadCoordFt(data, "z_mm", "zM", FtToMm(level.Elevation));

        using Transaction tx = new(doc, "AIGRAV - Create Column (Telemetry)");
        tx.Start();
        if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
        FamilyInstance inst = doc.Create.NewFamilyInstance(new XYZ(x, y, z), symbol, level, StructuralType.Column);
        doc.Regenerate();
        tx.Commit();

        return BuildSpatialTelemetryJson(doc, "create_column", new[] { inst.Id });
    }

    private static string CreateBeamWithTelemetry(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement data = jd.RootElement;

        string typeName = data.TryGetProperty("typeName", out JsonElement tn) ? tn.GetString() ?? "" : "";
        Level? level = ResolveLevel(doc, data);
        if (level == null) return JsonSerializer.Serialize(new { error = "No valid Level found." });

        FamilySymbol? symbol = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .FirstOrDefault(s => string.IsNullOrEmpty(typeName) || s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            ?? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();
        if (symbol == null) return JsonSerializer.Serialize(new { error = $"Beam type '{typeName}' not found." });

        double x1 = ReadCoordFt(data, "x1_mm", "x1M");
        double y1 = ReadCoordFt(data, "y1_mm", "y1M");
        double x2 = ReadCoordFt(data, "x2_mm", "x2M");
        double y2 = ReadCoordFt(data, "y2_mm", "y2M");
        double z = ReadCoordFt(data, "z_mm", "zM", FtToMm(level.Elevation));

        using Transaction tx = new(doc, "AIGRAV - Create Beam (Telemetry)");
        tx.Start();
        if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
        Line beamLine = Line.CreateBound(new XYZ(x1, y1, z), new XYZ(x2, y2, z));
        FamilyInstance inst = doc.Create.NewFamilyInstance(beamLine, symbol, level, StructuralType.Beam);
        doc.Regenerate();
        tx.Commit();

        return BuildSpatialTelemetryJson(doc, "create_beam", new[] { inst.Id });
    }

    private static string CreateFloorWithTelemetry(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement data = jd.RootElement;

        string typeName = data.TryGetProperty("typeName", out JsonElement tn) ? tn.GetString() ?? "" : "";
        Level? level = ResolveLevel(doc, data);
        if (level == null) return JsonSerializer.Serialize(new { error = "No valid Level found." });

        FloorType? floorType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>()
            .FirstOrDefault(x => string.IsNullOrEmpty(typeName) || x.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            ?? new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().FirstOrDefault();
        if (floorType == null) return JsonSerializer.Serialize(new { error = $"FloorType '{typeName}' not found." });

        if (!data.TryGetProperty("points", out JsonElement ptsEl))
            return JsonSerializer.Serialize(new { error = "Floor requires 'points' array." });

        var pts = ptsEl.EnumerateArray().ToList();
        CurveLoop loop = new();
        for (int i = 0; i < pts.Count; i++)
        {
            var p1 = pts[i];
            var p2 = pts[(i + 1) % pts.Count];
            double x1 = p1.TryGetProperty("x_mm", out JsonElement xm1) ? MmToFt(xm1.GetDouble()) : p1.GetProperty("x").GetDouble() / 0.3048;
            double y1 = p1.TryGetProperty("y_mm", out JsonElement ym1) ? MmToFt(ym1.GetDouble()) : p1.GetProperty("y").GetDouble() / 0.3048;
            double x2 = p2.TryGetProperty("x_mm", out JsonElement xm2) ? MmToFt(xm2.GetDouble()) : p2.GetProperty("x").GetDouble() / 0.3048;
            double y2 = p2.TryGetProperty("y_mm", out JsonElement ym2) ? MmToFt(ym2.GetDouble()) : p2.GetProperty("y").GetDouble() / 0.3048;
            loop.Append(Line.CreateBound(new XYZ(x1, y1, level.Elevation), new XYZ(x2, y2, level.Elevation)));
        }

        using Transaction tx = new(doc, "AIGRAV - Create Floor (Telemetry)");
        tx.Start();
        Floor fl = Floor.Create(doc, new List<CurveLoop> { loop }, floorType.Id, level.Id);
        doc.Regenerate();
        tx.Commit();

        return BuildSpatialTelemetryJson(doc, "create_floor", new[] { fl.Id });
    }

    private static FamilySymbol? ResolveOrLoadWallHostedSymbol(Document doc, string typeName, JsonElement data)
    {
        var cats = new List<BuiltInCategory> { BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows, BuiltInCategory.OST_GenericModel };
        var collector = new FilteredElementCollector(doc)
            .WherePasses(new ElementMulticategoryFilter(cats))
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>();

        var symbol = collector.FirstOrDefault(s => s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                                                || (s.Family?.Name != null && s.Family.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)))
                  ?? collector.FirstOrDefault(s => s.Name.Contains(typeName, StringComparison.OrdinalIgnoreCase)
                                                || (s.Family?.Name != null && s.Family.Name.Contains(typeName, StringComparison.OrdinalIgnoreCase)));

        if (symbol == null)
        {
            string libBase = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US";
            string? rfaPath = null;
            if (typeName.Contains("Casement", StringComparison.OrdinalIgnoreCase) || typeName.StartsWith("J1", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Double", StringComparison.OrdinalIgnoreCase))
            {
                rfaPath = Path.Combine(libBase, "Windows", "M_Window-Casement-Double.rfa");
            }
            else if (typeName.Contains("Fixed", StringComparison.OrdinalIgnoreCase) || typeName.StartsWith("J2", StringComparison.OrdinalIgnoreCase) || typeName.StartsWith("BV", StringComparison.OrdinalIgnoreCase))
            {
                rfaPath = Path.Combine(libBase, "Windows", "M_Window-Fixed.rfa");
            }
            else if (typeName.Contains("Door", StringComparison.OrdinalIgnoreCase) || typeName.StartsWith("P", StringComparison.OrdinalIgnoreCase))
            {
                rfaPath = Path.Combine(libBase, "Doors", "M_Door-Single-Panel.rfa");
                if (!File.Exists(rfaPath))
                    rfaPath = Path.Combine(libBase, "Doors", "M_Single-Flush.rfa");
            }
            else
            {
                string[] searchDirs = new[] { Path.Combine(libBase, "Windows"), Path.Combine(libBase, "Doors") };
                foreach (var dir in searchDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        var file = Directory.GetFiles(dir, "*.rfa")
                            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Contains(typeName, StringComparison.OrdinalIgnoreCase));
                        if (file != null) { rfaPath = file; break; }
                    }
                }
            }

            if (rfaPath != null && File.Exists(rfaPath))
            {
                if (doc.LoadFamily(rfaPath, out Family fam))
                {
                    foreach (ElementId symId in fam.GetFamilySymbolIds())
                    {
                        var s = doc.GetElement(symId) as FamilySymbol;
                        if (s != null)
                        {
                            if (symbol == null) symbol = s;
                            if (s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                            {
                                symbol = s;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    string famName = Path.GetFileNameWithoutExtension(rfaPath);
                    var existingFam = new FilteredElementCollector(doc)
                        .OfClass(typeof(Family))
                        .Cast<Family>()
                        .FirstOrDefault(f => f.Name.Equals(famName, StringComparison.OrdinalIgnoreCase));
                    if (existingFam != null)
                    {
                        foreach (ElementId symId in existingFam.GetFamilySymbolIds())
                        {
                            var s = doc.GetElement(symId) as FamilySymbol;
                            if (s != null)
                            {
                                if (symbol == null) symbol = s;
                                if (s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                                {
                                    symbol = s;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (symbol == null) return null;

        double wMm = data.TryGetProperty("width_mm", out JsonElement wEl) ? wEl.GetDouble() : 0;
        double hMm = data.TryGetProperty("height_mm", out JsonElement hEl) ? hEl.GetDouble() : 0;

        if (wMm == 0 && hMm == 0 && typeName.Contains("x"))
        {
            var parts = typeName.Replace("mm", "").Trim().Split('x');
            if (parts.Length == 2 && double.TryParse(parts[0].Trim(), out double pw) && double.TryParse(parts[1].Trim(), out double ph))
            {
                wMm = pw;
                hMm = ph;
            }
        }

        if (wMm > 0 && hMm > 0)
        {
            string newTypeName = $"{symbol.Family?.Name ?? "Window"} - {(int)wMm}x{(int)hMm} mm";
            var existingCustom = new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(cats))
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));

            if (existingCustom != null)
            {
                symbol = existingCustom;
            }
            else
            {
                try
                {
                    FamilySymbol dup = (FamilySymbol)symbol.Duplicate(newTypeName);
                    dup.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.Set(MmToFt(wMm));
                    dup.get_Parameter(BuiltInParameter.WINDOW_HEIGHT)?.Set(MmToFt(hMm));
                    dup.get_Parameter(BuiltInParameter.GENERIC_WIDTH)?.Set(MmToFt(wMm));
                    dup.get_Parameter(BuiltInParameter.GENERIC_HEIGHT)?.Set(MmToFt(hMm));
                    dup.LookupParameter("Width")?.Set(MmToFt(wMm));
                    dup.LookupParameter("Height")?.Set(MmToFt(hMm));
                    symbol = dup;
                }
                catch { }
            }
        }

        return symbol;
    }

    public static string CreateWallHostedWithTelemetry(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement data = jd.RootElement;

        string typeName = data.TryGetProperty("typeName", out JsonElement tn) ? tn.GetString() ?? "" : "";
        Level? level = ResolveLevel(doc, data);
        if (level == null) return JsonSerializer.Serialize(new { error = "No valid Level found." });

        double xFt = ReadCoordFt(data, "x_mm", "xM");
        double yFt = ReadCoordFt(data, "y_mm", "yM");
        double zFt = ReadCoordFt(data, "z_mm", "zM", 0.0);
        XYZ loc = new(xFt, yFt, level.Elevation + zFt);

        Wall? hostWall = null;
        if (data.TryGetProperty("wallId", out JsonElement widEl))
        {
            hostWall = doc.GetElement(new ElementId(widEl.GetInt64())) as Wall;
        }

        if (hostWall == null)
        {
            var walls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            double minDist = double.MaxValue;
            foreach (var w in walls)
            {
                if (w.Location is LocationCurve lc)
                {
                    double dist = lc.Curve.Distance(loc);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        hostWall = w;
                    }
                }
            }
        }

        if (hostWall == null)
            return JsonSerializer.Serialize(new { error = "No suitable host wall found for door/window placement." });

        using Transaction tx = new(doc, "AIGRAV - Place Wall Hosted Instance");
        tx.Start();

        FamilySymbol? symbol = ResolveOrLoadWallHostedSymbol(doc, typeName, data);
        if (symbol == null)
        {
            tx.RollBack();
            return JsonSerializer.Serialize(new { error = $"FamilySymbol '{typeName}' not found and could not be loaded." });
        }

        if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
        FamilyInstance inst = doc.Create.NewFamilyInstance(loc, symbol, hostWall, level, StructuralType.NonStructural);
        if (zFt > 0.001)
        {
            Parameter? sillParam = inst.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
            if (sillParam != null && !sillParam.IsReadOnly)
            {
                sillParam.Set(zFt);
            }
        }
        doc.Regenerate();
        tx.Commit();

        return BuildSpatialTelemetryJson(doc, "create_wall_hosted", new[] { inst.Id });
    }

    public static string CreateGableRoofWithTelemetry(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement data = jd.RootElement;

        string typeName = data.TryGetProperty("typeName", out JsonElement tn) ? tn.GetString() ?? "" : "";
        Level? level = ResolveLevel(doc, data);
        if (level == null) return JsonSerializer.Serialize(new { error = "No valid Level found." });

        RoofType? rt = new FilteredElementCollector(doc)
            .OfClass(typeof(RoofType))
            .Cast<RoofType>()
            .FirstOrDefault(r => string.IsNullOrEmpty(typeName) || r.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            ?? new FilteredElementCollector(doc).OfClass(typeof(RoofType)).Cast<RoofType>().FirstOrDefault();

        if (rt == null) return JsonSerializer.Serialize(new { error = "No RoofType available in document." });

        double xMin = data.TryGetProperty("xMin_mm", out JsonElement xm1) ? MmToFt(xm1.GetDouble()) : -0.6 / 0.3048;
        double xMax = data.TryGetProperty("xMax_mm", out JsonElement xm2) ? MmToFt(xm2.GetDouble()) : 6.6 / 0.3048;
        double yMin = data.TryGetProperty("yMin_mm", out JsonElement ym1) ? MmToFt(ym1.GetDouble()) : -0.6 / 0.3048;
        double yMax = data.TryGetProperty("yMax_mm", out JsonElement ym2) ? MmToFt(ym2.GetDouble()) : 6.6 / 0.3048;
        double slopeDeg = data.TryGetProperty("slopeDeg", out JsonElement sd) ? sd.GetDouble() : 30.0;
        double slopeRad = slopeDeg * Math.PI / 180.0;

        XYZ pA = new(xMin, yMin, level.Elevation);
        XYZ pB = new(xMax, yMin, level.Elevation);
        XYZ pC = new(xMax, yMax, level.Elevation);
        XYZ pD = new(xMin, yMax, level.Elevation);

        CurveArray footprint = new();
        footprint.Append(Line.CreateBound(pA, pB));
        footprint.Append(Line.CreateBound(pB, pC));
        footprint.Append(Line.CreateBound(pC, pD));
        footprint.Append(Line.CreateBound(pD, pA));

        using Transaction tx = new(doc, "AIGRAV - Create Gable Roof (Telemetry)");
        tx.Start();
        ModelCurveArray mcArray = new();
        FootPrintRoof roof = doc.Create.NewFootPrintRoof(footprint, level, rt, out mcArray);

        foreach (ModelCurve mc in mcArray)
        {
            XYZ dir = (mc.GeometryCurve.GetEndPoint(1) - mc.GeometryCurve.GetEndPoint(0)).Normalize();
            // Edges running along Y-axis slope up towards center ridge!
            if (Math.Abs(dir.Y) > 0.8)
            {
                roof.set_DefinesSlope(mc, true);
                roof.set_SlopeAngle(mc, slopeRad);
            }
            else
            {
                roof.set_DefinesSlope(mc, false);
            }
        }
        doc.Regenerate();
        tx.Commit();

        return BuildSpatialTelemetryJson(doc, "create_gable_roof", new[] { roof.Id });
    }

    public static string InspectRoofCurvesJson(Document doc, string? argument)
    {
        ElementId rId = new(1670662);
        if (!string.IsNullOrWhiteSpace(argument) && long.TryParse(argument.Trim(), out long idVal))
            rId = new ElementId(idVal);

        FootPrintRoof? roof = doc.GetElement(rId) as FootPrintRoof;
        if (roof == null) return JsonSerializer.Serialize(new { error = $"Not a FootPrintRoof: {rId.Value}" });

        var list = new List<object>();
        ModelCurveArrArray mcaa = roof.GetProfiles();
        foreach (ModelCurveArray mca in mcaa)
        {
            foreach (ModelCurve mc in mca)
            {
                XYZ p0 = mc.GeometryCurve.GetEndPoint(0);
                XYZ p1 = mc.GeometryCurve.GetEndPoint(1);
                XYZ dir = (p1 - p0).Normalize();
                bool definesSlope = roof.get_DefinesSlope(mc);
                double slope = definesSlope ? roof.get_SlopeAngle(mc) : 0;
                list.Add(new
                {
                    curveId = mc.Id.Value,
                    p0_mm = new { x = FtToMm(p0.X), y = FtToMm(p0.Y), z = FtToMm(p0.Z) },
                    p1_mm = new { x = FtToMm(p1.X), y = FtToMm(p1.Y), z = FtToMm(p1.Z) },
                    dir = new { x = Math.Round(dir.X, 2), y = Math.Round(dir.Y, 2) },
                    definesSlope,
                    slopeDeg = Math.Round(slope * 180.0 / Math.PI, 1)
                });
            }
        }
        return JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string InspectRoofFacesJson(Document doc, string? argument)
    {
        var wallCreateMethods = typeof(Wall).GetMethods()
            .Where(m => m.Name == "Create")
            .Select(m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})")
            .ToList();
        ElementId rId = new(1670662);
        if (!string.IsNullOrWhiteSpace(argument) && long.TryParse(argument.Trim(), out long idVal))
            rId = new ElementId(idVal);

        Element? roof = doc.GetElement(rId);
        if (roof == null) return JsonSerializer.Serialize(new { error = $"Roof element not found: {rId.Value}" });

        Options opt = new() { DetailLevel = ViewDetailLevel.Fine };
        GeometryElement geom = roof.get_Geometry(opt);
        var faces = new List<object>();
        double totalSolidVolume = 0;
        foreach (GeometryObject obj in geom)
        {
            if (obj is Solid s && s.Volume > 0.0001)
            {
                totalSolidVolume += s.Volume;
                foreach (Face f in s.Faces)
                {
                    if (f is PlanarFace pf)
                    {
                        faces.Add(new
                        {
                            normal = new { x = Math.Round(pf.FaceNormal.X, 2), y = Math.Round(pf.FaceNormal.Y, 2), z = Math.Round(pf.FaceNormal.Z, 2) },
                            area_m2 = Math.Round(pf.Area * 0.092903, 2),
                            origin_mm = new { x = FtToMm(pf.Origin.X), y = FtToMm(pf.Origin.Y), z = FtToMm(pf.Origin.Z) }
                        });
                    }
                }
            }
        }
        return JsonSerializer.Serialize(new
        {
            wallCreateMethods,
            roofId = rId.Value,
            totalVolume_m3 = Math.Round(totalSolidVolume * 0.0283168, 3),
            facesCount = faces.Count,
            faces
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
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
}

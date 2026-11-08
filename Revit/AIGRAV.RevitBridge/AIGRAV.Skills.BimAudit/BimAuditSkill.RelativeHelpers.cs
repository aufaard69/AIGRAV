using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class SpatialAgenticSandboxSkill
{
    public static string GetProjectContextJson(Document doc)
    {
        var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(l => l.Elevation)
            .Select(l => new
            {
                id = l.Id.Value,
                name = l.Name,
                elevation_mm = FtToMm(l.Elevation)
            }).ToList();

        var grids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().ToList();
        var vGrids = new List<(string name, double xMm)>();
        var hGrids = new List<(string name, double yMm)>();

        var gridDetails = new List<object>();
        foreach (var g in grids)
        {
            Curve c = g.Curve;
            XYZ p0 = c.GetEndPoint(0), p1 = c.GetEndPoint(1);
            double dx = Math.Abs(p1.X - p0.X), dy = Math.Abs(p1.Y - p0.Y);
            bool isVertical = dy >= dx;
            if (isVertical) vGrids.Add((g.Name, FtToMm((p0.X + p1.X) * 0.5)));
            else hGrids.Add((g.Name, FtToMm((p0.Y + p1.Y) * 0.5)));

            gridDetails.Add(new
            {
                id = g.Id.Value,
                name = g.Name,
                axis = isVertical ? "Vertical(X-const)" : "Horizontal(Y-const)",
                start_mm = new { x = FtToMm(p0.X), y = FtToMm(p0.Y) },
                end_mm = new { x = FtToMm(p1.X), y = FtToMm(p1.Y) }
            });
        }

        var intersections = new Dictionary<string, object>();
        foreach (var vg in vGrids.Take(15))
            foreach (var hg in hGrids.Take(15))
                intersections[$"{vg.name}-{hg.name}"] = new { x_mm = vg.xMm, y_mm = hg.yMm, z_mm = 0.0 };

        var wallTypes = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>()
            .Select(w => w.Name).Take(20).ToList();
        var floorTypes = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>()
            .Select(f => f.Name).Take(20).ToList();
        var colTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
            .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .Select(s => $"{s.FamilyName} : {s.Name}").Take(25).ToList();
        var beamTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
            .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .Select(s => $"{s.FamilyName} : {s.Name}").Take(25).ToList();

        var payload = new
        {
            project_title = doc.Title,
            active_view = doc.ActiveView?.Name ?? "None",
            levels,
            grids_count = grids.Count,
            grids = gridDetails.Take(40),
            grid_intersections_mm = intersections,
            available_types = new
            {
                wall_types = wallTypes,
                floor_types = floorTypes,
                column_types = colTypes,
                framing_beam_types = beamTypes
            },
            ascii_minimap = BuildPlanAsciiMinimap(doc)
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string CreateGridMatrixJson(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement root = jd.RootElement;

        List<double> xSpacings = new();
        if (root.TryGetProperty("x_spacings_mm", out JsonElement xs) && xs.ValueKind == JsonValueKind.Array)
            foreach (var item in xs.EnumerateArray()) xSpacings.Add(item.GetDouble());

        List<double> ySpacings = new();
        if (root.TryGetProperty("y_spacings_mm", out JsonElement ys) && ys.ValueKind == JsonValueKind.Array)
            foreach (var item in ys.EnumerateArray()) ySpacings.Add(item.GetDouble());

        double originX = root.TryGetProperty("origin_x_mm", out JsonElement ox) ? ox.GetDouble() : 0.0;
        double originY = root.TryGetProperty("origin_y_mm", out JsonElement oy) ? oy.GetDouble() : 0.0;

        List<double> xCoords = new() { originX };
        foreach (double sp in xSpacings) xCoords.Add(xCoords[^1] + sp);

        List<double> yCoords = new() { originY };
        foreach (double sp in ySpacings) yCoords.Add(yCoords[^1] + sp);

        double minX = xCoords.Min() - 2000.0, maxX = xCoords.Max() + 2000.0;
        double minY = yCoords.Min() - 2000.0, maxY = yCoords.Max() + 2000.0;

        List<ElementId> createdIds = new();
        List<string> xLabels = new();
        List<string> yLabels = new();

        using Transaction tx = new(doc, "AIGRAV - Create Grid Matrix");
        tx.Start();

        for (int i = 0; i < xCoords.Count; i++)
        {
            double xMm = xCoords[i];
            string label = ((char)('A' + i)).ToString();
            Grid g = Grid.Create(doc, Line.CreateBound(new XYZ(MmToFt(xMm), MmToFt(minY), 0), new XYZ(MmToFt(xMm), MmToFt(maxY), 0)));
            try { g.Name = label; } catch { label = g.Name; }
            xLabels.Add(label);
            createdIds.Add(g.Id);
        }

        for (int j = 0; j < yCoords.Count; j++)
        {
            double yMm = yCoords[j];
            string label = (j + 1).ToString();
            Grid g = Grid.Create(doc, Line.CreateBound(new XYZ(MmToFt(minX), MmToFt(yMm), 0), new XYZ(MmToFt(maxX), MmToFt(yMm), 0)));
            try { g.Name = label; } catch { label = g.Name; }
            yLabels.Add(label);
            createdIds.Add(g.Id);
        }

        doc.Regenerate();
        tx.Commit();

        var intersections = new Dictionary<string, object>();
        for (int i = 0; i < xCoords.Count; i++)
            for (int j = 0; j < yCoords.Count; j++)
                intersections[$"{xLabels[i]}-{yLabels[j]}"] = new { x = xCoords[i], y = yCoords[j], z = 0.0 };

        return BuildSpatialTelemetryJson(doc, "create_grid_matrix", createdIds, new
        {
            intersection_dictionary_mm = intersections
        });
    }

    public static string PlaceOnTopOfJson(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement root = jd.RootElement;

        long baseIdVal = root.TryGetProperty("base_element_id", out JsonElement b1) ? b1.GetInt64() : root.GetProperty("baseElementId").GetInt64();
        Element? baseEl = doc.GetElement(new ElementId(baseIdVal));
        if (baseEl == null) return JsonSerializer.Serialize(new { error = $"Base element {baseIdVal} not found." });

        BoundingBoxXYZ? baseBb = baseEl.get_BoundingBox(null);
        if (baseBb == null) return JsonSerializer.Serialize(new { error = $"Base element {baseIdVal} has no 3D BoundingBox." });

        string symbolName = root.TryGetProperty("family_symbol", out JsonElement fs) ? fs.GetString() ?? ""
            : (root.TryGetProperty("typeName", out JsonElement tn) ? tn.GetString() ?? "" : "");
        double offXmm = root.TryGetProperty("offset_x_mm", out JsonElement ox) ? ox.GetDouble() : 0.0;
        double offYmm = root.TryGetProperty("offset_y_mm", out JsonElement oy) ? oy.GetDouble() : 0.0;

        double targetXft = (baseBb.Min.X + baseBb.Max.X) * 0.5 + MmToFt(offXmm);
        double targetYft = (baseBb.Min.Y + baseBb.Max.Y) * 0.5 + MmToFt(offYmm);
        double targetMinZft = baseBb.Max.Z;

        FamilySymbol? symbol = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .FirstOrDefault(s => s.Name.Equals(symbolName, StringComparison.OrdinalIgnoreCase) ||
                                 $"{s.FamilyName} : {s.Name}".Equals(symbolName, StringComparison.OrdinalIgnoreCase))
            ?? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();

        if (symbol == null) return JsonSerializer.Serialize(new { error = $"FamilySymbol '{symbolName}' not found." });

        Level? nearestLevel = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(l => Math.Abs(l.Elevation - targetMinZft)).FirstOrDefault();
        if (nearestLevel == null) return JsonSerializer.Serialize(new { error = "No Level in document." });

        bool isCol = symbol.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns;

        using Transaction tx = new(doc, "AIGRAV - Place On Top Of");
        tx.Start();
        if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }

        FamilyInstance inst = doc.Create.NewFamilyInstance(
            new XYZ(targetXft, targetYft, targetMinZft),
            symbol,
            nearestLevel,
            isCol ? StructuralType.Column : StructuralType.NonStructural);

        // Adjust Base Offset if it's a column so its bottom sits flush at targetMinZft
        Parameter? baseOffsetParam = inst.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)
            ?? inst.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
        if (baseOffsetParam != null && !baseOffsetParam.IsReadOnly)
        {
            baseOffsetParam.Set(targetMinZft - nearestLevel.Elevation);
        }

        doc.Regenerate();
        tx.Commit();

        return BuildSpatialTelemetryJson(doc, "place_on_top_of", new[] { inst.Id }, new
        {
            anchor_base_element_id = baseIdVal,
            anchor_top_z_mm = FtToMm(targetMinZft),
            placed_xy_mm = new { x = FtToMm(targetXft), y = FtToMm(targetYft) }
        });
    }

    public static string ConnectElementsJson(Document doc, string? argument)
    {
        using JsonDocument jd = JsonDocument.Parse(argument ?? "{}");
        JsonElement root = jd.RootElement;

        long startId = root.TryGetProperty("start_element_id", out JsonElement s1) ? s1.GetInt64() : root.GetProperty("startElementId").GetInt64();
        long endId = root.TryGetProperty("end_element_id", out JsonElement e1) ? e1.GetInt64() : root.GetProperty("endElementId").GetInt64();
        string framingType = root.TryGetProperty("framing_type", out JsonElement ft) ? ft.GetString() ?? ""
            : (root.TryGetProperty("typeName", out JsonElement tn) ? tn.GetString() ?? "" : "");
        string zAnchor = root.TryGetProperty("z_anchor", out JsonElement za) ? (za.GetString() ?? "Top") : "Top";

        Element? elA = doc.GetElement(new ElementId(startId));
        Element? elB = doc.GetElement(new ElementId(endId));
        if (elA == null || elB == null)
            return JsonSerializer.Serialize(new { error = $"Start ({startId}) or End ({endId}) element not found." });

        BoundingBoxXYZ? bbA = elA.get_BoundingBox(null);
        BoundingBoxXYZ? bbB = elB.get_BoundingBox(null);
        if (bbA == null || bbB == null)
            return JsonSerializer.Serialize(new { error = "Start or End element has no BoundingBox." });

        double zFt = zAnchor.Equals("Bottom", StringComparison.OrdinalIgnoreCase)
            ? Math.Min(bbA.Min.Z, bbB.Min.Z)
            : (zAnchor.Equals("Centroid", StringComparison.OrdinalIgnoreCase)
                ? ((bbA.Min.Z + bbA.Max.Z) * 0.5 + (bbB.Min.Z + bbB.Max.Z) * 0.5) * 0.5
                : Math.Max(bbA.Max.Z, bbB.Max.Z));

        XYZ ptA = new((bbA.Min.X + bbA.Max.X) * 0.5, (bbA.Min.Y + bbA.Max.Y) * 0.5, zFt);
        XYZ ptB = new((bbB.Min.X + bbB.Max.X) * 0.5, (bbB.Min.Y + bbB.Max.Y) * 0.5, zFt);

        Level? level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(l => Math.Abs(l.Elevation - zFt)).FirstOrDefault();
        if (level == null) return JsonSerializer.Serialize(new { error = "No Level found." });

        FamilySymbol? symbol = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
            .FirstOrDefault(s => string.IsNullOrEmpty(framingType) ||
                                 s.Name.Equals(framingType, StringComparison.OrdinalIgnoreCase) ||
                                 $"{s.FamilyName} : {s.Name}".Equals(framingType, StringComparison.OrdinalIgnoreCase))
            ?? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();

        if (symbol == null) return JsonSerializer.Serialize(new { error = $"Framing symbol '{framingType}' not found." });

        using Transaction tx = new(doc, "AIGRAV - Connect Elements");
        tx.Start();
        if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
        FamilyInstance beam = doc.Create.NewFamilyInstance(Line.CreateBound(ptA, ptB), symbol, level, StructuralType.Beam);
        doc.Regenerate();
        tx.Commit();

        return BuildSpatialTelemetryJson(doc, "connect_elements", new[] { beam.Id }, new
        {
            start_element_id = startId,
            end_element_id = endId,
            start_point_mm = new { x = FtToMm(ptA.X), y = FtToMm(ptA.Y), z = FtToMm(ptA.Z) },
            end_point_mm = new { x = FtToMm(ptB.X), y = FtToMm(ptB.Y), z = FtToMm(ptB.Z) },
            length_mm = FtToMm(ptA.DistanceTo(ptB))
        });
    }
}

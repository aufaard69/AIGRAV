using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;

namespace AIGRAV.RevitBridge;

/// <summary>
/// Engine for converting dumb DirectShape solids (from IFC or CAD) into pristine Native Revit BIM elements.
/// Directly enforces AIGRAV's mandate against DirectShape usage by providing an automated native conversion pipeline.
/// </summary>
public static class IfcRebuildEngine
{
    public static string AnalyzeRebuildability(Document doc, string? argument)
    {
        try
        {
            var directShapes = IfcGeometryHelper.GetDirectShapes(doc);
            int wallCandidates = 0, floorCandidates = 0, columnCandidates = 0, beamCandidates = 0, unknownCount = 0;

            foreach (var ds in directShapes)
            {
                if (IfcGeometryHelper.ExtractWallProfile(ds) != null) wallCandidates++;
                else if (IfcGeometryHelper.ExtractColumnProfile(ds) != null) columnCandidates++;
                else if (IfcGeometryHelper.ExtractBeamProfile(ds) != null) beamCandidates++;
                else
                {
                    var solids = IfcGeometryHelper.GetSolids(ds);
                    if (solids.Any(s => IfcGeometryHelper.ExtractBottomFootprint(s) != null)) floorCandidates++;
                    else unknownCount++;
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                totalDirectShapes = directShapes.Count,
                wallCandidates,
                floorCandidates,
                columnCandidates,
                beamCandidates,
                unknownCount,
                message = $"Analyzed {directShapes.Count} DirectShapes: {wallCandidates} walls, {floorCandidates} floors, {columnCandidates} columns, {beamCandidates} beams."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }

    public static string RebuildWalls(Document doc, string? argument)
    {
        try
        {
            var directShapes = IfcGeometryHelper.GetDirectShapes(doc, BuiltInCategory.OST_Walls);
            if (directShapes.Count == 0)
                directShapes = IfcGeometryHelper.GetDirectShapes(doc);

            var defaultWallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

            if (defaultWallType == null)
                return JsonSerializer.Serialize(new { success = false, error = "No WallType found in project." });

            var createdIds = new List<long>();
            var convertedDsIds = new List<long>();

            using var tx = new Transaction(doc, "AIGRAV Cortex: Rebuild Walls from DirectShape");
            var capture = TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();

            foreach (var ds in directShapes)
            {
                var profile = IfcGeometryHelper.ExtractWallProfile(ds);
                if (profile == null) continue;

                var level = CortexModelingToolkit.FindNearestLevel(doc, profile.BaseElevation);
                if (level == null) continue;

                var line = Line.CreateBound(profile.StartPoint, profile.EndPoint);
                double baseOffset = profile.BaseElevation - level.Elevation;

                try
                {
                    var wall = Wall.Create(doc, line, defaultWallType.Id, level.Id, profile.Height, baseOffset, false, false);
                    if (wall != null)
                    {
                        createdIds.Add(wall.Id.Value);
                        convertedDsIds.Add(ds.Id.Value);
                        doc.Delete(ds.Id);
                    }
                }
                catch
                {
                    // Continue with next element
                }
            }

            tx.Commit();

            return JsonSerializer.Serialize(new
            {
                success = createdIds.Count > 0,
                rebuiltWallCount = createdIds.Count,
                createdElementIds = createdIds,
                message = $"Successfully rebuilt {createdIds.Count} native Revit walls from DirectShapes."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }

    public static string RebuildFloors(Document doc, string? argument)
    {
        try
        {
            var directShapes = IfcGeometryHelper.GetDirectShapes(doc, BuiltInCategory.OST_Floors);
            if (directShapes.Count == 0)
                directShapes = IfcGeometryHelper.GetDirectShapes(doc);

            var floorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .OfCategory(BuiltInCategory.OST_Floors)
                .Cast<FloorType>()
                .FirstOrDefault();

            if (floorType == null)
                return JsonSerializer.Serialize(new { success = false, error = "No FloorType found in project." });

            var createdIds = new List<long>();

            using var tx = new Transaction(doc, "AIGRAV Cortex: Rebuild Floors from DirectShape");
            var capture = TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();

            foreach (var ds in directShapes)
            {
                var solids = IfcGeometryHelper.GetSolids(ds);
                foreach (var solid in solids)
                {
                    var loop = IfcGeometryHelper.ExtractBottomFootprint(solid);
                    if (loop == null) continue;

                    var bb = ds.get_BoundingBox(null);
                    double z = bb != null ? bb.Min.Z : 0;
                    var level = CortexModelingToolkit.FindNearestLevel(doc, z);
                    if (level == null) continue;

                    try
                    {
                        var floor = Floor.Create(doc, new List<CurveLoop> { loop }, floorType.Id, level.Id);
                        if (floor != null)
                        {
                            createdIds.Add(floor.Id.Value);
                            doc.Delete(ds.Id);
                            break;
                        }
                    }
                    catch
                    {
                        // Continue
                    }
                }
            }

            tx.Commit();

            return JsonSerializer.Serialize(new
            {
                success = createdIds.Count > 0,
                rebuiltFloorCount = createdIds.Count,
                createdElementIds = createdIds,
                message = $"Successfully rebuilt {createdIds.Count} native Revit floors from DirectShapes."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.ToString() });
        }
    }
}

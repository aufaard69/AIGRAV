using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;

namespace AIGRAV.Skills.RebarExtension.Engine;

public sealed class ExtractedBeamInfo
{
    public Element Host { get; set; } = null!;
    public XYZ Origin { get; set; } = XYZ.Zero;
    public XYZ AxisX { get; set; } = XYZ.BasisX;   // Longitudinal start -> end
    public XYZ AxisY { get; set; } = XYZ.BasisY;   // Transverse horizontal
    public XYZ AxisZ { get; set; } = XYZ.BasisZ;   // Local Up (+Z)
    public double TotalLengthMm { get; set; }
    public double WidthMm { get; set; }
    public double DepthMm { get; set; }
    public double TopZMm { get; set; }
    public double BottomZMm { get; set; }
    public List<double> SupportStationsMm { get; set; } = new();
    public List<double> SupportWidthsMm { get; set; } = new();
    public string LevelName { get; set; } = string.Empty;
}

public sealed class ExtractedFloorInfo
{
    public Element Host { get; set; } = null!;
    public double TopZMm { get; set; }
    public double BottomZMm { get; set; }
    public double ThicknessMm { get; set; }
    public List<XYZ> OuterBoundaryFeet { get; set; } = new();
    public List<List<XYZ>> OpeningLoopsFeet { get; set; } = new();
    public XYZ AxisX { get; set; } = XYZ.BasisX;
    public XYZ AxisY { get; set; } = XYZ.BasisY;
    public string LevelName { get; set; } = string.Empty;
}

public sealed class ExtractedFoundationInfo
{
    public Element Host { get; set; } = null!;
    public XYZ CenterFeet { get; set; } = XYZ.Zero;
    public XYZ AxisX { get; set; } = XYZ.BasisX;
    public XYZ AxisY { get; set; } = XYZ.BasisY;
    public double LengthXMm { get; set; }
    public double WidthYMm { get; set; }
    public double ThicknessMm { get; set; }
    public double TopZMm { get; set; }
    public double BottomZMm { get; set; }
    public string LevelName { get; set; } = string.Empty;
}

/// <summary>
/// Extracts deterministic host dimensions, local coordinate frames, support intersections,
/// slab void openings, and triangulated 3D concrete preview meshes directly from Revit elements.
/// </summary>
public static class HostGeometryExtractor
{
    public static StructuralHostCategory ClassifyElement(Element? el)
    {
        if (el?.Category == null) return StructuralHostCategory.Unknown;
        long catId = el.Category.Id.Value;
        if (catId == (long)BuiltInCategory.OST_StructuralFraming) return StructuralHostCategory.Beam;
        if (catId == (long)BuiltInCategory.OST_StructuralColumns) return StructuralHostCategory.Column;
        if (catId == (long)BuiltInCategory.OST_StructuralFoundation) return StructuralHostCategory.Foundation;
        if (catId == (long)BuiltInCategory.OST_Floors) return StructuralHostCategory.Floor;
        return StructuralHostCategory.Unknown;
    }

    public static bool CanHostRebar(Element? el) => ClassifyElement(el) != StructuralHostCategory.Unknown;

    public static string GetLevelName(Document doc, Element el)
    {
        try
        {
            if (el.LevelId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(el.LevelId) is Level lvl) return lvl.Name;
            }
            foreach (BuiltInParameter bip in new[]
            {
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.FAMILY_LEVEL_PARAM,
                BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                BuiltInParameter.LEVEL_PARAM
            })
            {
                Parameter? p = el.get_Parameter(bip);
                if (p != null && p.HasValue && p.StorageType == StorageType.ElementId)
                {
                    if (doc.GetElement(p.AsElementId()) is Level lvl) return lvl.Name;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostGeometryExtractor] GetLevelName: {ex.Message}");
        }
        return "Unassigned";
    }

    public static double GetConcreteVolumeM3(Element el)
    {
        Parameter? p = el.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
        if (p != null && p.HasValue)
        {
            // Revit internal volume is cubic feet -> 1 ft^3 = 0.0283168466 m^3
            return Math.Round(p.AsDouble() * 0.028316846592, 4);
        }
        BoundingBoxXYZ? bb = el.get_BoundingBox(null);
        if (bb == null) return 0.0;
        double dx = (bb.Max.X - bb.Min.X) * 0.3048;
        double dy = (bb.Max.Y - bb.Min.Y) * 0.3048;
        double dz = (bb.Max.Z - bb.Min.Z) * 0.3048;
        return Math.Round(Math.Max(0, dx * dy * dz), 4);
    }

    public static ExtractedBeamInfo ExtractBeam(Document doc, Element beam, BeamConfig? overrideCfg = null)
    {
        var info = new ExtractedBeamInfo
        {
            Host = beam,
            LevelName = GetLevelName(doc, beam)
        };

        XYZ p0, p1;
        if (beam.Location is LocationCurve lc)
        {
            p0 = lc.Curve.GetEndPoint(0);
            p1 = lc.Curve.GetEndPoint(1);
        }
        else
        {
            BoundingBoxXYZ bb = beam.get_BoundingBox(null);
            p0 = new XYZ(bb.Min.X, (bb.Min.Y + bb.Max.Y) * 0.5, bb.Max.Z);
            p1 = new XYZ(bb.Max.X, (bb.Min.Y + bb.Max.Y) * 0.5, bb.Max.Z);
        }

        XYZ delta = new(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
        double lenFt = delta.GetLength();
        if (lenFt < 1e-4) lenFt = RebarResourceResolver.MmToFeet(3000.0);

        XYZ axisX = delta.Normalize();
        XYZ upRef = Math.Abs(axisX.DotProduct(XYZ.BasisZ)) < 0.99 ? XYZ.BasisZ : XYZ.BasisX;
        XYZ axisY = upRef.CrossProduct(axisX).Normalize();
        XYZ axisZ = axisX.CrossProduct(axisY).Normalize();

        var (solidOk, solidWidthFt, solidDepthFt, solidTopZFt, solidBotZFt, solidCenterYFt) =
            MeasureBeamSolidSectionFeet(beam, p0, axisX, axisY, axisZ);

        var (paramWidthFt, paramDepthFt) = ReadFamilySectionWidthDepthFeet(doc, beam);
        BoundingBoxXYZ? bbox = beam.get_BoundingBox(null);

        double widthFt;
        double depthFt;
        if (solidOk && solidDepthFt > 0.15 && solidWidthFt > 0.10)
        {
            // Trust actual physical 3D Solid geometry over ambiguous type parameter names!
            widthFt = solidWidthFt;
            depthFt = solidDepthFt;
            info.TopZMm = RebarResourceResolver.FeetToMm(solidTopZFt);
            info.BottomZMm = RebarResourceResolver.FeetToMm(solidBotZFt);
        }
        else
        {
            depthFt = paramDepthFt > 0.15 ? paramDepthFt : (bbox != null ? Math.Max(0.2, bbox.Max.Z - bbox.Min.Z) : RebarResourceResolver.MmToFeet(500));
            widthFt = paramWidthFt > 0.10 ? paramWidthFt : (bbox != null ? Math.Max(0.2, Math.Abs((bbox.Max.X - bbox.Min.X) * axisY.X) + Math.Abs((bbox.Max.Y - bbox.Min.Y) * axisY.Y)) : RebarResourceResolver.MmToFeet(250));
            info.TopZMm = bbox != null ? RebarResourceResolver.FeetToMm(bbox.Max.Z) : RebarResourceResolver.FeetToMm(p0.Z);
            info.BottomZMm = info.TopZMm - RebarResourceResolver.FeetToMm(depthFt);
        }

        info.AxisX = axisX;
        info.AxisY = axisY;
        info.AxisZ = axisZ;
        // Center Origin transversely on true concrete web center & vertically at TopZMm
        XYZ originBase = new(p0.X, p0.Y, RebarResourceResolver.MmToFeet(info.TopZMm));
        info.Origin = solidOk ? originBase + axisY * solidCenterYFt : originBase;
        info.TotalLengthMm = RebarResourceResolver.FeetToMm(lenFt);
        info.WidthMm = RebarResourceResolver.FeetToMm(widthFt);
        info.DepthMm = RebarResourceResolver.FeetToMm(depthFt);

        if (overrideCfg is { ManualSizeOverride: true })
        {
            info.WidthMm = Math.Max(120, overrideCfg.OverrideWidthMm);
            info.DepthMm = Math.Max(200, overrideCfg.OverrideDepthMm);
            info.TotalLengthMm = Math.Max(500, overrideCfg.OverrideLengthMm);
            info.BottomZMm = info.TopZMm - info.DepthMm;
        }

        // Detect ONLY true vertical supports (Columns, Deep Girders, Structural Walls)
        DetectBeamSupportStations(doc, beam, info);
        return info;
    }

    private static (bool Ok, double WidthFt, double DepthFt, double TopZFt, double BotZFt, double CenterYFt)
        MeasureBeamSolidSectionFeet(Element beam, XYZ p0, XYZ axisX, XYZ axisY, XYZ axisZ)
    {
        try
        {
            Options opts = new() { ComputeReferences = false, DetailLevel = ViewDetailLevel.Coarse };
            GeometryElement? geom = beam.get_Geometry(opts);
            var pts = new List<XYZ>();

            if (geom != null)
            {
                foreach (Solid s in EnumerateSolids(geom))
                {
                    if (s.Volume <= 1e-6) continue;
                    foreach (Edge e in s.Edges)
                    {
                        foreach (XYZ v in e.Tessellate()) pts.Add(v);
                    }
                }
            }

            if (pts.Count < 4 && beam is FamilyInstance fi)
            {
                GeometryElement? orig = fi.GetOriginalGeometry(opts);
                Transform tr = fi.GetTransform();
                if (orig != null)
                {
                    foreach (Solid s in EnumerateSolids(orig))
                    {
                        if (s.Volume <= 1e-6) continue;
                        foreach (Edge e in s.Edges)
                        {
                            foreach (XYZ v in e.Tessellate()) pts.Add(tr.OfPoint(v));
                        }
                    }
                }
            }

            if (pts.Count < 4) return (false, 0, 0, 0, 0, 0);

            double minZ = double.MaxValue, maxZ = double.MinValue;
            var localPts = new List<(double Y, double Z, double WorldZ)>(pts.Count);
            foreach (XYZ v in pts)
            {
                XYZ rel = v - p0;
                double ly = rel.DotProduct(axisY);
                double lz = rel.DotProduct(axisZ);
                localPts.Add((ly, lz, v.Z));
                if (v.Z < minZ) minZ = v.Z;
                if (v.Z > maxZ) maxZ = v.Z;
            }

            double depth = maxZ - minZ;
            if (depth <= 0.15) return (false, 0, 0, 0, 0, 0);

            // Measure web width strictly in lower 65% of beam depth to ignore top slab flange joins!
            double webCutZ = minZ + depth * 0.65;
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (var p in localPts)
            {
                if (p.WorldZ <= webCutZ + 1e-4)
                {
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                }
            }
            if (minY > maxY)
            {
                minY = localPts.Min(p => p.Y);
                maxY = localPts.Max(p => p.Y);
            }

            double width = maxY - minY;
            double centerY = (minY + maxY) * 0.5;
            return (width > 0.10 && depth > 0.15, width, depth, maxZ, minZ, centerY);
        }
        catch
        {
            return (false, 0, 0, 0, 0, 0);
        }
    }

    private static void DetectBeamSupportStations(Document doc, Element beam, ExtractedBeamInfo info)
    {
        var stations = new List<(double CenterMm, double WidthMm)>();
        BoundingBoxXYZ? bb = beam.get_BoundingBox(null);
        double beamBotFt = RebarResourceResolver.MmToFeet(info.BottomZMm);

        if (bb != null)
        {
            Outline outline = new(
                new XYZ(bb.Min.X - 0.35, bb.Min.Y - 0.35, bb.Min.Z - 0.35),
                new XYZ(bb.Max.X + 0.35, bb.Max.Y + 0.35, bb.Max.Z + 0.25));

            var cats = new ElementMulticategoryFilter(new[]
            {
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Walls
            });

            var hits = new FilteredElementCollector(doc)
                .WherePasses(cats)
                .WherePasses(new BoundingBoxIntersectsFilter(outline))
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var h in hits)
            {
                if (h.Id == beam.Id) continue;
                BoundingBoxXYZ? hBb = h.get_BoundingBox(null);
                if (hBb == null) continue;

                long catId = h.Category?.Id.Value ?? 0;
                if (catId == (long)BuiltInCategory.OST_StructuralColumns)
                {
                    // Must actually reach below the beam soffit to support it!
                    if (hBb.Min.Z > beamBotFt - 0.05) continue;
                }
                else if (catId == (long)BuiltInCategory.OST_StructuralFraming)
                {
                    if (h.Location is LocationCurve hLc)
                    {
                        XYZ hDir = (hLc.Curve.GetEndPoint(1) - hLc.Curve.GetEndPoint(0)).Normalize();
                        if (Math.Abs(hDir.DotProduct(info.AxisX)) > 0.55) continue; // Ignore parallel/skewed secondary framing
                    }
                    // Ignore shallow joists resting near the top; only deeper/equal primary girders act as supports
                    if (hBb.Min.Z > beamBotFt + 0.08) continue;
                }
                else if (catId == (long)BuiltInCategory.OST_Walls)
                {
                    if (h is Wall w && (w.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT)?.AsInteger() != 1 || w.StructuralUsage == Autodesk.Revit.DB.Structure.StructuralWallUsage.NonBearing)) continue;
                    if (hBb.Min.Z > beamBotFt - 0.05) continue;
                }

                XYZ hCenter = (hBb.Min + hBb.Max) * 0.5;
                double projFeet = (hCenter - info.Origin).DotProduct(info.AxisX);
                double projMm = RebarResourceResolver.FeetToMm(projFeet);

                if (projMm < -350.0 || projMm > info.TotalLengthMm + 350.0) continue;

                double suppWidthFt =
                    Math.Abs((hBb.Max.X - hBb.Min.X) * info.AxisX.X) +
                    Math.Abs((hBb.Max.Y - hBb.Min.Y) * info.AxisX.Y);
                double suppWidthMm = Math.Clamp(RebarResourceResolver.FeetToMm(suppWidthFt), 200.0, 900.0);

                if (!stations.Any(s => Math.Abs(s.CenterMm - projMm) < 350.0))
                {
                    stations.Add((Math.Clamp(projMm, 0.0, info.TotalLengthMm), suppWidthMm));
                }
            }
        }

        if (!stations.Any(s => s.CenterMm < 450.0))
            stations.Add((0.0, 350.0));
        if (!stations.Any(s => Math.Abs(s.CenterMm - info.TotalLengthMm) < 450.0))
            stations.Add((info.TotalLengthMm, 350.0));

        stations = stations.OrderBy(s => s.CenterMm).ToList();
        info.SupportStationsMm = stations.Select(s => s.CenterMm).ToList();
        info.SupportWidthsMm = stations.Select(s => s.WidthMm).ToList();
    }

    public static ColumnSegmentModel ExtractColumnSegment(Document doc, Element col, ColumnConfig? overrideCfg = null)
    {
        BoundingBoxXYZ bb = col.get_BoundingBox(null);
        XYZ center = col.Location is LocationPoint lp
            ? lp.Point
            : (bb != null ? (bb.Min + bb.Max) * 0.5 : XYZ.Zero);

        var (wFt, dFt) = ReadFamilySectionWidthDepthFeet(doc, col);
        double baseZMm = bb != null ? RebarResourceResolver.FeetToMm(bb.Min.Z) : 0.0;
        double topZMm = bb != null ? RebarResourceResolver.FeetToMm(bb.Max.Z) : 4000.0;

        // Cross-check column cross section directly against Solid XY extents in mid-height 20..80%
        if (bb != null)
        {
            Options opts = new() { ComputeReferences = false, DetailLevel = ViewDetailLevel.Coarse };
            GeometryElement? geom = col.get_Geometry(opts);
            double zLow = bb.Min.Z + (bb.Max.Z - bb.Min.Z) * 0.2;
            double zHigh = bb.Min.Z + (bb.Max.Z - bb.Min.Z) * 0.8;
            double sMinX = double.MaxValue, sMaxX = double.MinValue, sMinY = double.MaxValue, sMaxY = double.MinValue;
            if (geom != null)
            {
                foreach (Solid s in EnumerateSolids(geom))
                {
                    if (s.Volume <= 1e-6) continue;
                    foreach (Edge e in s.Edges)
                    {
                        foreach (XYZ v in e.Tessellate())
                        {
                            if (v.Z >= zLow && v.Z <= zHigh)
                            {
                                if (v.X < sMinX) sMinX = v.X; if (v.X > sMaxX) sMaxX = v.X;
                                if (v.Y < sMinY) sMinY = v.Y; if (v.Y > sMaxY) sMaxY = v.Y;
                            }
                        }
                    }
                }
            }
            if (sMaxX > sMinX + 0.15 && sMaxY > sMinY + 0.15)
            {
                wFt = sMaxX - sMinX;
                dFt = sMaxY - sMinY;
            }
            else
            {
                if (wFt <= 0.10) wFt = Math.Max(0.5, bb.Max.X - bb.Min.X);
                if (dFt <= 0.10) dFt = Math.Max(0.5, bb.Max.Y - bb.Min.Y);
            }
        }
        else
        {
            if (wFt <= 0.10) wFt = RebarResourceResolver.MmToFeet(400);
            if (dFt <= 0.10) dFt = RebarResourceResolver.MmToFeet(400);
        }
        double widthMm = RebarResourceResolver.FeetToMm(wFt);
        double depthMm = RebarResourceResolver.FeetToMm(dFt);

        if (overrideCfg is { ManualSizeOverride: true })
        {
            widthMm = Math.Max(150, overrideCfg.OverrideWidthMm);
            depthMm = Math.Max(150, overrideCfg.OverrideDepthMm);
            topZMm = baseZMm + Math.Max(1000, overrideCfg.OverrideHeightMm);
        }

        // Scan intersecting beams / slabs along column height so uncut multi-story columns still receive l_o confining ties
        var interZ = new List<double>();
        if (bb != null)
        {
            Outline outline = new(
                new XYZ(bb.Min.X - 0.15, bb.Min.Y - 0.15, bb.Min.Z + 0.5),
                new XYZ(bb.Max.X + 0.15, bb.Max.Y + 0.15, bb.Max.Z - 0.5));
            var interCats = new ElementMulticategoryFilter(new[]
            {
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFraming
            });
            foreach (var h in new FilteredElementCollector(doc)
                .WherePasses(interCats)
                .WherePasses(new BoundingBoxIntersectsFilter(outline))
                .WhereElementIsNotElementType()
                .ToElements())
            {
                BoundingBoxXYZ? hBb = h.get_BoundingBox(null);
                if (hBb == null) continue;
                double hz = RebarResourceResolver.FeetToMm(hBb.Max.Z);
                if (hz > baseZMm + 400.0 && hz < topZMm - 400.0 && !interZ.Any(x => Math.Abs(x - hz) < 200.0))
                    interZ.Add(hz);
            }
        }

        return new ColumnSegmentModel
        {
            ElementId = col.Id.Value,
            LevelName = GetLevelName(doc, col),
            FamilyAndType = doc.GetElement(col.GetTypeId())?.Name ?? col.Name ?? "Column",
            CenterXMm = RebarResourceResolver.FeetToMm(center.X),
            CenterYMm = RebarResourceResolver.FeetToMm(center.Y),
            BaseZMm = baseZMm,
            TopZMm = topZMm,
            WidthXMm = widthMm,
            DepthYMm = depthMm,
            IntersectingFloorOrBeamTopZMm = interZ.OrderBy(z => z).ToList()
        };
    }

    public static DetectedFootingSupport? FindSupportingFoundation(Document doc, ColumnSegmentModel lowestCol)
    {
        double colXFt = RebarResourceResolver.MmToFeet(lowestCol.CenterXMm);
        double colYFt = RebarResourceResolver.MmToFeet(lowestCol.CenterYMm);
        double colZFt = RebarResourceResolver.MmToFeet(lowestCol.BaseZMm);

        Outline searchBox = new(
            new XYZ(colXFt - 1.5, colYFt - 1.5, colZFt - 5.0),
            new XYZ(colXFt + 1.5, colYFt + 1.5, colZFt + 1.0));

        var founds = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFoundation)
            .WherePasses(new BoundingBoxIntersectsFilter(searchBox))
            .WhereElementIsNotElementType()
            .ToElements();

        Element? best = null;
        double bestDist = double.MaxValue;
        foreach (var f in founds)
        {
            BoundingBoxXYZ? bb = f.get_BoundingBox(null);
            if (bb == null) continue;
            if (colXFt >= bb.Min.X - 0.5 && colXFt <= bb.Max.X + 0.5 &&
                colYFt >= bb.Min.Y - 0.5 && colYFt <= bb.Max.Y + 0.5)
            {
                double dist = Math.Abs(bb.Max.Z - colZFt);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = f;
                }
            }
        }

        if (best == null) return null;
        BoundingBoxXYZ fBb = best.get_BoundingBox(null);
        return new DetectedFootingSupport
        {
            FoundationElementId = best.Id.Value,
            FamilyAndType = doc.GetElement(best.GetTypeId())?.Name ?? best.Name ?? "Foundation",
            TopZMm = RebarResourceResolver.FeetToMm(fBb.Max.Z),
            BottomZMm = RebarResourceResolver.FeetToMm(fBb.Min.Z)
        };
    }

    public static ExtractedFoundationInfo ExtractFoundation(Document doc, Element foundation, FoundationConfig? overrideCfg = null)
    {
        BoundingBoxXYZ bb = foundation.get_BoundingBox(null);
        XYZ center = (bb.Min + bb.Max) * 0.5;
        double lx = RebarResourceResolver.FeetToMm(bb.Max.X - bb.Min.X);
        double ly = RebarResourceResolver.FeetToMm(bb.Max.Y - bb.Min.Y);
        double lz = RebarResourceResolver.FeetToMm(bb.Max.Z - bb.Min.Z);
        double topZ = RebarResourceResolver.FeetToMm(bb.Max.Z);

        if (overrideCfg is { ManualSizeOverride: true })
        {
            lx = Math.Max(400, overrideCfg.OverrideLengthXMm);
            ly = Math.Max(400, overrideCfg.OverrideWidthYMm);
            lz = Math.Max(200, overrideCfg.OverrideThicknessMm);
        }

        return new ExtractedFoundationInfo
        {
            Host = foundation,
            CenterFeet = center,
            AxisX = XYZ.BasisX,
            AxisY = XYZ.BasisY,
            LengthXMm = lx,
            WidthYMm = ly,
            ThicknessMm = lz,
            TopZMm = topZ,
            BottomZMm = topZ - lz,
            LevelName = GetLevelName(doc, foundation)
        };
    }

    public static ExtractedFloorInfo ExtractFloor(Document doc, Element floor)
    {
        var info = new ExtractedFloorInfo
        {
            Host = floor,
            LevelName = GetLevelName(doc, floor)
        };

        BoundingBoxXYZ bb = floor.get_BoundingBox(null);
        if (bb != null)
        {
            info.TopZMm = RebarResourceResolver.FeetToMm(bb.Max.Z);
            info.BottomZMm = RebarResourceResolver.FeetToMm(bb.Min.Z);
            info.ThicknessMm = Math.Max(80.0, info.TopZMm - info.BottomZMm);
        }

        // Extract top horizontal planar face to obtain exact outer perimeter AND interior void openings!
        Options opts = new() { ComputeReferences = false, DetailLevel = ViewDetailLevel.Coarse };
        GeometryElement geom = floor.get_Geometry(opts);
        PlanarFace? topFace = null;
        double highestZ = double.MinValue;

        if (geom != null)
        {
            foreach (Solid solid in EnumerateSolids(geom))
            {
                if (solid.Volume <= 1e-6) continue;
                foreach (Face f in solid.Faces)
                {
                    if (f is PlanarFace pf && pf.FaceNormal.Z > 0.95 && pf.Origin.Z > highestZ)
                    {
                        highestZ = pf.Origin.Z;
                        topFace = pf;
                    }
                }
            }
        }

        if (topFace != null)
        {
            var loops = topFace.GetEdgesAsCurveLoops()
                .Select(cl => (Loop: SampleCurveLoop(cl), Area: ComputePolygonArea2D(SampleCurveLoop(cl))))
                .Where(x => x.Loop.Count >= 3)
                .OrderByDescending(x => Math.Abs(x.Area))
                .ToList();

            if (loops.Count > 0)
            {
                info.OuterBoundaryFeet = loops[0].Loop;
                for (int i = 1; i < loops.Count; i++)
                    info.OpeningLoopsFeet.Add(loops[i].Loop);
            }
        }

        if (info.OuterBoundaryFeet.Count < 3 && bb != null)
        {
            double z = bb.Max.Z;
            info.OuterBoundaryFeet = new List<XYZ>
            {
                new(bb.Min.X, bb.Min.Y, z),
                new(bb.Max.X, bb.Min.Y, z),
                new(bb.Max.X, bb.Max.Y, z),
                new(bb.Min.X, bb.Max.Y, z)
            };
        }

        return info;
    }

    public static ConcreteMeshPreviewDto ExtractConcreteMeshForViewport(
        Element el,
        StructuralHostCategory category,
        string label)
    {
        var dto = new ConcreteMeshPreviewDto
        {
            ElementId = el.Id.Value,
            Category = category,
            Label = label
        };

        Options opts = new() { ComputeReferences = false, DetailLevel = ViewDetailLevel.Medium };
        GeometryElement? geom = el.get_Geometry(opts);
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        if (geom != null)
        {
            foreach (Solid solid in EnumerateSolids(geom))
            {
                if (solid.Volume <= 1e-7) continue;
                foreach (Face face in solid.Faces)
                {
                    Mesh mesh = face.Triangulate(0.35);
                    int baseIdx = dto.VerticesMm.Count;
                    for (int i = 0; i < mesh.Vertices.Count; i++)
                    {
                        XYZ v = mesh.Vertices[i];
                        double xm = RebarResourceResolver.FeetToMm(v.X);
                        double ym = RebarResourceResolver.FeetToMm(v.Y);
                        double zm = RebarResourceResolver.FeetToMm(v.Z);
                        dto.VerticesMm.Add(new Point3DData(xm, ym, zm));
                        minX = Math.Min(minX, xm); minY = Math.Min(minY, ym); minZ = Math.Min(minZ, zm);
                        maxX = Math.Max(maxX, xm); maxY = Math.Max(maxY, ym); maxZ = Math.Max(maxZ, zm);
                    }
                    for (int t = 0; t < mesh.NumTriangles; t++)
                    {
                        MeshTriangle tri = mesh.get_Triangle(t);
                        dto.TriangleIndices.Add(baseIdx + (int)tri.get_Index(0));
                        dto.TriangleIndices.Add(baseIdx + (int)tri.get_Index(1));
                        dto.TriangleIndices.Add(baseIdx + (int)tri.get_Index(2));
                    }
                }
            }
        }

        if (dto.VerticesMm.Count == 0)
        {
            BoundingBoxXYZ? bb = el.get_BoundingBox(null);
            if (bb != null)
            {
                minX = RebarResourceResolver.FeetToMm(bb.Min.X); minY = RebarResourceResolver.FeetToMm(bb.Min.Y); minZ = RebarResourceResolver.FeetToMm(bb.Min.Z);
                maxX = RebarResourceResolver.FeetToMm(bb.Max.X); maxY = RebarResourceResolver.FeetToMm(bb.Max.Y); maxZ = RebarResourceResolver.FeetToMm(bb.Max.Z);
                AddBoxMesh(dto, minX, minY, minZ, maxX, maxY, maxZ);
            }
        }

        dto.MinMm = new Point3DData(minX, minY, minZ);
        dto.MaxMm = new Point3DData(maxX, maxY, maxZ);
        return dto;
    }

    private static void AddBoxMesh(ConcreteMeshPreviewDto dto, double x0, double y0, double z0, double x1, double y1, double z1)
    {
        int b = dto.VerticesMm.Count;
        dto.VerticesMm.AddRange(new[]
        {
            new Point3DData(x0, y0, z0), new Point3DData(x1, y0, z0),
            new Point3DData(x1, y1, z0), new Point3DData(x0, y1, z0),
            new Point3DData(x0, y0, z1), new Point3DData(x1, y0, z1),
            new Point3DData(x1, y1, z1), new Point3DData(x0, y1, z1)
        });
        int[] tris = {
            0,2,1, 0,3,2, 4,5,6, 4,6,7,
            0,1,5, 0,5,4, 2,3,7, 2,7,6,
            1,2,6, 1,6,5, 3,0,4, 3,4,7
        };
        foreach (int i in tris) dto.TriangleIndices.Add(b + i);
    }

    private static (double WidthFt, double DepthFt) ReadFamilySectionWidthDepthFeet(Document doc, Element inst)
    {
        Element? sym = doc.GetElement(inst.GetTypeId());
        double w = 0, d = 0;

        static double ReadLenParam(Element? e, params string[] names)
        {
            if (e == null) return 0;
            foreach (string n in names)
            {
                Parameter? p = e.LookupParameter(n);
                if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                    return p.AsDouble();
            }
            return 0;
        }

        w = ReadLenParam(sym, "b", "Width", "Lebar", "B", "bf");
        if (w <= 0) w = ReadLenParam(inst, "b", "Width", "Lebar");

        d = ReadLenParam(sym, "h", "Height", "Depth", "Tinggi", "H", "d");
        if (d <= 0) d = ReadLenParam(inst, "h", "Height", "Depth");

        return (w, d);
    }

    private static IEnumerable<Solid> EnumerateSolids(GeometryElement geom)
    {
        foreach (GeometryObject obj in geom)
        {
            if (obj is Solid s) yield return s;
            else if (obj is GeometryInstance gi)
            {
                foreach (Solid sub in EnumerateSolids(gi.GetInstanceGeometry()))
                    yield return sub;
            }
        }
    }

    private static List<XYZ> SampleCurveLoop(CurveLoop cl)
    {
        var pts = new List<XYZ>();
        foreach (Curve c in cl)
        {
            var tPts = c.Tessellate();
            if (tPts.Count > 0)
            {
                for (int i = 0; i < tPts.Count - 1; i++)
                    pts.Add(tPts[i]);
            }
        }
        return pts;
    }

    private static double ComputePolygonArea2D(IReadOnlyList<XYZ> pts)
    {
        double area = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            XYZ a = pts[i];
            XYZ b = pts[(i + 1) % pts.Count];
            area += (a.X * b.Y - b.X * a.Y) * 0.5;
        }
        return area;
    }
}

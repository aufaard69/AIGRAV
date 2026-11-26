using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Builders;

/// <summary>
/// Builds two-way Floor Slab reinforcement clipped cleanly to exact floor polygon boundaries
/// AND around detected interior void openings (with optional 45-deg corner trimmer bars),
/// automatically splitting bar runs <= MaxStockLengthMm with 50% staggered splices.
/// </summary>
public static class FloorRebarBuilder
{
    public static List<RebarPlannedBar> BuildPlan(
        ExtractedFloorInfo floor,
        FloorConfig cfg,
        RebarGlobalSettings global)
    {
        var planned = new List<RebarPlannedBar>();
        if (floor.OuterBoundaryFeet.Count < 3) return planned;

        double cover = Math.Max(15.0, cfg.CoverMm);
        double minX = floor.OuterBoundaryFeet.Min(p => RebarResourceResolver.FeetToMm(p.X));
        double maxX = floor.OuterBoundaryFeet.Max(p => RebarResourceResolver.FeetToMm(p.X));
        double minY = floor.OuterBoundaryFeet.Min(p => RebarResourceResolver.FeetToMm(p.Y));
        double maxY = floor.OuterBoundaryFeet.Max(p => RebarResourceResolver.FeetToMm(p.Y));

        double lx = maxX - minX;
        double ly = maxY - minY;
        if (lx < 300.0 || ly < 300.0) return planned;

        var outerMm = floor.OuterBoundaryFeet
            .Select(p => new Point3DData(RebarResourceResolver.FeetToMm(p.X), RebarResourceResolver.FeetToMm(p.Y), 0))
            .ToList();

        var holesMm = cfg.TrimAtVoidOpenings
            ? floor.OpeningLoopsFeet
                .Select(l => (IReadOnlyList<Point3DData>)l
                    .Select(p => new Point3DData(RebarResourceResolver.FeetToMm(p.X), RebarResourceResolver.FeetToMm(p.Y), 0))
                    .ToList())
                .ToList()
            : new List<IReadOnlyList<Point3DData>>();

        double hookZ = cfg.EdgeHook90 ? Math.Max(60.0, floor.ThicknessMm - 2.0 * cover) : 0.0;
        RebarGlobalSettings floorSpliceGlobal = cfg.MeshLayoutMode == FloorMeshLayoutMode.FullExtent
            ? global with { MaxStockLengthMm = Math.Max(global.MaxStockLengthMm, 100000.0) }
            : global;

        static XYZ ToFeet(double xMm, double yMm, double zMm) => new(
            RebarResourceResolver.MmToFeet(xMm),
            RebarResourceResolver.MmToFeet(yMm),
            RebarResourceResolver.MmToFeet(zMm));

        // 1. Bottom X & Y Layers
        double zBotX = floor.BottomZMm + cover + cfg.BottomXDiaMm * 0.5;
        double zBotY = zBotX + (cfg.BottomXDiaMm + cfg.BottomYDiaMm) * 0.5;

        EmitLayerAlongX(planned, floor, outerMm, holesMm, cover,
            minY + cover, maxY - cover, cfg.BottomXSpacingMm, cfg.BottomXDiaMm, zBotX, +hookZ,
            floorSpliceGlobal, "Slab Bottom X", ToFeet);

        EmitLayerAlongY(planned, floor, outerMm, holesMm, cover,
            minX + cover, maxX - cover, cfg.BottomYSpacingMm, cfg.BottomYDiaMm, zBotY, +hookZ,
            floorSpliceGlobal, "Slab Bottom Y", ToFeet);

        // 2. Top X & Y Layers (Full Double Mesh OR Edge Negative Moment Support Strip L/4)
        double zTopX = floor.TopZMm - cover - cfg.TopXDiaMm * 0.5;
        double zTopY = zTopX - (cfg.TopXDiaMm + cfg.TopYDiaMm) * 0.5;

        if (cfg.TopMode == FloorTopReinforcementMode.FullDoubleMesh)
        {
            EmitLayerAlongX(planned, floor, outerMm, holesMm, cover,
                minY + cover, maxY - cover, cfg.TopXSpacingMm, cfg.TopXDiaMm, zTopX, -hookZ,
                floorSpliceGlobal, "Slab Top Full X", ToFeet);

            EmitLayerAlongY(planned, floor, outerMm, holesMm, cover,
                minX + cover, maxX - cover, cfg.TopYSpacingMm, cfg.TopYDiaMm, zTopY, -hookZ,
                floorSpliceGlobal, "Slab Top Full Y", ToFeet);
        }
        else
        {
            double stripX = Math.Clamp(lx * 0.25, 600.0, 2500.0);
            double stripY = Math.Clamp(ly * 0.25, 600.0, 2500.0);

            // Top X negative strips near Left & Right edges
            EmitTopStripX(planned, floor.Host.Id.Value, minX + cover, minX + stripX,
                minY + cover, maxY - cover, cfg.TopXSpacingMm, cfg.TopXDiaMm, zTopX, -hookZ, "Slab Top Strip X-Left", ToFeet);
            EmitTopStripX(planned, floor.Host.Id.Value, maxX - stripX, maxX - cover,
                minY + cover, maxY - cover, cfg.TopXSpacingMm, cfg.TopXDiaMm, zTopX, -hookZ, "Slab Top Strip X-Right", ToFeet);

            // Top Y negative strips near Bottom & Top edges
            EmitTopStripY(planned, floor.Host.Id.Value, minY + cover, minY + stripY,
                minX + cover, maxX - cover, cfg.TopYSpacingMm, cfg.TopYDiaMm, zTopY, -hookZ, "Slab Top Strip Y-Bot", ToFeet);
            EmitTopStripY(planned, floor.Host.Id.Value, maxY - stripY, maxY - cover,
                minX + cover, maxX - cover, cfg.TopYSpacingMm, cfg.TopYDiaMm, zTopY, -hookZ, "Slab Top Strip Y-Top", ToFeet);
        }

        // 3. Opening Corner Diagonal 45 deg Trimmers
        if (cfg.AddOpeningDiagonalTrimmers && floor.OpeningLoopsFeet.Count > 0)
        {
            double trimmerLen = 1200.0;
            foreach (var hole in floor.OpeningLoopsFeet)
            {
                if (hole.Count < 3) continue;
                double hCx = hole.Average(p => RebarResourceResolver.FeetToMm(p.X));
                double hCy = hole.Average(p => RebarResourceResolver.FeetToMm(p.Y));

                foreach (XYZ corner in hole)
                {
                    double vx = RebarResourceResolver.FeetToMm(corner.X);
                    double vy = RebarResourceResolver.FeetToMm(corner.Y);
                    double dirOutX = vx >= hCx ? +1.0 : -1.0;
                    double dirOutY = vy >= hCy ? +1.0 : -1.0;

                    // Diagonal tangent along +/- 45 deg tangent outside opening corner
                    double tx = -dirOutY * 0.7071;
                    double ty = dirOutX * 0.7071;
                    double anchorX = vx + dirOutX * 80.0;
                    double anchorY = vy + dirOutY * 80.0;

                    if (anchorX <= minX + cover || anchorX >= maxX - cover || anchorY <= minY + cover || anchorY >= maxY - cover)
                        continue;

                    for (int tIdx = 0; tIdx < cfg.OpeningTrimmerBarsPerCorner; tIdx++)
                    {
                        double shift = tIdx * 80.0;
                        double px = anchorX + dirOutX * shift;
                        double py = anchorY + dirOutY * shift;
                        var pts = new List<XYZ>
                        {
                            ToFeet(px - tx * trimmerLen * 0.5, py - ty * trimmerLen * 0.5, zTopY - 12.0),
                            ToFeet(px + tx * trimmerLen * 0.5, py + ty * trimmerLen * 0.5, zTopY - 12.0)
                        };
                        planned.Add(new RebarPlannedBar
                        {
                            HostElementId = floor.Host.Id.Value,
                            Role = "Opening Corner Diagonal Trimmer",
                            ShapeCode = "00",
                            DiameterMm = cfg.OpeningTrimmerDiaMm,
                            Style = RebarStyle.Standard,
                            PreferredNormal = XYZ.BasisZ,
                            PolylineCurvesFeet = new List<List<XYZ>> { pts },
                            CutLengthMm = trimmerLen,
                            Quantity = 1,
                            Notes = "45 deg Void corner crack control"
                        });
                    }
                }
            }
        }

        return planned;
    }

    private static void EmitLayerAlongX(
        List<RebarPlannedBar> planned,
        ExtractedFloorInfo floor,
        IReadOnlyList<Point3DData> outerMm,
        IReadOnlyList<IReadOnlyList<Point3DData>> holesMm,
        double cover,
        double yMin,
        double yMax,
        double spacing,
        int diaMm,
        double zMm,
        double edgeHookDz,
        RebarGlobalSettings global,
        string role,
        Func<double, double, double, XYZ> toFeet)
    {
        spacing = Math.Max(75.0, spacing);
        int lineIdx = 0;
        for (double y = yMin; y <= yMax + 1.0; y += spacing)
        {
            double clampedY = Math.Min(y, yMax);
            var intervals = PolygonClipper2D.ClipScanlineAlongX(clampedY, cover, outerMm, holesMm);
            foreach (var iv in intervals)
            {
                var pieces = StockSpliceSolver.Solve1DSplice(
                    iv.StartMm, iv.EndMm, diaMm, Math.Abs(edgeHookDz), Math.Abs(edgeHookDz),
                    false, null, global, lineIdx);
                foreach (var p in pieces)
                {
                    var pts = new List<XYZ>();
                    if (p.HasStartHook && Math.Abs(edgeHookDz) > 10.0)
                        pts.Add(toFeet(p.StartCoordMm, clampedY + p.TransverseOffsetMm * 0.4, zMm + edgeHookDz));
                    pts.Add(toFeet(p.StartCoordMm, clampedY + p.TransverseOffsetMm * 0.4, zMm));
                    pts.Add(toFeet(p.EndCoordMm, clampedY + p.TransverseOffsetMm * 0.4, zMm));
                    if (p.HasEndHook && Math.Abs(edgeHookDz) > 10.0)
                        pts.Add(toFeet(p.EndCoordMm, clampedY + p.TransverseOffsetMm * 0.4, zMm + edgeHookDz));

                    planned.Add(new RebarPlannedBar
                    {
                        HostElementId = floor.Host.Id.Value,
                        Role = role,
                        ShapeCode = (p.HasStartHook || p.HasEndHook) ? "21" : "00",
                        DiameterMm = diaMm,
                        Style = RebarStyle.Standard,
                        PreferredNormal = XYZ.BasisY,
                        PolylineCurvesFeet = new List<List<XYZ>> { pts },
                        CutLengthMm = p.TotalCutLengthMm,
                        Quantity = 1
                    });
                }
            }
            lineIdx++;
        }
    }

    private static void EmitLayerAlongY(
        List<RebarPlannedBar> planned,
        ExtractedFloorInfo floor,
        IReadOnlyList<Point3DData> outerMm,
        IReadOnlyList<IReadOnlyList<Point3DData>> holesMm,
        double cover,
        double xMin,
        double xMax,
        double spacing,
        int diaMm,
        double zMm,
        double edgeHookDz,
        RebarGlobalSettings global,
        string role,
        Func<double, double, double, XYZ> toFeet)
    {
        spacing = Math.Max(75.0, spacing);
        int lineIdx = 0;
        for (double x = xMin; x <= xMax + 1.0; x += spacing)
        {
            double clampedX = Math.Min(x, xMax);
            var intervals = PolygonClipper2D.ClipScanlineAlongY(clampedX, cover, outerMm, holesMm);
            foreach (var iv in intervals)
            {
                var pieces = StockSpliceSolver.Solve1DSplice(
                    iv.StartMm, iv.EndMm, diaMm, Math.Abs(edgeHookDz), Math.Abs(edgeHookDz),
                    false, null, global, lineIdx);
                foreach (var p in pieces)
                {
                    var pts = new List<XYZ>();
                    if (p.HasStartHook && Math.Abs(edgeHookDz) > 10.0)
                        pts.Add(toFeet(clampedX + p.TransverseOffsetMm * 0.4, p.StartCoordMm, zMm + edgeHookDz));
                    pts.Add(toFeet(clampedX + p.TransverseOffsetMm * 0.4, p.StartCoordMm, zMm));
                    pts.Add(toFeet(clampedX + p.TransverseOffsetMm * 0.4, p.EndCoordMm, zMm));
                    if (p.HasEndHook && Math.Abs(edgeHookDz) > 10.0)
                        pts.Add(toFeet(clampedX + p.TransverseOffsetMm * 0.4, p.EndCoordMm, zMm + edgeHookDz));

                    planned.Add(new RebarPlannedBar
                    {
                        HostElementId = floor.Host.Id.Value,
                        Role = role,
                        ShapeCode = (p.HasStartHook || p.HasEndHook) ? "21" : "00",
                        DiameterMm = diaMm,
                        Style = RebarStyle.Standard,
                        PreferredNormal = XYZ.BasisX,
                        PolylineCurvesFeet = new List<List<XYZ>> { pts },
                        CutLengthMm = p.TotalCutLengthMm,
                        Quantity = 1
                    });
                }
            }
            lineIdx++;
        }
    }

    private static void EmitTopStripX(
        List<RebarPlannedBar> planned, long hostId, double x0, double x1, double yMin, double yMax,
        double spacing, int diaMm, double zMm, double hookDz, string role, Func<double, double, double, XYZ> toFeet)
    {
        if (x1 - x0 < 200.0 || yMax - yMin < 200.0) return;
        int qty = Math.Max(2, (int)Math.Floor((yMax - yMin) / Math.Max(75.0, spacing)) + 1);
        var pts = new List<XYZ>();
        if (Math.Abs(hookDz) > 10.0) pts.Add(toFeet(x0, yMin, zMm + hookDz));
        pts.Add(toFeet(x0, yMin, zMm));
        pts.Add(toFeet(x1, yMin, zMm));
        planned.Add(new RebarPlannedBar
        {
            HostElementId = hostId,
            Role = role,
            ShapeCode = Math.Abs(hookDz) > 10.0 ? "11" : "00",
            DiameterMm = diaMm,
            Style = RebarStyle.Standard,
            PreferredNormal = XYZ.BasisY,
            PolylineCurvesFeet = new List<List<XYZ>> { pts },
            LayoutFixedCount = qty,
            ArrayLengthFeet = RebarResourceResolver.MmToFeet(yMax - yMin),
            BarsOnNormalSide = true,
            CutLengthMm = Math.Round((x1 - x0) + Math.Abs(hookDz), 0),
            Quantity = qty
        });
    }

    private static void EmitTopStripY(
        List<RebarPlannedBar> planned, long hostId, double y0, double y1, double xMin, double xMax,
        double spacing, int diaMm, double zMm, double hookDz, string role, Func<double, double, double, XYZ> toFeet)
    {
        if (y1 - y0 < 200.0 || xMax - xMin < 200.0) return;
        int qty = Math.Max(2, (int)Math.Floor((xMax - xMin) / Math.Max(75.0, spacing)) + 1);
        var pts = new List<XYZ>();
        if (Math.Abs(hookDz) > 10.0) pts.Add(toFeet(xMin, y0, zMm + hookDz));
        pts.Add(toFeet(xMin, y0, zMm));
        pts.Add(toFeet(xMin, y1, zMm));
        planned.Add(new RebarPlannedBar
        {
            HostElementId = hostId,
            Role = role,
            ShapeCode = Math.Abs(hookDz) > 10.0 ? "11" : "00",
            DiameterMm = diaMm,
            Style = RebarStyle.Standard,
            PreferredNormal = XYZ.BasisX,
            PolylineCurvesFeet = new List<List<XYZ>> { pts },
            LayoutFixedCount = qty,
            ArrayLengthFeet = RebarResourceResolver.MmToFeet(xMax - xMin),
            BarsOnNormalSide = true,
            CutLengthMm = Math.Round((y1 - y0) + Math.Abs(hookDz), 0),
            Quantity = qty
        });
    }
}

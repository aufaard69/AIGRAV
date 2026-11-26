using System;
using System.Collections.Generic;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Builders;

/// <summary>
/// Builds pure Footplate / Pile Cap foundation reinforcement (Bottom X/Y mat, optional Top X/Y mat,
/// optional perimeter side binder) strictly WITHOUT column starter dowels so BBS schedules never double-count.
/// </summary>
public static class FoundationRebarBuilder
{
    public static List<RebarPlannedBar> BuildPlan(
        ExtractedFoundationInfo pad,
        FoundationConfig cfg,
        RebarGlobalSettings global)
    {
        var planned = new List<RebarPlannedBar>();
        double coverSide = Math.Max(40.0, cfg.CoverMm);
        double coverBot = Math.Max(50.0, cfg.CoverMm);
        double coverTop = Math.Max(40.0, Math.Min(cfg.CoverMm, 50.0));

        double cx = RebarResourceResolver.FeetToMm(pad.CenterFeet.X);
        double cy = RebarResourceResolver.FeetToMm(pad.CenterFeet.Y);

        double minX = cx - pad.LengthXMm * 0.5 + coverSide;
        double maxX = cx + pad.LengthXMm * 0.5 - coverSide;
        double minY = cy - pad.WidthYMm * 0.5 + coverSide;
        double maxY = cy + pad.WidthYMm * 0.5 - coverSide;

        double spanX = maxX - minX;
        double spanY = maxY - minY;
        if (spanX < 150.0 || spanY < 150.0) return planned;

        double hookLegMax = Math.Min(
            Math.Max(150.0, cfg.HookLegMm),
            Math.Max(120.0, pad.ThicknessMm - coverBot - coverTop));

        static XYZ ToFeet(double xMm, double yMm, double zMm) => new(
            RebarResourceResolver.MmToFeet(xMm),
            RebarResourceResolver.MmToFeet(yMm),
            RebarResourceResolver.MmToFeet(zMm));

        // 1. Bottom X Mesh (bars parallel to X, arrayed across Y)
        double zBotX = pad.BottomZMm + coverBot + cfg.BottomXDiaMm * 0.5;
        var botXPieces = StockSpliceSolver.Solve1DSplice(minX, maxX, cfg.BottomXDiaMm,
            cfg.EdgeTermination == EdgeTerminationMode.Straight ? 0 : hookLegMax,
            cfg.EdgeTermination == EdgeTerminationMode.Straight ? 0 : hookLegMax,
            false, null, global, 0);
        int qtyBotX = Math.Max(2, (int)Math.Floor(spanY / Math.Max(50.0, cfg.BottomXSpacingMm)) + 1);

        foreach (var p in botXPieces)
        {
            var pts = BuildPadBarPolylineX(ToFeet, p.StartCoordMm, p.EndCoordMm, minY, zBotX,
                p.HasStartHook ? hookLegMax : 0.0, p.HasEndHook ? hookLegMax : 0.0, up: true);

            planned.Add(new RebarPlannedBar
            {
                HostElementId = pad.Host.Id.Value,
                Role = "Pondasi Bawah Arah-X",
                ShapeCode = cfg.EdgeTermination == EdgeTerminationMode.Straight ? "00" : "21",
                DiameterMm = cfg.BottomXDiaMm,
                Style = RebarStyle.Standard,
                PreferredNormal = XYZ.BasisY,
                PolylineCurvesFeet = new List<List<XYZ>> { pts },
                LayoutFixedCount = qtyBotX,
                ArrayLengthFeet = RebarResourceResolver.MmToFeet(spanY),
                BarsOnNormalSide = true,
                CutLengthMm = p.TotalCutLengthMm,
                Quantity = qtyBotX,
                Notes = $"D{cfg.BottomXDiaMm}-{(int)cfg.BottomXSpacingMm} (Pure Pad)"
            });
        }

        // 2. Bottom Y Mesh (bars parallel to Y, arrayed across X, stacked directly on X mat)
        double zBotY = zBotX + (cfg.BottomXDiaMm + cfg.BottomYDiaMm) * 0.5;
        var botYPieces = StockSpliceSolver.Solve1DSplice(minY, maxY, cfg.BottomYDiaMm,
            cfg.EdgeTermination == EdgeTerminationMode.Straight ? 0 : hookLegMax,
            cfg.EdgeTermination == EdgeTerminationMode.Straight ? 0 : hookLegMax,
            false, null, global, 0);
        int qtyBotY = Math.Max(2, (int)Math.Floor(spanX / Math.Max(50.0, cfg.BottomYSpacingMm)) + 1);

        foreach (var p in botYPieces)
        {
            var pts = BuildPadBarPolylineY(ToFeet, minX, p.StartCoordMm, p.EndCoordMm, zBotY,
                p.HasStartHook ? hookLegMax : 0.0, p.HasEndHook ? hookLegMax : 0.0, up: true);

            planned.Add(new RebarPlannedBar
            {
                HostElementId = pad.Host.Id.Value,
                Role = "Pondasi Bawah Arah-Y",
                ShapeCode = cfg.EdgeTermination == EdgeTerminationMode.Straight ? "00" : "21",
                DiameterMm = cfg.BottomYDiaMm,
                Style = RebarStyle.Standard,
                PreferredNormal = XYZ.BasisX,
                PolylineCurvesFeet = new List<List<XYZ>> { pts },
                LayoutFixedCount = qtyBotY,
                ArrayLengthFeet = RebarResourceResolver.MmToFeet(spanX),
                BarsOnNormalSide = true,
                CutLengthMm = p.TotalCutLengthMm,
                Quantity = qtyBotY,
                Notes = $"D{cfg.BottomYDiaMm}-{(int)cfg.BottomYSpacingMm} (Pure Pad)"
            });
        }

        // 3. Optional Top X/Y Mesh
        if (cfg.EnableTopMesh)
        {
            double zTopX = pad.TopZMm - coverTop - cfg.TopXDiaMm * 0.5;
            double zTopY = zTopX - (cfg.TopXDiaMm + cfg.TopYDiaMm) * 0.5;

            int qtyTopX = Math.Max(2, (int)Math.Floor(spanY / Math.Max(50.0, cfg.TopXSpacingMm)) + 1);
            foreach (var p in StockSpliceSolver.Solve1DSplice(minX, maxX, cfg.TopXDiaMm,
                cfg.EdgeTermination == EdgeTerminationMode.Straight ? 0 : hookLegMax,
                cfg.EdgeTermination == EdgeTerminationMode.Straight ? 0 : hookLegMax,
                true, null, global, 0))
            {
                var pts = BuildPadBarPolylineX(ToFeet, p.StartCoordMm, p.EndCoordMm, minY, zTopX,
                    p.HasStartHook ? hookLegMax : 0.0, p.HasEndHook ? hookLegMax : 0.0, up: false);

                planned.Add(new RebarPlannedBar
                {
                    HostElementId = pad.Host.Id.Value,
                    Role = "Pondasi Atas Arah-X",
                    ShapeCode = cfg.EdgeTermination == EdgeTerminationMode.Straight ? "00" : "21",
                    DiameterMm = cfg.TopXDiaMm,
                    Style = RebarStyle.Standard,
                    PreferredNormal = XYZ.BasisY,
                    PolylineCurvesFeet = new List<List<XYZ>> { pts },
                    LayoutFixedCount = qtyTopX,
                    ArrayLengthFeet = RebarResourceResolver.MmToFeet(spanY),
                    BarsOnNormalSide = true,
                    CutLengthMm = p.TotalCutLengthMm,
                    Quantity = qtyTopX
                });
            }

            int qtyTopY = Math.Max(2, (int)Math.Floor(spanX / Math.Max(50.0, cfg.TopYSpacingMm)) + 1);
            foreach (var p in StockSpliceSolver.Solve1DSplice(minY, maxY, cfg.TopYDiaMm,
                cfg.EdgeTermination == EdgeTerminationMode.Straight ? 0 : hookLegMax,
                cfg.EdgeTermination == EdgeTerminationMode.Straight ? 0 : hookLegMax,
                true, null, global, 0))
            {
                var pts = BuildPadBarPolylineY(ToFeet, minX, p.StartCoordMm, p.EndCoordMm, zTopY,
                    p.HasStartHook ? hookLegMax : 0.0, p.HasEndHook ? hookLegMax : 0.0, up: false);

                planned.Add(new RebarPlannedBar
                {
                    HostElementId = pad.Host.Id.Value,
                    Role = "Pondasi Atas Arah-Y",
                    ShapeCode = cfg.EdgeTermination == EdgeTerminationMode.Straight ? "00" : "21",
                    DiameterMm = cfg.TopYDiaMm,
                    Style = RebarStyle.Standard,
                    PreferredNormal = XYZ.BasisX,
                    PolylineCurvesFeet = new List<List<XYZ>> { pts },
                    LayoutFixedCount = qtyTopY,
                    ArrayLengthFeet = RebarResourceResolver.MmToFeet(spanX),
                    BarsOnNormalSide = true,
                    CutLengthMm = p.TotalCutLengthMm,
                    Quantity = qtyTopY
                });
            }
        }

        // 4. Optional Perimeter Side Binder Hoop
        if (cfg.IncludePerimeterBinder)
        {
            double zMid = (pad.BottomZMm + pad.TopZMm) * 0.5;
            var binder = new List<XYZ>
            {
                ToFeet(minX - 10, minY - 10, zMid),
                ToFeet(maxX + 10, minY - 10, zMid),
                ToFeet(maxX + 10, maxY + 10, zMid),
                ToFeet(minX - 10, maxY + 10, zMid),
                ToFeet(minX - 10, minY - 10, zMid)
            };
            planned.Add(new RebarPlannedBar
            {
                HostElementId = pad.Host.Id.Value,
                Role = "Sengkang Keliling Pondasi",
                ShapeCode = "51",
                DiameterMm = cfg.PerimeterBinderDiaMm,
                Style = RebarStyle.StirrupTie,
                PreferredNormal = XYZ.BasisZ,
                PolylineCurvesFeet = new List<List<XYZ>> { binder },
                CutLengthMm = Math.Round(2.0 * (spanX + spanY + 40.0) + 300.0, 0),
                Quantity = 1
            });
        }

        return planned;
    }

    private static List<XYZ> BuildPadBarPolylineX(
        Func<double, double, double, XYZ> toFeet,
        double x0, double x1, double yMm, double zMm, double h0, double h1, bool up)
    {
        double dir = up ? +1.0 : -1.0;
        var pts = new List<XYZ>();
        if (h0 > 10.0) pts.Add(toFeet(x0, yMm, zMm + dir * h0));
        pts.Add(toFeet(x0, yMm, zMm));
        pts.Add(toFeet(x1, yMm, zMm));
        if (h1 > 10.0) pts.Add(toFeet(x1, yMm, zMm + dir * h1));
        return pts;
    }

    private static List<XYZ> BuildPadBarPolylineY(
        Func<double, double, double, XYZ> toFeet,
        double xMm, double y0, double y1, double zMm, double h0, double h1, bool up)
    {
        double dir = up ? +1.0 : -1.0;
        var pts = new List<XYZ>();
        if (h0 > 10.0) pts.Add(toFeet(xMm, y0, zMm + dir * h0));
        pts.Add(toFeet(xMm, y0, zMm));
        pts.Add(toFeet(xMm, y1, zMm));
        if (h1 > 10.0) pts.Add(toFeet(xMm, y1, zMm + dir * h1));
        return pts;
    }
}

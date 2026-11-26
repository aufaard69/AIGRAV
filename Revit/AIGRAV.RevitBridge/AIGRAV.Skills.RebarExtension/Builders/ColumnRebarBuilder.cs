using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Builders;

/// <summary>
/// Builds vertical column reinforcement cages starting inside the foundation pedestal (with 90 deg base bends
/// and footing confining ties — since Column owns its footing anchorage without foundation double-counting) up
/// to the roof joint hook, following Uncut <= 12m (no unnecessary floor cuts) vs Cut-per-Level (with 1:6 cranks).
/// </summary>
public static class ColumnRebarBuilder
{
    public static List<RebarPlannedBar> BuildPlanForChain(
        ColumnChainSolution sol,
        ColumnConfig cfg,
        RebarGlobalSettings global)
    {
        var planned = new List<RebarPlannedBar>();
        if (sol.Segments.Count == 0) return planned;

        ColumnSegmentModel bottomSeg = sol.Segments[0];
        double cover = Math.Max(20.0, cfg.CoverMm);
        double offsetFromFace = cover + cfg.TieDiaMm + cfg.MainBarDiaMm * 0.5;

        double baseHalfX = Math.Max(35.0, bottomSeg.WidthXMm * 0.5 - offsetFromFace);
        double baseHalfY = Math.Max(35.0, bottomSeg.DepthYMm * 0.5 - offsetFromFace);
        var barCoords = DistributePerimeterBars(cfg.TotalMainBars, baseHalfX, baseHalfY);

        static XYZ ToFeet(double xMm, double yMm, double zMm) => new(
            RebarResourceResolver.MmToFeet(xMm),
            RebarResourceResolver.MmToFeet(yMm),
            RebarResourceResolver.MmToFeet(zMm));

        // 1. Vertical Longitudinal Bars across solved chain runs
        for (int b = 0; b < barCoords.Count; b++)
        {
            var (dxMm, dyMm) = barCoords[b];
            double normDist = Math.Sqrt(dxMm * dxMm + dyMm * dyMm);
            double radX = normDist > 1e-3 ? dxMm / normDist : 1.0;
            double radY = normDist > 1e-3 ? dyMm / normDist : 0.0;

            foreach (var run in sol.MainBarRuns)
            {
                // Adjust bar radial offset when upper segment narrows vs bottom
                ColumnSegmentModel segAtStart = sol.Segments.FirstOrDefault(s => s.ElementId == run.PrimaryHostElementId) ?? bottomSeg;
                double segHalfX = Math.Max(35.0, segAtStart.WidthXMm * 0.5 - offsetFromFace);
                double segHalfY = Math.Max(35.0, segAtStart.DepthYMm * 0.5 - offsetFromFace);

                double sx = Math.Clamp(dxMm / Math.Max(1.0, baseHalfX), -1.0, 1.0) * segHalfX;
                double sy = Math.Clamp(dyMm / Math.Max(1.0, baseHalfY), -1.0, 1.0) * segHalfY;

                // Small radial stagger offset when lapping in upper segment to prevent bar self-collision
                double lapRadialShift = (run.OverlapBelowMm > 0 && run.SegmentIndex % 2 == 1) ? cfg.MainBarDiaMm * 0.85 : 0.0;
                double bx = sol.CenterXMm + sx - radX * lapRadialShift;
                double by = sol.CenterYMm + sy - radY * lapRadialShift;

                var pts = new List<XYZ>();
                if (run.StartsInsideFoundation && run.BaseHookLegMm > 10.0)
                {
                    double bendSign = cfg.BaseFootingBend == EndHookDirection.Bend90Inward ? -1.0 : +1.0;
                    pts.Add(ToFeet(bx + bendSign * radX * run.BaseHookLegMm, by + bendSign * radY * run.BaseHookLegMm, run.StartZMm));
                }

                pts.Add(ToFeet(bx, by, run.StartZMm));

                if (run.HasCrankOffset && run.CrankHeightMm > 20.0 && run.CrankInwardShiftMm > 5.0)
                {
                    double crankStartZ = Math.Max(run.StartZMm + 200.0, run.EndZMm - run.OverlapBelowMm - run.CrankHeightMm);
                    double crankEndZ = Math.Min(run.EndZMm - 50.0, crankStartZ + run.CrankHeightMm);
                    pts.Add(ToFeet(bx, by, crankStartZ));
                    pts.Add(ToFeet(bx - radX * run.CrankInwardShiftMm, by - radY * run.CrankInwardShiftMm, crankEndZ));
                    pts.Add(ToFeet(bx - radX * run.CrankInwardShiftMm, by - radY * run.CrankInwardShiftMm, run.EndZMm));
                }
                else
                {
                    pts.Add(ToFeet(bx, by, run.EndZMm));
                }

                if (run.TerminatesAtRoof && run.TopHookLegMm > 10.0)
                {
                    double topSign = cfg.RoofTopHook == EndHookDirection.Bend90Outward ? +1.0 : -1.0;
                    double lastX = run.HasCrankOffset ? bx - radX * run.CrankInwardShiftMm : bx;
                    double lastY = run.HasCrankOffset ? by - radY * run.CrankInwardShiftMm : by;
                    pts.Add(ToFeet(lastX + topSign * radX * run.TopHookLegMm, lastY + topSign * radY * run.TopHookLegMm, run.EndZMm));
                }

                string shapeCode = (run.StartsInsideFoundation && run.TerminatesAtRoof) ? "21"
                    : (run.HasCrankOffset ? "26" : ((run.StartsInsideFoundation || run.TerminatesAtRoof) ? "11" : "00"));

                planned.Add(new RebarPlannedBar
                {
                    HostElementId = run.PrimaryHostElementId,
                    Role = run.StartsInsideFoundation ? $"Col Main #{b + 1} (Base Pondasi)" : $"Col Main #{b + 1} Seg {run.SegmentIndex + 1}",
                    ShapeCode = shapeCode,
                    DiameterMm = cfg.MainBarDiaMm,
                    Style = RebarStyle.Standard,
                    PreferredNormal = new XYZ(-radY, radX, 0),
                    PolylineCurvesFeet = new List<List<XYZ>> { pts },
                    CutLengthMm = run.CutLengthMm,
                    Quantity = 1,
                    Notes = run.StartsInsideFoundation
                        ? $"Kait pondasi {run.BaseHookLegMm:F0}mm"
                        : (run.HasCrankOffset ? "Tekukan 1:6 joint level" : $"Overlap {run.OverlapBelowMm:F0}mm")
                });
            }
        }

        // 2. Column Confining Ties / Stirrups across all solved zones (including inside foundation pad!)
        foreach (var zone in sol.StirrupZones)
        {
            ColumnSegmentModel seg = sol.Segments.FirstOrDefault(s => s.ElementId == zone.HostElementId) ?? bottomSeg;
            double zLen = zone.EndZMm - zone.StartZMm;
            if (zLen < 60.0) continue;

            int qty = Math.Max(2, (int)Math.Floor(zLen / Math.Max(50.0, zone.SpacingMm)) + 1);
            double tieCenterOffset = cover + cfg.TieDiaMm * 0.5;
            double hx = Math.Max(30.0, seg.WidthXMm * 0.5 - tieCenterOffset);
            double hy = Math.Max(30.0, seg.DepthYMm * 0.5 - tieCenterOffset);
            double z0 = zone.StartZMm;

            var outerLoop = new List<XYZ>
            {
                ToFeet(sol.CenterXMm - hx, sol.CenterYMm - hy, z0),
                ToFeet(sol.CenterXMm + hx, sol.CenterYMm - hy, z0),
                ToFeet(sol.CenterXMm + hx, sol.CenterYMm + hy, z0),
                ToFeet(sol.CenterXMm - hx, sol.CenterYMm + hy, z0),
                ToFeet(sol.CenterXMm - hx, sol.CenterYMm - hy, z0)
            };

            double perimCut = 4.0 * (hx + hy) + 2.0 * Math.Max(75.0, 6.0 * cfg.TieDiaMm);
            planned.Add(new RebarPlannedBar
            {
                HostElementId = zone.HostElementId,
                Role = zone.ZoneLabel,
                ShapeCode = "51",
                DiameterMm = cfg.TieDiaMm,
                Style = RebarStyle.StirrupTie,
                PreferredNormal = XYZ.BasisZ,
                PolylineCurvesFeet = new List<List<XYZ>> { outerLoop },
                LayoutFixedCount = qty,
                ArrayLengthFeet = RebarResourceResolver.MmToFeet(zLen),
                UseSeismic135Hooks = true,
                BarsOnNormalSide = true,
                CutLengthMm = Math.Round(perimCut, 0),
                Quantity = qty,
                Notes = $"Ø{cfg.TieDiaMm}-{(int)zone.SpacingMm} ({(zone.IsInsideFoundation ? "Dalam Pondasi" : (zone.IsDenseZone ? "Zona l_o" : "Tengah"))})"
            });

            // 4-leg diamond / interior tie for 8+ bar ductile columns
            if (cfg.TieLegs >= 4 && cfg.TotalMainBars >= 8)
            {
                var diamond = new List<XYZ>
                {
                    ToFeet(sol.CenterXMm, sol.CenterYMm - hy, z0),
                    ToFeet(sol.CenterXMm + hx, sol.CenterYMm, z0),
                    ToFeet(sol.CenterXMm, sol.CenterYMm + hy, z0),
                    ToFeet(sol.CenterXMm - hx, sol.CenterYMm, z0),
                    ToFeet(sol.CenterXMm, sol.CenterYMm - hy, z0)
                };
                double dEdge = Math.Sqrt(hx * hx + hy * hy);
                planned.Add(new RebarPlannedBar
                {
                    HostElementId = zone.HostElementId,
                    Role = $"{zone.ZoneLabel} Diamond",
                    ShapeCode = "51",
                    DiameterMm = cfg.TieDiaMm,
                    Style = RebarStyle.StirrupTie,
                    PreferredNormal = XYZ.BasisZ,
                    PolylineCurvesFeet = new List<List<XYZ>> { diamond },
                    LayoutFixedCount = qty,
                    ArrayLengthFeet = RebarResourceResolver.MmToFeet(zLen),
                    UseSeismic135Hooks = true,
                    BarsOnNormalSide = true,
                    CutLengthMm = Math.Round(4.0 * dEdge + 160.0, 0),
                    Quantity = qty
                });
            }
        }

        return planned;
    }

    public static List<(double DxMm, double DyMm)> DistributePerimeterBars(int totalBars, double hx, double hy)
    {
        totalBars = Math.Max(4, totalBars);
        var pts = new List<(double, double)>
        {
            (-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy)
        };
        int remaining = totalBars - 4;
        if (remaining <= 0) return pts;

        // Distribute remaining bars symmetrically across X faces and Y faces
        int topBotEach = remaining / 4;
        int leftRightEach = (remaining - topBotEach * 2) / 2;
        if (remaining == 2) { topBotEach = 1; leftRightEach = 0; }

        for (int i = 1; i <= topBotEach; i++)
        {
            double x = -hx + ((double)i / (topBotEach + 1)) * (2.0 * hx);
            pts.Add((x, -hy));
            pts.Add((x, +hy));
        }
        for (int i = 1; i <= leftRightEach; i++)
        {
            double y = -hy + ((double)i / (leftRightEach + 1)) * (2.0 * hy);
            pts.Add((-hx, y));
            pts.Add((+hx, y));
        }
        // Handle odd extra if remaining not divisible neatly
        while (pts.Count < totalBars)
        {
            pts.Add((0.0, pts.Count % 2 == 0 ? -hy : +hy));
        }
        return pts;
    }
}

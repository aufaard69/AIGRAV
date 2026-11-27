using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Builders;

public sealed class RebarPlannedBar
{
    public long HostElementId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string ShapeCode { get; set; } = "00";
    public int DiameterMm { get; set; }
    public RebarStyle Style { get; set; } = RebarStyle.Standard;
    public XYZ PreferredNormal { get; set; } = XYZ.BasisY;
    public List<List<XYZ>> PolylineCurvesFeet { get; set; } = new();
    public int LayoutFixedCount { get; set; } = 1;
    public double ArrayLengthFeet { get; set; } = 0.0;
    public double MaxSpacingFeet { get; set; } = 0.0;
    public bool UseSeismic135Hooks { get; set; } = false;
    public bool BarsOnNormalSide { get; set; } = true;
    public double CutLengthMm { get; set; }
    public int Quantity { get; set; } = 1;
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Plans and creates native Revit Structural Framing (Beam) reinforcement:
/// - Continuous Top & Bottom longitudinal bars split cleanly <= MaxStockLengthMm with overlap & stagger
/// - Exterior beam-column joint 90-deg / U-hairpin anchorage hooks included in bar cut length
/// - Extra Top Support bars centered over EVERY support As 1..As N with L/4 + SNI/20D cutoff extension
/// - Extra Bottom Midspan bars centered in EVERY span with L/2 + 2*l_ext curtailment
/// - Optional side skin bars (depth >= 700 mm warning if omitted)
/// - 3-zone seismic stirrups (Tumpuan Kiri / Lapangan / Tumpuan Kanan) + 135 deg hook + cross-ties
/// </summary>
public static class BeamRebarBuilder
{
    public static List<RebarPlannedBar> BuildPlan(
        ExtractedBeamInfo beam,
        BeamConfig cfg,
        RebarGlobalSettings global,
        List<string> warnings)
    {
        var planned = new List<RebarPlannedBar>();
        double cover = Math.Max(15.0, cfg.CoverMm);
        double w = beam.WidthMm;
        double h = beam.DepthMm;
        double L = beam.TotalLengthMm;

        if (h >= 700.0 && !cfg.EnableSkinBars)
            warnings.Add($"Beam #{beam.Host.Id.Value} depth {h:F0} mm >= 700 mm: SNI 2847 §9.7.2.3 recommends side skin reinforcement.");

        double stirrupCenterR = cover + cfg.StirrupDiaMm * 0.5;
        double topCenterZMm = -(cover + cfg.StirrupDiaMm + cfg.TopBarDiameterMm * 0.5); // relative to beam TopZ (Origin)
        double botCenterZMm = -h + cover + cfg.StirrupDiaMm + cfg.BottomBarDiameterMm * 0.5;
        double effDepthDMm = Math.Max(100.0, h - cover - cfg.StirrupDiaMm - cfg.BottomBarDiameterMm * 0.5);
        double lExtMm = StockSpliceSolver.ComputeCutoffExtensionMm(cfg.CutoffMode, effDepthDMm, cfg.TopBarDiameterMm);

        double startX = cover + cfg.TopBarDiameterMm * 0.5;
        double endX = Math.Max(startX + 400.0, L - cover - cfg.TopBarDiameterMm * 0.5);
        double hookLegMm = StockSpliceSolver.ComputeHookLegMm(cfg.ExteriorJointHook, cfg.JointHookLegFormula, h, cover, cfg.TopBarDiameterMm);

        // Helper transforming local (xMm, yMm, zMm relative to Origin @ TopCenter) to Revit XYZ feet
        XYZ ToFeet(double xMm, double yMm, double zMm)
        {
            return beam.Origin
                + beam.AxisX * RebarResourceResolver.MmToFeet(xMm)
                + beam.AxisY * RebarResourceResolver.MmToFeet(yMm)
                + beam.AxisZ * RebarResourceResolver.MmToFeet(zMm);
        }

        // 1. Continuous Top Main Bars
        double topCoreHalfY = Math.Max(20.0, w * 0.5 - cover - cfg.StirrupDiaMm - cfg.TopBarDiameterMm * 0.5);
        double[] topYs = DistributeBarsAcrossWidth(cfg.TopBarCount, topCoreHalfY);
        for (int b = 0; b < topYs.Length; b++)
        {
            var pieces = StockSpliceSolver.Solve1DSplice(
                startX, endX, cfg.TopBarDiameterMm, hookLegMm, hookLegMm,
                isTensionTopBar: true, beam.SupportStationsMm, global, layerIndex: b);

            foreach (var p in pieces)
            {
                double yBar = topYs[b] + p.TransverseOffsetMm * (topYs[b] >= 0 ? -0.5 : 0.5);
                var poly = BuildLongitudinalPolyline(
                    ToFeet, p.StartCoordMm, p.EndCoordMm, yBar, topCenterZMm,
                    p.HasStartHook ? -p.StartHookLegMm : 0.0,
                    p.HasEndHook ? -p.EndHookLegMm : 0.0);

                planned.Add(new RebarPlannedBar
                {
                    HostElementId = beam.Host.Id.Value,
                    Role = $"Top Main #{b + 1}",
                    ShapeCode = (p.HasStartHook && p.HasEndHook) ? "21" : ((p.HasStartHook || p.HasEndHook) ? "11" : "00"),
                    DiameterMm = cfg.TopBarDiameterMm,
                    Style = RebarStyle.Standard,
                    PreferredNormal = beam.AxisY,
                    PolylineCurvesFeet = new List<List<XYZ>> { poly },
                    CutLengthMm = p.TotalCutLengthMm,
                    Quantity = 1,
                    Notes = p.OverlapWithPrevMm > 0 ? $"Splice lap {p.OverlapWithPrevMm:F0} mm" : ""
                });
            }
        }

        // 2. Continuous Bottom Main Bars (exterior joint hooks point UP +Z!)
        double botCoreHalfY = Math.Max(20.0, w * 0.5 - cover - cfg.StirrupDiaMm - cfg.BottomBarDiameterMm * 0.5);
        double[] botYs = DistributeBarsAcrossWidth(cfg.BottomBarCount, botCoreHalfY);
        double botHookLegMm = StockSpliceSolver.ComputeHookLegMm(cfg.ExteriorJointHook, cfg.JointHookLegFormula, h, cover, cfg.BottomBarDiameterMm);
        for (int b = 0; b < botYs.Length; b++)
        {
            var pieces = StockSpliceSolver.Solve1DSplice(
                startX, endX, cfg.BottomBarDiameterMm, botHookLegMm, botHookLegMm,
                isTensionTopBar: false, beam.SupportStationsMm, global, layerIndex: b);

            foreach (var p in pieces)
            {
                double yBar = botYs[b] + p.TransverseOffsetMm * (botYs[b] >= 0 ? -0.5 : 0.5);
                var poly = BuildLongitudinalPolyline(
                    ToFeet, p.StartCoordMm, p.EndCoordMm, yBar, botCenterZMm,
                    p.HasStartHook ? +p.StartHookLegMm : 0.0,
                    p.HasEndHook ? +p.EndHookLegMm : 0.0);

                planned.Add(new RebarPlannedBar
                {
                    HostElementId = beam.Host.Id.Value,
                    Role = $"Bottom Main #{b + 1}",
                    ShapeCode = (p.HasStartHook && p.HasEndHook) ? "21" : ((p.HasStartHook || p.HasEndHook) ? "11" : "00"),
                    DiameterMm = cfg.BottomBarDiameterMm,
                    Style = RebarStyle.Standard,
                    PreferredNormal = beam.AxisY,
                    PolylineCurvesFeet = new List<List<XYZ>> { poly },
                    CutLengthMm = p.TotalCutLengthMm,
                    Quantity = 1,
                    Notes = p.OverlapWithPrevMm > 0 ? $"Splice lap {p.OverlapWithPrevMm:F0} mm" : ""
                });
            }
        }

        // 3. Extra Top Support Bars on ALL supports As 1 .. As N (layer 2 below main top by max(25mm, db))
        if (cfg.EnableExtraTopSupport && cfg.ExtraTopSupportCount > 0 && beam.SupportStationsMm.Count >= 2)
        {
            double extraTopZ = topCenterZMm - Math.Max(25.0, cfg.TopBarDiameterMm) - (cfg.TopBarDiameterMm + cfg.ExtraTopSupportDiaMm) * 0.5;
            double extraTopHalfY = Math.Max(15.0, topCoreHalfY - 25.0);
            double[] exTopYs = DistributeBarsAcrossWidth(cfg.ExtraTopSupportCount, extraTopHalfY);
            double exHook = StockSpliceSolver.ComputeHookLegMm(cfg.ExteriorJointHook, cfg.JointHookLegFormula, h, cover, cfg.ExtraTopSupportDiaMm);

            for (int s = 0; s < beam.SupportStationsMm.Count; s++)
            {
                double sc = beam.SupportStationsMm[s];
                bool isStart = s == 0;
                bool isEnd = s == beam.SupportStationsMm.Count - 1;
                double leftSpan = s > 0 ? (sc - beam.SupportStationsMm[s - 1]) : 0.0;
                double rightSpan = s < beam.SupportStationsMm.Count - 1 ? (beam.SupportStationsMm[s + 1] - sc) : 0.0;

                double segStart = isStart
                    ? startX + 20.0
                    : Math.Max(startX + 100.0, sc - (0.25 * leftSpan + lExtMm));
                double segEnd = isEnd
                    ? endX - 20.0
                    : Math.Min(endX - 100.0, sc + (0.25 * rightSpan + lExtMm));

                if (segEnd - segStart < 300.0) continue;

                double hStart = isStart ? exHook : 0.0;
                double hEnd = isEnd ? exHook : 0.0;

                for (int k = 0; k < exTopYs.Length; k++)
                {
                    var pieces = StockSpliceSolver.Solve1DSplice(
                        segStart, segEnd, cfg.ExtraTopSupportDiaMm, hStart, hEnd,
                        isTensionTopBar: false, null, global, layerIndex: k);
                    foreach (var p in pieces)
                    {
                        var poly = BuildLongitudinalPolyline(
                            ToFeet, p.StartCoordMm, p.EndCoordMm, exTopYs[k], extraTopZ,
                            p.HasStartHook ? -p.StartHookLegMm : 0.0,
                            p.HasEndHook ? -p.EndHookLegMm : 0.0);

                        planned.Add(new RebarPlannedBar
                        {
                            HostElementId = beam.Host.Id.Value,
                            Role = $"Extra Top Tumpuan As {s + 1}",
                            ShapeCode = (p.HasStartHook || p.HasEndHook) ? "11" : "00",
                            DiameterMm = cfg.ExtraTopSupportDiaMm,
                            Style = RebarStyle.Standard,
                            PreferredNormal = beam.AxisY,
                            PolylineCurvesFeet = new List<List<XYZ>> { poly },
                            CutLengthMm = p.TotalCutLengthMm,
                            Quantity = 1,
                            Notes = isStart || isEnd ? $"L/4+{lExtMm:F0}mm + kait" : $"Simetris tumpuan + 2*{lExtMm:F0}mm"
                        });
                    }
                }
            }
        }

        // 4. Extra Bottom Midspan Bars in EVERY span [As k .. As k+1] (centered 0.5*L + 2*l_ext)
        if (cfg.EnableExtraBottomMidspan && cfg.ExtraBottomMidspanCount > 0 && beam.SupportStationsMm.Count >= 2)
        {
            double lExtBot = StockSpliceSolver.ComputeCutoffExtensionMm(cfg.CutoffMode, effDepthDMm, cfg.ExtraBottomMidspanDiaMm);
            double extraBotZ = botCenterZMm + Math.Max(25.0, cfg.BottomBarDiameterMm) + (cfg.BottomBarDiameterMm + cfg.ExtraBottomMidspanDiaMm) * 0.5;
            double extraBotHalfY = Math.Max(15.0, botCoreHalfY - 25.0);
            double[] exBotYs = DistributeBarsAcrossWidth(cfg.ExtraBottomMidspanCount, extraBotHalfY);

            for (int spanIdx = 0; spanIdx < beam.SupportStationsMm.Count - 1; spanIdx++)
            {
                double sL = beam.SupportStationsMm[spanIdx];
                double sR = beam.SupportStationsMm[spanIdx + 1];
                double spanLen = sR - sL;
                if (spanLen < 600.0) continue;

                double halfZone = 0.25 * spanLen + lExtBot;
                double mid = 0.5 * (sL + sR);
                double segStart = Math.Max(sL + 0.05 * spanLen, mid - halfZone);
                double segEnd = Math.Min(sR - 0.05 * spanLen, mid + halfZone);
                if (segEnd - segStart < 300.0) continue;

                for (int k = 0; k < exBotYs.Length; k++)
                {
                    var pieces = StockSpliceSolver.Solve1DSplice(
                        segStart, segEnd, cfg.ExtraBottomMidspanDiaMm, 0.0, 0.0,
                        isTensionTopBar: false, null, global, layerIndex: k);
                    foreach (var p in pieces)
                    {
                        var poly = BuildLongitudinalPolyline(
                            ToFeet, p.StartCoordMm, p.EndCoordMm, exBotYs[k], extraBotZ, 0.0, 0.0);
                        planned.Add(new RebarPlannedBar
                        {
                            HostElementId = beam.Host.Id.Value,
                            Role = $"Extra Bottom Lapangan Span {spanIdx + 1}",
                            ShapeCode = "00",
                            DiameterMm = cfg.ExtraBottomMidspanDiaMm,
                            Style = RebarStyle.Standard,
                            PreferredNormal = beam.AxisY,
                            PolylineCurvesFeet = new List<List<XYZ>> { poly },
                            CutLengthMm = p.TotalCutLengthMm,
                            Quantity = 1,
                            Notes = $"1/2L + 2*{lExtBot:F0}mm curtailment"
                        });
                    }
                }
            }
        }

        // 5. Optional Side Skin Bars (Pinggang / Torsion)
        if (cfg.EnableSkinBars && cfg.SkinBarPairs > 0)
        {
            double sideY = topCoreHalfY;
            for (int pair = 1; pair <= cfg.SkinBarPairs; pair++)
            {
                double t = (double)pair / (cfg.SkinBarPairs + 1);
                double skinZ = topCenterZMm + t * (botCenterZMm - topCenterZMm);
                foreach (double sign in new[] { -1.0, 1.0 })
                {
                    var pieces = StockSpliceSolver.Solve1DSplice(
                        startX, endX, cfg.SkinBarDiaMm, 0, 0,
                        isTensionTopBar: false, beam.SupportStationsMm, global, pair);
                    foreach (var p in pieces)
                    {
                        var poly = BuildLongitudinalPolyline(
                            ToFeet, p.StartCoordMm, p.EndCoordMm, sign * sideY, skinZ, 0, 0);
                        planned.Add(new RebarPlannedBar
                        {
                            HostElementId = beam.Host.Id.Value,
                            Role = $"Skin Bar Layer {pair}",
                            ShapeCode = "00",
                            DiameterMm = cfg.SkinBarDiaMm,
                            Style = RebarStyle.Standard,
                            PreferredNormal = beam.AxisY,
                            PolylineCurvesFeet = new List<List<XYZ>> { poly },
                            CutLengthMm = p.TotalCutLengthMm,
                            Quantity = 1
                        });
                    }
                }
            }
        }

        // 6. 3-Zone Seismic Stirrups per span (Tumpuan Kiri — Lapangan — Tumpuan Kanan)
        double stHalfY = Math.Max(15.0, w * 0.5 - stirrupCenterR);
        double stTopZ = -stirrupCenterR;
        double stBotZ = -h + stirrupCenterR;
        double singleStirrupCutMm = 2.0 * (2.0 * stHalfY + (stTopZ - stBotZ)) + (cfg.Use135SeismicHook ? 2.0 * Math.Max(75.0, 6.0 * cfg.StirrupDiaMm) : 150.0);

        for (int spanIdx = 0; spanIdx < beam.SupportStationsMm.Count - 1; spanIdx++)
        {
            double sL = beam.SupportStationsMm[spanIdx];
            double sR = beam.SupportStationsMm[spanIdx + 1];
            double wL = spanIdx < beam.SupportWidthsMm.Count ? beam.SupportWidthsMm[spanIdx] * 0.5 : 150.0;
            double wR = (spanIdx + 1) < beam.SupportWidthsMm.Count ? beam.SupportWidthsMm[spanIdx + 1] * 0.5 : 150.0;

            double clearStart = sL + wL + 50.0;
            double clearEnd = sR - wR - 50.0;
            if (clearEnd - clearStart < 300.0) continue;

            var zones = StockSpliceSolver.ComputeBeamStirrupZonesForSpan(clearStart, clearEnd, h, cfg, spanIdx + 1);

            foreach (var (zLabel, zx0, zx1, qty, effSpacing) in zones)
            {
                double lenZ = zx1 - zx0;
                if (lenZ < 30.0 || qty < 1) continue;

                // Wind CCW around +AxisX so natural normal (-AxisZ x +AxisY) == +AxisX!
                var rect = new List<XYZ>
                {
                    ToFeet(zx0, -stHalfY, stTopZ),
                    ToFeet(zx0, -stHalfY, stBotZ),
                    ToFeet(zx0, +stHalfY, stBotZ),
                    ToFeet(zx0, +stHalfY, stTopZ),
                    ToFeet(zx0, -stHalfY, stTopZ)
                };

                planned.Add(new RebarPlannedBar
                {
                    HostElementId = beam.Host.Id.Value,
                    Role = zLabel,
                    ShapeCode = "51",
                    DiameterMm = cfg.StirrupDiaMm,
                    Style = RebarStyle.StirrupTie,
                    PreferredNormal = beam.AxisX,
                    PolylineCurvesFeet = new List<List<XYZ>> { rect },
                    LayoutFixedCount = qty,
                    ArrayLengthFeet = RebarResourceResolver.MmToFeet(lenZ),
                    MaxSpacingFeet = RebarResourceResolver.MmToFeet(effSpacing),
                    UseSeismic135Hooks = cfg.Use135SeismicHook,
                    BarsOnNormalSide = true,
                    CutLengthMm = Math.Round(singleStirrupCutMm, 0),
                    Quantity = qty,
                    Notes = $"Ø{cfg.StirrupDiaMm}-{(int)Math.Round(effSpacing)} ({cfg.StirrupLegs} kaki)"
                });

                // Optional extra interior cross-tie leg(s) when StirrupLegs >= 3
                if (cfg.StirrupLegs >= 3)
                {
                    double tieY = 0.0;
                    var link = new List<XYZ> { ToFeet(zx0, tieY, stTopZ), ToFeet(zx0, tieY, stBotZ) };
                    planned.Add(new RebarPlannedBar
                    {
                        HostElementId = beam.Host.Id.Value,
                        Role = $"{zLabel} Crosstie",
                        ShapeCode = "11",
                        DiameterMm = cfg.StirrupDiaMm,
                        Style = RebarStyle.StirrupTie,
                        PreferredNormal = beam.AxisX,
                        PolylineCurvesFeet = new List<List<XYZ>> { link },
                        LayoutFixedCount = qty,
                        ArrayLengthFeet = RebarResourceResolver.MmToFeet(lenZ),
                        UseSeismic135Hooks = cfg.Use135SeismicHook,
                        BarsOnNormalSide = true,
                        CutLengthMm = Math.Round((stTopZ - stBotZ) + 240.0, 0),
                        Quantity = qty
                    });
                }
            }
        }

        return planned;
    }

    public static double[] DistributeBarsAcrossWidth(int count, double halfCoreMm)
    {
        if (count <= 1) return new[] { 0.0 };
        var arr = new double[count];
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / (count - 1);
            arr[i] = -halfCoreMm + t * (2.0 * halfCoreMm);
        }
        return arr;
    }

    private static List<XYZ> BuildLongitudinalPolyline(
        Func<double, double, double, XYZ> toFeet,
        double x0,
        double x1,
        double yMm,
        double zMm,
        double startHookDzMm,
        double endHookDzMm)
    {
        var pts = new List<XYZ>();
        if (Math.Abs(startHookDzMm) > 10.0)
            pts.Add(toFeet(x0, yMm, zMm + startHookDzMm));
        pts.Add(toFeet(x0, yMm, zMm));
        pts.Add(toFeet(x1, yMm, zMm));
        if (Math.Abs(endHookDzMm) > 10.0)
            pts.Add(toFeet(x1, yMm, zMm + endHookDzMm));
        return pts;
    }

    /// <summary>
    /// Converts a list of planned bars into genuine native Revit Rebar elements inside an open Transaction.
    /// </summary>
    public static List<long> CreateRevitRebars(
        Document doc,
        Element host,
        IReadOnlyList<RebarPlannedBar> plannedBars,
        RebarResourceResolver resolver,
        string lokasi,
        string hostHash,
        List<string> warnings,
        List<string> failedItems)
    {
        var createdIds = new List<long>();
        RebarHookType hook135 = resolver.ResolveOrCreateHookType(doc, 135, RebarStyle.StirrupTie, 8.0, warnings);

        foreach (var plan in plannedBars)
        {
            try
            {
                RebarBarType barType = resolver.ResolveOrCreateBarType(doc, plan.DiameterMm, deformed: true, warnings);
                foreach (var poly in plan.PolylineCurvesFeet)
                {
                    if (poly.Count < 2) continue;
                    var curves = BuildCurvesFromPolyline(poly);
                    if (curves.Count == 0) continue;

                    XYZ norm = ComputeSafePlaneNormal(poly, plan.PreferredNormal);
                    if (plan.PreferredNormal.GetLength() > 1e-5 && norm.DotProduct(plan.PreferredNormal) < 0)
                        norm = -norm;
                    RebarHookType? startHook = (plan.Style == RebarStyle.StirrupTie && plan.UseSeismic135Hooks) ? hook135 : null;
                    RebarHookType? endHook = (plan.Style == RebarStyle.StirrupTie && plan.UseSeismic135Hooks) ? hook135 : null;

                    using var terms = new BarTerminationsData(doc)
                    {
                        HookTypeIdAtStart = startHook?.Id ?? ElementId.InvalidElementId,
                        HookTypeIdAtEnd = endHook?.Id ?? ElementId.InvalidElementId,
                        TerminationOrientationAtStart = RebarTerminationOrientation.Left,
                        TerminationOrientationAtEnd = RebarTerminationOrientation.Left
                    };

                    Rebar rebar = Rebar.CreateFromCurves(
                        doc,
                        plan.Style,
                        barType,
                        host,
                        norm,
                        curves,
                        terms,
                        useExistingShapeIfPossible: true,
                        createNewShape: true);

                    RebarShapeDrivenAccessor acc = rebar.GetShapeDrivenAccessor();
                    if (plan.LayoutFixedCount > 1 && plan.ArrayLengthFeet > 0.02)
                    {
                        acc.SetLayoutAsFixedNumber(plan.LayoutFixedCount, plan.ArrayLengthFeet, plan.BarsOnNormalSide, true, true);
                    }
                    else
                    {
                        acc.SetLayoutAsSingle();
                    }

                    RebarIdempotencyManager.TagGeneratedRebar(
                        rebar, host.Id.Value, plan.Role, plan.ShapeCode, lokasi, hostHash);

                    createdIds.Add(rebar.Id.Value);
                }
            }
            catch (Exception ex)
            {
                failedItems.Add($"Host #{host.Id.Value} [{plan.Role}]: {ex.Message}");
            }
        }
        return createdIds;
    }

    public static IList<Curve> BuildCurvesFromPolyline(IReadOnlyList<XYZ> pts)
    {
        var curves = new List<Curve>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            if (pts[i].DistanceTo(pts[i + 1]) > 0.01) // > ~3 mm
            {
                curves.Add(Line.CreateBound(pts[i], pts[i + 1]));
            }
        }
        return curves;
    }

    public static XYZ ComputeSafePlaneNormal(IReadOnlyList<XYZ> pts, XYZ fallback)
    {
        if (pts.Count >= 3)
        {
            XYZ firstDir = (pts[1] - pts[0]);
            for (int i = 2; i < pts.Count; i++)
            {
                XYZ nextDir = (pts[i] - pts[i - 1]);
                XYZ cross = firstDir.CrossProduct(nextDir);
                if (cross.GetLength() > 1e-5)
                    return cross.Normalize();
            }
        }
        if (pts.Count >= 2)
        {
            XYZ dir = (pts[^1] - pts[0]);
            if (dir.GetLength() > 1e-5)
            {
                dir = dir.Normalize();
                if (Math.Abs(dir.DotProduct(fallback)) < 0.99)
                {
                    // Project fallback orthogonal to line direction
                    XYZ orth = fallback - dir * fallback.DotProduct(dir);
                    if (orth.GetLength() > 1e-5) return orth.Normalize();
                }
                XYZ alt = Math.Abs(dir.DotProduct(XYZ.BasisZ)) < 0.9 ? XYZ.BasisZ : XYZ.BasisX;
                return dir.CrossProduct(alt).Normalize();
            }
        }
        return fallback.GetLength() > 1e-5 ? fallback.Normalize() : XYZ.BasisZ;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Skills.RebarExtension.Models;

namespace AIGRAV.Skills.RebarExtension.Engine;

public sealed class ColumnSegmentModel
{
    public long ElementId { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public string FamilyAndType { get; set; } = string.Empty;
    public double CenterXMm { get; set; }
    public double CenterYMm { get; set; }
    public double BaseZMm { get; set; }
    public double TopZMm { get; set; }
    public double WidthXMm { get; set; }
    public double DepthYMm { get; set; }
    public List<double> IntersectingFloorOrBeamTopZMm { get; set; } = new();
}

public sealed class DetectedFootingSupport
{
    public long FoundationElementId { get; set; }
    public string FamilyAndType { get; set; } = string.Empty;
    public double TopZMm { get; set; }
    public double BottomZMm { get; set; }
    public double ThicknessMm => Math.Max(0.0, TopZMm - BottomZMm);
}

public sealed class ColumnVerticalBarRun
{
    public int SegmentIndex { get; set; }
    public long PrimaryHostElementId { get; set; }
    public double StartZMm { get; set; }
    public double EndZMm { get; set; }
    public double BaseHookLegMm { get; set; }
    public double TopHookLegMm { get; set; }
    public double CutLengthMm { get; set; }
    public double OverlapBelowMm { get; set; }
    public bool StartsInsideFoundation { get; set; }
    public bool TerminatesAtRoof { get; set; }
    public bool HasCrankOffset { get; set; }
    public double CrankInwardShiftMm { get; set; }
    public double CrankHeightMm { get; set; }
}

public sealed class ColumnStirrupZonePlan
{
    public long HostElementId { get; set; }
    public string ZoneLabel { get; set; } = string.Empty;
    public double StartZMm { get; set; }
    public double EndZMm { get; set; }
    public double SpacingMm { get; set; }
    public bool IsInsideFoundation { get; set; }
    public bool IsDenseZone { get; set; }
}

public sealed class ColumnChainSolution
{
    public string ChainId { get; set; } = string.Empty;
    public double CenterXMm { get; set; }
    public double CenterYMm { get; set; }
    public List<ColumnSegmentModel> Segments { get; set; } = new();
    public DetectedFootingSupport? Footing { get; set; }
    public List<ColumnVerticalBarRun> MainBarRuns { get; set; } = new();
    public List<ColumnStirrupZonePlan> StirrupZones { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Groups vertically aligned structural columns within 25 mm tolerance, attaches them
/// cleanly to any supporting foundation (transferring starter dowel + 90 deg footing bend
/// ownership into the Column assembly), and solves splice zones per AutoFollowModelCut /
/// ForceSplicePerLevel / OptimizeFull12m without unnecessary cuts inside continuous columns.
/// </summary>
public static class ColumnChainAnalyzer
{
    public const double PlanarToleranceMm = 25.0;

    public static List<List<ColumnSegmentModel>> GroupVerticalChains(IEnumerable<ColumnSegmentModel> columns)
    {
        var ordered = columns
            .OrderBy(c => c.BaseZMm)
            .ThenBy(c => c.ElementId)
            .ToList();

        var chains = new List<List<ColumnSegmentModel>>();
        foreach (var col in ordered)
        {
            var match = chains.FirstOrDefault(ch =>
                Math.Abs(ch[0].CenterXMm - col.CenterXMm) <= PlanarToleranceMm &&
                Math.Abs(ch[0].CenterYMm - col.CenterYMm) <= PlanarToleranceMm);

            if (match != null)
            {
                match.Add(col);
            }
            else
            {
                chains.Add(new List<ColumnSegmentModel> { col });
            }
        }

        foreach (var ch in chains)
        {
            ch.Sort((a, b) => a.BaseZMm.CompareTo(b.BaseZMm));
        }
        return chains;
    }

    public static ColumnChainSolution SolveChain(
        IReadOnlyList<ColumnSegmentModel> sortedChain,
        DetectedFootingSupport? footing,
        ColumnConfig cfg,
        RebarGlobalSettings global)
    {
        var sol = new ColumnChainSolution
        {
            ChainId = $"COL-{sortedChain[0].ElementId}",
            CenterXMm = sortedChain[0].CenterXMm,
            CenterYMm = sortedChain[0].CenterYMm,
            Segments = sortedChain.ToList(),
            Footing = footing
        };

        double lapMm = StockSpliceSolver.ComputeLapLengthMm(cfg.MainBarDiaMm, global);
        double maxStock = Math.Max(3000.0, global.MaxStockLengthMm);

        // Compute base start Z inside foundation (columns OWN their footing bend & embedment!)
        double minColBaseZ = sortedChain[0].BaseZMm;
        double footingThick = footing?.ThicknessMm ?? 500.0;
        double footingBottomZ = footing != null ? footing.BottomZMm : (minColBaseZ - footingThick);
        // Sit just atop bottom pad mat (clearance e.g. 100 mm = ~75 cover + bottom X/Y bar dia)
        double barStartZ = footingBottomZ + Math.Max(75.0, cfg.FootingEmbedCoverClearanceMm);

        double baseBendLegMm = StockSpliceSolver.ComputeHookLegMm(
            cfg.BaseFootingBend,
            formula: "12D",
            memberDepthMm: sortedChain[0].DepthYMm,
            coverMm: cfg.CoverMm,
            diameterMm: cfg.MainBarDiaMm);
        if (cfg.BaseFootingBendLegMm > 0 && cfg.BaseFootingBend != EndHookDirection.None && cfg.BaseFootingBend != EndHookDirection.Straight)
            baseBendLegMm = Math.Max(cfg.BaseFootingBendLegMm, 12.0 * cfg.MainBarDiaMm);

        double topHookLegMm = cfg.RoofTopHook is EndHookDirection.Bend90Inward or EndHookDirection.Bend90Outward
            ? Math.Max(12.0 * cfg.MainBarDiaMm, Math.Min(sortedChain[^1].DepthYMm, sortedChain[^1].WidthXMm) - 2.0 * cfg.CoverMm)
            : 0.0;

        double chainTopZ = sortedChain[^1].TopZMm - cfg.CoverMm;

        if (footing == null)
            sol.Warnings.Add($"No Structural Foundation detected directly under column #{sortedChain[0].ElementId}; using default footing depth {footingThick:F0} mm for 90-deg base bend preview.");

        // Decide break Z elevations according to ColumnSpliceStrategy
        bool hasSingleUncutElement = sortedChain.Count == 1;
        bool shouldFollowCuts = cfg.SpliceStrategy == ColumnSpliceStrategy.AutoFollowModelCut;
        bool forceEachLevel = cfg.SpliceStrategy == ColumnSpliceStrategy.ForceSplicePerLevel;

        if (hasSingleUncutElement && !forceEachLevel)
        {
            // Single continuous column from base to top!
            var col = sortedChain[0];
            double totalHeightRun = chainTopZ - barStartZ;
            double totalLengthWithHooks = totalHeightRun + baseBendLegMm + topHookLegMm;

            if (totalLengthWithHooks <= maxStock + 1e-6)
            {
                // UNCUT <= 12 m: 1 continuous vertical bar + base bend + top roof bend -> ZERO intermediate splices!
                sol.MainBarRuns.Add(new ColumnVerticalBarRun
                {
                    SegmentIndex = 0,
                    PrimaryHostElementId = col.ElementId,
                    StartZMm = barStartZ,
                    EndZMm = chainTopZ,
                    BaseHookLegMm = baseBendLegMm,
                    TopHookLegMm = topHookLegMm,
                    CutLengthMm = StockSpliceSolver.RoundLengthMm(totalLengthWithHooks, global.RoundingMode),
                    OverlapBelowMm = 0.0,
                    StartsInsideFoundation = true,
                    TerminatesAtRoof = true
                });
            }
            else
            {
                // Uncut column exceeds 12,000 mm -> splice cleanly at floor levels if present, else max stock
                var breakElevs = BuildBreakElevationsForLongRun(
                    barStartZ, chainTopZ, baseBendLegMm, topHookLegMm, maxStock, lapMm,
                    col.IntersectingFloorOrBeamTopZMm);
                EmitSegmentedRuns(sol, col.ElementId, barStartZ, chainTopZ, breakElevs, baseBendLegMm, topHookLegMm, lapMm, maxStock, global, cfg);
            }
        }
        else if (cfg.SpliceStrategy == ColumnSpliceStrategy.OptimizeFull12m)
        {
            var interLevels = sortedChain
                .SelectMany(s => s.IntersectingFloorOrBeamTopZMm.Concat(new[] { s.TopZMm }))
                .Distinct()
                .OrderBy(z => z)
                .ToList();
            var breakElevs = BuildBreakElevationsForLongRun(
                barStartZ, chainTopZ, baseBendLegMm, topHookLegMm, maxStock, lapMm, interLevels);
            EmitSegmentedRuns(sol, sortedChain[0].ElementId, barStartZ, chainTopZ, breakElevs, baseBendLegMm, topHookLegMm, lapMm, maxStock, global, cfg, sortedChain);
        }
        else
        {
            // Either multiple cut column segments per floor (FollowModelCut) OR ForceSplicePerLevel
            var cutElevs = new List<double>();
            if (sortedChain.Count > 1)
            {
                for (int i = 0; i < sortedChain.Count - 1; i++)
                    cutElevs.Add(sortedChain[i].TopZMm);
            }
            if (forceEachLevel)
            {
                foreach (var s in sortedChain)
                {
                    foreach (double z in s.IntersectingFloorOrBeamTopZMm)
                    {
                        if (z > barStartZ + 800.0 && z < chainTopZ - 600.0 && !cutElevs.Any(c => Math.Abs(c - z) < 150.0))
                            cutElevs.Add(z);
                    }
                }
            }
            cutElevs = cutElevs.OrderBy(z => z).ToList();

            // Also ensure no single story segment + overlap exceeds 12m stock
            EmitFloorCutRuns(sol, sortedChain, barStartZ, chainTopZ, cutElevs, baseBendLegMm, topHookLegMm, lapMm, maxStock, global, cfg);
        }

        BuildStirrupZones(sol, sortedChain, barStartZ, minColBaseZ, cfg);
        return sol;
    }

    private static List<double> BuildBreakElevationsForLongRun(
        double barStartZ,
        double chainTopZ,
        double baseBendLegMm,
        double topHookLegMm,
        double maxStock,
        double lapMm,
        IReadOnlyList<double> candidateLevels)
    {
        var breaks = new List<double>();
        double cursorZ = barStartZ;
        bool isFirst = true;

        while (true)
        {
            double startHook = isFirst ? baseBendLegMm : 0.0;
            double maxReachZ = cursorZ + (maxStock - startHook) - lapMm;
            if (chainTopZ - cursorZ + startHook + topHookLegMm <= maxStock + 1e-6)
                break;

            // Choose highest safe floor joint <= maxReachZ (excluding bottom floor slab right on top of footing if too short)
            double? floorChoice = candidateLevels
                .Where(z => z > cursorZ + Math.Max(1500.0, lapMm + 400.0) && z <= maxReachZ)
                .Cast<double?>()
                .LastOrDefault();

            double nextBreak = floorChoice ?? Math.Min(maxReachZ, cursorZ + maxStock - startHook - lapMm - 100.0);
            if (nextBreak <= cursorZ + 600.0)
                break;

            breaks.Add(nextBreak);
            cursorZ = nextBreak;
            isFirst = false;
        }
        return breaks;
    }

    private static void EmitSegmentedRuns(
        ColumnChainSolution sol,
        long defaultHostId,
        double barStartZ,
        double chainTopZ,
        List<double> breakElevs,
        double baseBendLegMm,
        double topHookLegMm,
        double lapMm,
        double maxStock,
        RebarGlobalSettings global,
        ColumnConfig cfg,
        IReadOnlyList<ColumnSegmentModel>? chain = null)
    {
        double prevStart = barStartZ;
        int idx = 0;

        foreach (double brk in breakElevs)
        {
            double segEnd = brk + lapMm;
            double startHook = idx == 0 ? baseBendLegMm : 0.0;
            double len = (segEnd - prevStart) + startHook;
            long hostId = FindHostIdAtZ(chain, (prevStart + segEnd) * 0.5, defaultHostId);

            sol.MainBarRuns.Add(new ColumnVerticalBarRun
            {
                SegmentIndex = idx,
                PrimaryHostElementId = hostId,
                StartZMm = prevStart,
                EndZMm = segEnd,
                BaseHookLegMm = startHook,
                TopHookLegMm = 0.0,
                CutLengthMm = Math.Min(maxStock, StockSpliceSolver.RoundLengthMm(len, global.RoundingMode)),
                OverlapBelowMm = idx > 0 ? lapMm : 0.0,
                StartsInsideFoundation = idx == 0,
                TerminatesAtRoof = false
            });
            prevStart = brk;
            idx++;
        }

        double finalHook = topHookLegMm;
        double finalLen = (chainTopZ - prevStart) + (idx == 0 ? baseBendLegMm : 0.0) + finalHook;
        sol.MainBarRuns.Add(new ColumnVerticalBarRun
        {
            SegmentIndex = idx,
            PrimaryHostElementId = FindHostIdAtZ(chain, chainTopZ - 300.0, defaultHostId),
            StartZMm = prevStart,
            EndZMm = chainTopZ,
            BaseHookLegMm = idx == 0 ? baseBendLegMm : 0.0,
            TopHookLegMm = finalHook,
            CutLengthMm = Math.Min(maxStock, StockSpliceSolver.RoundLengthMm(finalLen, global.RoundingMode)),
            OverlapBelowMm = idx > 0 ? lapMm : 0.0,
            StartsInsideFoundation = idx == 0,
            TerminatesAtRoof = true
        });
    }

    private static void EmitFloorCutRuns(
        ColumnChainSolution sol,
        IReadOnlyList<ColumnSegmentModel> sortedChain,
        double barStartZ,
        double chainTopZ,
        List<double> cutElevs,
        double baseBendLegMm,
        double topHookLegMm,
        double lapMm,
        double maxStock,
        RebarGlobalSettings global,
        ColumnConfig cfg)
    {
        var joints = new List<double>(cutElevs);
        joints.Add(chainTopZ);

        double currentStartZ = barStartZ;
        int idx = 0;

        for (int i = 0; i < joints.Count; i++)
        {
            bool isFirst = idx == 0;
            bool isLast = i == joints.Count - 1;
            double jointZ = joints[i];
            double segEndZ = isLast ? chainTopZ : jointZ + lapMm;

            double startHook = isFirst ? baseBendLegMm : 0.0;
            double endHook = isLast ? topHookLegMm : 0.0;

            // Detect column size step at jointZ -> compute 1:6 crank
            bool hasCrank = false;
            double crankShift = 0.0;
            double crankHeight = 0.0;
            if (!isLast && cfg.EnableCrankAtOffset && i < sortedChain.Count - 1)
            {
                var lower = sortedChain[i];
                var upper = sortedChain[Math.Min(i + 1, sortedChain.Count - 1)];
                double dW = (lower.WidthXMm - upper.WidthXMm) * 0.5;
                double dD = (lower.DepthYMm - upper.DepthYMm) * 0.5;
                double maxDelta = Math.Max(dW, dD);
                if (maxDelta >= 15.0 && maxDelta <= 120.0)
                {
                    hasCrank = true;
                    crankShift = maxDelta;
                    crankHeight = maxDelta * 6.0; // ACI 318 / SNI 2847 1:6 max crank slope
                }
            }

            // If segment still > maxStock, sub-split gracefully
            double rawLen = (segEndZ - currentStartZ) + startHook + endHook;
            long hostId = FindHostIdAtZ(sortedChain, (currentStartZ + Math.Min(jointZ, segEndZ)) * 0.5, sortedChain[0].ElementId);

            if (rawLen > maxStock)
            {
                double midBreak = currentStartZ + (maxStock - startHook) - lapMm - 100.0;
                sol.MainBarRuns.Add(new ColumnVerticalBarRun
                {
                    SegmentIndex = idx++,
                    PrimaryHostElementId = hostId,
                    StartZMm = currentStartZ,
                    EndZMm = midBreak + lapMm,
                    BaseHookLegMm = startHook,
                    TopHookLegMm = 0.0,
                    CutLengthMm = Math.Min(maxStock, StockSpliceSolver.RoundLengthMm((midBreak + lapMm - currentStartZ) + startHook, global.RoundingMode)),
                    OverlapBelowMm = idx > 1 ? lapMm : 0.0,
                    StartsInsideFoundation = isFirst,
                    TerminatesAtRoof = false
                });
                currentStartZ = midBreak;
                startHook = 0.0;
                isFirst = false;
                rawLen = (segEndZ - currentStartZ) + endHook;
            }

            sol.MainBarRuns.Add(new ColumnVerticalBarRun
            {
                SegmentIndex = idx++,
                PrimaryHostElementId = hostId,
                StartZMm = currentStartZ,
                EndZMm = segEndZ,
                BaseHookLegMm = startHook,
                TopHookLegMm = endHook,
                CutLengthMm = Math.Min(maxStock, StockSpliceSolver.RoundLengthMm(rawLen, global.RoundingMode)),
                OverlapBelowMm = idx > 1 ? lapMm : 0.0,
                StartsInsideFoundation = isFirst,
                TerminatesAtRoof = isLast,
                HasCrankOffset = hasCrank,
                CrankInwardShiftMm = crankShift,
                CrankHeightMm = crankHeight
            });

            currentStartZ = jointZ;
        }
    }

    private static long FindHostIdAtZ(IReadOnlyList<ColumnSegmentModel>? chain, double zMm, long fallbackId)
    {
        if (chain == null || chain.Count == 0) return fallbackId;
        var exact = chain.FirstOrDefault(c => zMm >= c.BaseZMm - 50.0 && zMm <= c.TopZMm + 50.0);
        return exact?.ElementId ?? chain.OrderBy(c => Math.Abs(((c.BaseZMm + c.TopZMm) * 0.5) - zMm)).First().ElementId;
    }

    private static void BuildStirrupZones(
        ColumnChainSolution sol,
        IReadOnlyList<ColumnSegmentModel> sortedChain,
        double barStartZ,
        double minColBaseZ,
        ColumnConfig cfg)
    {
        // 1. Confining ties INSIDE foundation pedestal core (above 90 deg base bend)
        if (cfg.IncludeFoundationConfiningTies && minColBaseZ - barStartZ > 150.0)
        {
            sol.StirrupZones.Add(new ColumnStirrupZonePlan
            {
                HostElementId = sortedChain[0].ElementId,
                ZoneLabel = "Pondasi Core Ties",
                StartZMm = barStartZ + 50.0,
                EndZMm = minColBaseZ - 30.0,
                SpacingMm = cfg.FoundationTieSpacingMm,
                IsInsideFoundation = true,
                IsDenseZone = true
            });
        }

        // 2. For each column segment (plus any intermediate floor/beam intersections inside uncut multi-story columns):
        foreach (var col in sortedChain)
        {
            double loLen = Math.Max(450.0, Math.Max(Math.Max(col.WidthXMm, col.DepthYMm), (col.TopZMm - col.BaseZMm) / 6.0));
            var breakPts = col.IntersectingFloorOrBeamTopZMm
                .Where(z => z > col.BaseZMm + 300.0 && z < col.TopZMm - 300.0)
                .OrderBy(z => z)
                .ToList();

            var subSpans = new List<(double Z0, double Z1)>();
            double cur = col.BaseZMm;
            foreach (double bp in breakPts)
            {
                subSpans.Add((cur, bp));
                cur = bp;
            }
            subSpans.Add((cur, col.TopZMm));

            foreach (var (z0, z1) in subSpans)
            {
                double h = z1 - z0;
                if (h < 200.0) continue;

                double actualLo = Math.Min(loLen, h * 0.30);
                double botEnd = z0 + actualLo;
                double topStart = z1 - actualLo;

                sol.StirrupZones.Add(new ColumnStirrupZonePlan
                {
                    HostElementId = col.ElementId,
                    ZoneLabel = "Tumpuan Bawah (l_o)",
                    StartZMm = z0 + 50.0,
                    EndZMm = botEnd,
                    SpacingMm = cfg.TieLoSpacingMm,
                    IsInsideFoundation = false,
                    IsDenseZone = true
                });

                if (topStart - botEnd > cfg.TieMidSpacingMm)
                {
                    sol.StirrupZones.Add(new ColumnStirrupZonePlan
                    {
                        HostElementId = col.ElementId,
                        ZoneLabel = "Tengah Kolom",
                        StartZMm = botEnd + cfg.TieMidSpacingMm * 0.5,
                        EndZMm = topStart - cfg.TieMidSpacingMm * 0.5,
                        SpacingMm = cfg.TieMidSpacingMm,
                        IsInsideFoundation = false,
                        IsDenseZone = false
                    });
                }

                sol.StirrupZones.Add(new ColumnStirrupZonePlan
                {
                    HostElementId = col.ElementId,
                    ZoneLabel = "Tumpuan Atas (l_o)",
                    StartZMm = topStart,
                    EndZMm = z1 - 50.0,
                    SpacingMm = cfg.TieLoSpacingMm,
                    IsInsideFoundation = false,
                    IsDenseZone = true
                });
            }
        }
    }
}

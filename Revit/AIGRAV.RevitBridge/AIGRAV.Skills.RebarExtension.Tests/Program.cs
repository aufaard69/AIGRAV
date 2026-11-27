using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;

namespace AIGRAV.Skills.RebarExtension.Tests;

internal static class Program
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Check(string name, bool condition, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  [PASS] {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  [FAIL] {name} :: {detail}");
        }
    }

    public static int Main()
    {
        Console.WriteLine("=== AIGRAV RebarExtension Deterministic Solver Tests ===");
        Test_StockSplice_SingleBarFitsWithHook();
        Test_StockSplice_MultiSpanNeverExceeds12mAndOverlapsCleanly();
        Test_StockSplice_PoorBondAndRounding();
        Test_Beam_SniCutoffModes();
        Test_Column_UncutBelow12mHasZeroIntermediateSplices();
        Test_Column_CutPerLevelHasStartersAndCrank();
        Test_Column_ChainPlanarGrouping25mm();
        Test_Floor_PolygonVoidClipper();
        Test_Beam_QuarterHalfQuarterStirrupZones();

        Console.WriteLine($"\nSummary: {_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void Test_StockSplice_SingleBarFitsWithHook()
    {
        var g = new RebarGlobalSettings { MaxStockLengthMm = 12000, LapMode = LapMode.D40, RoundingMode = LengthRoundingMode.Nearest25Mm };
        var pieces = StockSpliceSolver.Solve1DSplice(0, 9000, 16, 420, 420, true, new[] { 0.0, 4500.0, 9000.0 }, g, 0);
        Check("Single 9m span + hooks fits in 1 piece <= 12m", pieces.Count == 1 && pieces[0].TotalCutLengthMm <= 12000,
            $"count={pieces.Count}, cut={pieces.FirstOrDefault()?.TotalCutLengthMm}");
        Check("Single piece has both start & end hooks", pieces[0].HasStartHook && pieces[0].HasEndHook);
    }

    private static void Test_StockSplice_MultiSpanNeverExceeds12mAndOverlapsCleanly()
    {
        var g = new RebarGlobalSettings
        {
            MaxStockLengthMm = 12000,
            LapMode = LapMode.D40,
            BondCondition = BondCondition.Good,
            MomentSafeSplit = true,
            StaggerSplices = true,
            RoundingMode = LengthRoundingMode.Nearest25Mm
        };
        double[] supports = { 0, 6000, 12000, 18000, 24000, 30000 }; // 30 m 5-span beam!
        var layerA = StockSpliceSolver.Solve1DSplice(0, 30000, 19, 420, 420, true, supports, g, 0);
        var layerB = StockSpliceSolver.Solve1DSplice(0, 30000, 19, 420, 420, true, supports, g, 1);

        Check("30m top bar splits into >= 3 pieces", layerA.Count >= 3, $"count={layerA.Count}");
        Check("Every piece total cut length <= 12,000 mm (incl. 420mm end hook!)",
            layerA.All(p => p.TotalCutLengthMm <= 12000.0 + 1e-6) && layerB.All(p => p.TotalCutLengthMm <= 12000.0 + 1e-6),
            $"maxA={layerA.Max(p => p.TotalCutLengthMm)}, maxB={layerB.Max(p => p.TotalCutLengthMm)}");

        double expectedLap = StockSpliceSolver.ComputeLapLengthMm(19, g); // 40 * 19 = 760 mm
        bool overlapValid = true;
        for (int i = 1; i < layerA.Count; i++)
        {
            double actualOverlap = layerA[i - 1].EndCoordMm - layerA[i].StartCoordMm;
            if (Math.Abs(actualOverlap - expectedLap) > 1.0) overlapValid = false;
        }
        Check($"Adjacent pieces overlap by exact l_lap ({expectedLap} mm)", overlapValid);
        Check("Staggered Layer B cuts differ from Layer A first cut", Math.Abs(layerA[0].EndCoordMm - layerB[0].EndCoordMm) > 200.0,
            $"A0={layerA[0].EndCoordMm}, B0={layerB[0].EndCoordMm}");
    }

    private static void Test_StockSplice_PoorBondAndRounding()
    {
        var gGood = new RebarGlobalSettings { LapMode = LapMode.D40, BondCondition = BondCondition.Good };
        var gPoor = new RebarGlobalSettings { LapMode = LapMode.D40, BondCondition = BondCondition.Poor };
        double lapGood = StockSpliceSolver.ComputeLapLengthMm(16, gGood); // 640 mm
        double lapPoor = StockSpliceSolver.ComputeLapLengthMm(16, gPoor); // 640 * 1.3 = 832 -> ceil 5 -> 835 mm
        Check("Good bond 40*D16 == 640 mm", Math.Abs(lapGood - 640.0) < 0.1, $"got {lapGood}");
        Check("Poor bond 1.3x == 835 mm", Math.Abs(lapPoor - 835.0) < 0.1, $"got {lapPoor}");
        Check("Coupler mode returns 0 mm overlap", StockSpliceSolver.ComputeLapLengthMm(22, new RebarGlobalSettings { LapMode = LapMode.Coupler }) == 0.0);
    }

    private static void Test_Beam_SniCutoffModes()
    {
        double sni = StockSpliceSolver.ComputeCutoffExtensionMm(CutoffExtensionMode.SniMaxDOr12D, 450, 16); // max(450, 192) = 450
        double conservative = StockSpliceSolver.ComputeCutoffExtensionMm(CutoffExtensionMode.L20D, 450, 16); // 320
        double zero = StockSpliceSolver.ComputeCutoffExtensionMm(CutoffExtensionMode.Zero, 450, 16);
        Check("SNI max(d, 12D) returns 450 mm for d=450, D16", Math.Abs(sni - 450) < 0.1);
        Check("20D returns 320 mm for D16", Math.Abs(conservative - 320) < 0.1);
        Check("Zero returns 0 mm", Math.Abs(zero) < 0.1);
    }

    private static void Test_Column_UncutBelow12mHasZeroIntermediateSplices()
    {
        var g = new RebarGlobalSettings { MaxStockLengthMm = 12000, LapMode = LapMode.D40 };
        var cfg = new ColumnConfig { SpliceStrategy = ColumnSpliceStrategy.AutoFollowModelCut, MainBarDiaMm = 19, BaseFootingBendLegMm = 300, FootingEmbedCoverClearanceMm = 100 };
        var singleCol = new ColumnSegmentModel
        {
            ElementId = 1001, CenterXMm = 0, CenterYMm = 0, BaseZMm = 0, TopZMm = 8000,
            WidthXMm = 400, DepthYMm = 400,
            IntersectingFloorOrBeamTopZMm = new List<double> { 4000 } // Uncut multi-story column with Floor 2 @ +4.0m
        };
        var footing = new DetectedFootingSupport { FoundationElementId = 2001, TopZMm = 0, BottomZMm = -500 };

        var sol = ColumnChainAnalyzer.SolveChain(new[] { singleCol }, footing, cfg, g);
        Check("Uncut 8m col (total ~8.8m <= 12m) generates 1 continuous main bar with ZERO splices",
            sol.MainBarRuns.Count == 1 && sol.MainBarRuns[0].StartsInsideFoundation && sol.MainBarRuns[0].TerminatesAtRoof,
            $"runs={sol.MainBarRuns.Count}");
        Check("Uncut col starts at -400mm inside footing (not at Z=0)", Math.Abs(sol.MainBarRuns[0].StartZMm - (-400)) < 1.0,
            $"startZ={sol.MainBarRuns[0].StartZMm}");
        Check("Uncut multi-story col STILL gets dense l_o tie zones around intermediate floor @ +4000mm + footing core ties",
            sol.StirrupZones.Count(z => z.IsDenseZone) >= 5 && sol.StirrupZones.Any(z => z.IsInsideFoundation),
            $"denseZones={sol.StirrupZones.Count(z => z.IsDenseZone)}");
    }

    private static void Test_Column_CutPerLevelHasStartersAndCrank()
    {
        var g = new RebarGlobalSettings { MaxStockLengthMm = 12000, LapMode = LapMode.D40 };
        var cfg = new ColumnConfig { SpliceStrategy = ColumnSpliceStrategy.AutoFollowModelCut, MainBarDiaMm = 19, EnableCrankAtOffset = true };
        var segs = new List<ColumnSegmentModel>
        {
            new() { ElementId = 101, BaseZMm = 0, TopZMm = 4000, WidthXMm = 500, DepthYMm = 500 },
            new() { ElementId = 102, BaseZMm = 4000, TopZMm = 8000, WidthXMm = 400, DepthYMm = 400 } // Steps down 50mm per side!
        };
        var footing = new DetectedFootingSupport { FoundationElementId = 201, TopZMm = 0, BottomZMm = -600 };

        var sol = ColumnChainAnalyzer.SolveChain(segs, footing, cfg, g);
        Check("Cut 2-level column produces 2 spliced vertical runs", sol.MainBarRuns.Count == 2);
        Check("Lower run extends starter dowel 40D (760mm) above floor cut Z=4000",
            Math.Abs(sol.MainBarRuns[0].EndZMm - 4760.0) < 1.0, $"endZ={sol.MainBarRuns[0].EndZMm}");
        Check("Lower run detects 1:6 crank when column shrinks 500->400 (50mm/side => crankHeight=300mm)",
            sol.MainBarRuns[0].HasCrankOffset && Math.Abs(sol.MainBarRuns[0].CrankInwardShiftMm - 50) < 0.1 && Math.Abs(sol.MainBarRuns[0].CrankHeightMm - 300) < 0.1);
    }

    private static void Test_Column_ChainPlanarGrouping25mm()
    {
        var cols = new List<ColumnSegmentModel>
        {
            new() { ElementId = 1, CenterXMm = 1000, CenterYMm = 2000, BaseZMm = 4000, TopZMm = 8000 },
            new() { ElementId = 2, CenterXMm = 1010, CenterYMm = 1995, BaseZMm = 0, TopZMm = 4000 },
            new() { ElementId = 3, CenterXMm = 5000, CenterYMm = 2000, BaseZMm = 0, TopZMm = 4000 }
        };
        var chains = ColumnChainAnalyzer.GroupVerticalChains(cols);
        Check("Groups 2 aligned columns within 25mm sorted bottom-up, keeps distant column separate",
            chains.Count == 2 && chains[0].Count == 2 && chains[0][0].ElementId == 2 && chains[0][1].ElementId == 1);
    }

    private static void Test_Floor_PolygonVoidClipper()
    {
        var outer = new List<Point3DData>
        {
            new(0, 0, 0), new(10000, 0, 0), new(10000, 8000, 0), new(0, 8000, 0)
        };
        var hole = new List<Point3DData>
        {
            new(4000, 3000, 0), new(6000, 3000, 0), new(6000, 5000, 0), new(4000, 5000, 0)
        };
        var clippedThroughHole = PolygonClipper2D.ClipScanlineAlongX(4000, 25, outer, new List<IReadOnlyList<Point3DData>> { hole });
        Check("Scanline y=4000 splits cleanly into 2 intervals around void opening [4000..6000]",
            clippedThroughHole.Count == 2 && Math.Abs(clippedThroughHole[0].EndMm - 3975) < 1.0 && Math.Abs(clippedThroughHole[1].StartMm - 6025) < 1.0,
            $"count={clippedThroughHole.Count}");

        var clippedOutsideHole = PolygonClipper2D.ClipScanlineAlongX(1500, 25, outer, new List<IReadOnlyList<Point3DData>> { hole });
        Check("Scanline y=1500 remains 1 continuous segment [25..9975]",
            clippedOutsideHole.Count == 1 && Math.Abs(clippedOutsideHole[0].StartMm - 25) < 1.0 && Math.Abs(clippedOutsideHole[0].EndMm - 9975) < 1.0);
    }

    private static void Test_Beam_QuarterHalfQuarterStirrupZones()
    {
        var cfg = new BeamConfig
        {
            StirrupZoneMode = StirrupZoneMode.QuarterSpan_L4_L2_L4,
            StirrupSupportZoneRatio = 0.25,
            StirrupSupportSpacingMm = 100,
            StirrupMidspanSpacingMm = 150
        };
        // Clear span 4000mm on deep 600mm beam -> MUST stay 1/4 (1000) / 1/2 (2000) / 1/4 (1000), NOT 2h=1200!
        var zones = StockSpliceSolver.ComputeBeamStirrupZonesForSpan(0, 4000, 600, cfg, 1);
        Check("Beam stirrups partition into 3 contiguous zones (Tump-L, Lap, Tump-R)", zones.Count == 3);
        Check("Tumpuan Kiri == 1/4 bentang [0..1000] with 11 bars @ 100mm",
            zones[0].StartMm == 0 && Math.Abs(zones[0].EndMm - 1000) < 1.0 && zones[0].Qty == 11);
        Check("Lapangan sits strictly in middle 1/2 bentang without overlapping zone seams",
            zones[1].StartMm > zones[0].EndMm + 120 && zones[1].EndMm < zones[2].StartMm - 120,
            $"lap=[{zones[1].StartMm:F0}..{zones[1].EndMm:F0}]");
        Check("Tumpuan Kanan == symmetrical 1/4 bentang [3000..4000] with 11 bars @ 100mm",
            Math.Abs(zones[2].StartMm - 3000) < 1.0 && Math.Abs(zones[2].EndMm - 4000) < 1.0 && zones[2].Qty == 11);
    }
}

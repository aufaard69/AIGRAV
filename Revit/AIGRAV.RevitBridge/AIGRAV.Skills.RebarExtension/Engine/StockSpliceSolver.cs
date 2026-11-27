using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Skills.RebarExtension.Models;

namespace AIGRAV.Skills.RebarExtension.Engine;

public sealed record SplicedBarPiece(
    int PieceIndex,
    int LayerIndex,
    double StartCoordMm,
    double EndCoordMm,
    double StartHookLegMm,
    double EndHookLegMm,
    bool HasStartHook,
    bool HasEndHook,
    double TransverseOffsetMm,
    double OverlapWithPrevMm,
    double TotalCutLengthMm,
    bool IsStaggeredLayer);

public readonly record struct StirrupZoneSpec(
    string Label,
    double StartMm,
    double EndMm,
    int Qty,
    double EffectiveSpacingMm);

public static class StockSpliceSolver
{
    public static List<StirrupZoneSpec> ComputeBeamStirrupZonesForSpan(
        double clearStartMm,
        double clearEndMm,
        double beamDepthMm,
        BeamConfig cfg,
        int spanNumber = 1)
    {
        var result = new List<StirrupZoneSpec>();
        double clearL = clearEndMm - clearStartMm;
        if (clearL < 200.0) return result;

        double sTump = Math.Max(40.0, cfg.StirrupSupportSpacingMm);
        double sLap = Math.Max(40.0, cfg.StirrupMidspanSpacingMm);

        double tumpLen = cfg.StirrupZoneMode == StirrupZoneMode.SniSrpmkMax2hOrL4
            ? Math.Min(clearL * 0.35, Math.Max(clearL * 0.25, 2.0 * beamDepthMm))
            : clearL * Math.Clamp(cfg.StirrupSupportZoneRatio, 0.15, 0.35);

        int nIntervalsTump = Math.Max(1, (int)Math.Floor(tumpLen / sTump));
        double zone1End = clearStartMm + nIntervalsTump * sTump;
        double zone3Start = clearEndMm - nIntervalsTump * sTump;

        result.Add(new StirrupZoneSpec($"Sengkang Tumpuan S{spanNumber}-L (1/4 L)", clearStartMm, zone1End, nIntervalsTump + 1, sTump));

        double midGap = zone3Start - zone1End;
        if (midGap > sLap * 1.4)
        {
            int midIntervals = Math.Max(2, (int)Math.Round(midGap / sLap));
            double actualStep = midGap / midIntervals;
            double lapFirst = zone1End + actualStep;
            double lapLast = zone3Start - actualStep;
            int qtyMid = midIntervals - 1;
            if (qtyMid >= 2 && lapLast > lapFirst + 10.0)
            {
                result.Add(new StirrupZoneSpec($"Sengkang Lapangan S{spanNumber} (1/2 L)", lapFirst, lapLast, qtyMid, actualStep));
            }
            else if (qtyMid == 1)
            {
                result.Add(new StirrupZoneSpec($"Sengkang Lapangan S{spanNumber} (1/2 L)", lapFirst, lapFirst + 20.0, 1, actualStep));
            }
        }

        result.Add(new StirrupZoneSpec($"Sengkang Tumpuan S{spanNumber}-R (1/4 L)", zone3Start, clearEndMm, nIntervalsTump + 1, sTump));
        return result;
    }
    public static double ComputeLapLengthMm(int diameterMm, RebarGlobalSettings global)
    {
        if (global.LapMode == LapMode.Coupler) return 0.0;

        double raw = global.LapMode switch
        {
            LapMode.D40 => 40.0 * diameterMm,
            LapMode.D50 => 50.0 * diameterMm,
            LapMode.D60 => 60.0 * diameterMm,
            LapMode.CustomMm => Math.Max(300.0, global.CustomLapMm),
            _ => 40.0 * diameterMm
        };

        if (global.BondCondition == BondCondition.Poor)
            raw *= 1.3;

        // Always round lap length UP to nearest 5 mm, minimum 300 mm per SNI 2847-2019 §25.5.2.1
        return Math.Max(300.0, Math.Ceiling(raw / 5.0) * 5.0);
    }

    public static double ComputeCutoffExtensionMm(CutoffExtensionMode mode, double effectiveDepthDMm, int diameterMm)
    {
        return mode switch
        {
            CutoffExtensionMode.SniMaxDOr12D => Math.Max(Math.Max(0.0, effectiveDepthDMm), 12.0 * diameterMm),
            CutoffExtensionMode.L20D => 20.0 * diameterMm,
            CutoffExtensionMode.Zero => 0.0,
            _ => Math.Max(effectiveDepthDMm, 12.0 * diameterMm)
        };
    }

    public static double ComputeHookLegMm(EndHookDirection dir, string formula, double memberDepthMm, double coverMm, int diameterMm)
    {
        if (dir == EndHookDirection.None || dir == EndHookDirection.Straight)
            return 0.0;

        if (formula.Equals("12D", StringComparison.OrdinalIgnoreCase))
            return Math.Max(150.0, 12.0 * diameterMm);

        if (formula.Equals("15D", StringComparison.OrdinalIgnoreCase))
            return Math.Max(180.0, 15.0 * diameterMm);

        // Default "h-2c": bend into joint core, bounded reasonably
        return Math.Max(12.0 * diameterMm, memberDepthMm - 2.0 * coverMm);
    }

    public static double RoundLengthMm(double rawMm, LengthRoundingMode mode)
    {
        double step = Math.Max(1.0, (int)mode);
        return Math.Ceiling(rawMm / step) * step;
    }

    /// <summary>
    /// Splits a continuous 1D bar run [startMm .. endMm] strictly keeping every single bar's total cut length
    /// (straight run + start hook + end hook) <= MaxStockLengthMm, shifting cuts safely out of high-moment zones
    /// when MomentSafeSplit is enabled, and staggering layers B relative to A when StaggerSplices is enabled.
    /// </summary>
    public static List<SplicedBarPiece> Solve1DSplice(
        double startMm,
        double endMm,
        int diameterMm,
        double startHookMm,
        double endHookMm,
        bool isTensionTopBar,
        IReadOnlyList<double>? supportCentersMm,
        RebarGlobalSettings global,
        int layerIndex = 0)
    {
        var result = new List<SplicedBarPiece>();
        double totalRun = endMm - startMm;
        if (totalRun <= 10.0) return result;

        double maxStock = Math.Max(2000.0, global.MaxStockLengthMm);
        double lapMm = ComputeLapLengthMm(diameterMm, global);
        double staggerOffsetMm = (global.StaggerSplices && layerIndex % 2 == 1)
            ? Math.Max(lapMm * Math.Max(1.0, global.StaggerShiftFactor), 600.0)
            : 0.0;

        // Check single piece fit first (including hooks!)
        if (totalRun + startHookMm + endHookMm <= maxStock + 1e-6)
        {
            double cutLen = Math.Min(maxStock, RoundLengthMm(totalRun + startHookMm + endHookMm, global.RoundingMode));
            result.Add(new SplicedBarPiece(
                PieceIndex: 0,
                LayerIndex: layerIndex,
                StartCoordMm: startMm,
                EndCoordMm: endMm,
                StartHookLegMm: startHookMm,
                EndHookLegMm: endHookMm,
                HasStartHook: startHookMm > 1.0,
                HasEndHook: endHookMm > 1.0,
                TransverseOffsetMm: 0.0,
                OverlapWithPrevMm: 0.0,
                TotalCutLengthMm: cutLen,
                IsStaggeredLayer: layerIndex % 2 == 1));
            return result;
        }

        var supports = (supportCentersMm ?? Array.Empty<double>())
            .OrderBy(x => x)
            .ToList();
        if (supports.Count < 2)
        {
            supports = new List<double> { startMm, endMm };
        }

        double cursorStart = startMm;
        int idx = 0;
        const double minTailMm = 650.0;

        while (cursorStart < endMm - 1.0 && idx < 64)
        {
            bool isFirst = idx == 0;
            double activeStartHook = isFirst ? startHookMm : 0.0;
            double maxEndByStock = cursorStart + (maxStock - activeStartHook);

            // Check if remainder fits cleanly into final bar (with end hook)
            if (endMm - cursorStart + activeStartHook + endHookMm <= maxStock + 1e-6)
            {
                double remCut = RoundLengthMm((endMm - cursorStart) + activeStartHook + endHookMm, global.RoundingMode);
                if (remCut > maxStock) remCut = (endMm - cursorStart) + activeStartHook + endHookMm;

                result.Add(new SplicedBarPiece(
                    PieceIndex: idx,
                    LayerIndex: layerIndex,
                    StartCoordMm: cursorStart,
                    EndCoordMm: endMm,
                    StartHookLegMm: activeStartHook,
                    EndHookLegMm: endHookMm,
                    HasStartHook: activeStartHook > 1.0,
                    HasEndHook: endHookMm > 1.0,
                    TransverseOffsetMm: (idx % 2 == 1 && lapMm > 0) ? Math.Max(diameterMm, 18.0) : 0.0,
                    OverlapWithPrevMm: idx > 0 ? lapMm : 0.0,
                    TotalCutLengthMm: Math.Min(maxStock, remCut),
                    IsStaggeredLayer: staggerOffsetMm > 0));
                break;
            }

            // Determine ideal cut target within [minAllowedCut .. maxEndByStock]
            double desiredCut = maxEndByStock - 50.0; // 50mm margin before rounding

            // Avoid leaving an awkwardly tiny final tail (< minTailMm + lap)
            if (endMm - desiredCut < minTailMm && endMm - desiredCut > 0)
            {
                desiredCut = Math.Min(maxEndByStock, endMm - minTailMm - lapMm);
            }

            if (global.MomentSafeSplit && supports.Count >= 2)
            {
                desiredCut = AdjustCutToMomentSafeZone(
                    cursorStart, desiredCut, maxEndByStock, endMm, lapMm, isTensionTopBar, supports);
            }

            if (isFirst && staggerOffsetMm > 0)
            {
                double shifted = desiredCut - staggerOffsetMm;
                if (global.MomentSafeSplit && supports.Count >= 2)
                {
                    shifted = AdjustCutToMomentSafeZone(
                        cursorStart, shifted, desiredCut - 200.0, endMm, lapMm, isTensionTopBar, supports, preferLowerEnd: true);
                }
                if (shifted - cursorStart >= 1000.0 && Math.Abs(shifted - desiredCut) >= 200.0)
                    desiredCut = shifted;
            }

            // Round down/nearest straight run length strictly respecting maxStock
            double rawStraight = desiredCut - cursorStart;
            double step = Math.Max(1.0, (int)global.RoundingMode);
            double roundedStraight = Math.Floor(rawStraight / step) * step;
            double actualEnd = Math.Min(maxEndByStock, cursorStart + Math.Max(minTailMm, roundedStraight));

            // Make sure we advance forward past lap length so cursor doesn't stall
            if (actualEnd - cursorStart <= lapMm + 250.0)
                actualEnd = Math.Min(maxEndByStock, cursorStart + Math.Max(lapMm + 400.0, (maxEndByStock - cursorStart) * 0.75));

            double totalPieceCut = (actualEnd - cursorStart) + activeStartHook;
            result.Add(new SplicedBarPiece(
                PieceIndex: idx,
                LayerIndex: layerIndex,
                StartCoordMm: cursorStart,
                EndCoordMm: actualEnd,
                StartHookLegMm: activeStartHook,
                EndHookLegMm: 0.0,
                HasStartHook: activeStartHook > 1.0,
                HasEndHook: false,
                TransverseOffsetMm: (idx % 2 == 1 && lapMm > 0) ? Math.Max(diameterMm, 18.0) : 0.0,
                OverlapWithPrevMm: idx > 0 ? lapMm : 0.0,
                TotalCutLengthMm: Math.Min(maxStock, Math.Round(totalPieceCut, 1)),
                IsStaggeredLayer: staggerOffsetMm > 0));

            cursorStart = actualEnd - lapMm;
            idx++;
        }

        return result;
    }

    private static double AdjustCutToMomentSafeZone(
        double cursorStart,
        double candidateCut,
        double maxAllowedCut,
        double endMm,
        double lapMm,
        bool isTensionTopBar,
        List<double> supports,
        bool preferLowerEnd = false)
    {
        // Identify span k containing candidateCut
        for (int k = 0; k < supports.Count - 1; k++)
        {
            double sL = supports[k];
            double sR = supports[k + 1];
            double L = sR - sL;
            if (L <= 100.0) continue;

            if (candidateCut >= sL - 10.0 && candidateCut <= sR + 10.0)
            {
                if (isTensionTopBar)
                {
                    // Top bar should be spliced near midspan [0.28L .. 0.72L] (away from support hogging)
                    double safeMin = sL + 0.28 * L;
                    double safeMax = sL + 0.72 * L;
                    if (candidateCut >= safeMin && candidateCut <= safeMax)
                        return Math.Min(candidateCut, maxAllowedCut);

                    if (candidateCut > safeMax)
                    {
                        double target = preferLowerEnd ? Math.Max(safeMin, Math.Min(safeMax, maxAllowedCut)) : safeMax;
                        if (target >= cursorStart + lapMm + 400.0 && target <= maxAllowedCut)
                            return target;
                    }
                    if (candidateCut < safeMin && k > 0)
                    {
                        double prevL = supports[k] - supports[k - 1];
                        double prevSafeMax = Math.Min(maxAllowedCut, supports[k - 1] + 0.72 * prevL);
                        if (prevSafeMax >= cursorStart + lapMm + 400.0 && prevSafeMax <= maxAllowedCut)
                            return prevSafeMax;
                    }
                }
                else
                {
                    // Bottom bar should be spliced near support/inflection [0.05L .. 0.25L] or [0.75L .. 0.95L]
                    double midStart = sL + 0.25 * L;
                    double midEnd = sL + 0.75 * L;
                    if (candidateCut <= midStart || candidateCut >= midEnd)
                        return candidateCut;

                    // Inside sagging midspan tension zone -> shift backward into [0.15L .. 0.25L] of current span
                    double safeLeft = sL + 0.22 * L;
                    if (safeLeft >= cursorStart + lapMm + 400.0 && safeLeft <= maxAllowedCut && endMm - safeLeft > lapMm + 300.0)
                        return safeLeft;
                }
                break;
            }
        }
        return Math.Min(candidateCut, maxAllowedCut);
    }
}

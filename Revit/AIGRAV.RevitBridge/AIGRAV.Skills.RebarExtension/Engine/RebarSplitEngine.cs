using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Contracts;
using AIGRAV.Skills.RebarExtension.Builders;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Engine;

/// <summary>
/// Implements SOFiSTiK-style interactive Detailing & Editing tools on existing Revit Rebars:
/// - Split / Split Staggered / Split CustomZones (breaks bars > MaxStock into spliced pieces)
/// - Propagate (clones reinforcement pattern from one host to compatible target hosts)
/// - RebarConstraints preference toggle & cover-constraint repair
/// </summary>
public static class RebarSplitEngine
{
    public static RebarExecutionResult SplitRebars(
        Document doc,
        IReadOnlyList<long> rebarIds,
        SplitMethod method,
        RebarGlobalSettings global,
        double? customSplitDistanceMm = null)
    {
        var result = new RebarExecutionResult();
        if (rebarIds.Count == 0)
        {
            result.Message = "Pilih minimal satu elemen Rebar untuk di-Split.";
            return result;
        }

        var resolver = new RebarResourceResolver();
        using var tg = new TransactionGroup(doc, $"AIGRAV Rebar {method}");
        tg.Start();

        using var tx = new Transaction(doc, $"Split {rebarIds.Count} Rebar Set(s)");
        TransactionFailureHandling.SuppressWarnings(tx);
        tx.Start();

        try
        {
            int pairToggle = 0;
            foreach (long rId in rebarIds)
            {
                if (doc.GetElement(new ElementId(rId)) is not Rebar rebar) continue;
                Element? host = doc.GetElement(rebar.GetHostId());
                if (host == null)
                {
                    result.Warnings.Add($"Rebar #{rId} has no valid host; skipped.");
                    continue;
                }

                RebarBarType? barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
                int diaMm = barType != null ? Math.Max(8, (int)Math.Round(RebarResourceResolver.FeetToMm(barType.BarNominalDiameter))) : 16;

                IList<Curve> curves = rebar.GetCenterlineCurves(
                    adjustForSelfIntersection: false,
                    suppressHooksAndCranks: true,
                    suppressBendRadius: true,
                    multiplanarOption: MultiplanarOption.IncludeOnlyPlanarCurves,
                    barPositionIndex: 0);

                if (curves.Count != 1 || curves[0] is not Line line)
                {
                    result.Warnings.Add($"Rebar #{rId} is multi-segment/stirrup; automated straight split applies cleanly to straight segments.");
                    continue;
                }

                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);
                XYZ dir = (p1 - p0).Normalize();
                double totalMm = RebarResourceResolver.FeetToMm(p0.DistanceTo(p1));

                RebarGlobalSettings splitCfg = global with { StaggerSplices = method == SplitMethod.Staggered || global.StaggerSplices };
                if (method == SplitMethod.CustomZones && customSplitDistanceMm is > 1000.0)
                    splitCfg = splitCfg with { MaxStockLengthMm = customSplitDistanceMm.Value };

                var pieces = StockSpliceSolver.Solve1DSplice(
                    0.0, totalMm, diaMm, 0.0, 0.0, isTensionTopBar: false, null, splitCfg, pairToggle++);

                if (pieces.Count <= 1)
                {
                    result.Warnings.Add($"Rebar #{rId} length {totalMm:F0} mm fits within {splitCfg.MaxStockLengthMm:F0} mm; no split required.");
                    continue;
                }

                RebarShapeDrivenAccessor acc = rebar.GetShapeDrivenAccessor();
                int qty = Math.Max(1, rebar.Quantity);
                double arrayLenFt = qty > 1 ? acc.ArrayLength : 0.0;
                bool barsOnNormalSide = acc.BarsOnNormalSide;
                XYZ norm = rebar.GetShapeDrivenAccessor() != null ? ComputeOrthogonalNorm(dir) : XYZ.BasisZ;
                RebarStyle style = rebar.LayoutRule == RebarLayoutRule.Single ? RebarStyle.Standard : RebarStyle.Standard;
                string lokasi = RebarIdempotencyManager.TryGetStringParam(rebar, RebarIdempotencyManager.ParamLokasi) ?? global.DefaultLokasi;
                string hostHash = RebarIdempotencyManager.ComputeHostGeometryHash(host);

                foreach (var p in pieces)
                {
                    XYZ sPt = p0 + dir * RebarResourceResolver.MmToFeet(p.StartCoordMm)
                        + norm * RebarResourceResolver.MmToFeet(p.TransverseOffsetMm * 0.5);
                    XYZ ePt = p0 + dir * RebarResourceResolver.MmToFeet(p.EndCoordMm)
                        + norm * RebarResourceResolver.MmToFeet(p.TransverseOffsetMm * 0.5);

                    using var terms = new BarTerminationsData(doc)
                    {
                        TerminationOrientationAtStart = RebarTerminationOrientation.Left,
                        TerminationOrientationAtEnd = RebarTerminationOrientation.Left
                    };
                    Rebar created = Rebar.CreateFromCurves(
                        doc, style, barType ?? resolver.ResolveOrCreateBarType(doc, diaMm, true),
                        host, norm, new List<Curve> { Line.CreateBound(sPt, ePt) }, terms, true, true);

                    var cAcc = created.GetShapeDrivenAccessor();
                    if (qty > 1 && arrayLenFt > 0.02)
                        cAcc.SetLayoutAsFixedNumber(qty, arrayLenFt, barsOnNormalSide, true, true);
                    else
                        cAcc.SetLayoutAsSingle();

                    RebarIdempotencyManager.TagGeneratedRebar(created, host.Id.Value, "SplicePiece", "00", lokasi, hostHash);
                    result.CreatedRebarIds.Add(created.Id.Value);
                    result.TotalCreatedSets++;
                    result.TotalBarPieces += qty;
                }

                doc.Delete(rebar.Id);
                result.DeletedRebarIds.Add(rId);
            }

            tx.Commit();
            tg.Assimilate();
            result.Success = true;
            result.Message = $"Split selesai: {result.DeletedRebarIds.Count} set lama -> {result.CreatedRebarIds.Count} segmen baru.";
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            if (tg.HasStarted()) tg.RollBack();
            result.Success = false;
            result.Message = $"Split failed: {ex.Message}";
            result.FailedItems.Add(ex.ToString());
        }

        return result;
    }

    public static RebarExecutionResult PropagateRebars(
        Document doc,
        long sourceHostId,
        IReadOnlyList<long> targetHostIds,
        RebarGenerationRequest templateRequest)
    {
        var result = new RebarExecutionResult();
        Element? sourceHost = doc.GetElement(new ElementId(sourceHostId));
        if (sourceHost == null)
        {
            result.Message = $"Source host #{sourceHostId} tidak ditemukan.";
            return result;
        }

        var sourceCat = HostGeometryExtractor.ClassifyElement(sourceHost);
        var validTargets = new List<long>();
        foreach (long tid in targetHostIds)
        {
            if (tid == sourceHostId) continue;
            Element? target = doc.GetElement(new ElementId(tid));
            if (target != null && HostGeometryExtractor.ClassifyElement(target) == sourceCat)
                validTargets.Add(tid);
        }

        if (validTargets.Count == 0)
        {
            result.Message = $"Tidak ada target bertipe {sourceCat} yang valid untuk di-Propagate.";
            return result;
        }

        RebarGenerationRequest propReq = templateRequest with { SelectedHostElementIds = validTargets };
        result = RebarGenerationOrchestrator.GenerateSelected(doc, propReq);
        if (result.Success)
            result.Message = $"Propagate dari host #{sourceHostId} berhasil diterapkan ke {validTargets.Count} elemen {sourceCat} target.";
        return result;
    }

    public static void ApplyRebarConstraintsPreference(bool enableConstraints)
    {
        RebarConstraintsManager.IsRebarConstrainedPlacementEnabled = enableConstraints;
    }

    private static XYZ ComputeOrthogonalNorm(XYZ dir)
    {
        XYZ refVec = Math.Abs(dir.DotProduct(XYZ.BasisZ)) < 0.9 ? XYZ.BasisZ : XYZ.BasisX;
        return dir.CrossProduct(refVec).Normalize();
    }
}

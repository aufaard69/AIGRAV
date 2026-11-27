using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AIGRAV.Contracts;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Engine;

/// <summary>
/// Assigns SOFiSTiK-class deterministic Mark numbers (AIGRAV_Mark + native REBAR_ELEM_SCHEDULE_MARK)
/// based on configurable Equality Criteria (Diameter, Shape Code, Segment Dimensions, Hook Config)
/// across WholeProject / PerLevel / PerHost scopes, and compiles live BBS rows.
/// </summary>
public static class MarkNumberingEngine
{
    public static string ComputeCanonicalShapeKey(
        Rebar rebar,
        Document doc,
        RebarGlobalSettings global,
        out int diameterMm,
        out string shapeCode,
        out double cutLengthMm)
    {
        RebarBarType? bt = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
        diameterMm = bt != null ? Math.Max(8, (int)Math.Round(RebarResourceResolver.FeetToMm(bt.BarNominalDiameter))) : 16;

        Parameter? shapeParam = rebar.get_Parameter(BuiltInParameter.REBAR_SHAPE);
        Element? shapeEl = (shapeParam != null && shapeParam.HasValue) ? doc.GetElement(shapeParam.AsElementId()) : null;
        shapeCode = shapeEl?.Name ?? "00";

        IList<Curve> curves = rebar.GetCenterlineCurves(
            adjustForSelfIntersection: false,
            suppressHooksAndCranks: false,
            suppressBendRadius: true,
            multiplanarOption: MultiplanarOption.IncludeAllMultiplanarCurves,
            barPositionIndex: 0);

        var segDims = curves
            .Select(c => ((int)(Math.Round(RebarResourceResolver.FeetToMm(c.Length) / 10.0) * 10)).ToString(CultureInfo.InvariantCulture))
            .ToList();

        cutLengthMm = Math.Round(curves.Sum(c => RebarResourceResolver.FeetToMm(c.Length)), 0);

        ElementId hStart = rebar.GetHookTypeId(0);
        ElementId hEnd = rebar.GetHookTypeId(1);

        var parts = new List<string>();
        if (global.MarkMatchDiameter) parts.Add($"D{diameterMm}");
        if (global.MarkMatchShapeCode) parts.Add($"S:{shapeCode}");
        if (global.MarkMatchSegmentDims) parts.Add($"DIM:{string.Join("-", segDims)}");
        if (global.MarkMatchHooks) parts.Add($"HK:{hStart.Value}/{hEnd.Value}");

        if (global.MarkScope == MarkScope.PerHost)
        {
            parts.Add($"H:{rebar.GetHostId().Value}");
        }
        else if (global.MarkScope == MarkScope.PerLevel)
        {
            Element? host = doc.GetElement(rebar.GetHostId());
            string lvl = host != null ? HostGeometryExtractor.GetLevelName(doc, host) : "None";
            parts.Add($"L:{lvl}");
        }

        return string.Join("|", parts);
    }

    public static int AssignMarksInDocument(Document doc, RebarGlobalSettings global, IReadOnlyCollection<long>? restrictToRebarIds = null)
    {
        var allRebars = new FilteredElementCollector(doc)
            .OfClass(typeof(Rebar))
            .Cast<Rebar>()
            .Where(r => restrictToRebarIds == null || restrictToRebarIds.Count == 0 || restrictToRebarIds.Contains(r.Id.Value))
            .ToList();

        if (allRebars.Count == 0) return 0;

        var signatureToMark = new Dictionary<string, string>(StringComparer.Ordinal);
        var scopeCounter = new Dictionary<string, int>(StringComparer.Ordinal);
        int updated = 0;

        foreach (var r in allRebars)
        {
            string key = ComputeCanonicalShapeKey(r, doc, global, out int diaMm, out _, out _);
            if (!signatureToMark.TryGetValue(key, out string? mark))
            {
                string prefix = r.LayoutRule == RebarLayoutRule.MaximumSpacing || r.LayoutRule == RebarLayoutRule.FixedNumber && r.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0).Count >= 4
                    ? "S"
                    : "B";

                string scopeBucket = global.MarkScope == MarkScope.PerHost
                    ? r.GetHostId().Value.ToString(CultureInfo.InvariantCulture)
                    : (global.MarkScope == MarkScope.PerLevel
                        ? (doc.GetElement(r.GetHostId()) is Element h ? HostGeometryExtractor.GetLevelName(doc, h) : "L")
                        : "PRJ");

                scopeCounter.TryGetValue(scopeBucket, out int n);
                n++;
                scopeCounter[scopeBucket] = n;
                mark = $"{prefix}{n:D2}-D{diaMm}";
                signatureToMark[key] = mark;
            }

            RebarIdempotencyManager.TrySetStringParam(r, RebarIdempotencyManager.ParamMark, mark);
            Parameter? pSchedMark = r.get_Parameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK);
            if (pSchedMark != null && !pSchedMark.IsReadOnly)
            {
                pSchedMark.Set(mark);
            }
            updated++;
        }

        return updated;
    }

    public static List<BbsPreviewRowDto> ExtractBbsRowsFromModel(Document doc, IReadOnlyCollection<long>? hostIds = null)
    {
        var rebars = new FilteredElementCollector(doc)
            .OfClass(typeof(Rebar))
            .Cast<Rebar>()
            .Where(r => hostIds == null || hostIds.Count == 0 || hostIds.Contains(r.GetHostId().Value))
            .ToList();

        var groups = new Dictionary<string, BbsPreviewRowDto>(StringComparer.Ordinal);
        foreach (var r in rebars)
        {
            RebarBarType? bt = doc.GetElement(r.GetTypeId()) as RebarBarType;
            int dia = bt != null ? Math.Max(8, (int)Math.Round(RebarResourceResolver.FeetToMm(bt.BarNominalDiameter))) : 16;

            IList<Curve> curves = r.GetCenterlineCurves(false, false, true, MultiplanarOption.IncludeAllMultiplanarCurves, 0);
            double singleMm = Math.Round(curves.Sum(c => RebarResourceResolver.FeetToMm(c.Length)), 0);
            int qty = Math.Max(1, r.Quantity);

            string mark = RebarIdempotencyManager.TryGetStringParam(r, RebarIdempotencyManager.ParamMark)
                ?? r.get_Parameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK)?.AsString()
                ?? "UNMARKED";

            string lokasi = RebarIdempotencyManager.TryGetStringParam(r, RebarIdempotencyManager.ParamLokasi) ?? string.Empty;
            string comments = r.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? string.Empty;
            string shape = doc.GetElement(r.get_Parameter(BuiltInParameter.REBAR_SHAPE)?.AsElementId() ?? ElementId.InvalidElementId)?.Name ?? "00";

            Element? host = doc.GetElement(r.GetHostId());
            string cat = host != null ? HostGeometryExtractor.ClassifyElement(host).ToString() : "Structural";

            string gKey = $"{mark}|{dia}|{(int)singleMm}|{lokasi}|{cat}";
            if (groups.TryGetValue(gKey, out var row))
            {
                row.Qty += qty;
            }
            else
            {
                groups[gKey] = new BbsPreviewRowDto
                {
                    Mark = mark,
                    HostTag = $"#{r.GetHostId().Value}",
                    HostCategory = cat,
                    Lokasi = lokasi,
                    Role = ExtractTagField(comments, "ROLE:") ?? "Rebar",
                    ShapeCode = ExtractTagField(comments, "SHAPE:") ?? shape,
                    DiameterMm = dia,
                    CutLengthMm = singleMm,
                    Qty = qty
                };
            }
        }

        return groups.Values.OrderBy(v => v.Mark).ThenBy(v => v.DiameterMm).ToList();
    }

    private static string? ExtractTagField(string src, string prefix)
    {
        int i = src.IndexOf(prefix, StringComparison.Ordinal);
        if (i < 0) return null;
        int start = i + prefix.Length;
        int end = src.IndexOf('|', start);
        return end < 0 ? src[start..] : src[start..end];
    }
}

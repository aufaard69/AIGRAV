using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Engine;

/// <summary>
/// Provides idempotent clean-and-rebuild ownership via AIGRAV tags in Comments / AIGRAV_RebarHostTag,
/// and computes host geometry fingerprints so Reinforcement Browser detects modified hosts.
/// </summary>
public static class RebarIdempotencyManager
{
    public const string HostTagPrefix = "AIGRAV_REB_HOST:";
    public const string ParamHostTag = "AIGRAV_RebarHostTag";
    public const string ParamMark = "AIGRAV_Mark";
    public const string ParamLokasi = "Lokasi";
    public const string ParamHostHash = "AIGRAV_HostHash";

    public static string BuildHostTag(long hostId) => $"{HostTagPrefix}{hostId}";

    public static string ComputeHostGeometryHash(Element host)
    {
        var sb = new StringBuilder();
        sb.Append(host.Id.Value).Append('|').Append(host.GetTypeId().Value).Append('|');
        BoundingBoxXYZ? bb = host.get_BoundingBox(null);
        if (bb != null)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "{0:F4},{1:F4},{2:F4}|{3:F4},{4:F4},{5:F4}|",
                bb.Min.X, bb.Min.Y, bb.Min.Z, bb.Max.X, bb.Max.Y, bb.Max.Z);
        }
        if (host.Location is LocationCurve lc)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "L:{0:F5}|", lc.Curve.Length);
        }
        else if (host.Location is LocationPoint lp)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "P:{0:F4},{1:F4},{2:F4}|", lp.Point.X, lp.Point.Y, lp.Point.Z);
        }
        Parameter? volParam = host.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
        if (volParam != null)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "V:{0:F5}", volParam.AsDouble());
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes)[..12];
    }

    public static List<Rebar> FindExistingAigravRebarsForHost(Document doc, long hostId)
    {
        string tag = BuildHostTag(hostId);
        ElementId hostElemId = new(hostId);
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Rebar))
            .Cast<Rebar>()
            .Where(r => IsOwnedByAigravForHost(r, hostElemId, tag))
            .ToList();
    }

    private static bool IsOwnedByAigravForHost(Rebar rebar, ElementId hostElemId, string hostTag)
    {
        if (rebar.GetHostId() == hostElemId)
        {
            string? custom = TryGetStringParam(rebar, ParamHostTag);
            if (!string.IsNullOrEmpty(custom) && custom.Contains(hostTag, StringComparison.Ordinal))
                return true;

            string comments = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? string.Empty;
            if (comments.Contains(hostTag, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public static List<long> DeleteExistingAigravRebarsForHost(Document doc, long hostId)
    {
        var deleted = new List<long>();
        var existing = FindExistingAigravRebarsForHost(doc, hostId);
        if (existing.Count == 0) return deleted;

        var ids = existing.Select(r => r.Id).ToList();
        doc.Delete(ids);
        foreach (var id in ids) deleted.Add(id.Value);
        return deleted;
    }

    public static void TagGeneratedRebar(
        Rebar rebar,
        long hostId,
        string role,
        string shapeCode,
        string lokasi,
        string hostHash,
        string markHint = "")
    {
        string hostTag = BuildHostTag(hostId);
        string commentPayload = $"{hostTag}|ROLE:{role}|SHAPE:{shapeCode}|HASH:{hostHash}";

        Parameter? pComment = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
        if (pComment != null && !pComment.IsReadOnly)
            pComment.Set(commentPayload);

        TrySetStringParam(rebar, ParamHostTag, hostTag);
        TrySetStringParam(rebar, ParamHostHash, hostHash);

        if (!string.IsNullOrWhiteSpace(lokasi))
            TrySetStringParam(rebar, ParamLokasi, lokasi);

        if (!string.IsNullOrWhiteSpace(markHint))
        {
            TrySetStringParam(rebar, ParamMark, markHint);
            Parameter? pSchedMark = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK);
            if (pSchedMark != null && !pSchedMark.IsReadOnly)
                pSchedMark.Set(markHint);
        }
    }

    public static void EnsureSharedOrProjectParametersNotice(Document doc, List<string> warnings)
    {
        // Check if optional AIGRAV custom parameters exist on Rebar; if not, fallback still works via Comments + REBAR_ELEM_SCHEDULE_MARK.
        var anyRebar = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).FirstElement();
        if (anyRebar != null)
        {
            if (anyRebar.LookupParameter(ParamMark) == null)
                warnings.Add($"Optional parameter '{ParamMark}' not found in project; Schedule Mark & Comments fallback will be populated cleanly.");
        }
    }

    public static string? TryGetStringParam(Element elem, string paramName)
    {
        Parameter? p = elem.LookupParameter(paramName);
        if (p == null || !p.HasValue) return null;
        return p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
    }

    public static bool TrySetStringParam(Element elem, string paramName, string value)
    {
        Parameter? p = elem.LookupParameter(paramName);
        if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
        {
            p.Set(value);
            return true;
        }
        return false;
    }
}

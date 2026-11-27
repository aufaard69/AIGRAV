using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Engine;

/// <summary>
/// Resolves or creates native Revit 2026 RebarBarType, RebarCoverType, RebarHookType,
/// and deterministic diameter-to-color mappings (no hardcoded invalid defaults).
/// </summary>
public sealed class RebarResourceResolver
{
    public const double MmPerFoot = 304.8;
    public static double MmToFeet(double mm) => mm / MmPerFoot;
    public static double FeetToMm(double feet) => feet * MmPerFoot;

    private static readonly Dictionary<int, (byte R, byte G, byte B, string Hex)> DiameterColors = new()
    {
        { 8,  (148, 163, 184, "#94A3B8") }, // Cool slate
        { 10, (52,  211, 153, "#34D399") }, // Emerald (Stirrups/Mesh)
        { 12, (45,  212, 191, "#2DD4BF") }, // Teal (Skin)
        { 13, (96,  165, 250, "#60A5FA") }, // Sky Blue
        { 16, (56,  189, 248, "#38BDF8") }, // Cyan (Main/Mesh)
        { 19, (251, 191, 36,  "#FBBF24") }, // Amber (Heavy main)
        { 22, (249, 115, 22,  "#F97316") }, // Orange
        { 25, (244, 63,  94,  "#F43F5E") }, // Rose
        { 29, (168, 85,  247, "#A855F7") }, // Purple
        { 32, (236, 72,  153, "#EC4899") }  // Magenta
    };

    public static (byte R, byte G, byte B, string Hex) GetColorForDiameter(int diameterMm)
    {
        if (DiameterColors.TryGetValue(diameterMm, out var match)) return match;
        int closest = DiameterColors.Keys.OrderBy(k => Math.Abs(k - diameterMm)).First();
        return DiameterColors[closest];
    }

    public RebarBarType ResolveOrCreateBarType(Document doc, int diameterMm, bool deformed = true, List<string>? warnings = null)
    {
        double targetFeet = MmToFeet(diameterMm);
        var existing = new FilteredElementCollector(doc)
            .OfClass(typeof(RebarBarType))
            .Cast<RebarBarType>()
            .ToList();

        // Match nominal diameter within 0.4 mm tolerance and matching deformation if possible
        var exact = existing.FirstOrDefault(b =>
            Math.Abs(FeetToMm(b.BarNominalDiameter) - diameterMm) < 0.4 &&
            b.DeformationType == (deformed ? RebarDeformationType.Deformed : RebarDeformationType.Plain));

        exact ??= existing.FirstOrDefault(b => Math.Abs(FeetToMm(b.BarNominalDiameter) - diameterMm) < 0.4);
        if (exact != null) return exact;

        string typeName = $"{(deformed ? "D" : "P")}{diameterMm}";
        RebarBarType? template = existing.FirstOrDefault();
        RebarBarType created;
        if (template != null)
        {
            string uniqueName = EnsureUniqueName(existing.Select(x => x.Name), typeName);
            created = (RebarBarType)template.Duplicate(uniqueName);
        }
        else
        {
            created = RebarBarType.Create(doc);
            created.Name = typeName;
        }

        double modelMm = diameterMm * (deformed ? 1.08 : 1.0);
        double stdBendFactor = diameterMm <= 25 ? 6.0 : 8.0;
        created.BarNominalDiameter = targetFeet;
        created.BarModelDiameter = MmToFeet(modelMm);
        created.StandardBendDiameter = MmToFeet(diameterMm * stdBendFactor);
        created.StandardHookBendDiameter = MmToFeet(diameterMm * stdBendFactor);
        created.StirrupTieBendDiameter = MmToFeet(diameterMm * 4.0);
        created.DeformationType = deformed ? RebarDeformationType.Deformed : RebarDeformationType.Plain;

        warnings?.Add($"Auto-created Revit RebarBarType '{created.Name}' ({diameterMm} mm).");
        return created;
    }

    public RebarCoverType ResolveOrCreateCoverType(Document doc, double coverMm, string labelPrefix = "AIGRAV", List<string>? warnings = null)
    {
        double targetFeet = MmToFeet(coverMm);
        var existing = new FilteredElementCollector(doc)
            .OfClass(typeof(RebarCoverType))
            .Cast<RebarCoverType>()
            .ToList();

        var match = existing.FirstOrDefault(c => Math.Abs(FeetToMm(c.CoverDistance) - coverMm) < 0.5);
        if (match != null) return match;

        string name = EnsureUniqueName(existing.Select(x => x.Name), $"{labelPrefix} Cover {(int)Math.Round(coverMm)} mm");
        var created = RebarCoverType.Create(doc, name, targetFeet);
        warnings?.Add($"Auto-created RebarCoverType '{created.Name}'.");
        return created;
    }

    public RebarHookType ResolveOrCreateHookType(
        Document doc,
        int angleDegrees,
        RebarStyle style,
        double straightMultiplier = 12.0,
        List<string>? warnings = null)
    {
        double targetRad = angleDegrees * Math.PI / 180.0;
        var existing = new FilteredElementCollector(doc)
            .OfClass(typeof(RebarHookType))
            .Cast<RebarHookType>()
            .ToList();

        var match = existing.FirstOrDefault(h =>
            Math.Abs(h.HookAngle - targetRad) < 0.03 &&
            h.Style == style &&
            Math.Abs(h.StraightLineMultiplier - straightMultiplier) < 1.5);

        if (match != null) return match;

        var angleMatch = existing.FirstOrDefault(h =>
            Math.Abs(h.HookAngle - targetRad) < 0.03 &&
            h.Style == style);

        if (angleMatch != null) return angleMatch;

        RebarHookType created = RebarHookType.Create(doc, targetRad, straightMultiplier);
        try
        {
            created.Style = style;
            string prefix = style == RebarStyle.StirrupTie ? "Seismic Stirrup" : "Standard";
            created.Name = EnsureUniqueName(existing.Select(x => x.Name), $"AIGRAV {prefix} {angleDegrees} deg");
        }
        catch (Exception ex)
        {
            warnings?.Add($"HookType rename/style warning: {ex.Message}");
        }
        warnings?.Add($"Auto-created RebarHookType {angleDegrees} deg ({style}).");
        return created;
    }

    public void AssignHostCoverParameters(Element host, RebarCoverType coverType, List<string>? warnings = null)
    {
        try
        {
            foreach (BuiltInParameter bip in new[]
            {
                BuiltInParameter.CLEAR_COVER_TOP,
                BuiltInParameter.CLEAR_COVER_BOTTOM,
                BuiltInParameter.CLEAR_COVER_OTHER,
                BuiltInParameter.CLEAR_COVER_EXTERIOR,
                BuiltInParameter.CLEAR_COVER_INTERIOR
            })
            {
                Parameter? p = host.get_Parameter(bip);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.ElementId)
                {
                    p.Set(coverType.Id);
                }
            }
        }
        catch (Exception ex)
        {
            warnings?.Add($"Could not assign clear cover on host #{host.Id.Value}: {ex.Message}");
        }
    }

    private static string EnsureUniqueName(IEnumerable<string> existingNames, string baseName)
    {
        var set = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        if (!set.Contains(baseName)) return baseName;
        for (int i = 2; i < 100; i++)
        {
            string candidate = $"{baseName} ({i})";
            if (!set.Contains(candidate)) return candidate;
        }
        return $"{baseName}_{Guid.NewGuid().ToString("N")[..4]}";
    }
}

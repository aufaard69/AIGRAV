using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

/// <summary>
/// Adapter around AIGRAVToolExecutor upgraded with Pillar 1 Auto-Spatial Telemetry.
/// All legacy creation and modification tools automatically return JSON Telemetry
/// (created_elements, spatial_bounds_mm, spatial_check, ascii_minimap) while keeping
/// 100% backward compatibility with all existing static tools.
/// </summary>
public sealed class AIGRAVLegacyBimSkill : IAIGRAVSkill
{
    public const double FtToMmFactor = 304.8;
    private static double FtToMm(double ft) => Math.Round(ft * FtToMmFactor, 1);

    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "Legacy BIM Engine (Auto-Telemetry Upgraded)",
        Version = "2.0",
        Category = "Core",
        Description = "Adapter for AIGRAVToolExecutor with Pillar 1 Auto-Spatial Telemetry wrapper on all mutating tools.",
        Source = "AIGRAVToolExecutor.cs",
        Priority = -1000
    };

    public bool CanHandle(string toolName) => !string.IsNullOrWhiteSpace(toolName);

    private static bool IsMutatingTool(string op)
    {
        string clean = op.StartsWith("aigrav_", StringComparison.OrdinalIgnoreCase)
            ? op["aigrav_".Length..].ToLowerInvariant()
            : op.ToLowerInvariant();

        return clean.StartsWith("create_") ||
               clean.StartsWith("set_") ||
               clean.StartsWith("move_") ||
               clean.StartsWith("copy_") ||
               clean.StartsWith("rotate_") ||
               clean.StartsWith("mirror_") ||
               clean.StartsWith("change_") ||
               clean.StartsWith("join_") ||
               clean.StartsWith("unjoin_") ||
               clean.StartsWith("pin_") ||
               clean.StartsWith("unpin_") ||
               clean.StartsWith("rename_") ||
               clean == "switch_join_order";
    }

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (!Info.IsEnabled)
            return "AIGRAV Legacy BIM Engine is OFF.";

        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null || !IsMutatingTool(toolName))
            return AIGRAVToolExecutor.Execute(toolName, app!, argument);

        var preIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElementIds()
            .ToHashSet();

        string rawResult = AIGRAVToolExecutor.Execute(toolName, app!, argument);

        // If rawResult is already a JSON telemetry object from a higher-priority skill, return it directly
        if (rawResult.TrimStart().StartsWith("{") && rawResult.Contains("\"spatial_bounds_mm\""))
            return rawResult;

        var postIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElementIds()
            .Where(id => !preIds.Contains(id))
            .ToList();

        // If no new elements were created, extract target ElementIds from argument or rawResult
        if (postIds.Count == 0)
        {
            foreach (Match m in Regex.Matches((argument ?? "") + " " + rawResult, @"\b\d{5,10}\b"))
            {
                if (long.TryParse(m.Value, out long idVal))
                {
                    ElementId eid = new(idVal);
                    if (doc.GetElement(eid) != null && !postIds.Contains(eid))
                        postIds.Add(eid);
                }
            }
        }

        return WrapWithSpatialTelemetry(doc, toolName, rawResult, postIds);
    }

    private static string WrapWithSpatialTelemetry(
        Document doc,
        string toolName,
        string legacyMessage,
        List<ElementId> affectedIds)
    {
        List<object> createdElements = new();
        List<object> spatialBoundsMm = new();
        List<object> spatialChecks = new();

        var docWarnings = doc.GetWarnings();
        HashSet<ElementId> idSet = affectedIds.ToHashSet();

        foreach (ElementId id in affectedIds)
        {
            Element? el = doc.GetElement(id);
            if (el == null) continue;

            createdElements.Add(new
            {
                element_id = el.Id.Value,
                category = el.Category?.Name ?? el.GetType().Name,
                name = el.Name
            });

            BoundingBoxXYZ? bb = el.get_BoundingBox(null);
            if (bb == null) continue;

            XYZ cen = (bb.Min + bb.Max) * 0.5;
            spatialBoundsMm.Add(new
            {
                element_id = el.Id.Value,
                min_mm = new { x = FtToMm(bb.Min.X), y = FtToMm(bb.Min.Y), z = FtToMm(bb.Min.Z) },
                max_mm = new { x = FtToMm(bb.Max.X), y = FtToMm(bb.Max.Y), z = FtToMm(bb.Max.Z) },
                centroid_mm = new { x = FtToMm(cen.X), y = FtToMm(cen.Y), z = FtToMm(cen.Z) },
                dimensions_mm = new
                {
                    length_x_mm = FtToMm(bb.Max.X - bb.Min.X),
                    width_y_mm = FtToMm(bb.Max.Y - bb.Min.Y),
                    height_z_mm = FtToMm(bb.Max.Z - bb.Min.Z)
                }
            });

            bool touchesLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Any(l => Math.Abs(bb.Min.Z - l.Elevation) <= (25.0 / FtToMmFactor));

            var warningsForEl = docWarnings
                .Where(w => w.GetFailingElements().Contains(id))
                .Select(w => w.GetDescriptionText())
                .Distinct()
                .ToList();

            spatialChecks.Add(new
            {
                element_id = el.Id.Value,
                is_floating = !touchesLevel && bb.Min.Z > (25.0 / FtToMmFactor),
                revit_warnings = warningsForEl
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = legacyMessage.StartsWith("AIGRAV Error", StringComparison.OrdinalIgnoreCase) ? "Error" : "Success",
            tool = toolName,
            legacy_message = legacyMessage,
            created_elements = createdElements,
            spatial_bounds_mm = spatialBoundsMm,
            spatial_check = spatialChecks
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}

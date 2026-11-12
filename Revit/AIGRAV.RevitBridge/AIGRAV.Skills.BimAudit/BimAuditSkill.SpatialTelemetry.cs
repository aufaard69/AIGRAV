using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class SpatialAgenticSandboxSkill : IAIGRAVSkill
{
    public const double FtToMmFactor = 304.8;
    public static double MmToFt(double mm) => mm / FtToMmFactor;
    public static double FtToMm(double ft) => Math.Round(ft * FtToMmFactor, 1);

    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "Agentic Spatial Sandbox",
        Version = "2.5",
        Category = "Spatial Intelligence",
        Description = "5-Pillar Upgrade: Auto-Spatial Telemetry, Dynamic Python Runner, Viewport Visual Capture, and Relative Parametric Helpers.",
        Source = "External",
        Priority = 1850
    };

    private static readonly HashSet<string> SupportedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_project_context", "aigrav_get_project_context",
        "create_grid_matrix", "aigrav_create_grid_matrix",
        "place_on_top_of", "aigrav_place_on_top_of",
        "connect_elements", "aigrav_connect_elements",
        "capture_revit_viewport", "aigrav_capture_revit_viewport",
        "inspect_model_visually", "aigrav_inspect_model_visually",
        "execute_revit_python", "aigrav_execute_revit_python",
        "get_element_telemetry", "aigrav_get_element_telemetry",
        "create_wall", "aigrav_create_wall",
        "create_column", "aigrav_create_column",
        "create_beam", "aigrav_create_beam",
        "create_floor", "aigrav_create_floor",
        "create_roof", "aigrav_create_roof",
        "create_gable_roof", "aigrav_create_gable_roof",
        "create_family_instance", "aigrav_create_family_instance",
        "create_door", "aigrav_create_door",
        "create_window", "aigrav_create_window",
        "inspect_roof", "aigrav_inspect_roof",
        "inspect_roof_faces", "aigrav_inspect_roof_faces",
        "audit_material_transparency", "aigrav_audit_material_transparency",
        "capture_presentation_views", "aigrav_capture_presentation_views",
        "create_architectural_schedules", "aigrav_create_architectural_schedules",
        "detect_spatial_clashes", "aigrav_detect_spatial_clashes",
        "audit_unresolved_warnings", "aigrav_audit_unresolved_warnings"
    };

    public bool CanHandle(string toolName) => SupportedTools.Contains(toolName);

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null)
            return JsonSerializer.Serialize(new { error = "No active Revit document." });

        string clean = toolName.StartsWith("aigrav_", StringComparison.OrdinalIgnoreCase)
            ? toolName["aigrav_".Length..].ToLowerInvariant()
            : toolName.ToLowerInvariant();

        try
        {
            return clean switch
            {
                "get_project_context" => GetProjectContextJson(doc),
                "create_grid_matrix" => CreateGridMatrixJson(doc, argument),
                "place_on_top_of" => PlaceOnTopOfJson(doc, argument),
                "connect_elements" => ConnectElementsJson(doc, argument),
                "capture_revit_viewport" => CaptureRevitViewportJson(app!, doc, argument, multiView: false),
                "inspect_model_visually" => CaptureRevitViewportJson(app!, doc, argument, multiView: true),
                "execute_revit_python" => ExecuteRevitPythonSafe(app!, doc, argument),
                "get_element_telemetry" => GetElementTelemetryByIds(doc, argument),
                "create_wall" => CreateWallWithTelemetry(doc, argument),
                "create_column" => CreateColumnWithTelemetry(doc, argument),
                "create_beam" => CreateBeamWithTelemetry(doc, argument),
                "create_floor" => CreateFloorWithTelemetry(doc, argument),
                "create_roof" or "create_gable_roof" => CreateGableRoofWithTelemetry(doc, argument),
                "inspect_roof" => InspectRoofCurvesJson(doc, argument),
                "inspect_roof_faces" => InspectRoofFacesJson(doc, argument),
                "create_family_instance" or "create_door" or "create_window" => CreateWallHostedWithTelemetry(doc, argument),
                "audit_material_transparency" => AuditMaterialTransparencyJson(doc, argument),
                "capture_presentation_views" => ExportComprehensivePresentationViews(app!, doc),
                "create_architectural_schedules" => CreateArchitecturalSchedules(doc),
                "detect_spatial_clashes" => DetectSpatialClashesJson(doc, argument),
                "audit_unresolved_warnings" => AuditUnresolvedWarningsJson(doc),
                _ => JsonSerializer.Serialize(new { error = $"Unhandled tool: {toolName}" })
            };
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                status = "Error",
                tool = toolName,
                message = ex.Message,
                stackTrace = ex.StackTrace
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public static string BuildSpatialTelemetryJson(
        Document doc,
        string actionName,
        IReadOnlyList<ElementId> elementIds,
        object? extraData = null,
        bool includeAsciiMinimap = true)
    {
        List<object> createdList = new();
        List<object> boundsList = new();
        List<object> checksList = new();

        HashSet<ElementId> idSet = elementIds.ToHashSet();
        IList<FailureMessage> allWarnings = doc.GetWarnings();

        foreach (ElementId id in elementIds)
        {
            Element? el = doc.GetElement(id);
            if (el == null) continue;

            Element? typeEl = doc.GetElement(el.GetTypeId());
            Level? hostLvl = doc.GetElement(el.LevelId) as Level;

            createdList.Add(new
            {
                element_id = el.Id.Value,
                category = el.Category?.Name ?? "Unknown",
                name = el.Name,
                type_name = typeEl?.Name ?? el.Name,
                level_name = hostLvl?.Name ?? "Unassociated"
            });

            BoundingBoxXYZ? bb = el.get_BoundingBox(null);
            if (bb != null)
            {
                double minX = FtToMm(bb.Min.X), minY = FtToMm(bb.Min.Y), minZ = FtToMm(bb.Min.Z);
                double maxX = FtToMm(bb.Max.X), maxY = FtToMm(bb.Max.Y), maxZ = FtToMm(bb.Max.Z);
                double cx = Math.Round((minX + maxX) * 0.5, 1);
                double cy = Math.Round((minY + maxY) * 0.5, 1);
                double cz = Math.Round((minZ + maxZ) * 0.5, 1);

                boundsList.Add(new
                {
                    element_id = el.Id.Value,
                    min_mm = new { x = minX, y = minY, z = minZ },
                    max_mm = new { x = maxX, y = maxY, z = maxZ },
                    centroid_mm = new { x = cx, y = cy, z = cz },
                    dimensions_mm = new
                    {
                        dx = Math.Round(maxX - minX, 1),
                        dy = Math.Round(maxY - minY, 1),
                        dz = Math.Round(maxZ - minZ, 1)
                    }
                });

                // 1. Floating Detection
                bool isFloating = false;
                string supportNote = "Grounded on host level or supporting element.";
                double lvlZmm = hostLvl != null ? FtToMm(hostLvl.Elevation) : 0.0;

                if (Math.Abs(minZ - lvlZmm) > 60.0)
                {
                    Outline probeBelow = new(
                        new XYZ(bb.Min.X + 0.02, bb.Min.Y + 0.02, bb.Min.Z - MmToFt(120)),
                        new XYZ(bb.Max.X - 0.02, bb.Max.Y - 0.02, bb.Min.Z + MmToFt(20)));
                    var supporters = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(new BoundingBoxIntersectsFilter(probeBelow))
                        .Where(x => x.Id != el.Id && x.Category != null)
                        .ToList();

                    if (supporters.Count == 0 && minZ > lvlZmm + 60.0)
                    {
                        isFloating = true;
                        supportNote = $"FLOATING ALERT: Bottom Z ({minZ} mm) is {Math.Round(minZ - lvlZmm, 1)} mm above level '{hostLvl?.Name ?? "0"}' ({lvlZmm} mm) with 0 supporting elements below!";
                    }
                }

                // 2. Clash Detection (shrunk by 20mm to ignore flush face touches)
                List<object> clashes = new();
                double shrink = MmToFt(20.0);
                if (bb.Max.X - bb.Min.X > shrink * 2 && bb.Max.Y - bb.Min.Y > shrink * 2 && bb.Max.Z - bb.Min.Z > shrink * 2)
                {
                    Outline coreOutline = new(
                        new XYZ(bb.Min.X + shrink, bb.Min.Y + shrink, bb.Min.Z + shrink),
                        new XYZ(bb.Max.X - shrink, bb.Max.Y - shrink, bb.Max.Z - shrink));

                    var intersecting = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(new BoundingBoxIntersectsFilter(coreOutline))
                        .Where(x => x.Id != el.Id && x.Category != null && !JoinGeometryUtils.AreElementsJoined(doc, el, x))
                        .Take(5)
                        .ToList();

                    foreach (var other in intersecting)
                    {
                        clashes.Add(new { id = other.Id.Value, category = other.Category?.Name ?? "", name = other.Name });
                    }
                }

                // 3. Revit Warnings involving this element
                List<string> elemWarnings = allWarnings
                    .Where(w => w.GetFailingElements().Contains(el.Id))
                    .Select(w => w.GetDescriptionText())
                    .Distinct()
                    .ToList();

                checksList.Add(new
                {
                    element_id = el.Id.Value,
                    is_floating = isFloating,
                    support_diagnostic = supportNote,
                    unjoined_bbox_clashes = clashes,
                    revit_warnings = elemWarnings
                });
            }
        }

        var payload = new
        {
            status = "Success",
            action = actionName,
            created_elements = createdList,
            spatial_bounds_mm = boundsList,
            spatial_check = checksList,
            extra = extraData,
            ascii_minimap = includeAsciiMinimap ? BuildPlanAsciiMinimap(doc, idSet) : null
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

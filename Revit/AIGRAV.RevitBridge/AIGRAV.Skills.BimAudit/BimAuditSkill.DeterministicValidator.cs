using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class SpatialAgenticSandboxSkill
{
    public static string AuditMaterialTransparencyJson(Document doc, string? argument)
    {
        List<object> defects = new();
        List<object> verifiedMaterials = new();

        var windows = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Windows)
            .WhereElementIsNotElementType()
            .ToList();

        var curtainPanels = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
            .WhereElementIsNotElementType()
            .ToList();

        List<Element> glazingElements = new();
        glazingElements.AddRange(windows);
        glazingElements.AddRange(curtainPanels);

        foreach (var el in glazingElements)
        {
            var matIds = el.GetMaterialIds(false);
            foreach (var mid in matIds)
            {
                if (doc.GetElement(mid) is Material mat)
                {
                    bool isGlassName = mat.Name.Contains("Glass", StringComparison.OrdinalIgnoreCase) ||
                                       mat.Name.Contains("Kaca", StringComparison.OrdinalIgnoreCase) ||
                                       mat.Name.Contains("Glazing", StringComparison.OrdinalIgnoreCase);

                    if (isGlassName)
                    {
                        if (mat.Transparency < 50)
                        {
                            defects.Add(new
                            {
                                element_id = el.Id.Value,
                                element_name = el.Name,
                                category = el.Category?.Name ?? "Window/Glazing",
                                material_name = mat.Name,
                                transparency = mat.Transparency,
                                issue = "OPAQUE_GLASS: Kaca tampak abu-abu solid karena Transparency < 50%"
                            });
                        }
                        else
                        {
                            verifiedMaterials.Add(new
                            {
                                element_id = el.Id.Value,
                                material_name = mat.Name,
                                transparency = mat.Transparency,
                                status = "VERIFIED_TRANSPARENT"
                            });
                        }
                    }
                }
            }
        }

        // Check roofs for default unassigned materials
        var roofs = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Roofs)
            .WhereElementIsNotElementType()
            .ToList();

        foreach (var r in roofs)
        {
            var rMatIds = r.GetMaterialIds(false);
            if (rMatIds.Count == 0)
            {
                defects.Add(new
                {
                    element_id = r.Id.Value,
                    element_name = r.Name,
                    category = "Roof",
                    issue = "MISSING_MATERIAL: Atap tidak memiliki material resmi terdefinisi"
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            tool = "audit_material_transparency",
            total_glazing_checked = glazingElements.Count,
            total_defects = defects.Count,
            has_defects = defects.Count > 0,
            defects,
            verified_sample = verifiedMaterials.Take(5).ToList()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string DetectSpatialClashesJson(Document doc, string? argument)
    {
        List<object> clashes = new();

        var furniture = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Furniture)
            .WhereElementIsNotElementType()
            .ToList();

        var walls = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
            .ToList();

        foreach (var furn in furniture)
        {
            var bbF = furn.get_BoundingBox(null);
            if (bbF == null) continue;

            // Shrink bounding box slightly to avoid false touch boundary positives
            Outline outline = new(
                new XYZ(bbF.Min.X + 0.05, bbF.Min.Y + 0.05, bbF.Min.Z + 0.05),
                new XYZ(bbF.Max.X - 0.05, bbF.Max.Y - 0.05, bbF.Max.Z - 0.05)
            );

            BoundingBoxIntersectsFilter filter = new(outline);
            var clashingWalls = new FilteredElementCollector(doc, walls.Select(w => w.Id).ToList())
                .WherePasses(filter)
                .ToList();

            foreach (var w in clashingWalls)
            {
                clashes.Add(new
                {
                    furniture_id = furn.Id.Value,
                    furniture_name = furn.Name,
                    wall_id = w.Id.Value,
                    wall_name = w.Name,
                    issue = "FURNITURE_WALL_CLASH: Furnitur menembus atau tertanam di dalam dinding"
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            tool = "detect_spatial_clashes",
            total_clashes = clashes.Count,
            has_clashes = clashes.Count > 0,
            clashes
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string AuditUnresolvedWarningsJson(Document doc)
    {
        var rawWarnings = doc.GetWarnings();
        var warningList = rawWarnings.Select(w => new
        {
            description = w.GetDescriptionText(),
            severity = w.GetSeverity().ToString(),
            failing_elements = w.GetFailingElements().Select(id => id.Value).ToList()
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            tool = "audit_unresolved_warnings",
            total_warnings = warningList.Count,
            has_critical_warnings = warningList.Any(w => w.severity.Equals("Error", StringComparison.OrdinalIgnoreCase)),
            warnings = warningList
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}

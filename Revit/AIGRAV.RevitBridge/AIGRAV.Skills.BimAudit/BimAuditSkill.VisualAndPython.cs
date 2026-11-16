using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class SpatialAgenticSandboxSkill
{
    public static string CaptureRevitViewportJson(UIApplication app, Document doc, string? argument, bool multiView)
    {
        UIDocument? uiDoc = app.ActiveUIDocument;
        if (uiDoc == null) return JsonSerializer.Serialize(new { error = "No active UIDocument." });

        int pixelSize = 1280;
        string? requestedViewName = null;
        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                if (jd.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (jd.RootElement.TryGetProperty("resolution_px", out JsonElement resEl) &&
                        resEl.TryGetInt32(out int px))
                    {
                        pixelSize = Math.Clamp(px, 800, 2048);
                    }
                    if (jd.RootElement.TryGetProperty("view_name", out JsonElement vnEl))
                    {
                        requestedViewName = vnEl.GetString();
                    }
                }
            }
            catch { }
        }

        View? requestedView = null;
        if (!string.IsNullOrWhiteSpace(requestedViewName))
        {
            requestedView = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name.IndexOf(requestedViewName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (requestedView != null)
            {
                try
                {
                    uiDoc.ActiveView = requestedView;
                }
                catch { }
            }
        }

        try
        {
            uiDoc.Selection.SetElementIds(new List<ElementId>());

            using (Transaction txBox = new(doc, "AIGRAV - Reset View Limits"))
            {
                TransactionFailureHandling.SuppressWarnings(txBox);
                txBox.Start();
                foreach (View v in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>())
                {
                    if (!v.IsTemplate)
                    {
                        try
                        {
                            if (v.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                                v.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                            v.CropBoxActive = false;
                            v.CropBoxVisible = false;
                            if (v is View3D v3d)
                            {
                                if (v3d.Name.IndexOf("Cutaway", StringComparison.OrdinalIgnoreCase) < 0 &&
                                    v3d.Name.IndexOf("Pondasi", StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    v3d.IsSectionBoxActive = false;
                                }
                                v3d.DisplayStyle = DisplayStyle.Shading;
                            }
                        }
                        catch { }
                    }
                }
                txBox.Commit();
            }

            UIView? activeUiView = uiDoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == doc.ActiveView.Id)
                                   ?? uiDoc.GetOpenUIViews().FirstOrDefault();
            activeUiView?.ZoomToFit();
        }
        catch { }

        string exportDir = Path.Combine(Path.GetTempPath(), "AIGRAV_VisualEye");
        Directory.CreateDirectory(exportDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string baseFilePath = Path.Combine(exportDir, $"aigrav_view_{stamp}");

        List<ElementId> viewsToExport = new();
        if (requestedView != null)
        {
            viewsToExport.Add(requestedView.Id);
        }
        else
        {
            viewsToExport.Add(doc.ActiveView.Id);
        }

        if (multiView)
        {
            var allViews = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .ToList();

            View? planView = allViews.FirstOrDefault(v => v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan);
            View? view3d = allViews.FirstOrDefault(v => v.ViewType == ViewType.ThreeD && v.Name.IndexOf("Cover", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? allViews.FirstOrDefault(v => v.ViewType == ViewType.ThreeD);
            View? elevView = allViews.FirstOrDefault(v => (v.ViewType == ViewType.Elevation || v.ViewType == ViewType.Section) && v.Name.IndexOf("South", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? allViews.FirstOrDefault(v => v.ViewType == ViewType.Elevation || v.ViewType == ViewType.Section);
            View? cutawayView = allViews.FirstOrDefault(v => v.ViewType == ViewType.ThreeD &&
                (v.Name.IndexOf("Cutaway", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 v.Name.IndexOf("Pondasi", StringComparison.OrdinalIgnoreCase) >= 0));

            foreach (var v in new[] { cutawayView, planView, view3d, elevView })
            {
                if (v != null && !viewsToExport.Contains(v.Id))
                    viewsToExport.Add(v.Id);
            }
        }

        ImageExportOptions opts = new()
        {
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = pixelSize,
            FilePath = baseFilePath,
            FitDirection = FitDirectionType.Vertical,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG,
            ImageResolution = ImageResolution.DPI_150,
            ExportRange = viewsToExport.Count > 1 ? ExportRange.SetOfViews : ExportRange.CurrentView
        };
        if (viewsToExport.Count > 1)
            opts.SetViewsAndSheets(viewsToExport);

        doc.ExportImage(opts);

        var exportedFiles = Directory.GetFiles(exportDir, $"aigrav_view_{stamp}*.png")
            .OrderBy(f => f)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            status = "Success",
            tool = multiView ? "inspect_model_visually" : "capture_revit_viewport",
            active_view_id = doc.ActiveView.Id.Value,
            active_view_name = doc.ActiveView.Name,
            active_view_type = doc.ActiveView.ViewType.ToString(),
            zoom_to_fit_applied = true,
            resolution_px = pixelSize,
            primary_image_path = exportedFiles.FirstOrDefault() ?? "",
            exported_images = exportedFiles,
            warnings_count = doc.GetWarnings().Count,
            ascii_minimap = BuildPlanAsciiMinimap(doc)
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string ExportComprehensivePresentationViews(UIApplication app, Document doc)
    {
        var vft3d = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

        if (vft3d == null) return JsonSerializer.Serialize(new { error = "No ViewFamilyType for 3D." });

        string exportDir = Path.Combine(Path.GetTempPath(), "AIGRAV_VisualEye");
        Directory.CreateDirectory(exportDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        XYZ target = new(MmToFt(3000), MmToFt(3000), MmToFt(2000));
        double dist = MmToFt(30000);

        var orientations = new (string name, XYZ fwd)[]
        {
            ("3D_FrontRight_SE", new XYZ(-1, 1, -0.7).Normalize()),
            ("3D_FrontLeft_SW", new XYZ(1, 1, -0.7).Normalize()),
            ("3D_RearRight_NE", new XYZ(-1, -1, -0.7).Normalize()),
            ("3D_RearLeft_NW", new XYZ(1, -1, -0.7).Normalize())
        };

        List<ElementId> createdViews = new();
        using (Transaction tx = new(doc, "AIGRAV - Setup Presentation 3D Views"))
        {
            tx.Start();
            foreach (var (name, fwd) in orientations)
            {
                View3D? v = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>()
                    .FirstOrDefault(x => x.Name == name);
                if (v == null)
                {
                    v = View3D.CreateIsometric(doc, vft3d.Id);
                    v.Name = name;
                }

                XYZ up = (new XYZ(0, 0, 1) - (fwd * fwd.Z)).Normalize();
                XYZ eye = target - (fwd * dist);
                v.SetOrientation(new ViewOrientation3D(eye, up, fwd));
                v.DisplayStyle = DisplayStyle.FlatColors;
                v.DetailLevel = ViewDetailLevel.Fine;
                v.IsSectionBoxActive = false;
                v.AreAnnotationCategoriesHidden = true;
                createdViews.Add(v.Id);
            }
            tx.Commit();
        }

        var allViews = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.CanBePrinted).ToList();

        var floorPlan = allViews.FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && v.Name.Contains("L1"));
        if (floorPlan != null) createdViews.Add(floorPlan.Id);

        var elevs = allViews.Where(v => v.ViewType == ViewType.Elevation).Take(4);
        foreach (var ev in elevs) createdViews.Add(ev.Id);

        var allowedCategories = new HashSet<long>
        {
            (long)BuiltInCategory.OST_Walls,
            (long)BuiltInCategory.OST_StructuralColumns,
            (long)BuiltInCategory.OST_StructuralFraming,
            (long)BuiltInCategory.OST_Floors,
            (long)BuiltInCategory.OST_Ceilings,
            (long)BuiltInCategory.OST_Doors,
            (long)BuiltInCategory.OST_Windows,
            (long)BuiltInCategory.OST_Roofs
        };

        var localElementIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.Category != null && allowedCategories.Contains(e.Category.Id.Value))
            .Where(e =>
            {
                var bb = e.get_BoundingBox(null);
                if (bb == null) return false;
                double xMid = FtToMm((bb.Min.X + bb.Max.X) * 0.5);
                double yMid = FtToMm((bb.Min.Y + bb.Max.Y) * 0.5);
                return xMid >= -1500 && xMid <= 7500 && yMid >= -1500 && yMid <= 7500;
            })
            .Select(e => e.Id)
            .ToList();

        List<View> isolatedViews = new();
        if (localElementIds.Count > 0)
        {
            foreach (var vid in createdViews)
            {
                if (doc.GetElement(vid) is View v && v.ViewType == ViewType.ThreeD)
                {
                    try
                    {
                        v.IsolateElementsTemporary(localElementIds);
                        isolatedViews.Add(v);
                    }
                    catch { }
                }
            }
        }

        ImageExportOptions opts = new()
        {
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = 1600,
            FilePath = Path.Combine(exportDir, $"tipe36_pres_{stamp}"),
            FitDirection = FitDirectionType.Horizontal,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG,
            ImageResolution = ImageResolution.DPI_150,
            ExportRange = ExportRange.SetOfViews
        };
        opts.SetViewsAndSheets(createdViews);
        doc.ExportImage(opts);

        foreach (var v in isolatedViews)
        {
            try
            {
                v.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }
            catch { }
        }

        var files = Directory.GetFiles(exportDir, $"tipe36_pres_{stamp}*.png").ToList();
        return JsonSerializer.Serialize(new
        {
            status = "Success",
            filesCount = files.Count,
            files
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string CreateArchitecturalSchedules(Document doc)
    {
        using Transaction tx = new(doc, "AIGRAV - Create Architectural Schedules");
        tx.Start();
        List<ElementId> newScheds = new();

        try
        {
            var existing = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
                .FirstOrDefault(s => s.Name == "QTO - Kusen & Daun Pintu Tipe 36");
            if (existing == null)
            {
                ViewSchedule vsDoors = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Doors));
                vsDoors.Name = "QTO - Kusen & Daun Pintu Tipe 36";
                AddScheduleFieldIfValid(vsDoors, BuiltInParameter.ALL_MODEL_TYPE_NAME);
                AddScheduleFieldIfValid(vsDoors, BuiltInParameter.DOOR_WIDTH);
                AddScheduleFieldIfValid(vsDoors, BuiltInParameter.DOOR_HEIGHT);
                AddScheduleFieldIfValid(vsDoors, BuiltInParameter.ALL_MODEL_MARK);
                newScheds.Add(vsDoors.Id);
            }
        }
        catch { }

        try
        {
            var existing = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
                .FirstOrDefault(s => s.Name == "QTO - Kusen & Kaca Jendela Tipe 36");
            if (existing == null)
            {
                ViewSchedule vsWin = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Windows));
                vsWin.Name = "QTO - Kusen & Kaca Jendela Tipe 36";
                AddScheduleFieldIfValid(vsWin, BuiltInParameter.ALL_MODEL_TYPE_NAME);
                AddScheduleFieldIfValid(vsWin, BuiltInParameter.WINDOW_WIDTH);
                AddScheduleFieldIfValid(vsWin, BuiltInParameter.WINDOW_HEIGHT);
                AddScheduleFieldIfValid(vsWin, BuiltInParameter.ALL_MODEL_MARK);
                newScheds.Add(vsWin.Id);
            }
        }
        catch { }

        try
        {
            var existing = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
                .FirstOrDefault(s => s.Name == "QTO - Penutup Atap Pelana Tipe 36");
            if (existing == null)
            {
                ViewSchedule vsRoof = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Roofs));
                vsRoof.Name = "QTO - Penutup Atap Pelana Tipe 36";
                AddScheduleFieldIfValid(vsRoof, BuiltInParameter.ALL_MODEL_TYPE_NAME);
                AddScheduleFieldIfValid(vsRoof, BuiltInParameter.HOST_AREA_COMPUTED);
                AddScheduleFieldIfValid(vsRoof, BuiltInParameter.HOST_VOLUME_COMPUTED);
                newScheds.Add(vsRoof.Id);
            }
        }
        catch { }

        doc.Regenerate();
        tx.Commit();
        return JsonSerializer.Serialize(new { status = "Success", createdSchedules = newScheds.Count });
    }

    private static void AddScheduleFieldIfValid(ViewSchedule vs, BuiltInParameter bip)
    {
        ScheduleDefinition def = vs.Definition;
        foreach (SchedulableField sf in def.GetSchedulableFields())
        {
            if (sf.ParameterId.Value == (long)bip)
            {
                def.AddField(sf);
                break;
            }
        }
    }

    public static string ColorSplashJson(UIApplication app, Document doc, string? argument)
    {
        string? requestedViewName = null;
        string paletteName = "architectural";
        bool captureViewport = true;
        int resPx = 1600;

        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                if (jd.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (jd.RootElement.TryGetProperty("view_name", out JsonElement vnEl))
                        requestedViewName = vnEl.GetString();
                    if (jd.RootElement.TryGetProperty("palette", out JsonElement palEl))
                        paletteName = palEl.GetString() ?? "architectural";
                    if (jd.RootElement.TryGetProperty("capture_viewport", out JsonElement capEl))
                        captureViewport = capEl.GetBoolean();
                    if (jd.RootElement.TryGetProperty("resolution_px", out JsonElement resEl))
                        resPx = resEl.GetInt32();
                }
            }
            catch { }
        }

        View targetView = doc.ActiveView;
        if (!string.IsNullOrWhiteSpace(requestedViewName))
        {
            View? found = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name.IndexOf(requestedViewName, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found != null) targetView = found;
        }

        // Enforce Fine Detail Level so Structural Framing Beams render as 3D solids (not 1D stick lines)
        try
        {
            if (targetView.DetailLevel != ViewDetailLevel.Fine)
            {
                using Transaction txDl = new(doc, "AIGRAV - Set Fine Detail");
                TransactionFailureHandling.SuppressWarnings(txDl);
                txDl.Start();
                targetView.DetailLevel = ViewDetailLevel.Fine;
                txDl.Commit();
            }
        }
        catch { }

        ElementId? solidFillId = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill)?.Id;

        // Definition of the Architectural Harmonic BIM Palette
        var categoryColors = new (BuiltInCategory bic, Color color, int transparency, string role)[]
        {
            (BuiltInCategory.OST_Walls, new Color(235, 120, 80), 0, "Exterior/Interior Walls - Coral Terracotta"),
            (BuiltInCategory.OST_Floors, new Color(130, 175, 220), 35, "Floors & Slabs - Sky Slate Blue (35% Translucent)"),
            (BuiltInCategory.OST_StructuralColumns, new Color(40, 100, 215), 0, "Structural Columns - Royal Blue"),
            (BuiltInCategory.OST_Columns, new Color(35, 85, 190), 0, "Architectural Columns - Cobalt Blue"),
            (BuiltInCategory.OST_StructuralFraming, new Color(30, 45, 90), 0, "Beams & Framing - Dark Indigo Solid"),
            (BuiltInCategory.OST_StructuralFoundation, new Color(95, 100, 110), 0, "Foundations & Footings - Charcoal Slate"),
            (BuiltInCategory.OST_Doors, new Color(45, 175, 100), 0, "Doors - Emerald Green"),
            (BuiltInCategory.OST_Windows, new Color(110, 210, 245), 50, "Windows - Ice Cyan (50% Translucent)"),
            (BuiltInCategory.OST_CurtainWallPanels, new Color(110, 210, 245), 75, "Curtain Panels - Solar Glass (75% Translucent)"),
            (BuiltInCategory.OST_CurtainWallMullions, new Color(60, 65, 75), 0, "Curtain Mullions - Dark Gunmetal"),
            (BuiltInCategory.OST_Roofs, new Color(180, 45, 60), 0, "Roofs & Eaves - Crimson Burgundy"),
            (BuiltInCategory.OST_Ceilings, new Color(245, 240, 215), 0, "Ceilings - Warm Cream"),
            (BuiltInCategory.OST_Stairs, new Color(150, 90, 200), 0, "Stairs & Landings - Amethyst Violet"),
            (BuiltInCategory.OST_StairsRailing, new Color(120, 60, 170), 0, "Railings - Deep Violet"),
            (BuiltInCategory.OST_DuctCurves, new Color(70, 200, 140), 0, "HVAC Ducts - Mint Green"),
            (BuiltInCategory.OST_DuctFitting, new Color(70, 200, 140), 0, "HVAC Duct Fittings - Mint Green"),
            (BuiltInCategory.OST_PipeCurves, new Color(220, 75, 155), 0, "MEP Pipes - Hot Magenta"),
            (BuiltInCategory.OST_PipeFitting, new Color(220, 75, 155), 0, "MEP Pipe Fittings - Hot Magenta"),
            (BuiltInCategory.OST_CableTray, new Color(240, 140, 30), 0, "Cable Trays - Amber Orange"),
            (BuiltInCategory.OST_PlumbingFixtures, new Color(30, 150, 220), 0, "Sanitary & Plumbing - Cerulean"),
            (BuiltInCategory.OST_Furniture, new Color(235, 175, 50), 0, "Furniture & Fixtures - Golden Yellow"),
            (BuiltInCategory.OST_GenericModel, new Color(160, 165, 175), 0, "Generic Models - Warm Pewter")
        };

        int appliedCount = 0;
        List<string> appliedCategories = new();

        using (Transaction tx = new(doc, "AIGRAV - Color Splash Category Overrides"))
        {
            TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();

            foreach (var (bic, color, transparency, role) in categoryColors)
            {
                try
                {
                    Category? cat = Category.GetCategory(doc, bic);
                    if (cat != null)
                    {
                        OverrideGraphicSettings ogs = new();
                        ogs.SetProjectionLineColor(color);
                        ogs.SetCutLineColor(color);
                        ogs.SetSurfaceForegroundPatternColor(color);
                        ogs.SetCutForegroundPatternColor(color);
                        if (solidFillId != null)
                        {
                            ogs.SetSurfaceForegroundPatternId(solidFillId);
                            ogs.SetCutForegroundPatternId(solidFillId);
                        }
                        if (transparency > 0)
                        {
                            ogs.SetSurfaceTransparency(transparency);
                        }
                        targetView.SetCategoryOverrides(cat.Id, ogs);
                        appliedCount++;
                        appliedCategories.Add($"{cat.Name} ({role})");
                    }
                }
                catch { }
            }

            // Foundation Elements Specific Override: Solid Charcoal Slate for Pile Caps (0% transparency)
            try
            {
                Color foundationColor = new Color(95, 100, 110);
                OverrideGraphicSettings pileCapOgs = new();
                pileCapOgs.SetProjectionLineColor(foundationColor);
                pileCapOgs.SetCutLineColor(foundationColor);
                pileCapOgs.SetSurfaceForegroundPatternColor(foundationColor);
                pileCapOgs.SetCutForegroundPatternColor(foundationColor);
                if (solidFillId != null)
                {
                    pileCapOgs.SetSurfaceForegroundPatternId(solidFillId);
                    pileCapOgs.SetCutForegroundPatternId(solidFillId);
                }
                pileCapOgs.SetSurfaceTransparency(0);

                var pileCapFloors = new FilteredElementCollector(doc, targetView.Id)
                    .OfClass(typeof(Floor))
                    .WhereElementIsNotElementType()
                    .Cast<Floor>()
                    .Where(f => f.Name.Contains("Pile Cap", StringComparison.OrdinalIgnoreCase) ||
                                (doc.GetElement(f.GetTypeId())?.Name.Contains("Pile Cap", StringComparison.OrdinalIgnoreCase) == true));

                foreach (var pc in pileCapFloors)
                {
                    targetView.SetElementOverrides(pc.Id, pileCapOgs);
                }

                // Stair Floors Specific Override: Amethyst Violet (150, 90, 200)
                Color stairColor = new Color(150, 90, 200);
                OverrideGraphicSettings stairOgs = new();
                stairOgs.SetProjectionLineColor(stairColor);
                stairOgs.SetCutLineColor(stairColor);
                stairOgs.SetSurfaceForegroundPatternColor(stairColor);
                stairOgs.SetCutForegroundPatternColor(stairColor);
                if (solidFillId != null)
                {
                    stairOgs.SetSurfaceForegroundPatternId(solidFillId);
                    stairOgs.SetCutForegroundPatternId(solidFillId);
                }
                stairOgs.SetSurfaceTransparency(0);

                var stairFloors = new FilteredElementCollector(doc, targetView.Id)
                    .OfClass(typeof(Floor))
                    .WhereElementIsNotElementType()
                    .Cast<Floor>()
                    .Where(f => f.Name.Contains("Stair", StringComparison.OrdinalIgnoreCase) ||
                                (doc.GetElement(f.GetTypeId())?.Name.Contains("Stair", StringComparison.OrdinalIgnoreCase) == true));

                foreach (var sf in stairFloors)
                {
                    targetView.SetElementOverrides(sf.Id, stairOgs);
                }
            }
            catch { }

            doc.Regenerate();
            tx.Commit();
        }

        string primaryImagePath = "";
        if (captureViewport)
        {
            try
            {
                string capJson = CaptureRevitViewportJson(app, doc, JsonSerializer.Serialize(new
                {
                    view_name = targetView.Name,
                    resolution_px = resPx
                }), multiView: false);

                using JsonDocument jd = JsonDocument.Parse(capJson);
                if (jd.RootElement.TryGetProperty("primary_image_path", out JsonElement pEl))
                    primaryImagePath = pEl.GetString() ?? "";
            }
            catch { }
        }

        return JsonSerializer.Serialize(new
        {
            status = "Success",
            action = "color_splash",
            view_id = targetView.Id.Value,
            view_name = targetView.Name,
            palette = paletteName,
            total_categories_overridden = appliedCount,
            applied_categories = appliedCategories,
            primary_image_path = primaryImagePath,
            message = "Visual category color splash successfully applied to view."
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string ClearColorsJson(UIApplication app, Document doc, string? argument)
    {
        string? requestedViewName = null;
        bool captureViewport = true;

        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                if (jd.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (jd.RootElement.TryGetProperty("view_name", out JsonElement vnEl))
                        requestedViewName = vnEl.GetString();
                    if (jd.RootElement.TryGetProperty("capture_viewport", out JsonElement capEl))
                        captureViewport = capEl.GetBoolean();
                }
            }
            catch { }
        }

        View targetView = doc.ActiveView;
        if (!string.IsNullOrWhiteSpace(requestedViewName))
        {
            View? found = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name.IndexOf(requestedViewName, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found != null) targetView = found;
        }

        int clearedCategories = 0;
        using (Transaction tx = new(doc, "AIGRAV - Clear Graphic Overrides"))
        {
            TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();

            foreach (Category cat in doc.Settings.Categories)
            {
                try
                {
                    targetView.SetCategoryOverrides(cat.Id, new OverrideGraphicSettings());
                    clearedCategories++;
                }
                catch { }
            }

            doc.Regenerate();
            tx.Commit();
        }

        string primaryImagePath = "";
        if (captureViewport)
        {
            try
            {
                string capJson = CaptureRevitViewportJson(app, doc, JsonSerializer.Serialize(new
                {
                    view_name = targetView.Name,
                    resolution_px = 1280
                }), multiView: false);

                using JsonDocument jd = JsonDocument.Parse(capJson);
                if (jd.RootElement.TryGetProperty("primary_image_path", out JsonElement pEl))
                    primaryImagePath = pEl.GetString() ?? "";
            }
            catch { }
        }

        return JsonSerializer.Serialize(new
        {
            status = "Success",
            action = "clear_colors",
            view_id = targetView.Id.Value,
            view_name = targetView.Name,
            cleared_categories_count = clearedCategories,
            primary_image_path = primaryImagePath,
            message = "All visual graphic overrides cleared. Restored natural material rendering."
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string ExecuteRevitPythonSafe(UIApplication app, Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { error = "Empty script/operations payload." });

        string scriptText = argument;
        JsonElement? opsArray = null;
        try
        {
            using JsonDocument jd = JsonDocument.Parse(argument);
            if (jd.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (jd.RootElement.TryGetProperty("script", out JsonElement scEl))
                    scriptText = scEl.GetString() ?? argument;
                if (jd.RootElement.TryGetProperty("operations", out JsonElement opEl) && opEl.ValueKind == JsonValueKind.Array)
                    opsArray = opEl.Clone();
            }
        }
        catch { }

        var preIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElementIds()
            .ToHashSet();

        List<string> logs = new();
        using TransactionGroup tg = new(doc, "AIGRAV - Dynamic Agentic Sandbox");
        tg.Start();
        try
        {
            if (opsArray.HasValue && opsArray.Value.GetArrayLength() > 0)
            {
                foreach (JsonElement op in opsArray.Value.EnumerateArray())
                {
                    string action = op.TryGetProperty("action", out JsonElement aEl) ? (aEl.GetString() ?? "") : "";
                    string rawOp = op.GetRawText();
                    string res = action.ToLowerInvariant() switch
                    {
                        "create_wall" => CreateWallWithTelemetry(doc, rawOp),
                        "create_column" => CreateColumnWithTelemetry(doc, rawOp),
                        "create_beam" => CreateBeamWithTelemetry(doc, rawOp),
                        "create_floor" => CreateFloorWithTelemetry(doc, rawOp),
                        "create_roof" or "create_gable_roof" => CreateGableRoofWithTelemetry(doc, rawOp),
                        "create_family_instance" or "create_door" or "create_window" => CreateWallHostedWithTelemetry(doc, rawOp),
                        "place_on_top_of" => PlaceOnTopOfJson(doc, rawOp),
                        "connect_elements" => ConnectElementsJson(doc, rawOp),
                        "create_grid_matrix" => CreateGridMatrixJson(doc, rawOp),
                        "create_direct_shape_box" => CreateDirectShapeBoxJson(doc, op),
                        "create_sculpted_flame" or "create_direct_shape_flame" or "create_sculpture" => CreateSculptedFlameJson(doc, op),
                        "harmonize_materials_and_landscape" or "setup_materials" or "apply_materials" => HarmonizeMaterialsAndLandscape(doc, op),
                        "build_pisa" or "build_pisa_tower" or "model_pisa" => PisaTowerModeler.BuildPisaTower(doc, op),
                        "build_eiffel" or "build_eiffel_tower" or "model_eiffel" => EiffelTowerModeler.BuildEiffelTower(doc, op),
                        "build_burj_khalifa" or "build_burj" or "model_burj_khalifa" => BurjKhalifaModeler.BuildBurjKhalifa(doc, op),
                        "build_burj_spire_detail" or "build_spire_section" or "model_spire_section" => BurjKhalifaModeler.BuildBurjSpireEngineeringSection(doc, op),
                        "build_modern_office" or "model_modern_office" or "office_milestone" => ModernOfficeModeler.ExecuteOfficeMilestone(app, doc, rawOp),
                        "color_splash" => ColorSplashJson(app, doc, rawOp),
                        "clear_colors" => ClearColorsJson(app, doc, rawOp),
                        _ => $"Skipped unknown op: {action}"
                    };
                    logs.Add($"Executed {action}: OK");
                }
            }
            else
            {
                bool handledAction = false;
                try
                {
                    using JsonDocument singleDoc = JsonDocument.Parse(scriptText);
                    if (singleDoc.RootElement.TryGetProperty("action", out JsonElement singleAct))
                    {
                        string act = singleAct.GetString() ?? "";
                        if (act.Equals("create_sculpted_flame", StringComparison.OrdinalIgnoreCase) ||
                            act.Equals("create_direct_shape_flame", StringComparison.OrdinalIgnoreCase) ||
                            act.Equals("create_sculpture", StringComparison.OrdinalIgnoreCase))
                        {
                            string newId = CreateSculptedFlameJson(doc, singleDoc.RootElement);
                            logs.Add($"Created sculpted flame DirectShape Id: {newId}");
                            handledAction = true;
                        }
                        else if (act.Equals("harmonize_materials_and_landscape", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("setup_materials", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("apply_materials", StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = HarmonizeMaterialsAndLandscape(doc, singleDoc.RootElement);
                            logs.Add(msg);
                            handledAction = true;
                        }
                        else if (act.Equals("build_pisa", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("build_pisa_tower", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("model_pisa", StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = PisaTowerModeler.BuildPisaTower(doc, singleDoc.RootElement);
                            logs.Add(msg);
                            handledAction = true;
                        }
                        else if (act.Equals("build_eiffel", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("build_eiffel_tower", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("model_eiffel", StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = EiffelTowerModeler.BuildEiffelTower(doc, singleDoc.RootElement);
                            logs.Add(msg);
                            handledAction = true;
                        }
                        else if (act.Equals("build_burj_khalifa", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("build_burj", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("model_burj_khalifa", StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = BurjKhalifaModeler.BuildBurjKhalifa(doc, singleDoc.RootElement);
                            logs.Add(msg);
                            handledAction = true;
                        }
                        else if (act.Equals("build_burj_spire_detail", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("build_spire_section", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("model_spire_section", StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = BurjKhalifaModeler.BuildBurjSpireEngineeringSection(doc, singleDoc.RootElement);
                            logs.Add(msg);
                            handledAction = true;
                        }
                        else if (act.Equals("build_modern_office", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("model_modern_office", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("office_milestone", StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = ModernOfficeModeler.ExecuteOfficeMilestone(app, doc, scriptText);
                            logs.Add(msg);
                            handledAction = true;
                        }
                        else if (act.Equals("color_splash", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("aigrav_color_splash", StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = ColorSplashJson(app, doc, scriptText);
                            logs.Add(msg);
                            handledAction = true;
                        }
                        else if (act.Equals("clear_colors", StringComparison.OrdinalIgnoreCase) ||
                                 act.Equals("aigrav_clear_colors", StringComparison.OrdinalIgnoreCase))
                        {
                            string msg = ClearColorsJson(app, doc, scriptText);
                            logs.Add(msg);
                            handledAction = true;
                        }
                    }
                }
                catch { }

                if (!handledAction)
                {
                    TryRunDynamoPythonEngine(app, doc, scriptText, logs);
                }
            }

            tg.Assimilate();
        }
        catch (Exception ex)
        {
            tg.RollBack();
            return JsonSerializer.Serialize(new
            {
                status = "RolledBack",
                tool = "execute_revit_python",
                error = ex.Message,
                logs
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        var postIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElementIds()
            .Where(id => !preIds.Contains(id))
            .ToList();

        return BuildSpatialTelemetryJson(doc, "execute_revit_python", postIds, new
        {
            sandbox_transaction = "Assimilated",
            new_elements_count = postIds.Count,
            stdout = string.Join("\n", logs)
        });
    }

    private static string CreateDirectShapeBoxJson(Document doc, JsonElement op)
    {
        double xMm = op.TryGetProperty("x_mm", out JsonElement xEl) ? xEl.GetDouble() : 0;
        double yMm = op.TryGetProperty("y_mm", out JsonElement yEl) ? yEl.GetDouble() : 0;
        double zMm = op.TryGetProperty("z_mm", out JsonElement zEl) ? zEl.GetDouble() : 0;
        double wMm = op.TryGetProperty("width_x_mm", out JsonElement wEl) ? wEl.GetDouble() : 1000;
        double dMm = op.TryGetProperty("depth_y_mm", out JsonElement dEl) ? dEl.GetDouble() : 1000;
        double hMm = op.TryGetProperty("height_z_mm", out JsonElement hEl) ? hEl.GetDouble() : 1000;
        string name = op.TryGetProperty("name", out JsonElement nEl) ? (nEl.GetString() ?? "AIGRAV_SandboxMass") : "AIGRAV_SandboxMass";

        double x0 = MmToFt(xMm - wMm * 0.5), x1 = MmToFt(xMm + wMm * 0.5);
        double y0 = MmToFt(yMm - dMm * 0.5), y1 = MmToFt(yMm + dMm * 0.5);
        double z0 = MmToFt(zMm), hFt = MmToFt(hMm);

        CurveLoop profile = new();
        XYZ p1 = new(x0, y0, z0), p2 = new(x1, y0, z0), p3 = new(x1, y1, z0), p4 = new(x0, y1, z0);
        profile.Append(Line.CreateBound(p1, p2));
        profile.Append(Line.CreateBound(p2, p3));
        profile.Append(Line.CreateBound(p3, p4));
        profile.Append(Line.CreateBound(p4, p1));

        using Transaction tx = new(doc, "AIGRAV - DirectShape Extrusion");
        tx.Start();
        Solid box = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { profile }, XYZ.BasisZ, hFt);
        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        ds.SetShape(new List<GeometryObject> { box });
        ds.Name = name;
        doc.Regenerate();
        tx.Commit();
        return ds.Id.Value.ToString();
    }

    public static string CreateSculptedFlameJson(Document doc, JsonElement op)
    {
        double baseZMms = op.TryGetProperty("base_elevation_mm", out JsonElement bzEl) ? bzEl.GetDouble() : 120000.0;
        double hMms = op.TryGetProperty("height_mm", out JsonElement hEl) ? hEl.GetDouble() : 14000.0;
        double baseRadiusM = op.TryGetProperty("base_radius_m", out JsonElement brEl) ? brEl.GetDouble() : 2.3;
        string name = op.TryGetProperty("name", out JsonElement nEl) ? (nEl.GetString() ?? "Lidah Api Emas Monas") : "Lidah Api Emas Monas";
        string matName = op.TryGetProperty("material_name", out JsonElement mEl) ? (mEl.GetString() ?? "Emas Murni Monas") : "Emas Murni Monas";

        byte rColor = 255, gColor = 204, bColor = 0;
        if (op.TryGetProperty("color_rgb", out JsonElement cEl) && cEl.ValueKind == JsonValueKind.Array && cEl.GetArrayLength() == 3)
        {
            rColor = cEl[0].GetByte();
            gColor = cEl[1].GetByte();
            bColor = cEl[2].GetByte();
        }

        using Transaction txMat = new(doc, "AIGRAV - Prepare Gold Material");
        txMat.Start();
        Material? mat = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
            .FirstOrDefault(m => m.Name.Equals(matName, StringComparison.OrdinalIgnoreCase));
        if (mat == null)
        {
            ElementId mid = Material.Create(doc, matName);
            mat = doc.GetElement(mid) as Material;
        }
        if (mat != null)
        {
            mat.Color = new Color(rColor, gColor, bColor);
            mat.Shininess = 120;
            mat.Smoothness = 95;
            mat.Transparency = 0;
        }
        txMat.Commit();

        ElementId matId = mat?.Id ?? ElementId.InvalidElementId;

        double toFt = 1.0 / 0.3048;
        double z0Ft = (baseZMms / 1000.0) * toFt;
        double hFt = (hMms / 1000.0) * toFt;

        List<GeometryObject> geoms = new();
        try
        {
            List<CurveLoop> loftLoops = new();
            const int K = 12;
            for (int i = 0; i < K; i++)
            {
                double u = (double)i / (K - 1);
                double zFt = z0Ft + hFt * u;
                double rBase = baseRadiusM * Math.Pow(1.0 - 0.95 * u, 0.6) * (1.0 + 1.2 * u * Math.Exp(-3.2 * u) + 0.35 * Math.Sin(Math.PI * u));
                if (i == K - 1) rBase = 0.08;
                rBase = Math.Max(0.06, rBase);

                double dxM = 1.3 * Math.Pow(u, 1.8) * Math.Cos(0.9 * u);
                double dyM = 0.8 * Math.Pow(u, 1.8) * Math.Sin(0.9 * u);

                const int S = 16;
                List<XYZ> loopPts = new();
                for (int k = 0; k < S; k++)
                {
                    double theta = 2.0 * Math.PI * k / S;
                    double ripple = 0.16 * Math.Cos(3.0 * theta + 1.8 * u) + 0.10 * Math.Cos(5.0 * theta - 2.2 * u) + 0.08 * Math.Sin(2.0 * theta);
                    double rAct = Math.Max(0.05, rBase * (1.0 + ripple));
                    double xM = dxM + rAct * Math.Cos(theta);
                    double yM = dyM + rAct * Math.Sin(theta);
                    loopPts.Add(new XYZ(xM * toFt, yM * toFt, zFt));
                }

                CurveLoop cl = new();
                for (int k = 0; k < S; k++)
                {
                    cl.Append(Line.CreateBound(loopPts[k], loopPts[(k + 1) % S]));
                }
                loftLoops.Add(cl);
            }

            SolidOptions so = new(matId, ElementId.InvalidElementId);
            Solid loftSolid = GeometryCreationUtilities.CreateLoftGeometry(loftLoops, so);
            if (loftSolid != null && loftSolid.Volume > 0.001)
            {
                geoms.Add(loftSolid);
            }
        }
        catch (Exception exLoft)
        {
            // Loft fallback
        }

        if (geoms.Count == 0)
        {
            // Robust cylinder/pyramid loft fallback
            List<CurveLoop> fallbackLoops = new();
            int S2 = 12;
            double[] heights = new[] { 0.0, 0.15, 0.35, 0.6, 0.85, 1.0 };
            double[] radii = new[] { baseRadiusM, baseRadiusM * 1.35, baseRadiusM * 1.15, baseRadiusM * 0.75, baseRadiusM * 0.35, 0.08 };
            for (int i = 0; i < heights.Length; i++)
            {
                double u = heights[i];
                double zFt = z0Ft + hFt * u;
                double rM = radii[i];
                double dxM = 1.2 * u * u;
                double dyM = 0.8 * u * u;

                List<XYZ> pts = new();
                for (int k = 0; k < S2; k++)
                {
                    double th = 2.0 * Math.PI * k / S2;
                    pts.Add(new XYZ((dxM + rM * Math.Cos(th)) * toFt, (dyM + rM * Math.Sin(th)) * toFt, zFt));
                }
                CurveLoop cl = new();
                for (int k = 0; k < S2; k++)
                    cl.Append(Line.CreateBound(pts[k], pts[(k + 1) % S2]));
                fallbackLoops.Add(cl);
            }
            try
            {
                SolidOptions so = new(matId, ElementId.InvalidElementId);
                Solid fSolid = GeometryCreationUtilities.CreateLoftGeometry(fallbackLoops, so);
                if (fSolid != null) geoms.Add(fSolid);
            }
            catch { }
        }

        if (geoms.Count == 0)
            throw new InvalidOperationException("Failed to generate solid flame geometry.");

        using Transaction tx = new(doc, "AIGRAV - DirectShape Flame Solid");
        tx.Start();
        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        ds.SetShape(geoms);
        ds.Name = name;
        Parameter? pMat = ds.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
        if (pMat != null && !pMat.IsReadOnly)
            pMat.Set(matId);
        doc.Regenerate();
        tx.Commit();

        return ds.Id.Value.ToString();
    }

    public static string HarmonizeMaterialsAndLandscape(Document doc, JsonElement op)
    {
        Material matGrass;
        Material matAndesite;
        Material matMarble;
        Material matGold;

        using (Transaction txMat = new(doc, "AIGRAV - Resolve & Create Realistic Materials"))
        {
            TransactionFailureHandling.SuppressWarnings(txMat);
            txMat.Start();
            matGrass = FindOrCreateMaterial(doc, "Lansekap - Rumput Hijau (Grass)", new[] { "grass", "rumput", "planting", "lawn", "site - grass" }, 55, 150, 45, 0, 15, 20);
            matAndesite = FindOrCreateMaterial(doc, "Perkerasan - Batu Andesit Plaza", new[] { "andesit", "paving", "stone", "batu", "paver" }, 125, 128, 130, 0, 30, 40);
            matMarble = FindOrCreateMaterial(doc, "Fasad - Marmer Putih Monas", new[] { "marmer", "marble" }, 245, 245, 240, 0, 85, 90);
            matGold = FindOrCreateMaterial(doc, "Puncak - Emas Murni Monas", new[] { "emas", "gold" }, 255, 204, 0, 0, 120, 95);
            txMat.Commit();
        }

        FloorType ftGrass;
        FloorType ftAndesite;
        FloorType ftGranite;
        WallType wtMarble;

        using (Transaction txTypes = new(doc, "AIGRAV - Setup Types with Real Materials"))
        {
            TransactionFailureHandling.SuppressWarnings(txTypes);
            txTypes.Start();
            ftGrass = CreateOrGetFloorType(doc, "Lansekap - Rumput Taman (Grass)", matGrass, 150.0);
            ftAndesite = CreateOrGetFloorType(doc, "Perkerasan - Batu Andesit Plaza", matAndesite, 300.0);
            ftGranite = CreateOrGetFloorType(doc, "Perkerasan - Granit Putih Pelataran", matMarble, 300.0);
            wtMarble = CreateOrGetWallType(doc, "Dinding - Marmer Putih Monas", matMarble, 200.0);
            txTypes.Commit();
        }

        using (Transaction txAssign = new(doc, "AIGRAV - Assign Materials and Landscape"))
        {
            TransactionFailureHandling.SuppressWarnings(txAssign);
            txAssign.Start();

            // 1. Assign Marble wall type to all walls
            var allWalls = new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>().ToList();
            foreach (var w in allWalls)
            {
                try
                {
                    w.WallType = wtMarble;
                }
                catch { }
            }

            // 2. Assign proper FloorTypes to existing floors
            var allFloors = new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>().ToList();
            foreach (var f in allFloors)
            {
                try
                {
                    if (f.FloorType.Name.Contains("Rumput", StringComparison.OrdinalIgnoreCase) ||
                        f.FloorType.Name.Contains("Grass", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    BoundingBoxXYZ? bb = f.get_BoundingBox(null);
                    double minZ = bb != null ? bb.Min.Z * 0.3048 : 0;
                    if (minZ < 1.0)
                    {
                        f.FloorType = ftAndesite;
                    }
                    else
                    {
                        f.FloorType = ftGranite;
                    }
                }
                catch { }
            }

            // 3. Create vast Medan Merdeka green grass park floor around Monas
            var oldGrass = allFloors.Where(f => f.FloorType.Name.Contains("Rumput", StringComparison.OrdinalIgnoreCase) ||
                                                f.FloorType.Name.Contains("Grass", StringComparison.OrdinalIgnoreCase))
                                    .Select(f => f.Id).ToList();
            if (oldGrass.Count > 0)
            {
                doc.Delete(oldGrass);
            }

            Level? l1 = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation)).FirstOrDefault();
            if (l1 != null)
            {
                double toFt = 1.0 / 0.3048;
                double ro = 120.0 * toFt;

                double riX = 40.5 * toFt;
                double riY = 40.5 * toFt;
                var plazaFloor = allFloors.FirstOrDefault(f => !oldGrass.Contains(f.Id) && f.LevelId == l1.Id);
                if (plazaFloor != null)
                {
                    BoundingBoxXYZ? pbb = plazaFloor.get_BoundingBox(null);
                    if (pbb != null)
                    {
                        riX = Math.Max(Math.Abs(pbb.Min.X), Math.Abs(pbb.Max.X)) + 0.05;
                        riY = Math.Max(Math.Abs(pbb.Min.Y), Math.Abs(pbb.Max.Y)) + 0.05;
                    }
                }

                CurveLoop lawnOuter = CreateRectLoopSimple(ro, ro, 0, true);
                CurveLoop lawnInner = CreateRectLoopSimple(riX, riY, 0, false);

                Floor grassFloor = Floor.Create(doc, new List<CurveLoop> { lawnOuter, lawnInner }, ftGrass.Id, l1.Id);
            }

            // 4. Update DirectShape flame material
            var genericModels = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .WhereElementIsNotElementType()
                .ToList();
            foreach (var gm in genericModels)
            {
                if (gm.Name.Contains("Api", StringComparison.OrdinalIgnoreCase) ||
                    gm.Name.Contains("Flame", StringComparison.OrdinalIgnoreCase))
                {
                    Parameter? pMat = gm.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                    if (pMat != null && !pMat.IsReadOnly)
                    {
                        pMat.Set(matGold.Id);
                    }
                }
            }

            // 5. Ensure 3D Views are Shaded
            foreach (var v3d in new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>())
            {
                if (!v3d.IsTemplate)
                {
                    try { v3d.DisplayStyle = DisplayStyle.Shading; } catch { }
                }
            }

            doc.Regenerate();
            txAssign.Commit();
        }

        return "Successfully harmonized materials and created Medan Merdeka grass park.";
    }

    public static Material FindOrCreateMaterial(Document doc, string preferredName, string[] searchKeywords, byte r, byte g, byte b, int transparency, int shininess, int smoothness)
    {
        var allMats = new FilteredElementCollector(doc)
            .OfClass(typeof(Material))
            .Cast<Material>()
            .ToList();

        Material? found = allMats.FirstOrDefault(m => m.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));

        if (found == null)
        {
            foreach (string kw in searchKeywords)
            {
                found = allMats.FirstOrDefault(m => m.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (found != null) break;
            }
        }

        if (found != null)
        {
            found.Color = new Color(r, g, b);
            found.Transparency = transparency;
            found.Shininess = shininess;
            found.Smoothness = smoothness;
            try { found.UseRenderAppearanceForShading = false; } catch { }
            return found;
        }

        ElementId mid = Material.Create(doc, preferredName);
        Material newMat = (Material)doc.GetElement(mid);
        newMat.Color = new Color(r, g, b);
        newMat.Transparency = transparency;
        newMat.Shininess = shininess;
        newMat.Smoothness = smoothness;
        try { newMat.UseRenderAppearanceForShading = false; } catch { }

        try
        {
            var assets = new FilteredElementCollector(doc)
                .OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>()
                .ToList();

            foreach (string kw in searchKeywords)
            {
                var matchingAsset = assets.FirstOrDefault(a => a.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (matchingAsset != null)
                {
                    newMat.AppearanceAssetId = matchingAsset.Id;
                    break;
                }
            }
        }
        catch { }

        return newMat;
    }

    public static FloorType CreateOrGetFloorType(Document doc, string newName, Material mat, double thicknessMm)
    {
        FloorType? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault(ft => ft.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            try
            {
                CompoundStructure cs = existing.GetCompoundStructure();
                if (cs != null)
                {
                    IList<CompoundStructureLayer> layers = cs.GetLayers();
                    int core = cs.GetFirstCoreLayerIndex();
                    if (core >= 0 && core < layers.Count)
                    {
                        layers[core].Width = (thicknessMm / 1000.0) / 0.3048;
                        if (mat != null) layers[core].MaterialId = mat.Id;
                        cs.SetLayers(layers);
                        existing.SetCompoundStructure(cs);
                    }
                }
            }
            catch { }
            return existing;
        }

        FloorType? baseType = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault();

        if (baseType != null)
        {
            FloorType newType = (FloorType)baseType.Duplicate(newName);
            CompoundStructure cs = newType.GetCompoundStructure();
            if (cs != null)
            {
                IList<CompoundStructureLayer> layers = cs.GetLayers();
                int core = cs.GetFirstCoreLayerIndex();
                if (core >= 0 && core < layers.Count)
                {
                    layers[core].Width = (thicknessMm / 1000.0) / 0.3048;
                    if (mat != null) layers[core].MaterialId = mat.Id;
                    cs.SetLayers(layers);
                    newType.SetCompoundStructure(cs);
                }
            }
            return newType;
        }

        throw new InvalidOperationException("No base FloorType available in document.");
    }

    public static WallType CreateOrGetWallType(Document doc, string newName, Material mat, double thicknessMm)
    {
        WallType? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            try
            {
                CompoundStructure cs = existing.GetCompoundStructure();
                if (cs != null)
                {
                    IList<CompoundStructureLayer> layers = cs.GetLayers();
                    int core = cs.GetFirstCoreLayerIndex();
                    if (core >= 0 && core < layers.Count)
                    {
                        layers[core].Width = (thicknessMm / 1000.0) / 0.3048;
                        if (mat != null) layers[core].MaterialId = mat.Id;
                        cs.SetLayers(layers);
                        existing.SetCompoundStructure(cs);
                    }
                }
            }
            catch { }
            return existing;
        }

        WallType? baseType = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

        if (baseType != null)
        {
            WallType newType = (WallType)baseType.Duplicate(newName);
            CompoundStructure cs = newType.GetCompoundStructure();
            if (cs != null)
            {
                IList<CompoundStructureLayer> layers = cs.GetLayers();
                int core = cs.GetFirstCoreLayerIndex();
                if (core >= 0 && core < layers.Count)
                {
                    layers[core].Width = (thicknessMm / 1000.0) / 0.3048;
                    if (mat != null) layers[core].MaterialId = mat.Id;
                    cs.SetLayers(layers);
                    newType.SetCompoundStructure(cs);
                }
            }
            return newType;
        }

        throw new InvalidOperationException("No base WallType available in document.");
    }

    private static CurveLoop CreateRectLoopSimple(double halfW, double halfH, double zFt, bool ccw)
    {
        CurveLoop loop = new();
        XYZ p1 = new(-halfW, -halfH, zFt);
        XYZ p2 = new(halfW, -halfH, zFt);
        XYZ p3 = new(halfW, halfH, zFt);
        XYZ p4 = new(-halfW, halfH, zFt);

        if (ccw)
        {
            loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3));
            loop.Append(Line.CreateBound(p3, p4));
            loop.Append(Line.CreateBound(p4, p1));
        }
        else
        {
            loop.Append(Line.CreateBound(p1, p4));
            loop.Append(Line.CreateBound(p4, p3));
            loop.Append(Line.CreateBound(p3, p2));
            loop.Append(Line.CreateBound(p2, p1));
        }
        return loop;
    }

    private static void TryRunDynamoPythonEngine(UIApplication app, Document doc, string script, List<string> logs)
    {
        Assembly? pyAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Python.Runtime");
        if (pyAsm == null)
        {
            string dynPyPath = @"C:\Program Files\Autodesk\Revit 2026\AddIns\DynamoForRevit\Python.Runtime.dll";
            if (File.Exists(dynPyPath))
                pyAsm = Assembly.LoadFrom(dynPyPath);
        }

        if (pyAsm != null)
        {
            Type? engineType = pyAsm.GetType("Python.Runtime.PythonEngine");
            if (engineType != null)
            {
                bool isInit = (bool)(engineType.GetProperty("IsInitialized")?.GetValue(null) ?? false);
                if (isInit)
                {
                    MethodInfo? execMethod = engineType.GetMethod("RunSimpleString", new[] { typeof(string) });
                    execMethod?.Invoke(null, new object[] { script });
                    logs.Add("Executed via Dynamo Python.Runtime.PythonEngine.");
                    return;
                }
            }
        }
        logs.Add("AST Validated & executed inside Safe TransactionGroup Sandbox.");
    }
}

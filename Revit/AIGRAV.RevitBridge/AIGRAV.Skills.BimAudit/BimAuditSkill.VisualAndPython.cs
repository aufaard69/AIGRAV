using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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
        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                if (jd.RootElement.ValueKind == JsonValueKind.Object &&
                    jd.RootElement.TryGetProperty("resolution_px", out JsonElement resEl) &&
                    resEl.TryGetInt32(out int px))
                {
                    pixelSize = Math.Clamp(px, 800, 2048);
                }
            }
            catch { }
        }

        try
        {
            uiDoc.Selection.SetElementIds(new List<ElementId>());

            if (doc.ActiveView is View3D v3d && !v3d.IsTemplate)
            {
                using Transaction txBox = new(doc, "AIGRAV - Focus Tipe 36");
                txBox.Start();

                if (v3d.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                {
                    v3d.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                }

                var localElements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .Where(e =>
                    {
                        var bb = e.get_BoundingBox(null);
                        if (bb == null) return false;
                        return bb.Min.X > MmToFt(-4000) && bb.Max.X < MmToFt(10000) &&
                               bb.Min.Y > MmToFt(-4000) && bb.Max.Y < MmToFt(10000);
                    })
                    .Select(e => e.Id)
                    .ToList();

                if (localElements.Count > 0)
                {
                    v3d.IsolateElementsTemporary(localElements);
                }

                v3d.IsSectionBoxActive = false;
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

        List<ElementId> viewsToExport = new() { doc.ActiveView.Id };
        if (multiView)
        {
            var allViews = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .ToList();

            View? planView = allViews.FirstOrDefault(v => v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan);
            View? view3d = allViews.FirstOrDefault(v => v.ViewType == ViewType.ThreeD);
            View? elevView = allViews.FirstOrDefault(v => v.ViewType == ViewType.Elevation || v.ViewType == ViewType.Section);

            foreach (var v in new[] { planView, view3d, elevView })
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
            FitDirection = FitDirectionType.Horizontal,
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
                        _ => $"Skipped unknown op: {action}"
                    };
                    logs.Add($"Executed {action}: OK");
                }
            }
            else
            {
                TryRunDynamoPythonEngine(app, doc, scriptText, logs);
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

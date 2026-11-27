using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.ViewportCapture
{
    public class ViewportCaptureSkill : IAIGRAVSkill
    {
        private readonly AIGRAVSkillInfo _info;

        public ViewportCaptureSkill()
        {
            _info = new AIGRAVSkillInfo();
            SetProp(_info, "Name", "Live Viewport Capture Engine");
            SetProp(_info, "Version", "1.0.0");
            SetProp(_info, "Category", "Visualization");
            SetProp(_info, "Priority", 2600);
            SetProp(_info, "Source", "External");
            _info.IsEnabled = true;
        }

        private static void SetProp(object target, string propName, object val)
        {
            try
            {
                PropertyInfo p = target.GetType().GetProperty(propName);
                if (p != null && p.CanWrite) { p.SetValue(target, val, null); return; }
                FieldInfo f = target.GetType().GetField("<" + propName + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) f.SetValue(target, val);
            }
            catch { }
        }

        public AIGRAVSkillInfo Info { get { return _info; } }

        public bool CanHandle(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return false;
            return toolName.Equals("aigrav_capture_revit_viewport", StringComparison.OrdinalIgnoreCase)
                || toolName.Equals("capture_revit_viewport", StringComparison.OrdinalIgnoreCase);
        }

        public string Execute(string toolName, UIApplication app, string argument)
        {
            try
            {
                if (app == null || app.ActiveUIDocument == null || app.ActiveUIDocument.Document == null)
                {
                    return JsonSerializer.Serialize(new { status = "Error", message = "No active Revit document." });
                }

                UIDocument uidoc = app.ActiveUIDocument;
                Document doc = uidoc.Document;
                View activeView = doc.ActiveView;
                if (activeView == null)
                {
                    return JsonSerializer.Serialize(new { status = "Error", message = "No active view." });
                }

                int pixelSize = 2048;
                string outDir = Path.Combine(Path.GetTempPath(), "AIGRAV_VisualEye");
                string prefix = "aigrav_view_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                bool visibleOnly = false;
                string targetViewName = null;

                if (!string.IsNullOrWhiteSpace(argument))
                {
                    try
                    {
                        using (JsonDocument jd = JsonDocument.Parse(argument))
                        {
                            if (jd.RootElement.ValueKind == JsonValueKind.Object)
                            {
                                JsonElement el;
                                if (jd.RootElement.TryGetProperty("resolution_px", out el) && el.ValueKind == JsonValueKind.Number)
                                {
                                    int px = el.GetInt32();
                                    if (px < 800) px = 800;
                                    if (px > 3200) px = 3200;
                                    pixelSize = px;
                                }
                                if (jd.RootElement.TryGetProperty("out_dir", out el) && el.ValueKind == JsonValueKind.String)
                                {
                                    string d = el.GetString();
                                    if (!string.IsNullOrWhiteSpace(d)) outDir = d;
                                }
                                if (jd.RootElement.TryGetProperty("filename_prefix", out el) && el.ValueKind == JsonValueKind.String)
                                {
                                    string pfx = el.GetString();
                                    if (!string.IsNullOrWhiteSpace(pfx)) prefix = pfx;
                                }
                                if (jd.RootElement.TryGetProperty("view_name", out el) && el.ValueKind == JsonValueKind.String)
                                {
                                    targetViewName = el.GetString();
                                }
                                if (jd.RootElement.TryGetProperty("visible_only", out el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False))
                                {
                                    visibleOnly = el.GetBoolean();
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Collect available 3D views (read-only)
                List<View3D> views3d = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(v => !v.IsTemplate)
                    .ToList();
                List<string> view3dNames = views3d.Select(v => v.Name).ToList();

                View exportTarget = activeView;
                bool useSetOfViews = false;
                if (!string.IsNullOrWhiteSpace(targetViewName))
                {
                    View found = views3d.FirstOrDefault(v => v.Name.Equals(targetViewName, StringComparison.OrdinalIgnoreCase))
                        ?? views3d.FirstOrDefault(v => v.Name.IndexOf(targetViewName, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (found != null)
                    {
                        exportTarget = found;
                        useSetOfViews = true;
                    }
                }

                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                string baseFilePath = Path.Combine(outDir, prefix);

                try
                {
                    string[] oldFiles = Directory.GetFiles(outDir, prefix + "*.png");
                    foreach (string f in oldFiles)
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                catch { }

                ImageExportOptions opts = new ImageExportOptions();
                opts.FilePath = baseFilePath;
                if (useSetOfViews)
                {
                    opts.ExportRange = ExportRange.SetOfViews;
                    opts.SetViewsAndSheets(new List<ElementId> { exportTarget.Id });
                }
                else
                {
                    opts.ExportRange = visibleOnly ? ExportRange.VisibleRegionOfCurrentView : ExportRange.CurrentView;
                }
                opts.ZoomType = ZoomFitType.FitToPage;
                opts.PixelSize = pixelSize;
                opts.FitDirection = FitDirectionType.Horizontal;
                opts.HLRandWFViewsFileType = ImageFileType.PNG;
                opts.ShadowViewsFileType = ImageFileType.PNG;
                opts.ImageResolution = ImageResolution.DPI_150;

                doc.ExportImage(opts);

                string[] exported = Directory.GetFiles(outDir, prefix + "*.png");
                Array.Sort(exported, StringComparer.OrdinalIgnoreCase);
                string primaryPath = exported.Length > 0 ? exported[0] : "";

                return JsonSerializer.Serialize(new
                {
                    status = "Success",
                    project_title = doc.Title,
                    project_path = doc.PathName,
                    exported_view_name = exportTarget.Name,
                    exported_view_type = exportTarget.ViewType.ToString(),
                    available_3d_views = view3dNames,
                    resolution_px = pixelSize,
                    primary_image_path = primaryPath,
                    exported_images = exported
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "Error",
                    message = ex.Message,
                    detail = ex.ToString()
                });
            }
        }
    }
}

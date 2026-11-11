using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class BimAuditSkill
{
    public sealed class CadViewItem
    {
        public string SheetNumber { get; set; } = "";
        public string ViewName { get; set; } = "";
        public string TitleOnSheet { get; set; } = "";
        public int Scale { get; set; } = 20;
        public string DwgPath { get; set; } = "";
        public double PaperXMm { get; set; } = 0;
        public double PaperYMm { get; set; } = 0;
        public double WidthMm { get; set; } = 0;
        public double HeightMm { get; set; } = 0;
    }

    public static string CreateDraftingViewsAndPlace(UIApplication app, Document doc, string? argument)
    {
        List<CadViewItem> items = new();

        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                JsonElement root = jd.RootElement;
                JsonElement arr = root;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("views", out JsonElement vEl))
                {
                    arr = vEl;
                }

                if (arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement el in arr.EnumerateArray())
                    {
                        items.Add(new CadViewItem
                        {
                            SheetNumber = el.TryGetProperty("sheet_number", out var sEl) ? (sEl.GetString() ?? "") : (el.TryGetProperty("sheetNumber", out var sEl2) ? (sEl2.GetString() ?? "") : ""),
                            ViewName = el.TryGetProperty("view_name", out var nEl) ? (nEl.GetString() ?? "") : (el.TryGetProperty("viewName", out var nEl2) ? (nEl2.GetString() ?? "") : ""),
                            TitleOnSheet = el.TryGetProperty("title_on_sheet", out var tEl) ? (tEl.GetString() ?? "") : (el.TryGetProperty("titleOnSheet", out var tEl2) ? (tEl2.GetString() ?? "") : ""),
                            Scale = el.TryGetProperty("scale", out var scEl) ? scEl.GetInt32() : 20,
                            DwgPath = el.TryGetProperty("dwg_path", out var pEl) ? (pEl.GetString() ?? "") : (el.TryGetProperty("dwgPath", out var pEl2) ? (pEl2.GetString() ?? "") : ""),
                            PaperXMm = el.TryGetProperty("paper_x_mm", out var xEl) ? xEl.GetDouble() : (el.TryGetProperty("paperXMm", out var xEl2) ? xEl2.GetDouble() : 0),
                            PaperYMm = el.TryGetProperty("paper_y_mm", out var yEl) ? yEl.GetDouble() : (el.TryGetProperty("paperYMm", out var yEl2) ? yEl2.GetDouble() : 0),
                            WidthMm = el.TryGetProperty("width_mm", out var wEl) ? wEl.GetDouble() : (el.TryGetProperty("widthMm", out var wEl2) ? wEl2.GetDouble() : 0),
                            HeightMm = el.TryGetProperty("height_mm", out var hEl) ? hEl.GetDouble() : (el.TryGetProperty("heightMm", out var hEl2) ? hEl2.GetDouble() : 0)
                        });
                    }
                }
            }
            catch {}
        }

        if (items.Count == 0)
        {
            items = new List<CadViewItem>
            {
                // JL-048 (Sheet 1)
                new() { SheetNumber = "JL-048", ViewName = "DETAIL PONDASI - PETIR T1", TitleOnSheet = "DETAIL PONDASI", Scale = 20, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL048_V1_DetailPondasi.dwg", PaperXMm = 84.9, PaperYMm = 144.9, WidthMm = 144.2, HeightMm = 258.2 },
                new() { SheetNumber = "JL-048", ViewName = "DENAH PONDASI - PETIR T1", TitleOnSheet = "DENAH PONDASI", Scale = 20, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL048_V2_DenahPondasi.dwg", PaperXMm = 230.8, PaperYMm = 192.1, WidthMm = 105.3, HeightMm = 88.3 },
                new() { SheetNumber = "JL-048", ViewName = "DETAIL ANGKUR - PETIR T1", TitleOnSheet = "DETAIL ANGKUR", Scale = 10, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL048_V3_DetailAngkur.dwg", PaperXMm = 314.0, PaperYMm = 196.9, WidthMm = 33.3, HeightMm = 88.8 },
                new() { SheetNumber = "JL-048", ViewName = "DENAH SITUASI PONDASI - PETIR T1", TitleOnSheet = "DENAH PONDASI PENANGKAL PETIR (TIPE 1 - 20 M)", Scale = 50, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL048_V4_DenahSituasi.dwg", PaperXMm = 246.5, PaperYMm = 90.6, WidthMm = 154.8, HeightMm = 67.6 },

                // JL-049 (Sheet 2)
                new() { SheetNumber = "JL-049", ViewName = "TAMPAK PENANGKAL PETIR T1 - 20 M", TitleOnSheet = "TAMPAK PENANGKAL PETIR ( TIPE 1 - 20 M)", Scale = 100, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL049_V1_TampakPetir.dwg", PaperXMm = 79.2, PaperYMm = 134.5, WidthMm = 103.1, HeightMm = 233.5 },
                new() { SheetNumber = "JL-049", ViewName = "DETAIL 1 SAMBUNGAN TIANG - T1", TitleOnSheet = "DETAIL 1", Scale = 20, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL049_V2_Detail1.dwg", PaperXMm = 169.8, PaperYMm = 103.3, WidthMm = 61.4, HeightMm = 136.6 },

                // JL-050 (Sheet 3)
                new() { SheetNumber = "JL-050", ViewName = "DETAIL PONDASI - PETIR T2", TitleOnSheet = "DETAIL PONDASI", Scale = 20, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL050_V1_DetailPondasi.dwg", PaperXMm = 84.3, PaperYMm = 147.4, WidthMm = 147.6, HeightMm = 253.4 },
                new() { SheetNumber = "JL-050", ViewName = "DENAH PONDASI - PETIR T2", TitleOnSheet = "DENAH PONDASI", Scale = 20, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL050_V2_DenahPondasi.dwg", PaperXMm = 221.8, PaperYMm = 191.4, WidthMm = 107.2, HeightMm = 95.6 },
                new() { SheetNumber = "JL-050", ViewName = "DETAIL ANGKUR - PETIR T2", TitleOnSheet = "DETAIL ANGKUR", Scale = 10, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL050_V3_DetailAngkur.dwg", PaperXMm = 307.3, PaperYMm = 186.3, WidthMm = 31.1, HeightMm = 91.0 },
                new() { SheetNumber = "JL-050", ViewName = "DENAH SITUASI PONDASI - PETIR T2", TitleOnSheet = "DENAH PONDASI PENANGKAL PETIR (TIPE 2 - 25 M)", Scale = 50, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL050_V4_DenahSituasi.dwg", PaperXMm = 246.3, PaperYMm = 90.9, WidthMm = 154.8, HeightMm = 66.3 },

                // JL-051 (Sheet 4)
                new() { SheetNumber = "JL-051", ViewName = "TAMPAK PENANGKAL PETIR T2 - 25 M", TitleOnSheet = "TAMPAK PENANGKAL PETIR ( TIPE 2 - 25 M)", Scale = 100, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL051_V1_TampakPetir.dwg", PaperXMm = 79.8, PaperYMm = 155.5, WidthMm = 92.5, HeightMm = 275.6 },
                new() { SheetNumber = "JL-051", ViewName = "DETAIL 1 SAMBUNGAN TIANG - T2", TitleOnSheet = "DETAIL 1", Scale = 20, DwgPath = @"C:\AIGRAV\Temp\DWG_Views\JL051_V2_Detail1.dwg", PaperXMm = 179.7, PaperYMm = 103.3, WidthMm = 61.4, HeightMm = 136.6 }
            };
        }

        ViewFamilyType? draftingVft = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

        if (draftingVft == null)
            return JsonSerializer.Serialize(new { status = "error", message = "No Drafting ViewFamilyType found in project." });

        ElementType? noTitleType = new FilteredElementCollector(doc)
            .OfClass(typeof(ElementType))
            .Cast<ElementType>()
            .FirstOrDefault(et => et.Category != null &&
                                  et.Category.Id.Value == (long)BuiltInCategory.OST_Viewports &&
                                  (et.Name.Contains("No Title", StringComparison.OrdinalIgnoreCase) ||
                                   et.Name.Contains("Tanpa Judul", StringComparison.OrdinalIgnoreCase) ||
                                   et.Name.Contains("None", StringComparison.OrdinalIgnoreCase)));

        List<object> processed = new();
        List<string> errors = new();

        foreach (var item in items)
        {
            try
            {
                if (!File.Exists(item.DwgPath))
                {
                    errors.Add($"File not found: {item.DwgPath} for view '{item.ViewName}'");
                    continue;
                }

                ViewSheet? sheet = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .FirstOrDefault(s => s.SheetNumber.Equals(item.SheetNumber, StringComparison.OrdinalIgnoreCase));

                if (sheet == null)
                {
                    errors.Add($"Sheet '{item.SheetNumber}' not found for view '{item.ViewName}'");
                    continue;
                }

                using Transaction tx = new Transaction(doc, $"AIGRAV - Create View & Place {item.ViewName}");
                FailureHandlingOptions fOpts = tx.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                tx.SetFailureHandlingOptions(fOpts);
                tx.Start();

                ViewDrafting? dv = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewDrafting))
                    .Cast<ViewDrafting>()
                    .FirstOrDefault(v => v.Name.Equals(item.ViewName, StringComparison.OrdinalIgnoreCase));

                if (dv == null)
                {
                    dv = ViewDrafting.Create(doc, draftingVft.Id);
                    dv.Name = item.ViewName;
                }
                dv.Scale = item.Scale;

                if (!string.IsNullOrWhiteSpace(item.TitleOnSheet))
                {
                    Parameter? pDesc = dv.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
                    pDesc?.Set(item.TitleOnSheet);
                }

                var oldImports = new FilteredElementCollector(doc, dv.Id)
                    .OfClass(typeof(ImportInstance))
                    .ToElementIds();
                if (oldImports.Count > 0)
                {
                    doc.Delete(oldImports);
                }

                DWGImportOptions dwgOpts = new DWGImportOptions
                {
                    ThisViewOnly = true,
                    Unit = ImportUnit.Millimeter,
                    ColorMode = ImportColorMode.BlackAndWhite,
                    Placement = ImportPlacement.Origin
                };

                ElementId impId;
                bool impSuccess = doc.Import(item.DwgPath, dwgOpts, dv, out impId);

                if (item.WidthMm > 0 && item.HeightMm > 0)
                {
                    double halfWFt = (item.WidthMm * 0.5 / 304.8) * item.Scale;
                    double halfHFt = (item.HeightMm * 0.5 / 304.8) * item.Scale;

                    dv.CropBoxActive = true;
                    dv.CropBoxVisible = false;
                    BoundingBoxXYZ cb = dv.CropBox;
                    cb.Min = new XYZ(-halfWFt, -halfHFt, cb.Min.Z);
                    cb.Max = new XYZ(halfWFt, halfHFt, cb.Max.Z);
                    dv.CropBox = cb;
                }

                doc.Regenerate();

                Element? tb = new FilteredElementCollector(doc, sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault();

                XYZ centerPoint;
                if (tb != null)
                {
                    BoundingBoxXYZ? bb = tb.get_BoundingBox(sheet) ?? tb.get_BoundingBox(null);
                    if (bb != null)
                    {
                        double xFt = bb.Min.X + (item.PaperXMm / 304.8);
                        double yFt = bb.Min.Y + (item.PaperYMm / 304.8);
                        centerPoint = new XYZ(xFt, yFt, 0);
                    }
                    else
                    {
                        centerPoint = new XYZ(item.PaperXMm / 304.8, item.PaperYMm / 304.8, 0);
                    }
                }
                else
                {
                    centerPoint = new XYZ(item.PaperXMm / 304.8, item.PaperYMm / 304.8, 0);
                }

                Viewport? vp = null;
                var allVpsForView = new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Where(v => v.ViewId == dv.Id)
                    .ToList();

                var existingVpOnSheet = allVpsForView.FirstOrDefault(v => v.SheetId == sheet.Id);
                if (existingVpOnSheet != null)
                {
                    existingVpOnSheet.SetBoxCenter(centerPoint);
                    vp = existingVpOnSheet;
                }
                else
                {
                    foreach (var oldVp in allVpsForView)
                    {
                        try { doc.Delete(oldVp.Id); } catch {}
                    }
                    if (Viewport.CanAddViewToSheet(doc, sheet.Id, dv.Id))
                    {
                        vp = Viewport.Create(doc, sheet.Id, dv.Id, centerPoint);
                    }
                    else
                    {
                        errors.Add($"View '{item.ViewName}' cannot be added to sheet '{item.SheetNumber}'.");
                    }
                }

                if (vp != null && noTitleType != null)
                {
                    try { vp.ChangeTypeId(noTitleType.Id); } catch {}
                }

                tx.Commit();

                processed.Add(new
                {
                    view_name = item.ViewName,
                    title_on_sheet = item.TitleOnSheet,
                    view_id = dv.Id.Value,
                    sheet_number = item.SheetNumber,
                    scale = item.Scale,
                    viewport_id = vp?.Id.Value ?? -1,
                    imported = impSuccess,
                    center_coords_ft = new { x = centerPoint.X, y = centerPoint.Y }
                });
            }
            catch (Exception ex)
            {
                errors.Add($"Failed processing view '{item.ViewName}': {ex.Message}");
            }
        }

        return JsonSerializer.Serialize(new
        {
            status = errors.Count == 0 ? "success" : (processed.Count > 0 ? "partial_success" : "failed"),
            total_processed = processed.Count,
            views = processed,
            errors = errors
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string ExportComparisonSheets(Document doc)
    {
        var sheetNumbers = new[] { "JL-048", "JL-049", "JL-050", "JL-051" };
        var exported = new List<string>();

        foreach (var num in sheetNumbers)
        {
            ViewSheet? s = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .FirstOrDefault(x => x.SheetNumber.Equals(num, StringComparison.OrdinalIgnoreCase));
            if (s == null) continue;

            ImageExportOptions opts = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = Path.Combine(@"C:\AIGRAV\Temp", $"revit_sheet_{num}"),
                FitDirection = FitDirectionType.Horizontal,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ImageResolution = ImageResolution.DPI_300,
                PixelSize = 2481,
                ZoomType = ZoomFitType.FitToPage
            };
            opts.SetViewsAndSheets(new List<ElementId> { s.Id });
            doc.ExportImage(opts);

            var matching = Directory.GetFiles(@"C:\AIGRAV\Temp", $"revit_sheet_{num}*.png");
            exported.AddRange(matching);
        }

        return JsonSerializer.Serialize(new { status = "success", exported });
    }
}

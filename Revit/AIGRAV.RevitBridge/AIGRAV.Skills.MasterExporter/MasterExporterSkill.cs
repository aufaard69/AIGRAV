using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.MasterExporter
{
    public class MasterExporterSkill : IAIGRAVSkill
    {
        private readonly AIGRAVSkillInfo _info;

        public MasterExporterSkill()
        {
            _info = new AIGRAVSkillInfo();
            SetProp(_info, "Name", "Master Exporter Engine");
            SetProp(_info, "Version", "1.1.0");
            SetProp(_info, "Category", "Export");
            SetProp(_info, "Description", "Comprehensive Sheet, CAD, Vector PDF, and Standardized QTO Schedule Exporter.");
            SetProp(_info, "Priority", 2500);
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

        private static string NormalizeToolName(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return "";
            string t = toolName.Trim();
            if (t.StartsWith("aigrav_", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring("aigrav_".Length);
            }
            return t;
        }

        public AIGRAVSkillInfo Info { get { return _info; } }

        public bool CanHandle(string toolName)
        {
            string t = NormalizeToolName(toolName);
            return t.Equals("get_all_sheets_meta", StringComparison.OrdinalIgnoreCase)
                || t.Equals("export_all_schedules", StringComparison.OrdinalIgnoreCase)
                || t.Equals("export_single_sheet", StringComparison.OrdinalIgnoreCase)
                || t.Equals("export_all_deliverables", StringComparison.OrdinalIgnoreCase)
                || t.Equals("apply_renumbering_plan", StringComparison.OrdinalIgnoreCase)
                || t.Equals("save_active_doc", StringComparison.OrdinalIgnoreCase);
        }

        public string Execute(string toolName, UIApplication app, string argument)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                return "{\"error\": \"No active Revit document.\"}";
            }
            Document doc = uidoc.Document;
            string t = NormalizeToolName(toolName);

            if (t.Equals("get_all_sheets_meta", StringComparison.OrdinalIgnoreCase))
            {
                return GetAllSheetsMeta(doc);
            }

            if (t.Equals("export_all_schedules", StringComparison.OrdinalIgnoreCase))
            {
                return ExportAllSchedules(doc, argument);
            }

            if (t.Equals("export_single_sheet", StringComparison.OrdinalIgnoreCase))
            {
                return ExportSingleSheet(doc, argument);
            }

            if (t.Equals("export_all_deliverables", StringComparison.OrdinalIgnoreCase))
            {
                return ExportAllDeliverables(doc, argument);
            }

            if (t.Equals("apply_renumbering_plan", StringComparison.OrdinalIgnoreCase))
            {
                return ApplyRenumberingPlan(doc, argument);
            }

            if (t.Equals("save_active_doc", StringComparison.OrdinalIgnoreCase))
            {
                return SaveActiveDoc(doc);
            }

            return "{\"error\": \"Unknown tool: " + toolName + "\"}";
        }

        private string GetAllSheetsMeta(Document doc)
        {
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .ToList();

            var list = new List<Dictionary<string, object>>();
            foreach (var s in sheets)
            {
                string noVal = "";
                Parameter pNo = s.LookupParameter("NO");
                if (pNo != null && pNo.HasValue)
                {
                    noVal = pNo.AsString() ?? "";
                }

                list.Add(new Dictionary<string, object>
                {
                    { "id", s.Id.Value },
                    { "number", s.SheetNumber },
                    { "name", s.Name },
                    { "no", noVal }
                });
            }

            return JsonSerializer.Serialize(list);
        }

        private string ExportAllSchedules(Document doc, string outDir)
        {
            if (string.IsNullOrWhiteSpace(outDir))
            {
                outDir = Path.Combine(Path.GetTempPath(), "AIGRAV", "Exports", "schedules_tsv");
            }
            Directory.CreateDirectory(outDir);

            var schedules = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTemplate
                         && !s.IsInternalKeynoteSchedule
                         && !s.IsTitleblockRevisionSchedule
                         && !s.Name.Equals("Keynote Legend", StringComparison.OrdinalIgnoreCase)
                         && !s.Name.Equals("DAFTAR GAMBAR", StringComparison.OrdinalIgnoreCase)
                         && !s.Name.StartsWith("<", StringComparison.OrdinalIgnoreCase))
                .ToList();

            ViewScheduleExportOptions opt = new ViewScheduleExportOptions();
            opt.HeadersFootersBlanks = true;
            opt.FieldDelimiter = "\t";
            opt.TextQualifier = ExportTextQualifier.DoubleQuote;
            opt.Title = true;

            int count = 0;
            var exported = new List<string>();
            var failed = new List<string>();
            foreach (var sched in schedules)
            {
                try
                {
                    string cleanName = string.Join("_", sched.Name.Split(Path.GetInvalidFileNameChars()));
                    string fileName = cleanName + ".txt";
                    sched.Export(outDir, fileName, opt);
                    exported.Add(cleanName);
                    count++;
                }
                catch (Exception ex)
                {
                    failed.Add(sched.Name + ": " + ex.Message);
                }
            }

            return JsonSerializer.Serialize(new
            {
                total = count,
                total_failed = failed.Count,
                output_dir = outDir,
                schedules = exported,
                failed_items = failed
            });
        }

        private string ExportSingleSheet(Document doc, string argument)
        {
            // Argument: SheetNumber|OutDir
            string sheetNum = "";
            string outDir = Path.Combine(Path.GetTempPath(), "AIGRAV", "Exports", "single_sheet");

            if (!string.IsNullOrWhiteSpace(argument))
            {
                string[] parts = argument.Split('|');
                if (parts.Length > 0) sheetNum = parts[0].Trim();
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) outDir = parts[1].Trim();
            }

            var sheet = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .FirstOrDefault(s => s.SheetNumber.Equals(sheetNum, StringComparison.OrdinalIgnoreCase)
                                  || s.Id.Value.ToString() == sheetNum);

            if (sheet == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Sheet not found: " + sheetNum });
            }

            Directory.CreateDirectory(outDir);
            string pdfDir = Path.Combine(outDir, "PDF");
            string cadDir = Path.Combine(outDir, "CAD");
            Directory.CreateDirectory(pdfDir);
            Directory.CreateDirectory(cadDir);

            bool pdfOk = false;
            string pdfErr = "";
            try
            {
                PDFExportOptions pdfOpts = new PDFExportOptions();
                pdfOpts.FileName = sheet.SheetNumber;
                pdfOpts.Combine = false;
                pdfOpts.ZoomType = ZoomType.Zoom;
                pdfOpts.ZoomPercentage = 100;

                try
                {
                    PropertyInfo pColor = typeof(PDFExportOptions).GetProperty("ColorDepth");
                    if (pColor != null)
                    {
                        Type colorEnumType = typeof(ColorDepthType);
                        object val = Enum.Parse(colorEnumType, "Color");
                        pColor.SetValue(pdfOpts, val, null);
                    }
                }
                catch { }

                IList<ElementId> vIds = new List<ElementId> { sheet.Id };
                pdfOk = doc.Export(pdfDir, vIds, pdfOpts);
            }
            catch (Exception ex) { pdfErr = ex.Message; }

            bool dwgOk = false;
            string dwgErr = "";
            string baseFileName = doc.Title + "-Sheet - " + sheet.SheetNumber + " - " + sheet.Name;
            try
            {
                DWGExportOptions dwgOpts = new DWGExportOptions();
                dwgOpts.ExportingAreas = false;
                dwgOpts.MergedViews = true;

                IList<string> setups = BaseExportOptions.GetPredefinedSetupNames(doc);
                if (setups.Count > 0)
                {
                    try { dwgOpts = DWGExportOptions.GetPredefinedOptions(doc, setups[0]); dwgOpts.MergedViews = true; } catch { }
                }

                ICollection<ElementId> vSet = new List<ElementId> { sheet.Id };
                dwgOk = doc.Export(cadDir, baseFileName, vSet, dwgOpts);
            }
            catch (Exception ex) { dwgErr = ex.Message; }

            return JsonSerializer.Serialize(new
            {
                success = pdfOk || dwgOk,
                sheet_number = sheet.SheetNumber,
                sheet_name = sheet.Name,
                pdf_ok = pdfOk,
                pdf_err = pdfErr,
                dwg_ok = dwgOk,
                dwg_err = dwgErr,
                dwg_file = baseFileName + ".dwg"
            });
        }

        private static int GetSheetOrderScore(ViewSheet s)
        {
            string num = (s.SheetNumber ?? "").Trim().ToUpperInvariant();
            string name = (s.Name ?? "").Trim().ToUpperInvariant();

            if (num == "000" || name.Contains("COVER")) return 0;
            if (num.StartsWith("DG") || name.Contains("DAFTAR GAMBAR")) return 1;

            var p = s.LookupParameter("NO");
            if (p != null && p.HasValue)
            {
                int val;
                if (int.TryParse(p.AsString(), out val))
                {
                    return val + 10;
                }
            }
            return 999;
        }

        private string ExportAllDeliverables(Document doc, string argument)
        {
            string logDir = Path.Combine(Path.GetTempPath(), "AIGRAV");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "export_debug.log");
            File.AppendAllText(logFile, "=== ENTERED ExportAllDeliverables: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\n");

            // Argument: pdfFilePath|cadDirectory
            string baseDocDir = !string.IsNullOrWhiteSpace(doc.PathName)
                ? Path.GetDirectoryName(doc.PathName)
                : Path.Combine(logDir, "Exports");
            string docTitleSafe = string.IsNullOrWhiteSpace(doc.Title) ? "AIGRAV_Model" : doc.Title;
            string pdfFilePath = Path.Combine(baseDocDir, docTitleSafe + ".pdf");
            string cadDir = Path.Combine(baseDocDir, "CAD");

            if (!string.IsNullOrWhiteSpace(argument))
            {
                string[] parts = argument.Split('|');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) pdfFilePath = parts[0].Trim();
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) cadDir = parts[1].Trim();
            }

            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .ToList();

            // Sort sheets: COVER (Page 1), DAFTAR GAMBAR (Page 2), followed by NO (1..N), then SheetNumber
            sheets.Sort((a, b) =>
            {
                int scoreA = GetSheetOrderScore(a);
                int scoreB = GetSheetOrderScore(b);
                if (scoreA != scoreB) return scoreA.CompareTo(scoreB);
                return string.Compare(a.SheetNumber, b.SheetNumber, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < sheets.Count; i++)
            {
                File.AppendAllText(logFile, string.Format("Sheet [{0}]: {1} - {2}\n", i + 1, sheets[i].SheetNumber, sheets[i].Name));
            }

            string pdfDir = Path.GetDirectoryName(pdfFilePath);
            Directory.CreateDirectory(pdfDir);
            Directory.CreateDirectory(cadDir);

            // 1. Export Combined PDF
            bool pdfOk = false;
            string pdfErr = "";
            try
            {
                PDFExportOptions pdfOpts = new PDFExportOptions();
                pdfOpts.FileName = Path.GetFileNameWithoutExtension(pdfFilePath);
                pdfOpts.Combine = true;
                pdfOpts.ZoomType = ZoomType.Zoom;
                pdfOpts.ZoomPercentage = 100;

                try
                {
                    PropertyInfo pColor = typeof(PDFExportOptions).GetProperty("ColorDepth");
                    if (pColor != null)
                    {
                        Type colorEnumType = typeof(ColorDepthType);
                        object val = Enum.Parse(colorEnumType, "Color");
                        pColor.SetValue(pdfOpts, val, null);
                    }
                }
                catch { }

                IList<ElementId> vIds = sheets.Select(s => s.Id).ToList();
                File.AppendAllText(logFile, "Starting PDF doc.Export with " + vIds.Count + " sheets to " + pdfDir + "...\n");
                pdfOk = doc.Export(pdfDir, vIds, pdfOpts);
                File.AppendAllText(logFile, "PDF Export finished: ok=" + pdfOk + "\n");
            }
            catch (Exception ex)
            {
                pdfErr = ex.Message;
                File.AppendAllText(logFile, "PDF Export EXCEPTION: " + ex.ToString() + "\n");
            }

            // 2. Export CAD (DWG) for all sheets
            bool cadOk = false;
            string cadErr = "";
            int cadCount = 0;
            try
            {
                DWGExportOptions dwgOpts = new DWGExportOptions();
                dwgOpts.ExportingAreas = false;
                dwgOpts.MergedViews = true;

                IList<string> setups = BaseExportOptions.GetPredefinedSetupNames(doc);
                if (setups.Count > 0)
                {
                    try { dwgOpts = DWGExportOptions.GetPredefinedOptions(doc, setups[0]); dwgOpts.MergedViews = true; } catch { }
                }

                ICollection<ElementId> vSet = sheets.Select(s => s.Id).ToList();
                File.AppendAllText(logFile, "Starting CAD doc.Export with " + vSet.Count + " sheets to " + cadDir + "...\n");
                cadOk = doc.Export(cadDir, doc.Title, vSet, dwgOpts);
                File.AppendAllText(logFile, "CAD Export finished: ok=" + cadOk + "\n");
                if (cadOk)
                {
                    cadCount = sheets.Count;
                }
            }
            catch (Exception ex)
            {
                cadErr = ex.Message;
                File.AppendAllText(logFile, "CAD Export EXCEPTION: " + ex.ToString() + "\n");
            }

            return JsonSerializer.Serialize(new
            {
                success = pdfOk || cadOk,
                total_sheets = sheets.Count,
                pdf_ok = pdfOk,
                pdf_path = pdfFilePath,
                pdf_err = pdfErr,
                cad_ok = cadOk,
                cad_dir = cadDir,
                cad_err = cadErr,
                cad_count = cadCount
            });
        }

        private class RenumberPlanItem
        {
            public long id { get; set; }
            public string old_num { get; set; }
            public string new_num { get; set; }
            public string old_no { get; set; }
            public string new_no { get; set; }
            public string name { get; set; }
        }

        private string ApplyRenumberingPlan(Document doc, string argument)
        {
            try
            {
                string jsonText = argument;
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    string trimmed = argument.Trim('\"', '\'', ' ', '\t', '\r', '\n');
                    if (File.Exists(trimmed))
                    {
                        jsonText = File.ReadAllText(trimmed);
                    }
                    else if (argument.Trim().StartsWith("{") && argument.Contains("plan_file"))
                    {
                        using (var docJson = JsonDocument.Parse(argument.Trim()))
                        {
                            JsonElement pf;
                            if (docJson.RootElement.TryGetProperty("plan_file", out pf))
                            {
                                string path = pf.GetString();
                                if (!string.IsNullOrEmpty(path))
                                {
                                    path = path.Trim('\"', '\'', ' ');
                                    if (File.Exists(path)) jsonText = File.ReadAllText(path);
                                }
                            }
                        }
                    }
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var items = JsonSerializer.Deserialize<List<RenumberPlanItem>>(jsonText, options);
                if (items == null || items.Count == 0)
                {
                    return JsonSerializer.Serialize(new { success = false, error = "Plan is empty or invalid JSON." });
                }

                int countRenamedNum = 0;
                int countUpdatedNo = 0;
                var errors = new List<string>();

                using (TransactionGroup tg = new TransactionGroup(doc, "AIGRAV - Renumber Sheets and Suffixes"))
                {
                    tg.Start();

                    // Phase 1: Temporary rename all sheets where old_num != new_num
                    using (Transaction t1 = new Transaction(doc, "Phase 1: Temp Rename"))
                    {
                        t1.Start();
                        foreach (var item in items)
                        {
                            if (!string.Equals(item.old_num, item.new_num, StringComparison.OrdinalIgnoreCase))
                            {
                                ElementId elId = new ElementId(item.id);
                                ViewSheet vs = doc.GetElement(elId) as ViewSheet;
                                if (vs != null)
                                {
                                    try
                                    {
                                        vs.SheetNumber = "_TMP_" + vs.Id.Value.ToString();
                                    }
                                    catch (Exception ex)
                                    {
                                        errors.Add("Phase 1 rename failed for ID " + item.id + " (" + item.old_num + "): " + ex.Message);
                                    }
                                }
                                else
                                {
                                    errors.Add("Sheet not found in Phase 1 for ID: " + item.id);
                                }
                            }
                        }
                        t1.Commit();
                    }

                    // Phase 2: Set final SheetNumber and parameter NO
                    using (Transaction t2 = new Transaction(doc, "Phase 2: Final Renumber"))
                    {
                        t2.Start();
                        foreach (var item in items)
                        {
                            ElementId elId = new ElementId(item.id);
                            ViewSheet vs = doc.GetElement(elId) as ViewSheet;
                            if (vs == null)
                            {
                                errors.Add("Sheet not found in Phase 2 for ID: " + item.id);
                                continue;
                            }

                            if (!string.Equals(vs.SheetNumber, item.new_num, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    vs.SheetNumber = item.new_num;
                                    countRenamedNum++;
                                }
                                catch (Exception ex)
                                {
                                    errors.Add("Phase 2 SheetNumber failed for ID " + item.id + " -> " + item.new_num + ": " + ex.Message);
                                }
                            }

                            try
                            {
                                Parameter pNo = vs.LookupParameter("NO");
                                if (pNo != null && !pNo.IsReadOnly)
                                {
                                    string currentVal = pNo.AsString() ?? "";
                                    if (currentVal != item.new_no)
                                    {
                                        pNo.Set(item.new_no);
                                        countUpdatedNo++;
                                    }
                                }
                                else
                                {
                                    errors.Add("Parameter NO not found or read-only on sheet " + item.new_num + " (ID " + item.id + ")");
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add("Set parameter NO failed for ID " + item.id + ": " + ex.Message);
                            }
                        }
                        t2.Commit();
                    }

                    tg.Assimilate();
                }

                return JsonSerializer.Serialize(new
                {
                    success = errors.Count == 0,
                    total_items = items.Count,
                    sheet_numbers_changed = countRenamedNum,
                    no_params_changed = countUpdatedNo,
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.ToString()
                });
            }
        }

        private string SaveActiveDoc(Document doc)
        {
            try
            {
                if (doc.IsWorkshared)
                {
                    TransactWithCentralOptions twcOpts = new TransactWithCentralOptions();
                    SynchronizeWithCentralOptions swcOpts = new SynchronizeWithCentralOptions();
                    doc.SynchronizeWithCentral(twcOpts, swcOpts);
                    return JsonSerializer.Serialize(new { success = true, mode = "synchronized", path = doc.PathName });
                }
                else
                {
                    SaveOptions sOpts = new SaveOptions();
                    sOpts.Compact = false;
                    doc.Save(sOpts);
                    return JsonSerializer.Serialize(new { success = true, mode = "saved", path = doc.PathName });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }
    }
}
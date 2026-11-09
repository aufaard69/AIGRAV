using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.PlateStandardizer
{
    public class PlateScheduleStandardizerSkill : IAIGRAVSkill
    {
        private readonly AIGRAVSkillInfo _info;

        public PlateScheduleStandardizerSkill()
        {
            _info = new AIGRAVSkillInfo();
            SetProp(_info, "Name", "Plate QTO Standardizer");
            SetProp(_info, "Version", "2.0.0");
            SetProp(_info, "Category", "Quantity Takeoff");
            SetProp(_info, "Description", "Standardizes Plate QTO schedules to 5 columns with thickness sorting and subtotal footers");
            SetProp(_info, "Priority", 1850);
            SetProp(_info, "Source", "External");
            _info.IsEnabled = true;
        }

        private static void SetProp(object target, string propName, object val)
        {
            try
            {
                PropertyInfo p = target.GetType().GetProperty(propName);
                if (p != null && p.CanWrite)
                {
                    p.SetValue(target, val, null);
                    return;
                }
                FieldInfo f = target.GetType().GetField("<" + propName + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null)
                {
                    f.SetValue(target, val);
                }
            }
            catch { }
        }

        public AIGRAVSkillInfo Info
        {
            get { return _info; }
        }

        public bool CanHandle(string toolName)
        {
            return string.Equals(toolName, "aigrav_standardize_plate_schedules", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "standardize_plate_schedules", StringComparison.OrdinalIgnoreCase);
        }

        public string Execute(string toolName, UIApplication app, string argument)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null) return "Error: No active UIDocument.";
            Document doc = uidoc.Document;
            if (doc == null) return "Error: No active Document.";

            var allScheds = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)
                .ToList();

            ElementId plateCatId = new ElementId(BuiltInCategory.OST_StructConnectionPlates);

            List<ViewSchedule> targetScheds = new List<ViewSchedule>();

            if (!string.IsNullOrEmpty(argument) && !string.Equals(argument.Trim(), "all", StringComparison.OrdinalIgnoreCase) && !string.Equals(argument.Trim(), "{}", StringComparison.OrdinalIgnoreCase))
            {
                string targetArg = argument.Trim();
                long targetId;
                if (long.TryParse(targetArg, out targetId))
                {
                    ViewSchedule sById = doc.GetElement(new ElementId(targetId)) as ViewSchedule;
                    if (sById != null) targetScheds.Add(sById);
                }
                else
                {
                    ViewSchedule sByName = allScheds.FirstOrDefault(s => s.Name.IndexOf(targetArg, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (sByName != null) targetScheds.Add(sByName);
                }
            }

            if (targetScheds.Count == 0)
            {
                targetScheds = allScheds.Where(s =>
                {
                    if (s.Definition.CategoryId != plateCatId) return false;
                    if (s.Name.IndexOf("Baseplate", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    return true;
                }).OrderBy(s => s.Name).ToList();
            }

            if (targetScheds.Count == 0)
            {
                return "No matching Plate schedules found to standardize.";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== AIGRAV PLATE QTO SCHEDULE STANDARDIZATION ===");
            sb.AppendLine("Standard Specification:");
            sb.AppendLine("  Columns (5): Lokasi (A) | Tipe Sambungan (B) | Thickness (mm) (C) | Qty (D) | Berat (Kg) (E)");
            sb.AppendLine("  Sort 1: Type of Connection (Ascending, Header=No, Footer=No)");
            sb.AppendLine("  Sort 2: Thickness (Ascending, Header=No, Footer=Yes - Subtotal Qty & Berat)");
            sb.AppendLine("  Grand Total: Title='Total', Count=Yes, Totals=Yes, Itemize=No");
            sb.AppendLine(string.Format("Target Schedules to Standardize: {0}", targetScheds.Count));
            sb.AppendLine("--------------------------------------------------------------------------------");

            int updatedCount = 0;
            int failedCount = 0;

            using (Transaction tx = new Transaction(doc, "AIGRAV - Standardize Plate Schedules"))
            {
                tx.Start();

                foreach (ViewSchedule s in targetScheds)
                {
                    try
                    {
                        ScheduleDefinition def = s.Definition;

                        // 1. Lokasi
                        ScheduleField fLok = GetOrAddField(def, doc, "Lokasi");
                        if (fLok != null)
                        {
                            fLok.ColumnHeading = "Lokasi";
                            fLok.IsHidden = false;
                        }

                        // 2. Type of Connection
                        ScheduleField fType = GetOrAddField(def, doc, "Type of Connection");
                        if (fType == null) fType = GetOrAddField(def, doc, "Tipe Sambungan");
                        if (fType != null)
                        {
                            fType.ColumnHeading = "Tipe Sambungan";
                            fType.IsHidden = false;
                        }

                        // 3. Thickness (mm)
                        ScheduleField fThk = GetOrAddField(def, doc, "Thickness");
                        if (fThk == null) fThk = GetOrAddField(def, doc, "Thickness (mm)");
                        if (fThk != null)
                        {
                            fThk.ColumnHeading = "Thickness (mm)";
                            fThk.IsHidden = false;
                        }

                        // 4. Qty
                        ScheduleField fCount = GetOrAddField(def, doc, "Count");
                        if (fCount == null) fCount = GetOrAddField(def, doc, "Qty");
                        if (fCount != null)
                        {
                            fCount.ColumnHeading = "Qty";
                            fCount.IsHidden = false;
                            fCount.DisplayType = ScheduleFieldDisplayType.Totals;
                        }

                        // 5. Berat (Kg)
                        ScheduleField fWeight = GetOrAddField(def, doc, "Weight");
                        if (fWeight == null) fWeight = GetOrAddField(def, doc, "Berat");
                        if (fWeight != null)
                        {
                            fWeight.ColumnHeading = "Berat (Kg)";
                            fWeight.IsHidden = false;
                            fWeight.DisplayType = ScheduleFieldDisplayType.Totals;
                        }

                        // Hide all other fields
                        for (int i = 0; i < def.GetFieldCount(); i++)
                        {
                            ScheduleField f = def.GetField(i);
                            if (fLok != null && f.FieldId == fLok.FieldId) continue;
                            if (fType != null && f.FieldId == fType.FieldId) continue;
                            if (fThk != null && f.FieldId == fThk.FieldId) continue;
                            if (fCount != null && f.FieldId == fCount.FieldId) continue;
                            if (fWeight != null && f.FieldId == fWeight.FieldId) continue;
                            f.IsHidden = true;
                        }

                        // Reorder fields: [Lokasi, Tipe Sambungan, Thickness, Qty, Berat]
                        List<ScheduleField> visibleList = new List<ScheduleField>();
                        if (fLok != null) visibleList.Add(fLok);
                        if (fType != null) visibleList.Add(fType);
                        if (fThk != null) visibleList.Add(fThk);
                        if (fCount != null) visibleList.Add(fCount);
                        if (fWeight != null) visibleList.Add(fWeight);
                        ReorderVisibleFields(def, visibleList);

                        // Sorting
                        while (def.GetSortGroupFieldCount() > 0)
                        {
                            def.RemoveSortGroupField(0);
                        }

                        if (fType != null)
                        {
                            ScheduleSortGroupField sg0 = new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending);
                            sg0.ShowHeader = false;
                            sg0.ShowFooter = false;
                            def.AddSortGroupField(sg0);
                        }

                        if (fThk != null)
                        {
                            ScheduleSortGroupField sg1 = new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending);
                            sg1.ShowHeader = false;
                            sg1.ShowFooter = true;
                            def.AddSortGroupField(sg1);
                        }

                        def.IsItemized = false;
                        def.ShowGrandTotal = true;
                        def.ShowGrandTotalTitle = true;
                        def.ShowGrandTotalCount = true;
                        def.GrandTotalTitle = "Total";

                        updatedCount++;
                        sb.AppendLine(string.Format("  ✓ [{0}/{1}] Id: {2} | '{3}' -> FORMATTED (5 Kolom + Sort Thickness Footer)", 
                            updatedCount, targetScheds.Count, s.Id.Value, s.Name));
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        sb.AppendLine(string.Format("  ✗ [FAIL] Id: {0} | '{1}' -> Error: {2}", 
                            s.Id.Value, s.Name, ex.Message));
                    }
                }

                doc.Regenerate();
                tx.Commit();
            }

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("RESULT: {0} Sukses, {1} Gagal (Total {2} Schedules)", 
                updatedCount, failedCount, targetScheds.Count));
            sb.AppendLine("Status: SEMUA JADWAL PELAT BERHASIL DISTANDARDISASI DENGAN 5 KOLOM & SORT THICKNESS");

            return sb.ToString();
        }

        private static ScheduleField GetOrAddField(ScheduleDefinition d, Document doc, string name)
        {
            for (int i = 0; i < d.GetFieldCount(); i++)
            {
                ScheduleField f = d.GetField(i);
                if (f.GetName().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return f;
            }
            SchedulableField sf = d.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            return sf != null ? d.AddField(sf) : null;
        }

        private static void ReorderVisibleFields(ScheduleDefinition d, List<ScheduleField> visibleFields)
        {
            IList<ScheduleFieldId> allFields = d.GetFieldOrder();
            List<ScheduleFieldId> newOrder = new List<ScheduleFieldId>();

            foreach (ScheduleField vf in visibleFields)
            {
                if (vf != null)
                {
                    ScheduleFieldId matchingFid = allFields.FirstOrDefault(fid => fid == vf.FieldId);
                    if (matchingFid != null && !newOrder.Any(f => f == matchingFid))
                    {
                        newOrder.Add(matchingFid);
                    }
                }
            }

            foreach (ScheduleFieldId fid in allFields)
            {
                if (!newOrder.Any(f => f == fid))
                {
                    newOrder.Add(fid);
                }
            }

            d.SetFieldOrder(newOrder);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.QTO
{
    public class SchedTarget
    {
        public string ScheduleName { get; set; }
        public string Lokasi { get; set; }
        public string ConnPattern { get; set; }
        public SchedTarget(string s, string l, string c)
        {
            ScheduleName = s;
            Lokasi = l;
            ConnPattern = c;
        }
    }

    /// <summary>
    /// Skill for automating Voute (Haunch) & Steel Connection Profile QTO schedules
    /// using OST_StructConnectionProfiles with solid volume calibration.
    /// </summary>
    public class VouteProfileQTOSkill : IAIGRAVSkill
    {
        public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
        {
            Name = "Voute & Connection Profile QTO",
            Version = "1.0.0",
            Category = "QuantityTakeoff",
            Description = "Automates QTO schedules for voutes/haunches embedded in steel connections with exact volume calibration",
            Priority = 1900,
            Source = "External",
            IsEnabled = true
        };

        public bool CanHandle(string toolName)
        {
            return string.Equals(toolName, "aigrav_generate_voute_schedules", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "aigrav_audit_voute_schedules", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "aigrav_unify_wf300_profile_types", StringComparison.OrdinalIgnoreCase);
        }

        public string Execute(string toolName, UIApplication app, string argument)
        {
            Document doc = app.ActiveUIDocument.Document;
            if (doc == null) return "Error: No active document found.";

            if (string.Equals(toolName, "aigrav_unify_wf300_profile_types", StringComparison.OrdinalIgnoreCase))
            {
                return UnifyWF300ProfileTypes(doc);
            }

            if (string.Equals(toolName, "aigrav_generate_voute_schedules", StringComparison.OrdinalIgnoreCase))
            {
                UnifyWF300ProfileTypes(doc);
                return Generate16VouteSchedules(doc);
            }

            if (string.Equals(toolName, "aigrav_audit_voute_schedules", StringComparison.OrdinalIgnoreCase))
            {
                return AuditVouteSchedules(doc);
            }

            return "Unknown operation: " + toolName;
        }

        public static string UnifyWF300ProfileTypes(Document doc)
        {
            int updatedCount = 0;
            var conns = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructConnections)
                .WhereElementIsNotElementType()
                .ToList();

            ElementId pidProfileType = new ElementId(-1155146);

            using (Transaction t = new Transaction(doc, "Unify WF 300 Profile Types"))
            {
                t.Start();
                foreach (Element c in conns)
                {
                    try
                    {
                        var mSub = c.GetType().GetMethod("GetSubelements");
                        if (mSub == null) continue;
                        var subs = mSub.Invoke(c, null) as IList<Subelement>;
                        if (subs == null) continue;

                        foreach (var s in subs)
                        {
                            if (s.Category != null && s.Category.Name == "Profiles")
                            {
                                ParameterValue pv = s.GetParameterValue(pidProfileType);
                                StringParameterValue spv = pv as StringParameterValue;
                                if (spv != null && spv.Value != null && spv.Value.IndexOf("KALIBRASI VOLUME", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    s.SetParameterValue(pidProfileType, new StringParameterValue("WF 300"));
                                    updatedCount++;
                                }
                            }
                        }
                    }
                    catch { }
                }
                t.Commit();
            }

            return $"Updated {updatedCount} profile instances to standard 'WF 300'.";
        }

        public static string Generate16VouteSchedules(Document doc)
        {
            SchedTarget[] targets = new SchedTarget[]
            {
                // Waterline (5)
                new SchedTarget("C.3.1.22 Waterline - Voute & CT WF 350", "Waterline", "WF350-Endplate"),
                new SchedTarget("C.3.1.23 Waterline - Voute & CT WF 300", "Waterline", "WF300-Endplate"),
                new SchedTarget("C.3.1.24 Waterline - Voute & CT WF 200", "Waterline", "WF200-Endplate"),
                new SchedTarget("C.3.1.25 Waterline - Voute & CT WF 400", "Waterline", "WF-400 Endplate"),
                new SchedTarget("C.3.1.26 Waterline - Voute & CT WF 588", "Waterline", "WF588-Endplate"),

                // Carton & Cups Storage (4)
                new SchedTarget("C.3.3.20 Carton & Cups Storage - Voute & CT WF 350", "Carton & Cups Storage", "WF350-Endplate"),
                new SchedTarget("C.3.3.21 Carton & Cups Storage - Voute & CT WF 300", "Carton & Cups Storage", "WF300-Endplate"),
                new SchedTarget("C.3.3.22 Carton & Cups Storage - Voute & CT WF 400", "Carton & Cups Storage", "WF-400 Endplate"),
                new SchedTarget("C.3.3.23 Carton & Cups Storage - Voute & CT WF 200", "Carton & Cups Storage", "WF200-Endplate"),

                // Power House (2)
                new SchedTarget("C.3.2.10 Power House - Voute & CT WF 350", "Power House", "WF350-Endplate"),
                new SchedTarget("C.3.2.11 Power House - Voute & CT WF 200", "Power House", "WF200-Endplate"),

                // Production Office (1)
                new SchedTarget("C.3.4.12 Production Office - Voute & CT WF 350", "Production Office", "WF350-Endplate"),

                // Chiller Area 1 (2)
                new SchedTarget("C.3.5.10 Chiller Area 1 - Voute & CT WF 350", "Chiller Area 1", "WF350-Endplate"),
                new SchedTarget("C.3.5.11 Chiller Area 1 - Voute & CT WF 300", "Chiller Area 1", "WF300-Endplate"),

                // Chiller Area 2 (2)
                new SchedTarget("C.3.6.10 Chiller Area 2 - Voute & CT WF 350", "Chiller Area 2", "WF350-Endplate"),
                new SchedTarget("C.3.6.11 Chiller Area 2 - Voute & CT WF 300", "Chiller Area 2", "WF300-Endplate")
            };

            ElementId catId = new ElementId((int)BuiltInCategory.OST_StructConnectionProfiles);
            int created = 0;

            using (Transaction t = new Transaction(doc, "Generate 16 Voute Schedules"))
            {
                t.Start();
                HashSet<string> targetNames = new HashSet<string>(targets.Select(x => x.ScheduleName), StringComparer.OrdinalIgnoreCase);

                // Safe cleanup of obsolete versions
                List<ElementId> toDelete = new List<ElementId>();
                foreach (Element e in new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).ToElements())
                {
                    try
                    {
                        if (targetNames.Contains(e.Name))
                        {
                            e.Name = e.Name + "_OLD_" + DateTime.Now.Ticks + "_" + e.Id.Value;
                            toDelete.Add(e.Id);
                        }
                    }
                    catch { }
                }

                foreach (ElementId id in toDelete)
                {
                    try { doc.Delete(id); } catch { }
                }

                foreach (SchedTarget item in targets)
                {
                    CreateProfileSchedule(doc, catId, item.ScheduleName, item.Lokasi, item.ConnPattern);
                    created++;
                }

                doc.Regenerate();
                t.Commit();
            }

            return $"Successfully generated {created} voute & connection profile schedules.";
        }

        private static ViewSchedule CreateProfileSchedule(Document doc, ElementId catId, string name, string lokasi, string connFilter)
        {
            ViewSchedule vs = ViewSchedule.CreateSchedule(doc, catId);
            vs.Name = name;
            ScheduleDefinition def = vs.Definition;

            ScheduleField fLok = AddField(doc, def, "Lokasi", "Lokasi");
            ScheduleField fConn = AddField(doc, def, "Type of Connection", "Tipe Sambungan");
            ScheduleField fProf = AddField(doc, def, "Profile Type", "Type");
            ScheduleField fCut = AddField(doc, def, "Cut Length", "Length (mm)");
            ScheduleField fVol = AddField(doc, def, "Volume", "Volume");
            ScheduleField fQty = AddField(doc, def, "Count", "Qty");
            ScheduleField fWt = AddField(doc, def, "Exact Weight", "Berat (Kg)");

            // Filters
            if (fLok != null && !string.IsNullOrEmpty(lokasi))
            {
                def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, lokasi));
            }

            if (fConn != null && !string.IsNullOrEmpty(connFilter))
            {
                def.AddFilter(new ScheduleFilter(fConn.FieldId, ScheduleFilterType.Contains, connFilter));
            }

            // Sorting & Grouping
            if (fProf != null)
            {
                def.AddSortGroupField(new ScheduleSortGroupField(fProf.FieldId, ScheduleSortOrder.Ascending));
            }
            if (fCut != null)
            {
                var sortCut = new ScheduleSortGroupField(fCut.FieldId, ScheduleSortOrder.Ascending);
                sortCut.ShowFooter = true;
                def.AddSortGroupField(sortCut);
            }

            def.IsItemized = false;
            def.ShowGrandTotal = true;
            def.ShowGrandTotalTitle = true;
            def.ShowGrandTotalCount = true;

            // Calculate totals
            if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fWt != null) fWt.DisplayType = ScheduleFieldDisplayType.Totals;

            return vs;
        }

        private static ScheduleField AddField(Document doc, ScheduleDefinition def, string fieldName, string heading)
        {
            foreach (SchedulableField sf in def.GetSchedulableFields())
            {
                if (sf.GetName(doc).Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    ScheduleField f = def.AddField(sf);
                    if (!string.IsNullOrEmpty(heading)) f.ColumnHeading = heading;
                    return f;
                }
            }
            return null;
        }

        public static string AuditVouteSchedules(Document doc)
        {
            var scheds = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>().ToList();
            int passed = 0;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (var vs in scheds.Where(s => s.Name.Contains("Voute & CT")))
            {
                var def = vs.Definition;
                if (def.CategoryId.Value == (int)BuiltInCategory.OST_StructConnectionProfiles &&
                    def.GetFieldCount() >= 7 &&
                    def.GetField(0).GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase))
                {
                    passed++;
                }
            }

            sb.AppendLine($"Audit Result: {passed} / 16 Voute schedules verified compliant with 7-column OST_StructConnectionProfiles standard.");
            return sb.ToString();
        }
    }
}

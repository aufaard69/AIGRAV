using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.QTO;

/// <summary>
/// Universal WBS Schedule Planner & Creator across Structure, Architecture, and Siteplan:
/// - aigrav_qto_plan_wbs_schedules (Read-Only): discovers model categories/Lokasi/Comments/Type of Connection and proposes a duplicate-free WBS plan.
/// - aigrav_qto_create_standard_schedule: creates Regular or MaterialTakeoff ViewSchedules enforcing Dual-Key filters, Column A=Lokasi, Number for Bolts/Anchors, Totals & GrandTotal.
/// </summary>
public sealed class QTOScheduleBuilderSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "QTO Universal WBS Schedule Builder",
        Version = "4.0.0",
        Category = "Quantity Takeoff",
        Description = "Project-agnostic WBS planner and clean Regular/MaterialTakeoff Revit ViewSchedule creator following QTO v4.0 Dual-Key standards.",
        Source = "External",
        Priority = 1940,
        IsEnabled = true
    };

    private sealed class CreateScheduleRequest
    {
        public string ScheduleName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsMaterialTakeoff { get; set; } = false;
        public string Template { get; set; } = "Generic";
        public string? LokasiFilter { get; set; }
        public string? CommentsFilter { get; set; }
        public string? TypeOfConnectionFilter { get; set; }
        public string? MaterialNameFilter { get; set; }
        public string? TypeContainsFilter { get; set; }
        public bool ReplaceExisting { get; set; } = false;
    }

    public bool CanHandle(string toolName) =>
        toolName.Equals("aigrav_qto_plan_wbs_schedules", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("qto_plan_wbs_schedules", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("aigrav_qto_create_standard_schedule", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("qto_create_standard_schedule", StringComparison.OrdinalIgnoreCase);

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null) return JsonSerializer.Serialize(new { error = "No active Revit document." });

        if (toolName.Contains("plan_wbs_schedules", StringComparison.OrdinalIgnoreCase))
            return PlanWbsSchedules(doc);

        return CreateStandardSchedule(doc, argument);
    }

    private static string PlanWbsSchedules(Document doc)
    {
        BuiltInCategory[] scanCats =
        {
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_StairsRailing,
            BuiltInCategory.OST_StairsRuns,
            BuiltInCategory.OST_Rebar,
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_StructConnectionPlates,
            BuiltInCategory.OST_StructConnectionBolts,
            BuiltInCategory.OST_StructConnectionAnchors,
            BuiltInCategory.OST_StructConnectionProfiles
        };

        List<object> discoveredBuckets = new();
        HashSet<string> uniqueLokasis = new(StringComparer.OrdinalIgnoreCase);
        int seq = 1;

        foreach (BuiltInCategory bic in scanCats)
        {
            var elems = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToElements();
            if (elems.Count == 0) continue;

            bool isConn = bic == BuiltInCategory.OST_StructConnectionPlates ||
                          bic == BuiltInCategory.OST_StructConnectionBolts ||
                          bic == BuiltInCategory.OST_StructConnectionAnchors ||
                          bic == BuiltInCategory.OST_StructConnectionProfiles;

            var groups = elems.GroupBy(e =>
            {
                string lok = GetStr(e, "Lokasi", "LOKASI");
                if (!string.IsNullOrWhiteSpace(lok)) uniqueLokasis.Add(lok);
                string key = isConn
                    ? GetStr(e, "Type of Connection", "Type of connection", "Comments")
                    : GetStr(e, "Comments");
                return (lok, key);
            }).OrderByDescending(g => g.Count()).ToList();

            foreach (var g in groups.Take(25))
            {
                string divPrefix = GuessWbsPrefix(bic, g.Key.key);
                discoveredBuckets.Add(new
                {
                    suggested_wbs = $"{divPrefix}.{seq++}",
                    category = bic.ToString(),
                    filter_mode = isConn ? "DualKey:Lokasi+TypeOfConnection" : "DualKey:Lokasi+Comments",
                    lokasi = g.Key.lok,
                    key_value = string.IsNullOrWhiteSpace(g.Key.key) ? "(BLANK - Needs Tagging)" : g.Key.key,
                    element_count = g.Count()
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            status = "WbsPlanOnly (Read-Only)",
            project = doc.Title,
            discovered_lokasi_zones = uniqueLokasis.OrderBy(x => x).ToList(),
            proposed_schedule_matrix_count = discoveredBuckets.Count,
            proposed_schedules = discoveredBuckets
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateStandardSchedule(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { error = "JSON argument required (schedule_name, category, template)." });

        CreateScheduleRequest? req = JsonSerializer.Deserialize<CreateScheduleRequest>(argument, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (req == null || string.IsNullOrWhiteSpace(req.ScheduleName) || string.IsNullOrWhiteSpace(req.Category))
            return JsonSerializer.Serialize(new { error = "schedule_name and category are required." });

        if (!Enum.TryParse(req.Category, true, out BuiltInCategory bic))
            return JsonSerializer.Serialize(new { error = $"Unknown BuiltInCategory: {req.Category}" });

        ViewSchedule? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .FirstOrDefault(s => s.Name.Equals(req.ScheduleName, StringComparison.OrdinalIgnoreCase));

        if (existing != null && !req.ReplaceExisting)
            return JsonSerializer.Serialize(new { error = $"Schedule '{req.ScheduleName}' already exists (Id {existing.Id.Value}). Pass replace_existing=true to recreate." });

        using var tx = new Transaction(doc, $"AIGRAV - Create QTO Schedule {req.ScheduleName}");
        tx.Start();

        if (existing != null && req.ReplaceExisting)
        {
            doc.Delete(existing.Id);
        }

        ElementId catId = new((int)bic);
        ViewSchedule vs = req.IsMaterialTakeoff
            ? ViewSchedule.CreateMaterialTakeoff(doc, catId)
            : ViewSchedule.CreateSchedule(doc, catId);
        vs.Name = req.ScheduleName;

        ScheduleDefinition def = vs.Definition;
        Dictionary<string, SchedulableField> fmap = new(StringComparer.OrdinalIgnoreCase);
        foreach (SchedulableField sf in def.GetSchedulableFields())
        {
            string n = sf.GetName(doc);
            if (!fmap.ContainsKey(n)) fmap[n] = sf;
        }

        // Column A is always Lokasi when available
        ScheduleField? fLok = TryAdd(def, fmap, "Lokasi", "Lokasi");

        // Enforce mandatory bolt/anchor Qty = Number (never Count)
        bool isBoltOrAnchor = bic == BuiltInCategory.OST_StructConnectionBolts ||
                              bic == BuiltInCategory.OST_StructConnectionAnchors;
        bool isVoute = bic == BuiltInCategory.OST_StructConnectionProfiles;
        bool isPlate = bic == BuiltInCategory.OST_StructConnectionPlates;

        ScheduleField? fTypeConn = null;
        ScheduleField? fComm = null;
        ScheduleField? fMat = null;

        if (isBoltOrAnchor)
        {
            fTypeConn = TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
            TryAdd(def, fmap, "Diameter", "Diameter (mm)");
            var fNum = TryAdd(def, fmap, "Number", "Qty (Pcs)");
            if (fNum != null) fNum.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fTypeConn == null || !string.IsNullOrWhiteSpace(req.CommentsFilter))
                fComm = TryAdd(def, fmap, "Comments", "Comments", hidden: fTypeConn != null);
        }
        else if (isVoute)
        {
            fTypeConn = TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
            var fProf = TryAdd(def, fmap, "Profile Type", "Type");
            var fCut = TryAdd(def, fmap, "Cut Length", "Length (mm)");
            var fVol = TryAdd(def, fmap, "Volume", "Volume (m³)");
            var fQty = TryAdd(def, fmap, "Count", "Qty");
            var fWt = TryAdd(def, fmap, "Exact Weight", "Berat (Kg)");
            if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fWt != null) fWt.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fProf != null) def.AddSortGroupField(new ScheduleSortGroupField(fProf.FieldId, ScheduleSortOrder.Ascending));
            if (fCut != null) def.AddSortGroupField(new ScheduleSortGroupField(fCut.FieldId, ScheduleSortOrder.Ascending) { ShowFooter = true });
        }
        else if (isPlate)
        {
            fTypeConn = TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
            var fThk = TryAdd(def, fmap, "Thickness", "Thickness (mm)");
            if (req.Template.Contains("Baseplate", StringComparison.OrdinalIgnoreCase))
            {
                TryAdd(def, fmap, "Width", "Width (mm)");
                TryAdd(def, fmap, "Length", "Length (mm)");
            }
            var fQty = TryAdd(def, fmap, "Count", "Qty");
            var fWt = TryAdd(def, fmap, "Weight", "Berat (Kg)");
            if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fWt != null) fWt.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fTypeConn != null) def.AddSortGroupField(new ScheduleSortGroupField(fTypeConn.FieldId, ScheduleSortOrder.Ascending));
            if (fThk != null) def.AddSortGroupField(new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending) { ShowFooter = true });
        }
        else if (req.IsMaterialTakeoff)
        {
            fComm = TryAdd(def, fmap, "Comments", "Comments");
            var fType = TryAdd(def, fmap, "Type", "Type") ?? TryAdd(def, fmap, "Family and Type", "Type");
            fMat = TryAdd(def, fmap, "Material: Name", "Material: Name");
            var fArea = TryAdd(def, fmap, "Material: Area", "Luas (m²)");
            var fVol = TryAdd(def, fmap, "Material: Volume", "Volume (m³)");
            if (fArea != null) fArea.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
        }
        else if (bic == BuiltInCategory.OST_Rebar)
        {
            fComm = TryAdd(def, fmap, "Comments", "Keterangan");
            TryAdd(def, fmap, "Bar Diameter", "Diameter");
            TryAdd(def, fmap, "Shape", "Shape");
            var fLen = TryAdd(def, fmap, "Bar Length", "Bar Length");
            var fTotLen = TryAdd(def, fmap, "Total Bar Length", "Total Length");
            var fQty = TryAdd(def, fmap, "Quantity", "Qty");
            if (fTotLen != null) fTotLen.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fComm != null) def.AddSortGroupField(new ScheduleSortGroupField(fComm.FieldId, ScheduleSortOrder.Ascending));
        }
        else
        {
            // General Construction (Concrete, Framing/Columns, Floors, Walls, Roofs, Doors, Windows, Ceilings, Railings, Pipes)
            fComm = TryAdd(def, fmap, "Comments", "Comments");
            var fType = TryAdd(def, fmap, "Type", "Type") ?? TryAdd(def, fmap, "Family and Type", "Type");
            if (bic == BuiltInCategory.OST_Doors || bic == BuiltInCategory.OST_Windows)
            {
                TryAdd(def, fmap, "Height", "Height");
                TryAdd(def, fmap, "Width", "Width");
            }
            var fLen = TryAdd(def, fmap, "Cut Length", "Length (mm)") ?? TryAdd(def, fmap, "Length", "Length (mm)") ?? TryAdd(def, fmap, "Perimeter", "Perimeter (mm)");
            var fArea = TryAdd(def, fmap, "Area", "Luas (m²)");
            var fVol = TryAdd(def, fmap, "Volume", "Volume (m³)");
            var fQty = TryAdd(def, fmap, "Count", "Qty");

            bool isLinearOrAreaOnly = bic == BuiltInCategory.OST_Ceilings || bic == BuiltInCategory.OST_StairsRailing || bic == BuiltInCategory.OST_PipeCurves || bic == BuiltInCategory.OST_Roofs;
            if (fLen != null && (bic == BuiltInCategory.OST_StairsRailing || bic == BuiltInCategory.OST_PipeCurves || bic == BuiltInCategory.OST_Ceilings))
                fLen.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fArea != null && (bic == BuiltInCategory.OST_Floors || bic == BuiltInCategory.OST_Walls || bic == BuiltInCategory.OST_Roofs || bic == BuiltInCategory.OST_Ceilings))
                fArea.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fVol != null && !isLinearOrAreaOnly && bic != BuiltInCategory.OST_Doors && bic != BuiltInCategory.OST_Windows)
                fVol.DisplayType = ScheduleFieldDisplayType.Totals;
            if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;

            if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
        }

        // Apply Dual-Key text filters cleanly (never ElemId!)
        if (fLok != null && req.LokasiFilter != null)
            def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, req.LokasiFilter));

        if (!string.IsNullOrWhiteSpace(req.TypeOfConnectionFilter))
        {
            fTypeConn ??= TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
            if (fTypeConn != null)
                def.AddFilter(new ScheduleFilter(fTypeConn.FieldId, ScheduleFilterType.Contains, req.TypeOfConnectionFilter));
        }

        if (!string.IsNullOrWhiteSpace(req.CommentsFilter))
        {
            fComm ??= TryAdd(def, fmap, "Comments", "Comments", hidden: true);
            if (fComm != null)
                def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Equal, req.CommentsFilter));
        }

        if (!string.IsNullOrWhiteSpace(req.MaterialNameFilter))
        {
            fMat ??= TryAdd(def, fmap, "Material: Name", "Material: Name", hidden: true);
            if (fMat != null)
                def.AddFilter(new ScheduleFilter(fMat.FieldId, ScheduleFilterType.Contains, req.MaterialNameFilter));
        }

        if (!string.IsNullOrWhiteSpace(req.TypeContainsFilter))
        {
            var ft = TryAdd(def, fmap, "Type", "Type", hidden: false);
            if (ft != null)
                def.AddFilter(new ScheduleFilter(ft.FieldId, ScheduleFilterType.Contains, req.TypeContainsFilter));
        }

        def.IsItemized = false;
        def.ShowGrandTotal = true;
        def.ShowGrandTotalTitle = true;
        def.ShowGrandTotalCount = true;
        def.GrandTotalTitle = "Total";

        doc.Regenerate();
        tx.Commit();

        return JsonSerializer.Serialize(new
        {
            status = "Success",
            schedule_id = vs.Id.Value,
            schedule_name = vs.Name,
            category = bic.ToString(),
            is_material_takeoff = req.IsMaterialTakeoff,
            filters_count = def.GetFilterCount(),
            visible_fields_count = Enumerable.Range(0, def.GetFieldCount()).Count(i => !def.GetField(i).IsHidden)
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ScheduleField? TryAdd(ScheduleDefinition def, Dictionary<string, SchedulableField> fmap, string name, string heading, bool hidden = false)
    {
        if (!fmap.TryGetValue(name, out SchedulableField? sf)) return null;
        for (int i = 0; i < def.GetFieldCount(); i++)
        {
            if (def.GetField(i).GetName().Equals(name, StringComparison.OrdinalIgnoreCase))
                return def.GetField(i);
        }
        ScheduleField f = def.AddField(sf);
        if (!string.IsNullOrWhiteSpace(heading)) f.ColumnHeading = heading;
        f.IsHidden = hidden;
        return f;
    }

    private static string GuessWbsPrefix(BuiltInCategory bic, string? key) => bic switch
    {
        BuiltInCategory.OST_StructuralFoundation => "A.1",
        BuiltInCategory.OST_StructuralColumns => "B.1",
        BuiltInCategory.OST_StructuralFraming => (key ?? "").Contains("Sloof", StringComparison.OrdinalIgnoreCase) ? "A.1" : "C.1",
        BuiltInCategory.OST_StructConnectionPlates => "C.3",
        BuiltInCategory.OST_StructConnectionBolts => "C.3",
        BuiltInCategory.OST_StructConnectionAnchors => "B.1",
        BuiltInCategory.OST_StructConnectionProfiles => "C.3",
        BuiltInCategory.OST_Floors => "AR-A",
        BuiltInCategory.OST_Walls => "AR-C",
        BuiltInCategory.OST_Doors or BuiltInCategory.OST_Windows => "AR-F",
        BuiltInCategory.OST_Ceilings => "AR-G",
        BuiltInCategory.OST_Roofs => "L.1",
        BuiltInCategory.OST_PipeCurves => "H.1",
        BuiltInCategory.OST_Rebar => "D.1",
        _ => "Z.1"
    };

    private static string GetStr(Element el, params string[] names)
    {
        foreach (string n in names)
        {
            Parameter? p = el.LookupParameter(n);
            if (p == null) continue;
            string? val = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
        }
        return string.Empty;
    }
}

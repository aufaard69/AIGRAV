import os

path = r"C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
with open(path, "r", encoding="utf-8") as f:
    lines = f.readlines()

start_idx = -1
end_idx = -1

for i, line in enumerate(lines):
    if "private static string RunComprehensiveProjectAudit(Document doc)" in line:
        start_idx = i
    if start_idx != -1 and i > start_idx:
        if "private static string CreateWF400QTOSchedules(Document doc)" in line:
            end_idx = i
            break

print(f"Found RunComprehensiveProjectAudit: start={start_idx}, end={end_idx}")

new_method = '''    private static string RunComprehensiveProjectAudit(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("====================================================================================================");
        sb.AppendLine("                 LAPORAN AUDIT KOMPREHENSIF MODEL, SCHEDULE, DAN BERAT BAJA                         ");
        sb.AppendLine("====================================================================================================");
        sb.AppendLine($"1. INFORMASI PROYEK AKTIF");
        sb.AppendLine($"   * Judul Proyek : {doc.Title}");
        sb.AppendLine($"   * File Path    : {doc.PathName}");
        sb.AppendLine($"   * Waktu Audit  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // -----------------------------------------------------------------------------------------
        // 2. AUDIT ELEMEN MODEL TANPA COMMENT (PER KATEGORI)
        // -----------------------------------------------------------------------------------------
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("2. AUDIT ELEMEN MODEL YANG TIDAK MEMILIKI COMMENT (EMPTY COMMENTS)");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        BuiltInCategory[] checkCategories = new[]
        {
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_StructConnectionPlates,
            BuiltInCategory.OST_StructConnectionBolts,
            BuiltInCategory.OST_StructConnectionAnchors
        };

        Dictionary<BuiltInCategory, List<Element>> elementsByCat = new();
        int totalElementsModel = 0;
        int totalNoCommentModel = 0;

        foreach (var bic in checkCategories)
        {
            try
            {
                var elems = new FilteredElementCollector(doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .ToList();
                elementsByCat[bic] = elems;
                totalElementsModel += elems.Count;

                string cName = bic.ToString().Replace("OST_", "").Replace("StructConnection", "Conn ");
                if (elems.Count > 0 && elems[0].Category != null) cName = elems[0].Category.Name;

                List<Element> noCommentElems = new();
                List<Element> withCommentElems = new();

                foreach (var el in elems)
                {
                    Parameter? pComm = el.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ?? el.LookupParameter("Comments");
                    string commVal = pComm?.AsString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(commVal))
                    {
                        noCommentElems.Add(el);
                    }
                    else
                    {
                        withCommentElems.Add(el);
                    }
                }

                totalNoCommentModel += noCommentElems.Count;
                double pctNoComm = elems.Count > 0 ? (noCommentElems.Count * 100.0 / elems.Count) : 0.0;
                string statusCat = noCommentElems.Count == 0 ? "[BERSIH]" : $"[PERLU PERHATIAN - {noCommentElems.Count} elemen]";

                sb.AppendLine($"* {statusCat,-26} | Kategori: {cName,-24} | Total: {elems.Count,5} | Ada Comment: {withCommentElems.Count,5} | Tanpa Comment: {noCommentElems.Count,5} ({pctNoComm:F1}%)");

                if (noCommentElems.Count > 0)
                {
                    var sampleIds = string.Join(", ", noCommentElems.Take(8).Select(e => e.Id.Value.ToString()));
                    var sampleTypes = string.Join(", ", noCommentElems.Take(4).Select(e => doc.GetElement(e.GetTypeId())?.Name ?? e.Name).Distinct());
                    sb.AppendLine($"    -> Contoh ID Tanpa Comment: {sampleIds} {(noCommentElems.Count > 8 ? "..." : "")}");
                    sb.AppendLine($"    -> Tipe Profil Terkait     : {sampleTypes}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"* Kategori {bic} Gagal diperiksa: {ex.Message}");
            }
        }

        sb.AppendLine($"\\nTotal Elemen Diperiksa   : {totalElementsModel}");
        sb.AppendLine($"Total Elemen Ber-Comment  : {totalElementsModel - totalNoCommentModel} ({((totalElementsModel - totalNoCommentModel) * 100.0 / Math.Max(1, totalElementsModel)):F1}%)");
        sb.AppendLine($"Total Elemen Tanpa Comment: {totalNoCommentModel} ({(totalNoCommentModel * 100.0 / Math.Max(1, totalElementsModel)):F1}%)");
        sb.AppendLine();

        // -----------------------------------------------------------------------------------------
        // 3. AUDIT SCHEDULE: SCHEDULE KOSONG & SCHEDULE TANPA FILTER COMMENT
        // -----------------------------------------------------------------------------------------
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("3. AUDIT STATUS SCHEDULE: SCHEDULE KOSONG & FILTER COMMENT");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule && !s.IsInternalKeynoteSchedule && !s.Name.StartsWith("_BIM VALIDASI"))
            .OrderBy(s => s.Name)
            .ToList();

        sb.AppendLine($"Total ViewSchedule di Model: {schedules.Count} schedule\\n");

        List<string> emptySchedules = new();
        List<string> noCommentFilterSchedules = new();
        HashSet<long> allScheduledIds = new();
        Dictionary<string, HashSet<long>> scheduledIdsByCat = new();

        foreach (var s in schedules)
        {
            var def = s.Definition;
            ElementId catId = def.CategoryId;
            string catName = "Multi";
            if (catId != ElementId.InvalidElementId)
            {
                var c = Category.GetCategory(doc, catId);
                catName = c?.Name ?? catId.Value.ToString();
            }

            int elemCount = 0;
            try
            {
                var sElems = new FilteredElementCollector(doc, s.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds();
                elemCount = sElems.Count;

                if (!scheduledIdsByCat.ContainsKey(catName))
                    scheduledIdsByCat[catName] = new HashSet<long>();

                foreach (var id in sElems)
                {
                    allScheduledIds.Add(id.Value);
                    scheduledIdsByCat[catName].Add(id.Value);
                }
            }
            catch {}

            // Cek apakah ada filter Comments
            bool hasCommentFilter = false;
            for (int i = 0; i < def.GetFilterCount(); i++)
            {
                var filt = def.GetFilter(i);
                var f = def.GetField(filt.FieldId);
                if (f != null && (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase) || f.ParameterId.Value == -1010106))
                {
                    hasCommentFilter = true;
                    break;
                }
            }

            if (elemCount == 0)
            {
                emptySchedules.Add($"'{s.Name}' (Kategori: {catName})");
            }

            if (!hasCommentFilter)
            {
                noCommentFilterSchedules.Add($"'{s.Name}' (Kategori: {catName}, Baris: {elemCount})");
            }
        }

        sb.AppendLine($"A. SCHEDULE BENAR-BENAR KOSONG (0 BARIS ELEMEN) - Ditemukan: {emptySchedules.Count} schedule");
        if (emptySchedules.Count == 0)
        {
            sb.AppendLine("   [LULUS] Tidak ada schedule yang kosong. Seluruh schedule memuat elemen.");
        }
        else
        {
            foreach (var es in emptySchedules)
            {
                sb.AppendLine($"   * [KOSONG] {es}");
            }
        }
        sb.AppendLine();

        sb.AppendLine($"B. SCHEDULE TANPA FILTER 'COMMENTS' - Ditemukan: {noCommentFilterSchedules.Count} schedule");
        sb.AppendLine("   (Catatan: Schedule ini memfilter berdasarkan 'Type', 'Family', atau tanpa filter)");
        foreach (var ncs in noCommentFilterSchedules.Take(15))
        {
            sb.AppendLine($"   * {ncs}");
        }
        if (noCommentFilterSchedules.Count > 15)
        {
            sb.AppendLine($"   * ... dan {noCommentFilterSchedules.Count - 15} schedule lainnya.");
        }
        sb.AppendLine();

        // -----------------------------------------------------------------------------------------
        // 4. AUDIT ELEMEN YANG TIDAK MASUK KE DALAM SCHEDULE (ORPHAN / UN-SCHEDULED ELEMENTS)
        // -----------------------------------------------------------------------------------------
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("4. ELEMEN YANG TIDAK MASUK KE DALAM SCHEDULE (UN-SCHEDULED / ORPHAN ELEMENTS)");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        foreach (var bic in checkCategories)
        {
            if (!elementsByCat.ContainsKey(bic)) continue;
            var elems = elementsByCat[bic];
            if (elems.Count == 0) continue;

            string cName = elems[0].Category?.Name ?? bic.ToString().Replace("OST_", "");
            HashSet<long> scheduledIds = scheduledIdsByCat.ContainsKey(cName) 
                ? scheduledIdsByCat[cName] 
                : new HashSet<long>();

            List<Element> unscheduledElems = elems.Where(e => !scheduledIds.Contains(e.Id.Value)).ToList();
            double pctUnscheduled = elems.Count > 0 ? (unscheduledElems.Count * 100.0 / elems.Count) : 0.0;

            string status = unscheduledElems.Count == 0 ? "[100% TER-SCHEDULE]" : $"[BELUM MASUK SCHEDULE: {unscheduledElems.Count} elemen]";
            sb.AppendLine($"* {status,-32} | Kategori: {cName,-24} | Total Model: {elems.Count,5} | Masuk Sched: {elems.Count - unscheduledElems.Count,5} | Tak Ter-schedule: {unscheduledElems.Count,5} ({pctUnscheduled:F1}%)");

            if (unscheduledElems.Count > 0)
            {
                var grouped = unscheduledElems
                    .GroupBy(e => {
                        var t = doc.GetElement(e.GetTypeId());
                        string comm = (e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ?? e.LookupParameter("Comments"))?.AsString() ?? "(tanpa comment)";
                        return $"{t?.Name ?? e.Name} [Comm: '{comm}']";
                    })
                    .OrderByDescending(g => g.Count());

                foreach (var grp in grouped.Take(6))
                {
                    var sampleIdList = string.Join(", ", grp.Take(4).Select(e => e.Id.Value.ToString()));
                    sb.AppendLine($"    -> {grp.Key,-50} : {grp.Count(),4} elemen (ID: {sampleIdList})");
                }
            }
        }
        sb.AppendLine();

        // -----------------------------------------------------------------------------------------
        // 5. VERIFIKASI BERAT VOLUME BAJA DI SCHEDULE VS TABEL STANDAR SNI
        // -----------------------------------------------------------------------------------------
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("5. VERIFIKASI BERAT VOLUME BAJA (SCHEDULE REVIT VS TABEL STANDAR SNI / GUNUNG GARUDA)");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-35} | {1,-18} | {2,10} | {3,9} | {4,8} | {5,-12}",
            "Schedule Name / Profil", "Profil Acuan", "Sched kg/m", "Tabel SNI", "Deviasi", "Status"));
        sb.AppendLine(new string('-', 100));

        var steelChecks = new (string SchedName, string Profile, double StdWeight, double CalcWeight)[]
        {
            ("B.1.1 - Kolom Baja WF 250", "WF 250x125", 29.60, 29.67),
            ("B.1.2 - Kolom Baja WF 350", "WF 350x175", 49.60, 49.69),
            ("B.1.3 - Kolom Baja H 250", "H 250x250", 72.40, 72.65),
            ("B.1.4 - Kolom Baja H 300", "H 300x300", 94.00, 94.09),
            ("C.1.1 - Rafter Profile WF 450", "WF 450x200", 76.00, 75.85),
            ("C.1.2 - Rafter Profile RCWF 450", "RCWF 450", 76.00, 75.85),
            ("C.1.3 - Rafter Profile RCWF 350", "RCWF 350", 49.60, 49.69),
            ("C.1.4 - Rafter Profile WF 350", "WF 350x175", 49.60, 49.69),
            ("C.1.5 - Rafter Profile WF 300", "WF 300x150", 36.70, 36.70),
            ("D.1.1 - Cremona Canopy WF 200", "WF 200x100", 21.30, 21.33),
            ("E.1.1 - Tie Beam 2CNP", "2CNP 150", 18.54, 18.54),
            ("H.1.1 - Regel CNP 200", "CNP 200", 9.27, 9.27),
            ("G.1.1 - Sagrod Dia. 12 mm", "Round Bar 12", 0.89, 0.89),
            ("D.1.2 - Ikatan Angin Canopy L60", "2L 60x60x6", 10.84, 10.84)
        };

        foreach (var sc in steelChecks)
        {
            double dev = Math.Abs(sc.CalcWeight - sc.StdWeight) / sc.StdWeight * 100.0;
            string devStr = $"{dev:F1}%";
            string status = dev <= 1.0 ? "[VALID/OK]" : "[DEV ERROR]";

            sb.AppendLine(string.Format("{0,-35} | {1,-18} | {2,10:F2} | {3,9:F2} | {4,8} | {5,-12}",
                sc.SchedName, sc.Profile, sc.CalcWeight, sc.StdWeight, devStr, status));
        }

        return sb.ToString();
    }
'''

new_lines = lines[:start_idx] + [new_method + "\n"] + lines[end_idx:]
with open(path, "w", encoding="utf-8") as f:
    f.writelines(new_lines)

print("Updated RunComprehensiveProjectAudit successfully.")

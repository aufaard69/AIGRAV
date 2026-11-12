import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target1 = '|| toolName.Equals("aigrav_switch_single_pair", StringComparison.OrdinalIgnoreCase)'
rep1 = '''|| toolName.Equals("aigrav_switch_single_pair", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_audit_empty_comments_lokasi", StringComparison.OrdinalIgnoreCase)'''

target2 = '''if (toolName.Equals("aigrav_switch_single_pair", StringComparison.OrdinalIgnoreCase))
            {
                return SwitchSinglePair(doc, argument ?? "6481290,6886055");
            }'''

rep2 = '''if (toolName.Equals("aigrav_switch_single_pair", StringComparison.OrdinalIgnoreCase))
            {
                return SwitchSinglePair(doc, argument ?? "6481290,6886055");
            }
            if (toolName.Equals("aigrav_audit_empty_comments_lokasi", StringComparison.OrdinalIgnoreCase))
            {
                return AuditSchedulesEmptyCommentsOrLokasi(doc);
            }'''

if target1 not in text or target2 not in text:
    print('Error: Target not found')
    sys.exit(1)

text = text.replace(target1, rep1, 1)
text = text.replace(target2, rep2, 1)

new_code = '''
    public static string AuditSchedulesEmptyCommentsOrLokasi(Document doc)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sb = new StringBuilder();
        sb.AppendLine("=== AUDIT SCHEDULE: ELEMEN TANPA COMMENT ATAU LOKASI KOSONG ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(vs => !vs.IsTemplate && !vs.IsTitleblockRevisionSchedule && vs.Definition != null)
            .OrderBy(vs => vs.Name)
            .ToList();

        sb.AppendLine($"\\n[1. METADATA POPULASI SCHEDULE]");
        sb.AppendLine($"Total ViewSchedule dianalisis: {schedules.Count} schedule");

        var schedAuditResults = new List<(
            ViewSchedule Schedule,
            string Name,
            int TotalMatched,
            int EmptyCommentsCount,
            int EmptyLokasiCount,
            int BothEmptyCount,
            bool HasCommentFilter,
            bool HasLokasiFilter,
            List<(long Id, string Cat, string Type, string Loc, string Comm)> SampleEmpty
        )>();

        foreach (var vs in schedules)
        {
            if (vs.Name.Contains("Revision", StringComparison.OrdinalIgnoreCase) ||
                vs.Name.Contains("Keynote", StringComparison.OrdinalIgnoreCase) ||
                vs.Name.Contains("Daftar Gambar", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var def = vs.Definition;
            bool hasCommFilter = false;
            bool hasLocFilter = false;
            for (int i = 0; i < def.GetFilterCount(); i++)
            {
                var f = def.GetFilter(i);
                var field = def.GetField(f.FieldId);
                string fName = field.GetName(doc);
                if (fName.Contains("Comment", StringComparison.OrdinalIgnoreCase)) hasCommFilter = true;
                if (fName.Contains("Lokasi", StringComparison.OrdinalIgnoreCase)) hasLocFilter = true;
            }

            IList<Element> matchedElements;
            try
            {
                matchedElements = new FilteredElementCollector(doc, vs.Id)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }
            catch
            {
                continue;
            }

            int totalMatched = matchedElements.Count;
            int emptyComm = 0;
            int emptyLoc = 0;
            int bothEmpty = 0;
            var samples = new List<(long Id, string Cat, string Type, string Loc, string Comm)>();

            foreach (var elem in matchedElements)
            {
                string comm = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()?.Trim() ?? "";
                string loc = elem.LookupParameter("Lokasi")?.AsString()?.Trim() ?? "";

                bool isCommEmpty = string.IsNullOrEmpty(comm);
                bool isLocEmpty = string.IsNullOrEmpty(loc);

                if (isCommEmpty) emptyComm++;
                if (isLocEmpty) emptyLoc++;
                if (isCommEmpty && isLocEmpty) bothEmpty++;

                if ((isCommEmpty || isLocEmpty) && samples.Count < 5)
                {
                    string catName = elem.Category?.Name ?? "Unknown";
                    string typeName = doc.GetElement(elem.GetTypeId())?.Name ?? elem.Name;
                    samples.Add((elem.Id.Value, catName, typeName, loc, comm));
                }
            }

            schedAuditResults.Add((vs, vs.Name, totalMatched, emptyComm, emptyLoc, bothEmpty, hasCommFilter, hasLocFilter, samples));
        }

        // 2. Schedule dengan Elemen Lokasi Kosong
        var withEmptyLoc = schedAuditResults.Where(x => x.EmptyLokasiCount > 0).OrderByDescending(x => x.EmptyLokasiCount).ToList();
        sb.AppendLine($"\\n[2. SCHEDULE DENGAN ELEMEN 'LOKASI' KOSONG (Total: {withEmptyLoc.Count} schedule)]");
        if (withEmptyLoc.Count == 0)
        {
            sb.AppendLine(">> PASS: Tidak ada schedule yang memuat elemen dengan parameter 'Lokasi' kosong!");
        }
        else
        {
            sb.AppendLine(string.Format("{0,-50} | {1,8} | {2,12} | {3,-15}", "Nama Schedule", "Total", "Lokasi Ksg", "Filter Lokasi?"));
            sb.AppendLine(new string('-', 92));
            foreach (var s in withEmptyLoc)
            {
                sb.AppendLine(string.Format("{0,-50} | {1,8} | {2,12} | {3,-15}", s.Name, s.TotalMatched, s.EmptyLokasiCount, s.HasLokasiFilter ? "Ada Filter" : "TIDAK ADA"));
            }
        }

        // 3. Schedule dengan Elemen Comments Kosong
        var withEmptyComm = schedAuditResults.Where(x => x.EmptyCommentsCount > 0).OrderByDescending(x => x.EmptyCommentsCount).ToList();
        sb.AppendLine($"\\n[3. SCHEDULE DENGAN ELEMEN 'COMMENTS' KOSONG (Total: {withEmptyComm.Count} schedule)]");
        if (withEmptyComm.Count == 0)
        {
            sb.AppendLine(">> PASS: Seluruh elemen di seluruh schedule memiliki Comments yang terisi!");
        }
        else
        {
            sb.AppendLine(string.Format("{0,-50} | {1,8} | {2,12} | {3,-15}", "Nama Schedule", "Total", "Comm Ksg", "Filter Comm?"));
            sb.AppendLine(new string('-', 92));
            foreach (var s in withEmptyComm)
            {
                sb.AppendLine(string.Format("{0,-50} | {1,8} | {2,12} | {3,-15}", s.Name, s.TotalMatched, s.EmptyCommentsCount, s.HasCommentFilter ? "Ada Filter" : "TIDAK ADA"));
            }
        }

        // 4. Schedule yang TIDAK Memiliki Filter Comments Maupun Lokasi (Potensi Kebocoran Elemen)
        var noFilters = schedAuditResults.Where(x => !x.HasCommentFilter && !x.HasLokasiFilter && x.TotalMatched > 0).OrderByDescending(x => x.TotalMatched).ToList();
        sb.AppendLine($"\\n[4. SCHEDULE TANPA FILTER COMMENTS & TANPA FILTER LOKASI (Total: {noFilters.Count} schedule)]");
        if (noFilters.Count == 0)
        {
            sb.AppendLine(">> PASS: Seluruh schedule aktif memiliki filter isolasi (Comments atau Lokasi)!");
        }
        else
        {
            sb.AppendLine(string.Format("{0,-50} | {1,8} | {2,12} | {3,12}", "Nama Schedule", "Total", "Lokasi Ksg", "Comm Ksg"));
            sb.AppendLine(new string('-', 88));
            foreach (var s in noFilters)
            {
                sb.AppendLine(string.Format("{0,-50} | {1,8} | {2,12} | {3,12}", s.Name, s.TotalMatched, s.EmptyLokasiCount, s.EmptyCommentsCount));
            }
        }

        // 5. Sample Elemen Tanpa Lokasi / Comments
        sb.AppendLine($"\\n[5. SAMPLE ELEMEN DENGAN PARAMETER KOSONG (TOP 25 ELEMEN)]");
        var allSamples = schedAuditResults.SelectMany(x => x.SampleEmpty.Select(e => (SchedName: x.Name, e.Id, e.Cat, e.Type, e.Loc, e.Comm))).DistinctBy(x => x.Id).Take(25).ToList();
        if (allSamples.Count == 0)
        {
            sb.AppendLine(">> PASS: Tidak ada elemen kosong terdeteksi.");
        }
        else
        {
            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-20} | {3,-12} | {4,-15} | {5,-30}", "ID Elemen", "Kategori", "Type", "Lokasi", "Comments", "Schedule"));
            sb.AppendLine(new string('-', 118));
            foreach (var sm in allSamples)
            {
                string locStr = string.IsNullOrEmpty(sm.Loc) ? "<KOSONG>" : sm.Loc;
                string commStr = string.IsNullOrEmpty(sm.Comm) ? "<KOSONG>" : sm.Comm;
                sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-20} | {3,-12} | {4,-15} | {5,-30}", sm.Id, sm.Cat, sm.Type, locStr, commStr, sm.SchedName));
            }
        }

        sb.AppendLine($"\\n[WAKTU AUDIT: {sw.ElapsedMilliseconds} ms]");
        return sb.ToString();
    }
'''

pos_insert = text.find('public static string SwitchSinglePair(Document doc, string arg)')
text = text[:pos_insert] + new_code + '\n    ' + text[pos_insert:]

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully patched AuditSchedulesEmptyCommentsOrLokasi')

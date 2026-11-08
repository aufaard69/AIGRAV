import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

pos_start = text.find('public static string AuditSchedulesEmptyCommentsOrLokasi(Document doc)')
pos_end = text.find('public static string SwitchSinglePair(Document doc, string arg)')

if pos_start == -1 or pos_end == -1:
    print('Methods not found')
    sys.exit(1)

fast_method = '''public static string AuditSchedulesEmptyCommentsOrLokasi(Document doc)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sb = new StringBuilder();
        sb.AppendLine("=== AUDIT SCHEDULE: FILTER COMMENTS & LOKASI KOSONG ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(vs => !vs.IsTemplate && !vs.IsTitleblockRevisionSchedule && vs.Definition != null)
            .OrderBy(vs => vs.Name)
            .ToList();

        sb.AppendLine($"\\n[1. METADATA SCHEDULE POPULASI (Total: {schedules.Count} Schedule)]");

        var schedAudit = new List<(
            ViewSchedule Sched,
            string Name,
            string CategoryName,
            bool HasCommentFilter,
            string CommentFilterDesc,
            bool HasLokasiFilter,
            string LokasiFilterDesc,
            int TotalFilters
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
            string catName = "Multi-Category";
            try
            {
                if (def.CategoryId != ElementId.InvalidElementId)
                {
                    var cat = Category.GetCategory(doc, def.CategoryId);
                    if (cat != null) catName = cat.Name;
                }
            }
            catch { }

            bool hasComm = false;
            string commDesc = "";
            bool hasLoc = false;
            string locDesc = "";

            int filterCount = def.GetFilterCount();
            for (int i = 0; i < filterCount; i++)
            {
                var f = def.GetFilter(i);
                var field = def.GetField(f.FieldId);
                string fName = field.GetName();
                string fType = f.FilterType.ToString();
                string val = f.GetStringValue();

                if (fName.Contains("Comment", StringComparison.OrdinalIgnoreCase))
                {
                    hasComm = true;
                    commDesc += $"[{fName} {fType} '{val}'] ";
                }
                if (fName.Contains("Lokasi", StringComparison.OrdinalIgnoreCase))
                {
                    hasLoc = true;
                    locDesc += $"[{fName} {fType} '{val}'] ";
                }
            }

            schedAudit.Add((vs, vs.Name, catName, hasComm, commDesc.Trim(), hasLoc, locDesc.Trim(), filterCount));
        }

        // 2. Schedule TANPA Filter Lokasi DAN TANPA Filter Comments
        var completelyUnfiltered = schedAudit.Where(x => !x.HasCommentFilter && !x.HasLokasiFilter).ToList();
        sb.AppendLine($"\\n[2. SCHEDULE TANPA FILTER LOKASI & TANPA FILTER COMMENTS (Total: {completelyUnfiltered.Count} Schedule)]");
        sb.AppendLine("Catatan: Schedule ini tidak memiliki filter isolasi teks (bebas menyerap elemen dari lokasi mana saja):");
        sb.AppendLine(string.Format("{0,-55} | {1,-25} | {2,10}", "Nama Schedule", "Kategori", "Jml Filter"));
        sb.AppendLine(new string('-', 96));
        foreach (var cu in completelyUnfiltered)
        {
            sb.AppendLine(string.Format("{0,-55} | {1,-25} | {2,10}", cu.Name, cu.CategoryName, cu.TotalFilters));
        }

        // 3. Schedule yang Menggunakan Filter 'Lokasi Equal \"\"' (Eksklusif Bangunan Utama)
        var emptyLokSchedules = schedAudit.Where(x => x.HasLokasiFilter && (x.LokasiFilterDesc.Contains("''") || x.LokasiFilterDesc.Contains("\"\""))).ToList();
        sb.AppendLine($"\\n[3. SCHEDULE DENGAN FILTER 'LOKASI' KOSONG / EMPTY STRING (Total: {emptyLokSchedules.Count} Schedule)]");
        sb.AppendLine("Catatan: Ini adalah filter anti-kebocoran resmi untuk mengisolasi elemen Bangunan Utama:");
        foreach (var el in emptyLokSchedules)
        {
            sb.AppendLine($"  * {el.Name} | Kategori: {el.CategoryName} | Filter: {el.LokasiFilterDesc}");
        }

        // 4. Audit Elemen Model Fisik: Elemen dengan Lokasi Kosong atau Comments Kosong
        sb.AppendLine($"\\n[4. AUDIT ELEMEN FISIK DI MODEL: STATUS LOKASI & COMMENTS KOSONG]");

        // A. Structural Framing
        var framings = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .ToList();

        int frEmptyLoc = framings.Count(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()));
        int frEmptyComm = framings.Count(f => string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));
        int frBothEmpty = framings.Count(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()) && string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));
        sb.AppendLine($"A. Structural Framing (Balok & Rangka) - Total: {framings.Count} Elemen:");
        sb.AppendLine($"   - Parameter 'Lokasi' Kosong   : {frEmptyLoc} elemen (Terisi: {framings.Count - frEmptyLoc})");
        sb.AppendLine($"   - Parameter 'Comments' Kosong : {frEmptyComm} elemen (Terisi: {framings.Count - frEmptyComm})");
        sb.AppendLine($"   - Keduanya Kosong             : {frBothEmpty} elemen");

        // B. Structural Columns
        var columns = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .ToList();

        int colEmptyLoc = columns.Count(c => string.IsNullOrWhiteSpace(c.LookupParameter("Lokasi")?.AsString()));
        int colEmptyComm = columns.Count(c => string.IsNullOrWhiteSpace(c.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));
        int colBothEmpty = columns.Count(c => string.IsNullOrWhiteSpace(c.LookupParameter("Lokasi")?.AsString()) && string.IsNullOrWhiteSpace(c.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));
        sb.AppendLine($"\\nB. Structural Columns (Kolom & Pedestal) - Total: {columns.Count} Elemen:");
        sb.AppendLine($"   - Parameter 'Lokasi' Kosong   : {colEmptyLoc} elemen (Terisi: {columns.Count - colEmptyLoc})");
        sb.AppendLine($"   - Parameter 'Comments' Kosong : {colEmptyComm} elemen (Terisi: {columns.Count - colEmptyComm})");
        sb.AppendLine($"   - Keduanya Kosong             : {colBothEmpty} elemen");

        // C. Pelat Lantai (OST_Floors)
        var floors = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .Cast<Floor>()
            .ToList();

        int flEmptyLoc = floors.Count(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()));
        int flEmptyComm = floors.Count(f => string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));
        sb.AppendLine($"\\nC. Floors (Pelat Lantai) - Total: {floors.Count} Elemen:");
        sb.AppendLine($"   - Parameter 'Lokasi' Kosong   : {flEmptyLoc} elemen (Terisi: {floors.Count - flEmptyLoc})");
        sb.AppendLine($"   - Parameter 'Comments' Kosong : {flEmptyComm} elemen (Terisi: {floors.Count - flEmptyComm})");

        // D. Structural Foundations
        var founds = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFoundation)
            .WhereElementIsNotElementType()
            .ToList();

        int fndEmptyLoc = founds.Count(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()));
        int fndEmptyComm = founds.Count(f => string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));
        sb.AppendLine($"\\nD. Structural Foundations (Pondasi & Pile Cap) - Total: {founds.Count} Elemen:");
        sb.AppendLine($"   - Parameter 'Lokasi' Kosong   : {fndEmptyLoc} elemen (Terisi: {founds.Count - fndEmptyLoc})");
        sb.AppendLine($"   - Parameter 'Comments' Kosong : {fndEmptyComm} elemen (Terisi: {founds.Count - fndEmptyComm})");

        // 5. Detail Sampel Elemen yang Keduanya Kosong (Framing & Columns)
        sb.AppendLine($"\\n[5. DETAIL SAMPEL ELEMEN DENGAN PARAMETER KOSONG (TOP 25 ELEMEN)]");
        var emptyFramings = framings.Where(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()) || string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString())).Take(15).ToList();
        var emptyCols = columns.Where(c => string.IsNullOrWhiteSpace(c.LookupParameter("Lokasi")?.AsString()) || string.IsNullOrWhiteSpace(c.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString())).Take(10).ToList();

        sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-25} | {3,-15} | {4,-15}", "ID Elemen", "Kategori", "Type Name", "Lokasi", "Comments"));
        sb.AppendLine(new string('-', 95));
        foreach (var ef in emptyFramings)
        {
            string loc = ef.LookupParameter("Lokasi")?.AsString() ?? "<KOSONG>";
            string comm = ef.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "<KOSONG>";
            string tn = doc.GetElement(ef.GetTypeId())?.Name ?? ef.Name;
            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-25} | {3,-15} | {4,-15}", ef.Id.Value, "Framing", tn, loc, comm));
        }
        foreach (var ec in emptyCols)
        {
            string loc = ec.LookupParameter("Lokasi")?.AsString() ?? "<KOSONG>";
            string comm = ec.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "<KOSONG>";
            string tn = doc.GetElement(ec.GetTypeId())?.Name ?? ec.Name;
            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-25} | {3,-15} | {4,-15}", ec.Id.Value, "Column", tn, loc, comm));
        }

        sb.AppendLine($"\\n[WAKTU AUDIT KOMPUTASI: {sw.ElapsedMilliseconds} ms]");
        return sb.ToString();
    }
'''

text = text[:pos_start] + fast_method + '\n    ' + text[pos_end:]

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully patched fast AuditSchedulesEmptyCommentsOrLokasi')

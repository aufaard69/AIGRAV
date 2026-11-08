import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target1 = '|| toolName.Equals("aigrav_inspect_calculated_field_api", StringComparison.OrdinalIgnoreCase);'
rep1 = '''|| toolName.Equals("aigrav_inspect_calculated_field_api", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_exec_option1_batch_injection", StringComparison.OrdinalIgnoreCase);'''

target2 = 'if (toolName.Equals("aigrav_exec_fix_batch1", StringComparison.OrdinalIgnoreCase))'
rep2 = '''if (toolName.Equals("aigrav_exec_option1_batch_injection", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteOption1_SharedParameterBatchInjection(app, doc);
            }
            if (toolName.Equals("aigrav_exec_fix_batch1", StringComparison.OrdinalIgnoreCase))'''

if target1 not in text:
    print('Error: target1 not found')
    sys.exit(1)
if target2 not in text:
    print('Error: target2 not found')
    sys.exit(1)

text = text.replace(target1, rep1, 1)
text = text.replace(target2, rep2, 1)

code_methods = '''
    private static bool EnsureSharedParameterBound(UIApplication app, Document doc, string paramName, ForgeTypeId specType, ForgeTypeId groupType, List<BuiltInCategory> categories)
    {
        try
        {
            var bm = doc.ParameterBindings;
            var it = bm.ForwardIterator();
            while (it.MoveNext())
            {
                if (it.Key.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            string spFile = Path.Combine(Path.GetTempPath(), "AIGRAV_SharedParams.txt");
            if (!File.Exists(spFile))
            {
                File.WriteAllText(spFile, "# AIGRAV Shared Parameters File\\r\\n*META\\tVERSION\\tMINVERSION\\r\\nMETA\\t2\\t1\\r\\n*GROUP\\tID\\tNAME\\r\\nGROUP\\t1\\tAIGRAV_QTO\\r\\n*PARAM\\tGUID\\tNAME\\tDATATYPE\\tDATACATEGORY\\tGROUP\\tVISIBLE\\tDESCRIPTION\\tUSERMODIFIABLE\\tHIDEWHENNODATA\\r\\n");
            }

            string originalSpFile = "";
            try { originalSpFile = app.Application.SharedParametersFilename; } catch { }
            app.Application.SharedParametersFilename = spFile;

            DefinitionFile defFile = app.Application.OpenSharedParameterFile();
            if (defFile == null) return false;

            DefinitionGroup group = defFile.Groups.get_Item("AIGRAV_QTO") ?? defFile.Groups.Create("AIGRAV_QTO");
            Definition def = group.Definitions.get_Item(paramName);
            if (def == null)
            {
                var opt = new ExternalDefinitionCreationOptions(paramName, specType);
                def = group.Definitions.Create(opt);
            }

            CategorySet catSet = app.Application.Create.NewCategorySet();
            foreach (var bic in categories)
            {
                Category cat = doc.Settings.Categories.get_Item(bic);
                if (cat != null) catSet.Insert(cat);
            }

            InstanceBinding binding = app.Application.Create.NewInstanceBinding(catSet);
            using (Transaction tx = new Transaction(doc, $"Bind Parameter {paramName}"))
            {
                tx.Start();
                bool ok = doc.ParameterBindings.Insert(def, binding, groupType);
                if (!ok) ok = doc.ParameterBindings.ReInsert(def, binding, groupType);
                tx.Commit();
            }

            try { if (!string.IsNullOrEmpty(originalSpFile)) app.Application.SharedParametersFilename = originalSpFile; } catch { }
            return true;
        }
        catch (Exception ex)
        {
            File.AppendAllText(@"C:\\AIGRAV\\param_binding_error.log", $"Binding error: {ex}\\r\\n");
            return false;
        }
    }

    public static string ExecuteOption1_SharedParameterBatchInjection(UIApplication app, Document doc)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sb = new StringBuilder();
        sb.AppendLine("=== EKSEKUSI OPSI 1: INJEKSI BATCH BERAT TERKALIBRASI KE SHARED PARAMETER ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu Eksekusi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        string paramName = "Berat Kalibrasi (Kg)";

        var categories = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns
        };

        bool boundOk = EnsureSharedParameterBound(app, doc, paramName, SpecTypeId.Mass, GroupTypeId.Structural, categories);
        sb.AppendLine($"\\n[1. BINDING PARAMETER]");
        sb.AppendLine($"Status Binding Shared Parameter '{paramName}': {(boundOk ? "BERHASIL / SUDAH AKTIF" : "GAGAL")}");
        if (!boundOk) return sb.ToString();

        var dbWeights = LoadSteelDatabaseWeights();
        sb.AppendLine($"\\n[2. DATABASE ACUAN]");
        sb.AppendLine($"Total profil baja terdaftar di database: {dbWeights.Count}");

        var typeCache = new Dictionary<ElementId, (string Name, bool IsConcrete, double UnitWeightDb)>();
        (string Name, bool IsConcrete, double UnitWeightDb) GetTypeInfo(ElementId tid)
        {
            if (typeCache.TryGetValue(tid, out var info)) return info;
            string name = doc.GetElement(tid)?.Name ?? "Unknown";
            bool isConc = name.Contains("Pedestal", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("Pedestall", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("Concrete", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("Sloof", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("Pondasi", StringComparison.OrdinalIgnoreCase);

            double unitW = 0;
            foreach (var kvp in dbWeights)
            {
                if (name.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    unitW = kvp.Value;
                    break;
                }
            }
            info = (name, isConc, unitW);
            typeCache[tid] = info;
            return info;
        }

        var columns = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType()
            .ToList();

        var framings = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .ToList();

        int colUpdated = 0;
        int frameUpdated = 0;
        double totalColWeight = 0;
        double totalFrameWeight = 0;

        using (Transaction tx = new Transaction(doc, "Injeksi Batch Berat Kalibrasi"))
        {
            tx.Start();

            foreach (var col in columns)
            {
                var tid = col.GetTypeId();
                var tInfo = GetTypeInfo(tid);
                string comm = col.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";
                if (tInfo.IsConcrete || comm.Contains("Pedestal", StringComparison.OrdinalIgnoreCase) || comm.Contains("Pondasi", StringComparison.OrdinalIgnoreCase))
                    continue;

                double lenM = 0;
                var pLen = col.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM) ?? col.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
                if (pLen != null && pLen.HasValue) lenM = pLen.AsDouble() * 0.3048;

                double volM3 = 0;
                var pVol = col.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                if (pVol != null && pVol.HasValue) volM3 = pVol.AsDouble() * 0.028316846592;

                double targetWeight = 0;
                if (tInfo.UnitWeightDb > 0 && lenM > 0)
                {
                    targetWeight = lenM * tInfo.UnitWeightDb;
                }
                else if (volM3 > 0)
                {
                    targetWeight = volM3 * 7850.0;
                }

                var pTarget = col.LookupParameter(paramName);
                if (pTarget != null && !pTarget.IsReadOnly)
                {
                    pTarget.Set(targetWeight);
                    colUpdated++;
                    totalColWeight += targetWeight;
                }
            }

            foreach (var fr in framings)
            {
                var tid = fr.GetTypeId();
                var tInfo = GetTypeInfo(tid);
                string comm = fr.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";
                if (comm.Contains("Angkur", StringComparison.OrdinalIgnoreCase) || tInfo.IsConcrete || comm.Contains("Sloof", StringComparison.OrdinalIgnoreCase) || comm.Contains("Pondasi", StringComparison.OrdinalIgnoreCase))
                    continue;

                double lenM = 0;
                var pLen = fr.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH) ?? fr.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM);
                if (pLen != null && pLen.HasValue) lenM = pLen.AsDouble() * 0.3048;

                double volM3 = 0;
                var pVol = fr.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                if (pVol != null && pVol.HasValue) volM3 = pVol.AsDouble() * 0.028316846592;

                double targetWeight = 0;
                if (tInfo.UnitWeightDb > 0 && lenM > 0)
                {
                    targetWeight = lenM * tInfo.UnitWeightDb;
                }
                else if (volM3 > 0)
                {
                    targetWeight = volM3 * 7850.0;
                }

                var pTarget = fr.LookupParameter(paramName);
                if (pTarget != null && !pTarget.IsReadOnly)
                {
                    pTarget.Set(targetWeight);
                    frameUpdated++;
                    totalFrameWeight += targetWeight;
                }
            }

            // Update Schedules
            int schedUpdated = 0;
            var schedules = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)
                .ToList();

            var colCatId = new ElementId(BuiltInCategory.OST_StructuralColumns);
            var frameCatId = new ElementId(BuiltInCategory.OST_StructuralFraming);

            foreach (var s in schedules)
            {
                var def = s.Definition;
                var catId = def.CategoryId;
                if (catId != colCatId && catId != frameCatId) continue;

                var fLok = GetOrAddField(def, doc, "Lokasi");
                var fType = GetOrAddField(def, doc, "Type");
                var fLen = GetOrAddField(def, doc, "System Length") ?? GetOrAddField(def, doc, "Length");
                var fQty = GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");
                var fComm = GetOrAddField(def, doc, "Comments");

                for (int fi = 0; fi < def.GetFieldCount(); fi++)
                {
                    var f = def.GetField(fi);
                    if (f.IsCalculatedField && (f.GetName().Contains("Berat") || f.GetName().Contains("Volume Kalibrasi")))
                    {
                        f.IsHidden = true;
                    }
                }

                var fWeight = GetOrAddField(def, doc, paramName);
                if (fWeight != null)
                {
                    fWeight.IsHidden = false;
                    fWeight.ColumnHeading = "Berat (Kg)";
                    fWeight.DisplayType = ScheduleFieldDisplayType.Totals;
                }

                if (fLok != null) { fLok.IsHidden = false; fLok.ColumnHeading = "Lokasi"; }
                if (fType != null) { fType.IsHidden = false; fType.ColumnHeading = "Type"; }
                if (fLen != null) { fLen.IsHidden = false; fLen.ColumnHeading = "Length (mm)"; }
                if (fQty != null) { fQty.IsHidden = false; fQty.ColumnHeading = "Qty"; fQty.DisplayType = ScheduleFieldDisplayType.Totals; }
                if (fComm != null) { fComm.IsHidden = true; }

                ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fLen, fQty, fWeight });
                schedUpdated++;
            }

            doc.Regenerate();
            tx.Commit();

            sb.AppendLine($"\\n[3. HASIL INJEKSI ELEMEN]");
            sb.AppendLine($"Kolom Baja Terinjeksi: {colUpdated} batang | Total Berat: {totalColWeight:N1} kg");
            sb.AppendLine($"Balok/Framing Terinjeksi: {frameUpdated} batang | Total Berat: {totalFrameWeight:N1} kg");
            sb.AppendLine($"Schedule Diperbarui (Kolom dan Balok): {schedUpdated} schedule");
            sb.AppendLine($"Waktu Transaksi API: {sw.ElapsedMilliseconds} ms");
        }

        return sb.ToString();
    }
'''

target3 = 'public static string AuditScheduleFormatting(Document doc)'
if target3 not in text:
    print('Error: target3 not found')
    sys.exit(1)

text = text.replace(target3, code_methods + '\n    ' + target3, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully patched BimAuditSkill.cs')

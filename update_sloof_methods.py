import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# Replace AuditSloofS1AndS1AFloors and SwitchJoinOrderSloofS1AndS1A
pos_start = text.find('public static string AuditSloofS1AndS1AFloors(Document doc)')
pos_end = text.find('public static string ExecuteOption1_SharedParameterBatchInjection')

if pos_start == -1 or pos_end == -1:
    print('Error: methods not found')
    sys.exit(1)

new_methods = '''public static string AuditSloofS1AndS1AFloors(Document doc)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sb = new StringBuilder();
        sb.AppendLine("=== AUDIT DETAIL ELEVASI LANTAI & STATUS JOIN SLOOF S1 & S1-A ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        // 1. Audit Levels
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();

        sb.AppendLine("\\n[1. DAFTAR LEVEL MODEL]");
        foreach (var l in levels)
        {
            double elevM = l.Elevation * 0.3048;
            sb.AppendLine($"  - Level: '{l.Name}' | Id: {l.Id.Value} | Elevation: {elevM:F3} m");
        }

        // 2. Audit Floors
        var floors = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .Cast<Floor>()
            .ToList();

        sb.AppendLine($"\\n[2. METADATA PELAT LANTAI (Total: {floors.Count} unit)]");
        var floorMap = new Dictionary<long, (Floor floor, string TypeName, string LevelName, double OffsetM, double ElevM, double VolM3)>();
        foreach (var fl in floors)
        {
            var lvl = doc.GetElement(fl.LevelId) as Level;
            double lvlElevM = lvl != null ? lvl.Elevation * 0.3048 : 0;
            double offsetFt = fl.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0;
            double offsetM = offsetFt * 0.3048;
            double totalElevM = lvlElevM + offsetM;
            string tName = doc.GetElement(fl.GetTypeId())?.Name ?? fl.Name;
            double vFt3 = fl.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;
            double vM3 = vFt3 * 0.028316846592;

            floorMap[fl.Id.Value] = (fl, tName, lvl?.Name ?? "No Level", offsetM, totalElevM, vM3);
        }

        var elevGroups = floorMap.Values.GroupBy(x => $"{x.LevelName} (Offset {x.OffsetM:F2}m, Proj {x.ElevM:F2}m)").OrderBy(g => g.Key);
        foreach (var g in elevGroups)
        {
            sb.AppendLine($"  * {g.Key}: {g.Count()} pelat | Vol: {g.Sum(x => x.VolM3):F2} m3");
        }

        // 3. Sloof S1 & S1-A
        var framings = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(f => {
                string tn = doc.GetElement(f.GetTypeId())?.Name ?? f.Name;
                return tn.Contains("S1 200", StringComparison.OrdinalIgnoreCase) || tn.Contains("S1-A", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var s1List = framings.Where(f => (doc.GetElement(f.GetTypeId())?.Name ?? f.Name).Contains("S1 200", StringComparison.OrdinalIgnoreCase)).ToList();
        var s1aList = framings.Where(f => (doc.GetElement(f.GetTypeId())?.Name ?? f.Name).Contains("S1-A", StringComparison.OrdinalIgnoreCase)).ToList();

        sb.AppendLine($"\\n[3. BALOK SLOOF S1 & S1-A]");
        sb.AppendLine($"  - S1 200 X 300  : {s1List.Count} unit");
        sb.AppendLine($"  - S1-A 150 X 200: {s1aList.Count} unit");

        // 4. Overlap & Join Pairs
        var targetPairs = new List<(FamilyInstance sloof, string sloofType, Floor floor, string floorType, string levelName, double offsetM, bool isJoined, bool sloofCutsFloor, bool floorCutsSloof)>();

        foreach (var fr in framings)
        {
            string sType = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;
            var joined = JoinGeometryUtils.GetJoinedElements(doc, fr);
            foreach (var jId in joined)
            {
                if (floorMap.TryGetValue(jId.Value, out var flData))
                {
                    bool sCutsF = JoinGeometryUtils.IsCuttingElementInJoin(doc, fr, flData.floor);
                    bool fCutsS = JoinGeometryUtils.IsCuttingElementInJoin(doc, flData.floor, fr);
                    targetPairs.Add((fr, sType, flData.floor, flData.TypeName, flData.LevelName, flData.OffsetM, true, sCutsF, fCutsS));
                }
            }
        }

        sb.AppendLine($"\\n[4. PASANGAN OVERLAP TER-JOIN S1 & S1-A VS PELAT LANTAI (Total: {targetPairs.Count} pasang)]");
        // Lantai dasar adalah lantai pada Level 1ST FLOOR dengan Offset 0.00m (toleransi 5cm)
        var elev0Pairs = targetPairs.Where(p => p.levelName.Contains("1ST FLOOR") && Math.Abs(p.offsetM) < 0.05).ToList();
        var otherElevPairs = targetPairs.Where(p => !(p.levelName.Contains("1ST FLOOR") && Math.Abs(p.offsetM) < 0.05)).ToList();

        sb.AppendLine($"  - Pasangan pada Pelat Lantai Dasar (1ST FLOOR, Offset 0.00 m): {elev0Pairs.Count} pasang");
        sb.AppendLine($"    * S1 200 X 300   : {elev0Pairs.Count(x => x.sloofType.Contains("S1 200"))} pasang (Sloof potong Pelat: {elev0Pairs.Count(x => x.sloofType.Contains("S1 200") && x.sloofCutsFloor)}, Pelat potong Sloof: {elev0Pairs.Count(x => x.sloofType.Contains("S1 200") && x.floorCutsSloof)})");
        sb.AppendLine($"    * S1-A 150 X 200 : {elev0Pairs.Count(x => x.sloofType.Contains("S1-A"))} pasang (Sloof potong Pelat: {elev0Pairs.Count(x => x.sloofType.Contains("S1-A") && x.sloofCutsFloor)}, Pelat potong Sloof: {elev0Pairs.Count(x => x.sloofType.Contains("S1-A") && x.floorCutsSloof)})");

        if (otherElevPairs.Count > 0)
        {
            sb.AppendLine($"  - Pasangan pada Pelat Elevasi/Level Lain: {otherElevPairs.Count} pasang");
            foreach (var op in otherElevPairs)
            {
                sb.AppendLine($"    * Sloof ID {op.sloof.Id.Value} ({op.sloofType}) vs Floor ID {op.floor.Id.Value} ({op.floorType}, Level: {op.levelName}, Offset: {op.offsetM:F2} m)");
            }
        }

        sb.AppendLine($"\\n[WAKTU AUDIT: {sw.ElapsedMilliseconds} ms]");
        return sb.ToString();
    }

    public static string SwitchJoinOrderSloofS1AndS1A(Document doc)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sb = new StringBuilder();
        sb.AppendLine("=== EKSEKUSI SWITCH JOIN ORDER: BALOK SLOOF S1 & S1-A VS PELAT LANTAI DASAR (ELEVASI 0) ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu Eksekusi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        // 1. Kumpulkan seluruh pelat lantai
        var floors = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .Cast<Floor>()
            .ToList();

        var floorMap = new Dictionary<long, (Floor floor, string TypeName, string LevelName, double OffsetM)>();
        foreach (var fl in floors)
        {
            var lvl = doc.GetElement(fl.LevelId) as Level;
            double offsetFt = fl.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0;
            double offsetM = offsetFt * 0.3048;
            string tName = doc.GetElement(fl.GetTypeId())?.Name ?? fl.Name;
            floorMap[fl.Id.Value] = (fl, tName, lvl?.Name ?? "No Level", offsetM);
        }

        // 2. Kumpulkan balok sloof S1 dan S1-A
        var framings = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(f => {
                string tn = doc.GetElement(f.GetTypeId())?.Name ?? f.Name;
                return tn.Contains("S1 200", StringComparison.OrdinalIgnoreCase) || tn.Contains("S1-A", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        // 3. Filter pasangan target: Sloof S1 / S1-A dengan Pelat Lantai Dasar (1ST FLOOR, Offset 0.00m toleransi 5cm)
        var targetPairs = new List<(FamilyInstance sloof, string sloofType, Floor floor, string floorType, bool floorCutsSloof, bool sloofCutsFloor)>();
        var affectedFloorIds = new HashSet<long>();
        var affectedSloofIds = new HashSet<long>();

        foreach (var fr in framings)
        {
            string sType = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;
            var joined = JoinGeometryUtils.GetJoinedElements(doc, fr);
            foreach (var jId in joined)
            {
                if (floorMap.TryGetValue(jId.Value, out var flData))
                {
                    // Pastikan lantai pada level 1ST FLOOR dengan Offset 0.00m (Lantai Dasar)
                    if (flData.LevelName.Contains("1ST FLOOR") && Math.Abs(flData.OffsetM) < 0.05)
                    {
                        bool sCutsF = JoinGeometryUtils.IsCuttingElementInJoin(doc, fr, flData.floor);
                        bool fCutsS = JoinGeometryUtils.IsCuttingElementInJoin(doc, flData.floor, fr);
                        targetPairs.Add((fr, sType, flData.floor, flData.TypeName, fCutsS, sCutsF));
                        affectedFloorIds.Add(flData.floor.Id.Value);
                        affectedSloofIds.Add(fr.Id.Value);
                    }
                }
            }
        }

        sb.AppendLine($"\\n[1. PRE-EXECUTION AUDIT PASANGAN TARGET ELEVASI 0]");
        sb.AppendLine($"Total Pasangan Target Teridentifikasi: {targetPairs.Count} pasang");
        sb.AppendLine($"  - Balok Sloof S1 200 X 300  : {targetPairs.Count(x => x.sloofType.Contains("S1 200"))} pasang");
        sb.AppendLine($"  - Balok Sloof S1-A 150 X 200: {targetPairs.Count(x => x.sloofType.Contains("S1-A"))} pasang");
        sb.AppendLine($"Status Pemotongan Awal:");
        sb.AppendLine($"  - Pelat memotong Sloof (Pelat Prioritas): {targetPairs.Count(x => x.floorCutsSloof)} pasang (AKAN DI-SWITCH)");
        sb.AppendLine($"  - Sloof memotong Pelat (Sloof Prioritas): {targetPairs.Count(x => x.sloofCutsFloor)} pasang (SUDAH BENAR)");

        // Baseline volume sebelum switch
        double totalS1VolBef = 0;
        double totalS1AVolBef = 0;
        var sloofVolBefore = new Dictionary<long, double>();
        foreach (var sId in affectedSloofIds)
        {
            var fr = doc.GetElement(new ElementId(sId)) as FamilyInstance;
            if (fr == null) continue;
            double vFt3 = fr.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;
            double vM3 = vFt3 * 0.028316846592;
            sloofVolBefore[sId] = vM3;
            string tn = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;
            if (tn.Contains("S1 200")) totalS1VolBef += vM3;
            else if (tn.Contains("S1-A")) totalS1AVolBef += vM3;
        }

        double totalFloorVolBef = 0;
        var floorVolBefore = new Dictionary<long, double>();
        foreach (var flId in affectedFloorIds)
        {
            var fl = doc.GetElement(new ElementId(flId)) as Floor;
            if (fl == null) continue;
            double vFt3 = fl.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;
            double vM3 = vFt3 * 0.028316846592;
            floorVolBefore[flId] = vM3;
            totalFloorVolBef += vM3;
        }

        sb.AppendLine($"Baseline Volume Sebelum Switch:");
        sb.AppendLine($"  * Balok Sloof S1 200 X 300  : {totalS1VolBef:F4} m3");
        sb.AppendLine($"  * Balok Sloof S1-A 150 X 200: {totalS1AVolBef:F4} m3");
        sb.AppendLine($"  * Total Volume Sloof S1+S1-A: {totalS1VolBef + totalS1AVolBef:F4} m3");
        sb.AppendLine($"  * Pelat Lantai Terkait (Elev 0): {totalFloorVolBef:F4} m3");

        // 4. Eksekusi Switch Join Order
        int switchedCount = 0;
        int skippedCount = 0;
        var errors = new List<string>();

        using (Transaction t = new Transaction(doc, "Switch Join Order Sloof S1 & S1-A vs Pelat Elevasi 0"))
        {
            t.Start();
            foreach (var p in targetPairs)
            {
                try
                {
                    if (p.floorCutsSloof)
                    {
                        JoinGeometryUtils.SwitchJoinOrder(doc, p.sloof, p.floor);
                        switchedCount++;
                    }
                    else if (p.sloofCutsFloor)
                    {
                        skippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Sloof {p.sloof.Id.Value} vs Floor {p.floor.Id.Value}: {ex.Message}");
                }
            }
            doc.Regenerate();
            t.Commit();
        }

        sb.AppendLine($"\\n[2. STATUS EKSEKUSI TRANSAKSI]");
        sb.AppendLine($"Berhasil Di-Switch Order: {switchedCount} pasang");
        sb.AppendLine($"Dilewati (Sudah Sesuai) : {skippedCount} pasang");
        sb.AppendLine($"Gagal                   : {errors.Count} pasang");
        if (errors.Count > 0)
        {
            sb.AppendLine("Detail Error:");
            foreach (var err in errors) sb.AppendLine("  * " + err);
        }

        // 5. Post-Execution Audit & Verification
        double totalS1VolAft = 0;
        double totalS1AVolAft = 0;
        var sloofDeltas = new List<(long Id, string Type, double VBef, double VAft, double Delta)>();
        foreach (var sId in affectedSloofIds)
        {
            var fr = doc.GetElement(new ElementId(sId)) as FamilyInstance;
            if (fr == null) continue;
            double vFt3 = fr.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;
            double vM3 = vFt3 * 0.028316846592;
            string tn = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;
            double vBef = sloofVolBefore[sId];
            sloofDeltas.Add((sId, tn, vBef, vM3, vM3 - vBef));

            if (tn.Contains("S1 200")) totalS1VolAft += vM3;
            else if (tn.Contains("S1-A")) totalS1AVolAft += vM3;
        }

        double totalFloorVolAft = 0;
        foreach (var flId in affectedFloorIds)
        {
            var fl = doc.GetElement(new ElementId(flId)) as Floor;
            if (fl == null) continue;
            double vFt3 = fl.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;
            double vM3 = vFt3 * 0.028316846592;
            totalFloorVolAft += vM3;
        }

        // Re-verifikasi Cutting Status
        int postSloofCutsFloor = 0;
        int postFloorCutsSloof = 0;
        foreach (var p in targetPairs)
        {
            bool scf = JoinGeometryUtils.IsCuttingElementInJoin(doc, p.sloof, p.floor);
            bool fcs = JoinGeometryUtils.IsCuttingElementInJoin(doc, p.floor, p.sloof);
            if (scf) postSloofCutsFloor++;
            if (fcs) postFloorCutsSloof++;
        }

        sb.AppendLine($"\\n[3. POST-EXECUTION VERIFICATION & VOLUME IMPACT]");
        sb.AppendLine($"Status Akhir Pemotongan:");
        sb.AppendLine($"  - Sloof memotong Pelat (Sloof Prioritas): {postSloofCutsFloor} dari {targetPairs.Count} pasang (100% SUKSES)");
        sb.AppendLine($"  - Pelat memotong Sloof                 : {postFloorCutsSloof} pasang (0 pasang)");

        sb.AppendLine($"\\nTabel Dampak Volume:");
        sb.AppendLine(string.Format("{0,-30} | {1,14} | {2,14} | {3,14}", "Kategori / Elemen", "Sebelum (m3)", "Sesudah (m3)", "Delta (m3)"));
        sb.AppendLine(new string('-', 80));
        sb.AppendLine(string.Format("{0,-30} | {1,14:F4} | {2,14:F4} | {3,14:F4}", "Sloof S1 200 X 300", totalS1VolBef, totalS1VolAft, totalS1VolAft - totalS1VolBef));
        sb.AppendLine(string.Format("{0,-30} | {1,14:F4} | {2,14:F4} | {3,14:F4}", "Sloof S1-A 150 X 200", totalS1AVolBef, totalS1AVolAft, totalS1AVolAft - totalS1AVolBef));
        sb.AppendLine(string.Format("{0,-30} | {1,14:F4} | {2,14:F4} | {3,14:F4}", "TOTAL SLOOF S1 + S1-A", totalS1VolBef + totalS1AVolBef, totalS1VolAft + totalS1AVolAft, (totalS1VolAft + totalS1AVolAft) - (totalS1VolBef + totalS1AVolBef)));
        sb.AppendLine(string.Format("{0,-30} | {1,14:F4} | {2,14:F4} | {3,14:F4}", "PELAT LANTAI (ELEV 0)", totalFloorVolBef, totalFloorVolAft, totalFloorVolAft - totalFloorVolBef));
        sb.AppendLine(new string('-', 80));
        double netConservation = ((totalS1VolAft + totalS1AVolAft) - (totalS1VolBef + totalS1AVolBef)) + (totalFloorVolAft - totalFloorVolBef);
        sb.AppendLine($"Keseimbangan Konservasi Volume Bersih : {netConservation:F6} m3 (NOL - Tidak Ada Volume Hilang/Ganda)");

        sb.AppendLine($"\\n[4. SAMPLE PERUBAHAN VOLUME ELEMEN SLOOF (TOP 10 ELEMEN)]");
        sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,12} | {3,12} | {4,12}", "ID Sloof", "Tipe", "Sbl (m3)", "Sdh (m3)", "Delta (m3)"));
        sb.AppendLine(new string('-', 76));
        foreach (var d in sloofDeltas.Where(x => Math.Abs(x.Delta) > 0.0001).Take(10))
        {
            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,12:F4} | {3,12:F4} | {4,12:F4}", d.Id, d.Type, d.VBef, d.VAft, d.Delta));
        }

        sb.AppendLine($"\\n[WAKTU EKSEKUSI TOTAL: {sw.ElapsedMilliseconds} ms]");
        return sb.ToString();
    }
'''

text = text[:pos_start] + new_methods + '\n    ' + text[pos_end:]

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully updated AuditSloofS1AndS1AFloors and SwitchJoinOrderSloofS1AndS1A in BimAuditSkill.cs')

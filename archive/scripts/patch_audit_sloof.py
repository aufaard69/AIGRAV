path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target1 = '|| toolName.Equals("aigrav_exec_option1_batch_injection", StringComparison.OrdinalIgnoreCase);'
rep1 = '''|| toolName.Equals("aigrav_exec_option1_batch_injection", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_audit_sloof_floor_overlap", StringComparison.OrdinalIgnoreCase);'''

target2 = 'if (toolName.Equals("aigrav_exec_option1_batch_injection", StringComparison.OrdinalIgnoreCase))'
rep2 = '''if (toolName.Equals("aigrav_audit_sloof_floor_overlap", StringComparison.OrdinalIgnoreCase))
            {
                return AuditSloofFloorOverlap(doc);
            }
            if (toolName.Equals("aigrav_exec_option1_batch_injection", StringComparison.OrdinalIgnoreCase))'''

if target1 not in text:
    print('Error: target1 not found')
if target2 not in text:
    print('Error: target2 not found')

text = text.replace(target1, rep1, 1)
text = text.replace(target2, rep2, 1)

code_sloof = '''
    public static string AuditSloofFloorOverlap(Document doc)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sb = new StringBuilder();
        sb.AppendLine("=== AUDIT KUALITAS MODEL: OVERLAP/CLASH FAMILY SLOOF VS PELAT LANTAI ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var floors = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .Cast<Floor>()
            .ToList();

        sb.AppendLine($"\\n[1. METADATA ELEMEN]");
        sb.AppendLine($"Total Pelat Lantai (OST_Floors): {floors.Count} unit");

        var framings = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .ToList();

        var sloofs = framings.Where(f => {
            string fn = f.Symbol?.Family?.Name ?? "";
            string tn = f.Name;
            string comm = f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";
            return comm.Contains("Sloof", StringComparison.OrdinalIgnoreCase)
                || fn.Contains("Sloof", StringComparison.OrdinalIgnoreCase)
                || tn.Contains("Sloof", StringComparison.OrdinalIgnoreCase)
                || tn.StartsWith("S1", StringComparison.OrdinalIgnoreCase)
                || tn.StartsWith("S2", StringComparison.OrdinalIgnoreCase)
                || tn.StartsWith("S3", StringComparison.OrdinalIgnoreCase)
                || fn.Contains("Concrete", StringComparison.OrdinalIgnoreCase)
                || f.StructuralMaterialType == StructuralMaterialType.Concrete;
        }).ToList();

        sb.AppendLine($"Total Balok Sloof / Beton (OST_StructuralFraming): {sloofs.Count} unit");

        var geomOptions = new Options
        {
            ComputeReferences = false,
            DetailLevel = ViewDetailLevel.Fine,
            IncludeNonVisibleObjects = false
        };

        var floorData = new List<(Floor floor, string TypeName, BoundingBoxXYZ bbox, List<Solid> solids)>();
        foreach (var fl in floors)
        {
            var bbox = fl.get_BoundingBox(null);
            if (bbox == null) continue;

            var solids = new List<Solid>();
            var geom = fl.get_Geometry(geomOptions);
            if (geom != null)
            {
                foreach (var gObj in geom)
                {
                    if (gObj is Solid s && s.Volume > 0.0001) solids.Add(s);
                }
            }
            string tName = doc.GetElement(fl.GetTypeId())?.Name ?? fl.Name;
            floorData.Add((fl, tName, bbox, solids));
        }

        int totalOverlapPairs = 0;
        int unjoinedClashCount = 0;
        int joinedCount = 0;
        int floorCutsSloofCount = 0;
        int sloofCutsFloorCount = 0;
        double totalUnjoinedClashVolM3 = 0;

        var overlapDetails = new List<(long SloofId, string SloofType, string SloofLoc, long FloorId, string FloorType, bool IsJoined, string CuttingStatus, double ClashVolM3)>();

        foreach (var sl in sloofs)
        {
            var slBbox = sl.get_BoundingBox(null);
            if (slBbox == null) continue;

            string slType = doc.GetElement(sl.GetTypeId())?.Name ?? sl.Name;
            string slLoc = sl.LookupParameter("Lokasi")?.AsString() ?? "Model Utama";

            var slSolids = new List<Solid>();
            var slGeom = sl.get_Geometry(geomOptions);
            if (slGeom != null)
            {
                foreach (var gObj in slGeom)
                {
                    if (gObj is Solid s && s.Volume > 0.0001) slSolids.Add(s);
                    else if (gObj is GeometryInstance gi)
                    {
                        var instGeom = gi.GetInstanceGeometry();
                        if (instGeom != null)
                        {
                            foreach (var ig in instGeom)
                            {
                                if (ig is Solid igs && igs.Volume > 0.0001) slSolids.Add(igs);
                            }
                        }
                    }
                }
            }

            foreach (var fd in floorData)
            {
                bool bboxOverlap = !(slBbox.Max.X < fd.bbox.Min.X || slBbox.Min.X > fd.bbox.Max.X ||
                                     slBbox.Max.Y < fd.bbox.Min.Y || slBbox.Min.Y > fd.bbox.Max.Y ||
                                     slBbox.Max.Z < fd.bbox.Min.Z || slBbox.Min.Z > fd.bbox.Max.Z);

                if (!bboxOverlap) continue;

                bool isJoined = JoinGeometryUtils.AreElementsJoined(doc, sl, fd.floor);
                string cuttingStatus = "Not Joined";
                if (isJoined)
                {
                    bool slCutsFl = JoinGeometryUtils.IsCuttingElementInJoin(doc, sl, fd.floor);
                    bool flCutsSl = JoinGeometryUtils.IsCuttingElementInJoin(doc, fd.floor, sl);
                    if (slCutsFl) cuttingStatus = "Sloof memotong Pelat";
                    else if (flCutsSl) cuttingStatus = "Pelat memotong Sloof";
                    else cuttingStatus = "Joined (Unknown cut)";
                }

                double clashVolM3 = 0;
                foreach (var sSl in slSolids)
                {
                    foreach (var sFl in fd.solids)
                    {
                        try
                        {
                            var inter = BooleanOperationsUtils.ExecuteBooleanOperation(sSl, sFl, BooleanOperationsType.Intersect);
                            if (inter != null && inter.Volume > 0.0001)
                            {
                                clashVolM3 += inter.Volume * 0.028316846592;
                            }
                        }
                        catch { }
                    }
                }

                if (clashVolM3 > 0.0001 || (bboxOverlap && isJoined))
                {
                    totalOverlapPairs++;
                    if (isJoined)
                    {
                        joinedCount++;
                        if (cuttingStatus.Contains("Pelat memotong Sloof")) floorCutsSloofCount++;
                        else if (cuttingStatus.Contains("Sloof memotong Pelat")) sloofCutsFloorCount++;
                    }
                    else
                    {
                        unjoinedClashCount++;
                        totalUnjoinedClashVolM3 += clashVolM3;
                    }

                    overlapDetails.Add((sl.Id.Value, slType, slLoc, fd.floor.Id.Value, fd.TypeName, isJoined, cuttingStatus, clashVolM3));
                }
            }
        }

        sb.AppendLine($"\\n[2. RINGKASAN HASIL CLASH & OVERLAP]");
        sb.AppendLine($"Total Pasangan Overlap Terdeteksi : {totalOverlapPairs} pasangan");
        sb.AppendLine($"  - Elemen SUDAH Ter-Join (Bersih) : {joinedCount} pasangan (Sloof potong Pelat: {sloofCutsFloorCount}, Pelat potong Sloof: {floorCutsSloofCount})");
        sb.AppendLine($"  - Elemen BELUM Ter-Join (CLASH!) : {unjoinedClashCount} pasangan");
        sb.AppendLine($"  - Total Volume Clash Unjoined    : {totalUnjoinedClashVolM3:F3} m3 (Potensi Double Counting Volume Beton)");

        var byType = overlapDetails.GroupBy(x => x.SloofType)
            .Select(g => new {
                Type = g.Key,
                Total = g.Count(),
                Unjoined = g.Count(x => !x.IsJoined),
                Joined = g.Count(x => x.IsJoined),
                ClashVol = g.Where(x => !x.IsJoined).Sum(x => x.ClashVolM3)
            })
            .OrderByDescending(x => x.Unjoined)
            .ToList();

        sb.AppendLine($"\\n[3. REKAPITULASI PER TIPE FAMILY SLOOF]");
        sb.AppendLine(string.Format("{0,-25} | {1,8} | {2,12} | {3,10} | {4,16}", "Tipe Sloof", "Total", "Belum Join", "Sudah Join", "Vol Clash (m3)"));
        sb.AppendLine(new string('-', 80));
        foreach (var bt in byType)
        {
            sb.AppendLine(string.Format("{0,-25} | {1,8} | {2,12} | {3,10} | {4,16:F3}", bt.Type, bt.Total, bt.Unjoined, bt.Joined, bt.ClashVol));
        }

        sb.AppendLine($"\\n[4. DETAIL ELEMEN CLASH BELUM TER-JOIN (TOP 25 ELEMEN)]");
        var unjoinedList = overlapDetails.Where(x => !x.IsJoined).OrderByDescending(x => x.ClashVolM3).Take(25).ToList();
        if (unjoinedList.Count == 0)
        {
            sb.AppendLine(">> STATUS: PASS - Seluruh pertemuan Sloof dan Pelat Lantai telah ter-join secara rapi!");
        }
        else
        {
            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-12} | {3,-20} | {4,14}", "ID Sloof", "Tipe Sloof", "ID Pelat", "Tipe Pelat", "Vol Clash (m3)"));
            sb.AppendLine(new string('-', 88));
            foreach (var un in unjoinedList)
            {
                sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-12} | {3,-20} | {4,14:F3}", un.SloofId, un.SloofType, un.FloorId, un.FloorType, un.ClashVolM3));
            }
        }

        sb.AppendLine($"\\n[WAKTU AUDIT: {sw.ElapsedMilliseconds} ms]");
        return sb.ToString();
    }
'''

target3 = 'public static string ExecuteOption1_SharedParameterBatchInjection'
if target3 not in text:
    print('Error: target3 not found')
    sys.exit(1)

text = text.replace(target3, code_sloof + '\n    ' + target3, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully patched BimAuditSkill.cs with AuditSloofFloorOverlap')

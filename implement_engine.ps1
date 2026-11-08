$path = "C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
$txt = [System.IO.File]::ReadAllText($path)

$codeToAppend = @'

    // =========================================================================
    // MULTI-AGENT EXECUTION ENGINE: BATCH 1, BATCH 2, BATCH 3 & QA AUDIT
    // =========================================================================

    public static string CleanupOrphanedCopies(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== HOUSEKEEPING: CLEANUP 56 ORPHANED COPY SCHEDULES ===");
        var allScheds = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .ToList();

        var toDelete = allScheds.Where(s => 
            s.Name.IndexOf(" Copy ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (s.Name.StartsWith("C.3.") && (s.Name.EndsWith("(Carton & Cups Storage)") || s.Name.EndsWith("(Production Office)") || s.Name.EndsWith("(Waterline)") || s.Name.EndsWith("(Power House)") || s.Name.EndsWith("(Chiller Area 1)") || s.Name.EndsWith("(Chiller Area 2)")))
        ).ToList();

        sb.AppendLine($"Ditemukan {toDelete.Count} schedule duplikat / copy usang untuk dihapus.");
        using (Transaction tx = new(doc, "AIGRAV - Delete Orphaned Copy Schedules"))
        {
            tx.Start();
            int deleted = 0;
            foreach (var s in toDelete)
            {
                try
                {
                    doc.Delete(s.Id);
                    deleted++;
                }
                catch { }
            }
            doc.Regenerate();
            tx.Commit();
            sb.AppendLine($"Sukses menghapus {deleted} schedule sampah dari Project Browser.");
        }
        return sb.ToString();
    }

    public static string ApplyAntiDoubleCountingToBangunanUtama(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV: PENERAPAN FILTER ANTI DOUBLE-COUNTING KE BANGUNAN UTAMA ===");

        var targetNames = new string[]
        {
            "B.1.8 - Kolom Angkur",
            "B.2.1 - Kolom Baja WF 350 Mezzanine",
            "C.2.3 - Beam Mezzanine WF 350",
            "C.2.5 - Beam Mezzanine WF 250",
            "C.2.8 - Mezzanine Pelat WF 350 Endplate",
            "C.2.9 - Mezzanine Pelat WF 400 Endplate",
            "C.2.10 - Mezzanine Pelat WF 350 Endplate",
            "C.2.11 - Mezzanine Pelat WF 350 Gusset Plate",
            "C.2.12 - Mezzanine Pelat WF 300 Endplate",
            "C.2.13 - Mezzanine Pelat WF 300 Gusset Plate",
            "C.2.14 - Mezzanine Pelat WF 200 Endplate",
            "C.2.15 - Mezzanine Pelat WF 200 Gusset Plate",
            "C.2.16 - Mezzanine Baut WF 588 Endplate",
            "C.2.17 - Mezzanine Baut WF 400 Endplate",
            "C.2.18 - Mezzanine Baut WF 350 Endplate",
            "C.2.19 - Mezzanine Baut WF 350 Gusset Plate",
            "C.2.20 - Mezzanine Baut WF 300 Endplate",
            "C.2.21 - Mezzanine Baut WF 300 Gusset Plate",
            "C.2.22 - Mezzanine Baut WF 200 Endplate",
            "C.2.23 - Mezzanine Baut WF 200 Gusset Plate"
        };

        using (Transaction tx = new(doc, "AIGRAV - Anti Double Counting Filter on Bangunan Utama"))
        {
            tx.Start();
            int updated = 0;
            foreach (var name in targetNames)
            {
                var s = FindSched(doc, x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (s == null) continue;

                var def = s.Definition;
                ScheduleField? fLok = null;
                for (int i = 0; i < def.GetFieldCount(); i++)
                {
                    var f = def.GetField(i);
                    if (f.GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase))
                    {
                        fLok = f;
                        break;
                    }
                }
                if (fLok == null)
                {
                    var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Lokasi", StringComparison.OrdinalIgnoreCase));
                    if (sf != null)
                    {
                        fLok = def.AddField(sf);
                        fLok.IsHidden = true;
                    }
                }

                if (fLok != null)
                {
                    // Hapus filter Lokasi lama jika ada
                    for (int i = def.GetFilterCount() - 1; i >= 0; i--)
                    {
                        var flt = def.GetFilter(i);
                        if (flt.FieldId == fLok.FieldId)
                        {
                            def.RemoveFilter(i);
                        }
                    }

                    // Tambahkan filter Lokasi = ""
                    try
                    {
                        def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, ""));
                        updated++;
                        sb.AppendLine($"  ✓ {name,-42} : Berhasil diterapkan filter Lokasi=\"\"");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  ✕ {name,-42} : Gagal AddFilter ({ex.Message})");
                    }
                }
            }
            doc.Regenerate();
            tx.Commit();
            sb.AppendLine($"\nTotal schedule Bangunan Utama terisolasi dari double-counting: {updated}");
        }
        return sb.ToString();
    }

    private static (ViewSchedule? vs, string err) CreateStructuredSchedule(
        Document doc,
        ViewSchedule? srcTemplate,
        BuiltInCategory bic,
        string newName,
        string lokName,
        string itemKind,
        string profileFilter)
    {
        try
        {
            var old = FindSched(doc, s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
            if (old != null) doc.Delete(old.Id);

            ViewSchedule vs;
            if (srcTemplate != null)
            {
                ElementId newId = srcTemplate.Duplicate(ViewDuplicateOption.Duplicate);
                vs = (ViewSchedule)doc.GetElement(newId);
                vs.Name = newName;
            }
            else
            {
                ElementId catId = new ElementId(bic);
                if (!ViewSchedule.IsValidCategoryForSchedule(catId))
                {
                    return (null, $"Kategori {bic} tidak valid untuk schedule");
                }
                vs = ViewSchedule.CreateSchedule(doc, catId);
                vs.Name = newName;
            }

            var def = vs.Definition;
            def.IsItemized = false;
            def.ClearFilters();

            // 1. Pastikan Field 'Lokasi' ada dan terfilter
            ScheduleField? fLok = null;
            for (int i = 0; i < def.GetFieldCount(); i++)
            {
                var f = def.GetField(i);
                if (f.GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase))
                {
                    fLok = f;
                    break;
                }
            }
            if (fLok == null)
            {
                var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Lokasi", StringComparison.OrdinalIgnoreCase));
                if (sf != null)
                {
                    fLok = def.AddField(sf);
                }
            }
            if (fLok != null)
            {
                fLok.IsHidden = true;
                def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, lokName));
            }

            // 2. Filter Spesifik Profil / Sambungan
            if (!string.IsNullOrWhiteSpace(profileFilter))
            {
                if (itemKind == "Kolom" || itemKind == "Balok")
                {
                    ScheduleField? fType = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Type", StringComparison.OrdinalIgnoreCase) || f.GetName().Equals("Family and Type", StringComparison.OrdinalIgnoreCase))
                        {
                            fType = f;
                            break;
                        }
                    }
                    if (fType == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Type", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fType = def.AddField(sf);
                    }
                    if (fType != null)
                    {
                        def.AddFilter(new ScheduleFilter(fType.FieldId, ScheduleFilterType.Contains, profileFilter));
                    }
                }
                else if (itemKind == "Connection")
                {
                    ScheduleField? fTypeConn = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0 || f.GetName().IndexOf("Tipe Sambungan", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            fTypeConn = f;
                            break;
                        }
                    }
                    if (fTypeConn == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (sf != null) fTypeConn = def.AddField(sf);
                    }
                    if (fTypeConn != null)
                    {
                        fTypeConn.ColumnHeading = "Tipe Sambungan";
                        def.AddFilter(new ScheduleFilter(fTypeConn.FieldId, ScheduleFilterType.Contains, profileFilter));
                    }
                }
                else if (itemKind == "Baseplate")
                {
                    ScheduleField? fComm = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase))
                        {
                            fComm = f;
                            break;
                        }
                    }
                    if (fComm == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Comments", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fComm = def.AddField(sf);
                    }
                    if (fComm != null)
                    {
                        def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, profileFilter));
                    }
                }
            }

            return (vs, "");
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public static string ExecuteBatch1_CartonCupsAndProductionOffice(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SUB-AGENT 1 (BATCH 1): CARTON & CUPS STORAGE & PRODUCTION OFFICE ===");

        var srcKolom = doc.GetElement(new ElementId(851714)) as ViewSchedule; // B.2.1
        var srcBalok = doc.GetElement(new ElementId(851763)) as ViewSchedule; // C.2.3
        var srcPelat = doc.GetElement(new ElementId(6862604)) as ViewSchedule; // C.2.10
        var srcAngkur = doc.GetElement(new ElementId(672435)) as ViewSchedule; // B.1.8

        var items = new List<(string Name, string Lokasi, string Kind, string Filter)>
        {
            // Carton & Cups Storage (C.3.3)
            ("C.3.3.1 Carton & Cups Storage - Kolom Baja H 250", "Carton & Cups Storage", "Kolom", "H 250"),
            ("C.3.3.2 Carton & Cups Storage - Kolom Baja H 200", "Carton & Cups Storage", "Kolom", "H 200"),
            ("C.3.3.3 Carton & Cups Storage - Kolom Baja WF 200", "Carton & Cups Storage", "Kolom", "WF 200"),
            ("C.3.3.4 Carton & Cups Storage - Balok Baja WF 350", "Carton & Cups Storage", "Balok", "WF 350"),
            ("C.3.3.5 Carton & Cups Storage - Balok Baja WF 400", "Carton & Cups Storage", "Balok", "WF 400"),
            ("C.3.3.6 Carton & Cups Storage - Balok Baja WF 300", "Carton & Cups Storage", "Balok", "WF 300"),
            ("C.3.3.7 Carton & Cups Storage - Balok Baja WF 250", "Carton & Cups Storage", "Balok", "WF 250"),
            ("C.3.3.8 Carton & Cups Storage - Connection Steel WF350-Endplate", "Carton & Cups Storage", "Connection", "WF350-Endplate"),
            ("C.3.3.9 Carton & Cups Storage - Connection Steel WF350-Gusset Plate", "Carton & Cups Storage", "Connection", "WF350-Gusset"),
            ("C.3.3.10 Carton & Cups Storage - Connection Steel WF-400 Endplate", "Carton & Cups Storage", "Connection", "WF-400 Endplate"),
            ("C.3.3.11 Carton & Cups Storage - Connection Steel WF300-Endplate", "Carton & Cups Storage", "Connection", "WF300-Endplate"),
            ("C.3.3.12 Carton & Cups Storage - Connection Steel WF200-Baseplate Mezzanine", "Carton & Cups Storage", "Connection", "WF200-Baseplate"),
            ("C.3.3.13 Carton & Cups Storage - Connection Steel WF200-Endplate", "Carton & Cups Storage", "Connection", "WF200-Endplate"),
            ("C.3.3.14 Carton & Cups Storage - Connection Steel WF200-Gusset Plate", "Carton & Cups Storage", "Connection", "WF200-Gusset"),
            ("C.3.3.15 Carton & Cups Storage - Baseplate Kolom t=16", "Carton & Cups Storage", "Baseplate", "Baseplate"),
            ("C.3.3.16 Carton & Cups Storage - Angkur Kolom Dia 19mm", "Carton & Cups Storage", "Angkur", ""),

            // Production Office (C.3.4)
            ("C.3.4.1 Production Office - Kolom Baja WF 350", "Production Office", "Kolom", "WF 350"),
            ("C.3.4.2 Production Office - Balok Baja L 40x40x4", "Production Office", "Balok", "L 40"),
            ("C.3.4.3 Production Office - Balok Baja WF 350", "Production Office", "Balok", "WF 350"),
            ("C.3.4.4 Production Office - Balok Baja WF 250", "Production Office", "Balok", "WF 250"),
            ("C.3.4.5 Production Office - Balok Baja UNP 200", "Production Office", "Balok", "UNP 200"),
            ("C.3.4.6 Production Office - Connection Steel WF350-Endplate", "Production Office", "Connection", "WF350-Endplate"),
            ("C.3.4.7 Production Office - Connection Steel WF200-Gusset Plate", "Production Office", "Connection", "WF200-Gusset"),
            ("C.3.4.8 Production Office - Connection Steel WF350-Gusset Plate", "Production Office", "Connection", "WF350-Gusset"),
            ("C.3.4.9 Production Office - Baseplate Kolom t=12", "Production Office", "Baseplate", "Baseplate"),
            ("C.3.4.10 Production Office - Baseplate Kolom t=19", "Production Office", "Baseplate", "Baseplate"),
            ("C.3.4.11 Production Office - Angkur Kolom Dia 19mm", "Production Office", "Angkur", "")
        };

        using (Transaction tx = new(doc, "AIGRAV - Execute Batch 1 Schedules"))
        {
            tx.Start();
            int success = 0;
            foreach (var item in items)
            {
                ViewSchedule? src = item.Kind == "Kolom" ? srcKolom :
                                    item.Kind == "Balok" ? srcBalok :
                                    item.Kind == "Baseplate" ? srcPelat :
                                    item.Kind == "Angkur" ? srcAngkur : null;

                BuiltInCategory bic = item.Kind == "Kolom" ? BuiltInCategory.OST_StructuralColumns :
                                      item.Kind == "Balok" ? BuiltInCategory.OST_StructuralFraming :
                                      item.Kind == "Baseplate" ? BuiltInCategory.OST_StructConnectionPlates :
                                      item.Kind == "Angkur" ? BuiltInCategory.OST_StructConnectionAnchors :
                                      BuiltInCategory.OST_StructConnections;

                var (vs, err) = CreateStructuredSchedule(doc, src, bic, item.Name, item.Lokasi, item.Kind, item.Filter);
                if (vs != null)
                {
                    success++;
                    sb.AppendLine($"  ✓ Berhasil: {item.Name}");
                }
                else
                {
                    sb.AppendLine($"  ✕ Gagal: {item.Name} ({err})");
                }
            }
            doc.Regenerate();
            tx.Commit();
            sb.AppendLine($"\nTotal Batch 1 Sukses Dibuat: {success} / {items.Count}");
        }
        return sb.ToString();
    }

    public static string ExecuteBatch2_WaterlineAndPowerHouse(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SUB-AGENT 2 (BATCH 2): WATERLINE & POWER HOUSE ===");

        var srcKolom = doc.GetElement(new ElementId(851714)) as ViewSchedule;
        var srcBalok = doc.GetElement(new ElementId(851763)) as ViewSchedule;
        var srcPelat = doc.GetElement(new ElementId(6862604)) as ViewSchedule;
        var srcAngkur = doc.GetElement(new ElementId(672435)) as ViewSchedule;

        var items = new List<(string Name, string Lokasi, string Kind, string Filter)>
        {
            // Waterline (C.3.1)
            ("C.3.1.1 Waterline - Balok Baja WF 350", "Waterline", "Balok", "WF 350"),
            ("C.3.1.2 Waterline - Balok Baja WF 250", "Waterline", "Balok", "WF 250"),
            ("C.3.1.3 Waterline - Balok Baja WF 400", "Waterline", "Balok", "WF 400"),
            ("C.3.1.4 Waterline - Balok Baja WF 882", "Waterline", "Balok", "WF 882"),
            ("C.3.1.5 Waterline - Balok Baja WF 300", "Waterline", "Balok", "WF 300"),
            ("C.3.1.6 Waterline - Balok Baja RCWF.588", "Waterline", "Balok", "RCWF"),
            ("C.3.1.7 Waterline - Balok Baja WF 588", "Waterline", "Balok", "WF 588"),
            ("C.3.1.8 Waterline - Connection Steel WF350-Endplate", "Waterline", "Connection", "WF350-Endplate"),
            ("C.3.1.9 Waterline - Connection Steel WF350-Gusset Plate", "Waterline", "Connection", "WF350-Gusset"),
            ("C.3.1.10 Waterline - Connection Steel WF-400 Endplate", "Waterline", "Connection", "WF-400 Endplate"),
            ("C.3.1.11 Waterline - Connection Steel WF200-Gusset Plate", "Waterline", "Connection", "WF200-Gusset"),
            ("C.3.1.12 Waterline - Connection Steel WF588-Endplate", "Waterline", "Connection", "WF588-Endplate"),
            ("C.3.1.13 Waterline - Connection Steel WF200-Endplate", "Waterline", "Connection", "WF200-Endplate"),
            ("C.3.1.14 Waterline - Connection Steel WF300-Endplate", "Waterline", "Connection", "WF300-Endplate"),

            // Power House (C.3.2)
            ("C.3.2.1 Power House - Kolom Baja H 200", "Power House", "Kolom", "H 200"),
            ("C.3.2.2 Power House - Balok Baja WF 250", "Power House", "Balok", "WF 250"),
            ("C.3.2.3 Power House - Balok Baja WF 350", "Power House", "Balok", "WF 350"),
            ("C.3.2.4 Power House - Connection Steel WF200-Endplate", "Power House", "Connection", "WF200-Endplate"),
            ("C.3.2.5 Power House - Connection Steel WF200-Gusset Plate", "Power House", "Connection", "WF200-Gusset"),
            ("C.3.2.6 Power House - Connection Steel WF350-Endplate", "Power House", "Connection", "WF350-Endplate"),
            ("C.3.2.7 Power House - Baseplate Kolom t=16", "Power House", "Baseplate", "Baseplate"),
            ("C.3.2.8 Power House - Angkur Kolom Dia 19mm", "Power House", "Angkur", "")
        };

        using (Transaction tx = new(doc, "AIGRAV - Execute Batch 2 Schedules"))
        {
            tx.Start();
            int success = 0;
            foreach (var item in items)
            {
                ViewSchedule? src = item.Kind == "Kolom" ? srcKolom :
                                    item.Kind == "Balok" ? srcBalok :
                                    item.Kind == "Baseplate" ? srcPelat :
                                    item.Kind == "Angkur" ? srcAngkur : null;

                BuiltInCategory bic = item.Kind == "Kolom" ? BuiltInCategory.OST_StructuralColumns :
                                      item.Kind == "Balok" ? BuiltInCategory.OST_StructuralFraming :
                                      item.Kind == "Baseplate" ? BuiltInCategory.OST_StructConnectionPlates :
                                      item.Kind == "Angkur" ? BuiltInCategory.OST_StructConnectionAnchors :
                                      BuiltInCategory.OST_StructConnections;

                var (vs, err) = CreateStructuredSchedule(doc, src, bic, item.Name, item.Lokasi, item.Kind, item.Filter);
                if (vs != null)
                {
                    success++;
                    sb.AppendLine($"  ✓ Berhasil: {item.Name}");
                }
                else
                {
                    sb.AppendLine($"  ✕ Gagal: {item.Name} ({err})");
                }
            }
            doc.Regenerate();
            tx.Commit();
            sb.AppendLine($"\nTotal Batch 2 Sukses Dibuat: {success} / {items.Count}");
        }
        return sb.ToString();
    }

    public static string ExecuteBatch3_ChillerAndHousekeeping(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SUB-AGENT 3 (BATCH 3): CHILLER 1 & 2 + HOUSEKEEPING + ANTI-DOUBLE COUNTING ===");

        // 1. Housekeeping: Hapus schedule sampah
        sb.AppendLine(CleanupOrphanedCopies(doc));

        // 2. Terapkan Anti-Double Counting ke Bangunan Utama
        sb.AppendLine(ApplyAntiDoubleCountingToBangunanUtama(doc));

        // 3. Buat Schedule Chiller Area 1 & Chiller Area 2
        var srcKolom = doc.GetElement(new ElementId(851714)) as ViewSchedule;
        var srcBalok = doc.GetElement(new ElementId(851763)) as ViewSchedule;
        var srcPelat = doc.GetElement(new ElementId(6862604)) as ViewSchedule;
        var srcAngkur = doc.GetElement(new ElementId(672435)) as ViewSchedule;

        var items = new List<(string Name, string Lokasi, string Kind, string Filter)>
        {
            // Chiller Area 1 (C.3.5)
            ("C.3.5.1 Chiller Area 1 - Kolom Baja H 200", "Chiller Area 1", "Kolom", "H 200"),
            ("C.3.5.2 Chiller Area 1 - Balok Baja WF 350", "Chiller Area 1", "Balok", "WF 350"),
            ("C.3.5.3 Chiller Area 1 - Balok Baja WF 300", "Chiller Area 1", "Balok", "WF 300"),
            ("C.3.5.4 Chiller Area 1 - Connection Steel WF350-Gusset Plate", "Chiller Area 1", "Connection", "WF350-Gusset"),
            ("C.3.5.5 Chiller Area 1 - Connection Steel WF350-Endplate", "Chiller Area 1", "Connection", "WF350-Endplate"),
            ("C.3.5.6 Chiller Area 1 - Connection Steel WF300-Endplate", "Chiller Area 1", "Connection", "WF300-Endplate"),
            ("C.3.5.7 Chiller Area 1 - Baseplate Kolom t=16", "Chiller Area 1", "Baseplate", "Baseplate"),
            ("C.3.5.8 Chiller Area 1 - Angkur Kolom Dia 19mm", "Chiller Area 1", "Angkur", ""),

            // Chiller Area 2 (C.3.6)
            ("C.3.6.1 Chiller Area 2 - Kolom Baja H 200", "Chiller Area 2", "Kolom", "H 200"),
            ("C.3.6.2 Chiller Area 2 - Balok Baja WF 350", "Chiller Area 2", "Balok", "WF 350"),
            ("C.3.6.3 Chiller Area 2 - Balok Baja WF 300", "Chiller Area 2", "Balok", "WF 300"),
            ("C.3.6.4 Chiller Area 2 - Connection Steel WF350-Gusset Plate", "Chiller Area 2", "Connection", "WF350-Gusset"),
            ("C.3.6.5 Chiller Area 2 - Connection Steel WF300-Endplate", "Chiller Area 2", "Connection", "WF300-Endplate"),
            ("C.3.6.6 Chiller Area 2 - Connection Steel WF350-Endplate", "Chiller Area 2", "Connection", "WF350-Endplate"),
            ("C.3.6.7 Chiller Area 2 - Baseplate Kolom t=16", "Chiller Area 2", "Baseplate", "Baseplate"),
            ("C.3.6.8 Chiller Area 2 - Angkur Kolom Dia 19mm", "Chiller Area 2", "Angkur", "")
        };

        using (Transaction tx = new(doc, "AIGRAV - Execute Batch 3 Schedules"))
        {
            tx.Start();
            int success = 0;
            foreach (var item in items)
            {
                ViewSchedule? src = item.Kind == "Kolom" ? srcKolom :
                                    item.Kind == "Balok" ? srcBalok :
                                    item.Kind == "Baseplate" ? srcPelat :
                                    item.Kind == "Angkur" ? srcAngkur : null;

                BuiltInCategory bic = item.Kind == "Kolom" ? BuiltInCategory.OST_StructuralColumns :
                                      item.Kind == "Balok" ? BuiltInCategory.OST_StructuralFraming :
                                      item.Kind == "Baseplate" ? BuiltInCategory.OST_StructConnectionPlates :
                                      item.Kind == "Angkur" ? BuiltInCategory.OST_StructConnectionAnchors :
                                      BuiltInCategory.OST_StructConnections;

                var (vs, err) = CreateStructuredSchedule(doc, src, bic, item.Name, item.Lokasi, item.Kind, item.Filter);
                if (vs != null)
                {
                    success++;
                    sb.AppendLine($"  ✓ Berhasil: {item.Name}");
                }
                else
                {
                    sb.AppendLine($"  ✕ Gagal: {item.Name} ({err})");
                }
            }
            doc.Regenerate();
            tx.Commit();
            sb.AppendLine($"\nTotal Batch 3 Sukses Dibuat: {success} / {items.Count}");
        }
        return sb.ToString();
    }

    public static string QaAuditAllLocationSchedules(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SUB-AGENT QA/QC AUDITOR: VERIFIKASI LENGKAP HASIL SCHEDULE & ANTI-DOUBLE COUNTING ===");

        var allScheds = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .ToList();

        var locScheds = allScheds.Where(s => s.Name.StartsWith("C.3.")).OrderBy(s => s.Name).ToList();
        sb.AppendLine($"\n[1. REKAPITULASI TOTAL SCHEDULE LOKASI]");
        sb.AppendLine($"Total Schedule C.3.* Ditemukan: {locScheds.Count} schedule (Target: 65 schedule)");

        int totalPass = 0;
        int totalZero = 0;
        int totalFilterIssues = 0;

        sb.AppendLine($"\n[2. STATUS FILTER & ELEMEN TERJADWAL PER SCHEDULE]");
        sb.AppendLine(string.Format("{0,-65} | {1,-10} | {2,-12} | {3}", "Nama Schedule", "Kategori", "Qty Elemen", "Status Filter"));
        sb.AppendLine(new string('-', 115));

        foreach (var s in locScheds)
        {
            var def = s.Definition;
            int filterCount = def.GetFilterCount();
            int elemCount = 0;
            try
            {
                elemCount = new FilteredElementCollector(doc, s.Id).WhereElementIsNotElementType().GetElementCount();
            }
            catch { }

            string filterStatus = filterCount >= 1 ? $"PASS ({filterCount} Filter)" : "FAIL (No Filter)";
            if (filterCount < 1) totalFilterIssues++;
            if (elemCount == 0) totalZero++; else totalPass++;

            string catName = s.Category?.Name ?? def.CategoryId.Value.ToString();
            sb.AppendLine(string.Format("{0,-65} | {1,-10} | {2,12} | {3}", s.Name, catName, elemCount, filterStatus));
        }

        sb.AppendLine(new string('-', 115));
        sb.AppendLine($"Ringkasan QA: {totalPass} Berisi Elemen, {totalZero} Kosong, {totalFilterIssues} Masalah Filter.");

        // 3. Verifikasi Bangunan Utama Anti Double-Counting
        sb.AppendLine("\n[3. VERIFIKASI ANTI DOUBLE-COUNTING BANGUNAN UTAMA]");
        var schedC23 = FindSched(doc, s => s.Name.Equals("C.2.3 - Beam Mezzanine WF 350", StringComparison.OrdinalIgnoreCase));
        if (schedC23 != null)
        {
            int c23Count = new FilteredElementCollector(doc, schedC23.Id).WhereElementIsNotElementType().GetElementCount();
            int c23Filters = schedC23.Definition.GetFilterCount();
            sb.AppendLine($"Schedule 'C.2.3 - Beam Mezzanine WF 350': {c23Count} elemen terjadwal | {c23Filters} filter aktif (Lokasi=\"\")");
        }

        var copyCount = allScheds.Count(s => s.Name.IndexOf(" Copy ", StringComparison.OrdinalIgnoreCase) >= 0);
        sb.AppendLine($"Schedule 'Copy' tersisa di Project Browser: {copyCount} (Wajib: 0)");

        return sb.ToString();
    }
'@

$lastBrace = $txt.LastIndexOf('}')
if ($lastBrace -gt 0) {
    $newTxt = $txt.Substring(0, $lastBrace) + $codeToAppend + "`n}"
    [System.IO.File]::WriteAllText($path, $newTxt, [System.Text.Encoding]::UTF8)
    Write-Output "Successfully appended batch execution engine to BimAuditSkill.cs"
} else {
    Write-Error "Could not find closing brace of BimAuditSkill.cs"
}

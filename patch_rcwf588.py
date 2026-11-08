import os

path = r"C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
with open(path, "r", encoding="utf-8") as f:
    code = f.read()

# 1. Add to CanHandle
old_ch = 'return toolName.Equals("aigrav_comprehensive_audit", StringComparison.OrdinalIgnoreCase)'
new_ch = '''return toolName.Equals("aigrav_create_rcwf_588", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("create_rcwf_588", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_comprehensive_audit", StringComparison.OrdinalIgnoreCase)'''

if old_ch in code:
    code = code.replace(old_ch, new_ch, 1)
    print("Updated CanHandle")

# 2. Add to Execute
old_ex = 'if (toolName.Equals("aigrav_tag_wf400_endplate", StringComparison.OrdinalIgnoreCase)'
new_ex = '''if (toolName.Equals("aigrav_create_rcwf_588", StringComparison.OrdinalIgnoreCase)
                || toolName.Equals("create_rcwf_588", StringComparison.OrdinalIgnoreCase))
            {
                return CreateRCWF588Type(doc);
            }

            if (toolName.Equals("aigrav_tag_wf400_endplate", StringComparison.OrdinalIgnoreCase)'''

if old_ex in code:
    code = code.replace(old_ex, new_ex, 1)
    print("Updated Execute")

# 3. Add method CreateRCWF588Type right before the last closing brace
method_code = '''    private static string CreateRCWF588Type(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("           PEMBUATAN PROFIL BARU: RCWF 588 (CASTELLATED BEAM)                   ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Project : {doc.Title}");
        sb.AppendLine($"Waktu   : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\\n");

        Family? family = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .FirstOrDefault(f => f.Name.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase));

        if (family == null)
        {
            string famPath = @"E:\\BIM SMU\\Family Revit\\Castellated Beam.rfa";
            if (File.Exists(famPath))
            {
                using (Transaction txLoad = new(doc, "Load Castellated Beam Family"))
                {
                    txLoad.Start();
                    doc.LoadFamily(famPath, out family);
                    txLoad.Commit();
                }
                sb.AppendLine($"* Berhasil memuat Family 'Castellated Beam' dari disk ({famPath}).");
            }
        }

        if (family == null)
        {
            return "Gagal: Family 'Castellated Beam' tidak ditemukan di model maupun di folder 'E:\\\\BIM SMU\\\\Family Revit\\\\'.";
        }

        var symbols = family.GetFamilySymbolIds()
            .Select(id => doc.GetElement(id) as FamilySymbol)
            .Where(s => s != null)
            .ToList();

        if (symbols.Count == 0)
        {
            return "Gagal: Tidak ada tipe FamilySymbol yang tersedia pada family 'Castellated Beam'.";
        }

        var baseSymbol = symbols.FirstOrDefault(s => s.Name.Contains("400"))
                      ?? symbols.FirstOrDefault(s => s.Name.Contains("450"))
                      ?? symbols.FirstOrDefault(s => s.Name.Contains("350"))
                      ?? symbols.First();

        sb.AppendLine($"* Family Target   : '{family.Name}'");
        sb.AppendLine($"* Base Type Acuan : '{baseSymbol?.Name}'");
        sb.AppendLine();

        using (Transaction tx = new(doc, "AIGRAV - Buat Profil RCWF 588"))
        {
            tx.Start();

            string[] targetNames = new string[] { "RCWF 588", "RCWF.588" };
            foreach (var targetName in targetNames)
            {
                FamilySymbol? target = symbols.FirstOrDefault(s => s.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                bool isNew = false;
                if (target == null)
                {
                    target = baseSymbol!.Duplicate(targetName) as FamilySymbol;
                    isNew = true;
                    sb.AppendLine($"* [BARU] Berhasil membuat tipe: '{targetName}'");
                }
                else
                {
                    sb.AppendLine($"* [UPDATE] Tipe '{targetName}' sudah ada, menyelaraskan parameter...");
                }

                if (target == null) continue;

                Action<string, double> setMm = (paramName, mmVal) => {
                    Parameter? p = target.LookupParameter(paramName);
                    if (p != null && !p.IsReadOnly)
                    {
                        p.Set(mmVal / 304.8);
                    }
                };

                Action<string, double> setDeg = (paramName, degVal) => {
                    Parameter? p = target.LookupParameter(paramName);
                    if (p != null && !p.IsReadOnly)
                    {
                        p.Set(degVal * Math.PI / 180.0);
                    }
                };

                // 1. Profil Induk WF 588x300x12x20 (JIS / SNI Gunung Garuda Standard)
                setMm("d1", 588.0);
                setMm("bf1", 300.0);
                setMm("tf1", 20.0);
                setMm("tw1", 12.0);

                setMm("d2", 588.0);
                setMm("bf2", 300.0);
                setMm("tf2", 20.0);
                setMm("tw2", 12.0);

                // 2. Dimensi Geometri Honeycomb / Castellated
                setMm("d", 882.0);      // Total Depth = 1.5 x 588
                setMm("ho", 588.0);     // Opening Height = d1
                setMm("e1", 147.0);     // Stem Tee Height = (882 - 588)/2
                setMm("e2", 147.0);
                setMm("h1", 294.0);     // Half Parent Depth = 588/2
                setMm("h2", 294.0);
                setMm("dt1", 147.0);
                setMm("dt2", 147.0);
                setMm("b", 171.0);      // Web Post Width = 0.29 x 588
                setMm("wo", 487.0);     // Opening Width = 0.8275 x 588
                setMm("Spacing", 634.0);// Pitch Spacing = 1.0775 x 588
                setDeg("phi1", 60.0);   // Cutting Angle = 60 deg
                setDeg("phi2", 60.0);

                // 3. Metadata & Identity
                target.LookupParameter("Type Comments")?.Set("AIGRAV Steel DB | CWF 588 (WF 588x300)");
                target.LookupParameter("Comments")?.Set("CWF 588");
                target.LookupParameter("Description")?.Set("Castellated Wide Flange 882x300x12x20 (Profil Asal WF 588)");

                sb.AppendLine($"    -> Dimensi: d=882mm (Expanded), ho=588mm, bf=300mm, tf=20mm, tw=12mm");
                sb.AppendLine($"    -> Geometri Lubang: wo=487mm, b=171mm, Spacing=634mm, phi=60°");
            }

            tx.Commit();
        }

        sb.AppendLine("\\n[HASIL]: Profil 'RCWF 588' & 'RCWF.588' berhasil dibuat dan siap digunakan di model Revit!");
        return sb.ToString();
    }
'''

last_brace = code.rfind('}')
if last_brace != -1:
    code = code[:last_brace] + method_code + "\n}\n"
    print("Appended CreateRCWF588Type method")

with open(path, "w", encoding="utf-8") as f:
    f.write(code)

print("Finished updating BimAuditSkill.cs")

import sys

path = r"C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
with open(path, "r", encoding="utf-8") as f:
    lines = f.readlines()

clean_code = ''.join(lines[:374]) + '''
    private static string CreateRCWF588Type(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== PEMBUATAN PROFIL BARU: RCWF 588 ===");
        Family? family = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .FirstOrDefault(f => f.Name.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase));

        if (family == null) return "Family Castellated Beam tidak ditemukan.";

        var symbols = family.GetFamilySymbolIds()
            .Select(id => doc.GetElement(id) as FamilySymbol)
            .Where(s => s != null)
            .ToList();

        if (symbols.Count == 0) return "Tidak ada tipe di family Castellated Beam.";

        var baseSymbol = symbols.FirstOrDefault(s => s.Name.Contains("400"))
                      ?? symbols.FirstOrDefault(s => s.Name.Contains("450"))
                      ?? symbols.FirstOrDefault(s => s.Name.Contains("350"))
                      ?? symbols.First();

        using (Transaction tx = new(doc, "AIGRAV - Buat Profil RCWF 588"))
        {
            tx.Start();
            string[] targetNames = new string[] { "RCWF 588", "RCWF.588" };
            foreach (var targetName in targetNames)
            {
                FamilySymbol? target = symbols.FirstOrDefault(s => s.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                {
                    target = baseSymbol!.Duplicate(targetName) as FamilySymbol;
                }
                if (target == null) continue;

                Action<string, double> setMm = (paramName, mmVal) => {
                    Parameter? p = target.LookupParameter(paramName);
                    if (p != null && !p.IsReadOnly) p.Set(mmVal / 304.8);
                };

                Action<string, double> setDeg = (paramName, degVal) => {
                    Parameter? p = target.LookupParameter(paramName);
                    if (p != null && !p.IsReadOnly) p.Set(degVal * Math.PI / 180.0);
                };

                setMm("d1", 588.0);
                setMm("bf1", 300.0);
                setMm("tf1", 20.0);
                setMm("tw1", 12.0);
                setMm("d2", 588.0);
                setMm("bf2", 300.0);
                setMm("tf2", 20.0);
                setMm("tw2", 12.0);
                setMm("d", 882.0);
                setMm("ho", 588.0);
                setMm("e1", 147.0);
                setMm("e2", 147.0);
                setMm("h1", 294.0);
                setMm("h2", 294.0);
                setMm("dt1", 147.0);
                setMm("dt2", 147.0);
                setMm("b", 171.0);
                setMm("wo", 487.0);
                setMm("Spacing", 634.0);
                setDeg("phi1", 60.0);
                setDeg("phi2", 60.0);

                target.LookupParameter("Type Comments")?.Set("AIGRAV Steel DB | CWF 588 (WF 588x300)");
                target.LookupParameter("Comments")?.Set("CWF 588");
                target.LookupParameter("Description")?.Set("Castellated Wide Flange 882x300x12x20 (Profil Asal WF 588)");
            }
            tx.Commit();
        }
        sb.AppendLine("Profil RCWF 588 berhasil disinkronkan.");
        return sb.ToString();
    }
}
'''

with open(path, "w", encoding="utf-8") as f:
    f.write(clean_code)

print("Cleanly restored BimAuditSkill.cs")

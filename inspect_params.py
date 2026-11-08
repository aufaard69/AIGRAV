import urllib.request
import json

path = r"C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
with open(path, "r", encoding="utf-8") as f:
    code = f.read()

# Let's inspect what parameters are missing in CreateRCWF588Type
# We can replace CreateRCWF588Type temporarily to dump all parameters of RCWF 588 and baseSymbol
start_str = "private static string CreateRCWF588Type(Document doc)"
idx = code.find(start_str)

new_func = '''private static string CreateRCWF588Type(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== INSPEKSI STRUCTURAL SECTION PARAMETERS: CASTELLATED BEAM ===");

        Family? family = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .FirstOrDefault(f => f.Name.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase));

        if (family == null) return "Family 'Castellated Beam' tidak ditemukan.";

        var symbols = family.GetFamilySymbolIds()
            .Select(id => doc.GetElement(id) as FamilySymbol)
            .Where(s => s != null)
            .ToList();

        foreach (var sym in symbols)
        {
            sb.AppendLine($"\\n--- TIPE: '{sym.Name}' (Id: {sym.Id.Value}) ---");
            foreach (Parameter p in sym.Parameters)
            {
                string val = p.AsValueString() ?? p.AsString() ?? (p.StorageType == StorageType.Double ? (p.AsDouble() * 304.8).ToString("F1") + " mm" : p.AsInteger().ToString());
                sb.AppendLine($"  [{p.Definition.GetGroupTypeId().TypeId}] {p.Definition.Name} = '{val}' (HasValue: {p.HasValue}, ReadOnly: {p.IsReadOnly})");
            }
        }

        return sb.ToString();
    }'''

end_str = "        return sb.ToString();\n    }"
end_idx = code.find(end_str, idx) + len(end_str)

code = code[:idx] + new_func + code[end_idx:]
with open(path, "w", encoding="utf-8") as f:
    f.write(code)

print("Updated BimAuditSkill.cs to inspect parameters.")

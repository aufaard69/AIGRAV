import urllib.request, json

code = '''
using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace AIGRAV.Skills.BimAudit;

public static class EmptyLokasiAudit
{
    public static string Inspect68Framings(Document doc)
    {
        var framings = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()))
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Total Framing Lokasi Kosong: {framings.Count}");
        var grouped = framings.GroupBy(f => new {
            Type = doc.GetElement(f.GetTypeId())?.Name ?? f.Name,
            Comm = f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "<KOSONG>"
        });

        foreach (var g in grouped)
        {
            sb.AppendLine($"  - Type: '{g.Key.Type}' | Comm: '{g.Key.Comm}' | Count: {g.Count()}");
        }
        return sb.ToString();
    }
}
'''
print("Ready to patch or inspect")

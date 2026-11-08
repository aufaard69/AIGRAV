using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class BimAuditSkill
{
    public static string AuditExistingTagsInView(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AUDIT EXISTING ROOM TAGS DI VIEW DENAH BANGUNAN UTAMA ===");

        var view = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(v => v.Name.Equals("DENAH BANGUNAN UTAMA", StringComparison.OrdinalIgnoreCase) && !v.IsTemplate);

        if (view == null) return "View not found";

        var tags = new FilteredElementCollector(doc, view.Id)
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .WhereElementIsNotElementType()
            .Cast<RoomTag>()
            .ToList();

        sb.AppendLine($"Total Room Tags saat ini di View: {tags.Count}");

        var linkInstance = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .FirstOrDefault(l => l.Name.Contains("NTS-SI_01-BU-AR", StringComparison.OrdinalIgnoreCase) || (l.GetLinkDocument() != null && l.GetLinkDocument().Title.Contains("NTS-SI_01-BU-AR", StringComparison.OrdinalIgnoreCase)));

        Document? linkDoc = linkInstance?.GetLinkDocument();

        var taggedRoomIds = new HashSet<long>();

        foreach (var t in tags)
        {
            var sym = doc.GetElement(t.GetTypeId()) as FamilySymbol;
            string symName = sym != null ? $"{sym.FamilyName} : {sym.Name}" : "Unknown";

            long rIdVal = -1;
            string roomDetails = "Linked Room";

            try
            {
                var rId = t.TaggedLocalRoomId;
                if (rId != null && rId != ElementId.InvalidElementId)
                {
                    rIdVal = rId.Value;
                    taggedRoomIds.Add(rIdVal);

                    if (linkDoc != null)
                    {
                        var r = linkDoc.GetElement(rId) as Room;
                        if (r != null)
                        {
                            roomDetails = $"#{r.Number} '{r.Name}'";
                        }
                    }
                }
            }
            catch { }

            sb.AppendLine($"  - Tag Id {t.Id.Value} | Tipe: '{symName}' | Room: {roomDetails} (Id: {rIdVal}) | Head: ({t.TagHeadPosition.X*304.8:F0}, {t.TagHeadPosition.Y*304.8:F0})");
        }

        if (linkDoc != null)
        {
            var rooms = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0 && r.Level != null && r.Level.Name.Equals("DENAH BANGUNAN UTAMA", StringComparison.OrdinalIgnoreCase))
                .ToList();

            sb.AppendLine($"\nRekap 20 Ruangan Level 'DENAH BANGUNAN UTAMA':");
            int sudah = 0, belum = 0;
            foreach (var r in rooms)
            {
                bool isTagged = taggedRoomIds.Contains(r.Id.Value);
                if (isTagged) sudah++; else belum++;
                string tagStatus = isTagged ? "[SUDAH DI-TAG]" : "[BELUM DI-TAG]";
                sb.AppendLine($"  {tagStatus} #{r.Number} '{r.Name}' (Id: {r.Id.Value}, Area: {r.Area * 0.092903:F1} m2)");
            }
            sb.AppendLine($"\nTotal: {sudah} sudah di-tag, {belum} belum di-tag.");
        }

        return sb.ToString();
    }
}

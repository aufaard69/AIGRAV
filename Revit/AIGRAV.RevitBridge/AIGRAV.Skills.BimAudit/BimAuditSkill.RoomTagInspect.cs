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
    public static string InspectRoomTagReflection(Document doc)
    {
        var sb = new StringBuilder();
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

        sb.AppendLine($"=== TOTAL ROOM TAGS DI VIEW '{view.Name}': {tags.Count} ===");
        int idx = 1;
        foreach (var tag in tags)
        {
            var lid = tag.TaggedRoomId;
            long linkedIdVal = lid?.LinkedElementId?.Value ?? -1;
            sb.AppendLine($"{idx++}. Tag Id: {tag.Id.Value} | Tipe: '{tag.RoomTagType?.FamilyName} : {tag.RoomTagType?.Name}' | TagText: '{tag.TagText.Replace("\r", " ").Replace("\n", " ")}' | LinkedRoomId: {linkedIdVal}");
        }

        return sb.ToString();
    }
}

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
    public static string CleanAndPlaceAllLinkedRoomTags(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV: SINKRONISASI TOTAL 20 ROOM TAGS DARI LINK KE DENAH BANGUNAN UTAMA ===");

        var view = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(v => v.Name.Equals("DENAH BANGUNAN UTAMA", StringComparison.OrdinalIgnoreCase) && !v.IsTemplate);

        if (view == null) return "ERROR: View 'DENAH BANGUNAN UTAMA' tidak ditemukan.";

        var linkInstance = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .FirstOrDefault(l => l.Name.Contains("NTS-SI_01-BU-AR", StringComparison.OrdinalIgnoreCase) || (l.GetLinkDocument() != null && l.GetLinkDocument().Title.Contains("NTS-SI_01-BU-AR", StringComparison.OrdinalIgnoreCase)));

        if (linkInstance == null) return "ERROR: RevitLinkInstance 'NTS-SI_01-BU-AR' tidak ditemukan.";
        Document? linkDoc = linkInstance.GetLinkDocument();
        if (linkDoc == null) return "ERROR: Dokumen link null.";

        Transform transform = linkInstance.GetTotalTransform();

        // Target rooms in linkDoc
        var rooms = new FilteredElementCollector(linkDoc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .Cast<Room>()
            .Where(r => r.Area > 0 && r.Level != null && r.Level.Name.Equals("DENAH BANGUNAN UTAMA", StringComparison.OrdinalIgnoreCase))
            .ToList();

        sb.AppendLine($"Ditemukan {rooms.Count} Ruangan di Dokumen Link pada Level 'DENAH BANGUNAN UTAMA'.");

        // Reference Tags in Link Document
        var linkDocTags = new FilteredElementCollector(linkDoc)
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .WhereElementIsNotElementType()
            .Cast<RoomTag>()
            .ToList();

        var roomIds = new HashSet<long>(rooms.Select(r => r.Id.Value));
        var tagsForTheseRooms = linkDocTags.Where(t => t.Room != null && roomIds.Contains(t.Room.Id.Value)).ToList();

        // Host symbols
        var hostSymbols = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .Cast<FamilySymbol>()
            .ToList();

        Func<string?, string?, FamilySymbol?> findHostSymbol = (famName, typeName) =>
        {
            if (string.IsNullOrWhiteSpace(famName) || string.IsNullOrWhiteSpace(typeName)) return null;
            return hostSymbols.FirstOrDefault(s => s.FamilyName.Equals(famName, StringComparison.OrdinalIgnoreCase) && s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                ?? hostSymbols.FirstOrDefault(s => s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
        };

        var fallbackSymbol = hostSymbols.FirstOrDefault(s => s.Name.Equals("SMU- Room Tag", StringComparison.OrdinalIgnoreCase))
            ?? hostSymbols.FirstOrDefault();

        if (fallbackSymbol == null) return "ERROR: Tidak ada Room Tag Symbol di Host Document.";

        int placedCount = 0;

        using (Transaction tx = new(doc, "AIGRAV - Reset & Place All Linked Room Tags"))
        {
            var fOpts = tx.GetFailureHandlingOptions();
            fOpts.SetFailuresPreprocessor(new RoomTagFailurePreprocessor());
            tx.SetFailureHandlingOptions(fOpts);

            tx.Start();

            // 1. Delete all existing room tags in view to avoid duplicates / invalid tags
            var oldTags = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_RoomTags)
                .WhereElementIsNotElementType()
                .Select(t => t.Id)
                .ToList();

            if (oldTags.Count > 0)
            {
                doc.Delete(oldTags);
                sb.AppendLine($"Membersihkan {oldTags.Count} Room Tag lama di View agar rapi.");
                doc.Regenerate();
            }

            // 2. Place exact tag for each room
            foreach (var room in rooms)
            {
                if (room.Location is not LocationPoint lp) continue;

                XYZ center = transform.OfPoint(lp.Point);
                UV centerUv = new UV(center.X, center.Y);

                // Find matching tag in linkDoc
                var matchingLinkTag = tagsForTheseRooms.FirstOrDefault(t => t.Room != null && t.Room.Id == room.Id);

                FamilySymbol? targetSym = null;
                if (matchingLinkTag != null && matchingLinkTag.RoomTagType != null)
                {
                    targetSym = findHostSymbol(matchingLinkTag.RoomTagType.FamilyName, matchingLinkTag.RoomTagType.Name);
                }
                if (targetSym == null) targetSym = fallbackSymbol;

                try
                {
                    LinkElementId linkElemId = new LinkElementId(linkInstance.Id, room.Id);
                    RoomTag newTag = doc.Create.NewRoomTag(linkElemId, centerUv, view.Id);

                    if (targetSym != null)
                    {
                        newTag.ChangeTypeId(targetSym.Id);
                    }

                    if (matchingLinkTag != null)
                    {
                        newTag.TagOrientation = matchingLinkTag.TagOrientation;
                        XYZ desiredHead = transform.OfPoint(matchingLinkTag.TagHeadPosition);

                        if (matchingLinkTag.HasLeader)
                        {
                            newTag.HasLeader = true;
                            try { newTag.LeaderEnd = transform.OfPoint(matchingLinkTag.LeaderEnd); } catch { }
                            try { newTag.TagHeadPosition = desiredHead; } catch { }
                        }
                        else
                        {
                            try
                            {
                                newTag.TagHeadPosition = desiredHead;
                            }
                            catch
                            {
                                try
                                {
                                    newTag.HasLeader = true;
                                    newTag.TagHeadPosition = desiredHead;
                                }
                                catch { }
                            }
                        }
                    }

                    placedCount++;
                    sb.AppendLine($"  [OK] #{room.Number} '{room.Name}' ({room.Area * 0.092903:F1} m2) -> Tipe: '{targetSym?.FamilyName} : {targetSym?.Name}'");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  [FAIL] #{room.Number} '{room.Name}': {ex.Message}");
                }
            }

            var st = tx.Commit();
            sb.AppendLine($"Status Transaksi: {st}");
        }

        sb.AppendLine($"\n[HASIL AKHIR SINKRONISASI]");
        sb.AppendLine($"- Total Ruangan Level DENAH BANGUNAN UTAMA : {rooms.Count} Ruangan");
        sb.AppendLine($"- Berhasil Ditempatkan Sempurna            : {placedCount} Room Tag");

        return sb.ToString();
    }
}

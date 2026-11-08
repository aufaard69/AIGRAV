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
    private sealed class RoomTagFailurePreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages();
            foreach (var f in failures)
            {
                if (f.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(f);
                }
            }
            return FailureProcessingResult.Continue;
        }
    }

    public static string AuditLinkedRooms(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AUDIT LINKED ROOMS & VIEW DENAH BANGUNAN UTAMA ===");

        var view = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(v => v.Name.Equals("DENAH BANGUNAN UTAMA", StringComparison.OrdinalIgnoreCase) && !v.IsTemplate);

        if (view == null)
            return "ERROR: View 'DENAH BANGUNAN UTAMA' tidak ditemukan di dokumen host!";

        sb.AppendLine($"Target View: '{view.Name}' (Id: {view.Id.Value}, Type: {view.ViewType})");
        sb.AppendLine($"Level View : {(view.GenLevel != null ? $"{view.GenLevel.Name} (Id: {view.GenLevel.Id.Value}, Elev: {view.GenLevel.Elevation * 304.8:F1} mm)" : "None")}");

        var existingTags = new FilteredElementCollector(doc, view.Id)
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .WhereElementIsNotElementType()
            .ToList();
        sb.AppendLine($"Existing Room Tags di View '{view.Name}': {existingTags.Count}");

        var linkInstance = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .FirstOrDefault(l => l.Name.Contains("NTS-SI_01-BU-AR", StringComparison.OrdinalIgnoreCase) || (l.GetLinkDocument() != null && l.GetLinkDocument().Title.Contains("NTS-SI_01-BU-AR", StringComparison.OrdinalIgnoreCase)));

        if (linkInstance == null)
            return sb.ToString() + "\nERROR: RevitLinkInstance 'NTS-SI_01-BU-AR' tidak ditemukan atau unloaded!";

        Document? linkDoc = linkInstance.GetLinkDocument();
        if (linkDoc == null)
            return sb.ToString() + "\nERROR: Dokumen Link 'NTS-SI_01-BU-AR' null!";

        Transform transform = linkInstance.GetTotalTransform();
        sb.AppendLine($"Link Transform Origin: ({transform.Origin.X * 304.8:F1}, {transform.Origin.Y * 304.8:F1}, {transform.Origin.Z * 304.8:F1}) mm");

        var rooms = new FilteredElementCollector(linkDoc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .Cast<Room>()
            .Where(r => r.Area > 0 && r.Level != null && r.Level.Name.Equals("DENAH BANGUNAN UTAMA", StringComparison.OrdinalIgnoreCase))
            .ToList();

        sb.AppendLine($"Total Room Terdeteksi pada Level 'DENAH BANGUNAN UTAMA': {rooms.Count} rooms");

        return sb.ToString();
    }

    public static string TransferLinkedRoomTags(Document doc, string? argument = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV: TRANSFER LINKED ROOM TAGS KE DENAH BANGUNAN UTAMA ===");

        // 1. Target View
        var view = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(v => v.Name.Equals("DENAH BANGUNAN UTAMA", StringComparison.OrdinalIgnoreCase) && !v.IsTemplate);

        if (view == null)
            return "ERROR: View 'DENAH BANGUNAN UTAMA' tidak ditemukan di dokumen host.";

        // 2. Link Instance
        var linkInstance = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .FirstOrDefault(l => l.Name.Contains("NTS-SI_01-BU-AR", StringComparison.OrdinalIgnoreCase) || (l.GetLinkDocument() != null && l.GetLinkDocument().Title.Contains("NTS-SI_01-BU-AR", StringComparison.OrdinalIgnoreCase)));

        if (linkInstance == null)
            return "ERROR: RevitLinkInstance 'NTS-SI_01-BU-AR' tidak ditemukan.";

        Document? linkDoc = linkInstance.GetLinkDocument();
        if (linkDoc == null)
            return "ERROR: Dokumen link 'NTS-SI_01-BU-AR' tidak dapat diakses.";

        Transform transform = linkInstance.GetTotalTransform();

        // 3. Rooms in Link on Level DENAH BANGUNAN UTAMA
        var rooms = new FilteredElementCollector(linkDoc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .Cast<Room>()
            .Where(r => r.Area > 0 && r.Level != null && r.Level.Name.Equals("DENAH BANGUNAN UTAMA", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rooms.Count == 0)
            return "ERROR: Tidak ditemukan Room valid pada Level 'DENAH BANGUNAN UTAMA' di dokumen link.";

        // Reference Tags in Link Document
        var linkDocTags = new FilteredElementCollector(linkDoc)
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .WhereElementIsNotElementType()
            .Cast<RoomTag>()
            .ToList();

        var roomIds = new HashSet<long>(rooms.Select(r => r.Id.Value));
        var tagsForTheseRooms = linkDocTags.Where(t => t.Room != null && roomIds.Contains(t.Room.Id.Value)).ToList();

        int createdCount = 0;
        int failedCount = 0;

        using (Transaction tx = new(doc, "AIGRAV - Transfer Linked Room Tags to DENAH BANGUNAN UTAMA"))
        {
            var fOpts = tx.GetFailureHandlingOptions();
            fOpts.SetFailuresPreprocessor(new RoomTagFailurePreprocessor());
            fOpts.SetClearAfterRollback(false);
            tx.SetFailureHandlingOptions(fOpts);

            tx.Start();

            // A. Copy Room Tag Symbols from linkDoc if not present
            var linkSymbols = new FilteredElementCollector(linkDoc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_RoomTags)
                .Cast<FamilySymbol>()
                .ToList();

            var symbolIdsToCopy = linkSymbols.Select(s => s.Id).ToList();

            try
            {
                CopyPasteOptions cpOpts = new();
                var copiedIds = ElementTransformUtils.CopyElements(linkDoc, symbolIdsToCopy, doc, Transform.Identity, cpOpts);
                sb.AppendLine($"[BERHASIL] CopyElements menyalin/memperbarui {copiedIds.Count} elemen ke Host Document.");
                doc.Regenerate();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[INFO COPY] {ex.Message}");
            }

            // B. Map Target Room Tag Types in Host Document
            var hostSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_RoomTags)
                .Cast<FamilySymbol>()
                .ToList();

            foreach (var sym in hostSymbols)
            {
                try
                {
                    if (!sym.IsActive) sym.Activate();
                }
                catch { }
            }
            doc.Regenerate();

            Func<string?, string?, FamilySymbol?> findHostSymbol = (famName, typeName) =>
            {
                if (string.IsNullOrWhiteSpace(famName) || string.IsNullOrWhiteSpace(typeName)) return null;
                return hostSymbols.FirstOrDefault(s => s.FamilyName.Equals(famName, StringComparison.OrdinalIgnoreCase) && s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                    ?? hostSymbols.FirstOrDefault(s => s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
            };

            var fallbackSymbol = hostSymbols.FirstOrDefault(s => s.Name.Equals("SMU- Room Tag", StringComparison.OrdinalIgnoreCase))
                ?? hostSymbols.FirstOrDefault();

            if (fallbackSymbol == null)
            {
                tx.RollBack();
                return sb.ToString() + "\nERROR: Tidak ada Room Tag Symbol yang tersedia di Host Document.";
            }

            sb.AppendLine($"Tipe Tag Dasar: '{fallbackSymbol.FamilyName} : {fallbackSymbol.Name}'");

            // C. Place Room Tags safely
            sb.AppendLine($"\nMemulai Penempatan Room Tag untuk {rooms.Count} Ruangan...");

            foreach (var room in rooms)
            {
                if (room.Location is not LocationPoint lp)
                {
                    failedCount++;
                    sb.AppendLine($"  [FAIL] #{room.Number} '{room.Name}': Lokasi ruangan tidak valid.");
                    continue;
                }

                // 1. Initial point ALWAYS at room center (guaranteed inside room boundary)
                XYZ hostCenterPoint = transform.OfPoint(lp.Point);
                UV centerUv = new UV(hostCenterPoint.X, hostCenterPoint.Y);

                // 2. Find target symbol based on link doc
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

                    // 3. Adjust position & orientation if matching tag exists
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
                                // If position is outside boundary without leader, enable leader
                                try
                                {
                                    newTag.HasLeader = true;
                                    newTag.TagHeadPosition = desiredHead;
                                }
                                catch { }
                            }
                        }
                    }

                    createdCount++;
                    sb.AppendLine($"  [OK] #{room.Number} '{room.Name}' -> Tipe: '{targetSym?.FamilyName} : {targetSym?.Name}'");
                }
                catch (Exception ex)
                {
                    failedCount++;
                    sb.AppendLine($"  [FAIL] #{room.Number} '{room.Name}': {ex.Message}");
                }
            }

            var status = tx.Commit();
            sb.AppendLine($"\nStatus Transaksi: {status}");
        }

        sb.AppendLine($"\n[HASIL AKHIR PENEMPATAN TAG]");
        sb.AppendLine($"- Total Ruangan Valid : {rooms.Count}");
        sb.AppendLine($"- Berhasil Ditempatkan: {createdCount} Room Tag");
        sb.AppendLine($"- Gagal / Dilewati   : {failedCount} Room Tag");

        return sb.ToString();
    }
}

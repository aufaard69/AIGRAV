import os

path = r"C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
with open(path, "r", encoding="utf-8") as f:
    lines = f.readlines()

start_idx = -1

for i, line in enumerate(lines):
    if "private static string InspectSteelConnectionsInModel(Document doc)" in line:
        start_idx = i
        break

if start_idx != -1:
    end_idx = len(lines) - 1

print(f"Found InspectSteelConnectionsInModel: start={start_idx}, end={end_idx}")

new_method = '''    private static string InspectSteelConnectionsInModel(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("     AUDIT KONEKSI BAJA: WF350-Gusset Plate to WF400 2 & BALOK TERHUBUNG        ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Model: {doc.Title}");
        sb.AppendLine($"Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\\n");

        var allConnections = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructConnections)
            .WhereElementIsNotElementType()
            .ToList();

        sb.AppendLine($"Total Elemen Koneksi (OST_StructConnections) di Model: {allConnections.Count}\\n");

        var connGroups = allConnections.GroupBy(c => {
            var typeElem = doc.GetElement(c.GetTypeId());
            return typeElem?.Name ?? "Unknown Type";
        }).OrderByDescending(g => g.Count());

        sb.AppendLine("Daftar Tipe Koneksi Terpasang di Model:");
        foreach (var g in connGroups)
        {
            sb.AppendLine($"  * '{g.Key}' : {g.Count()} buah");
        }
        sb.AppendLine();

        var targetConns = allConnections.Where(c => {
            var tName = doc.GetElement(c.GetTypeId())?.Name ?? "";
            return tName.IndexOf("WF350", StringComparison.OrdinalIgnoreCase) >= 0
                || tName.IndexOf("Gusset", StringComparison.OrdinalIgnoreCase) >= 0
                || tName.IndexOf("WF400", StringComparison.OrdinalIgnoreCase) >= 0;
        }).ToList();

        sb.AppendLine($"--------------------------------------------------------------------------------");
        sb.AppendLine($"ANALISIS DETAIL KONEKSI TARGET (Ditemukan: {targetConns.Count} koneksi)");
        sb.AppendLine($"--------------------------------------------------------------------------------");

        foreach (var c in targetConns)
        {
            var typeElem = doc.GetElement(c.GetTypeId());
            string typeName = typeElem?.Name ?? "Unknown";

            sb.AppendLine($"\\n[KONEKSI ID: {c.Id.Value}] Tipe: '{typeName}'");

            Autodesk.Revit.DB.Structure.StructuralConnectionHandler? conn = c as Autodesk.Revit.DB.Structure.StructuralConnectionHandler;

            var bbConn = c.get_BoundingBox(null);
            XYZ midConn = bbConn != null ? (bbConn.Min + bbConn.Max) * 0.5 : XYZ.Zero;
            XYZ origin = conn != null ? conn.GetOrigin() : XYZ.Zero;

            if (bbConn != null)
            {
                sb.AppendLine($"  * BoundingBox Koneksi : Min=({bbConn.Min.X * 0.3048:F3}, {bbConn.Min.Y * 0.3048:F3}, {bbConn.Min.Z * 0.3048:F3})m | Max=({bbConn.Max.X * 0.3048:F3}, {bbConn.Max.Y * 0.3048:F3}, {bbConn.Max.Z * 0.3048:F3})m");
                sb.AppendLine($"  * Titik Tengah (Mid)  : ({midConn.X * 0.3048:F3}, {midConn.Y * 0.3048:F3}, {midConn.Z * 0.3048:F3})m");
            }
            sb.AppendLine($"  * Origin Koneksi      : ({origin.X * 0.3048:F3}, {origin.Y * 0.3048:F3}, {origin.Z * 0.3048:F3})m");

            if (conn != null)
            {
                try
                {
                    var subels = conn.GetSubelements();
                    sb.AppendLine($"  * Jumlah Subelements Native: {subels.Count}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  * Subelements info: {ex.Message}");
                }
            }

            IList<ElementId> connectedIds = conn != null ? conn.GetConnectedElementIds() : new List<ElementId>();
            sb.AppendLine($"  * Elemen Terhubung ({connectedIds.Count} elemen):");

            for (int i = 0; i < connectedIds.Count; i++)
            {
                var elId = connectedIds[i];
                var el = doc.GetElement(elId);
                if (el == null) continue;

                string role = i == 0 ? "PRIMARY (Induk / WF400?)" : $"SECONDARY #{i} (Anak / WF350?)";
                string famType = $"{doc.GetElement(el.GetTypeId())?.Name ?? el.Name}";
                string cat = el.Category?.Name ?? "Framing";

                sb.AppendLine($"    -> Member [{i}] ({role}): Id {el.Id.Value} | Kategori: {cat} | Profil: '{famType}'");

                var pCrossRot = el.get_Parameter(BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE);
                double crossRotDeg = (pCrossRot?.AsDouble() ?? 0.0) * 180.0 / Math.PI;

                var pYJust = el.get_Parameter(BuiltInParameter.YZ_JUSTIFICATION);
                var pZJust = el.get_Parameter(BuiltInParameter.Z_JUSTIFICATION);
                var pRefLvl = el.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                string refLvlStr = pRefLvl?.AsValueString() ?? "-";

                FamilyInstance? fi = el as FamilyInstance;
                string flipInfo = fi != null ? $"HandFlipped={fi.HandFlipped}, FacingFlipped={fi.FacingFlipped}, Mirrored={fi.Mirrored}" : "-";

                sb.AppendLine($"       Cross-Section Rotation: {crossRotDeg:F1}° | Ref Level: {refLvlStr} | Flags: {flipInfo}");

                if (el.Location is LocationCurve locCurve && locCurve.Curve != null)
                {
                    Curve curve = locCurve.Curve;
                    XYZ ptStart = curve.GetEndPoint(0);
                    XYZ ptEnd = curve.GetEndPoint(1);
                    XYZ dir = (ptEnd - ptStart).Normalize();

                    double distToStart = (midConn - ptStart).GetLength() * 0.3048;
                    double distToEnd = (midConn - ptEnd).GetLength() * 0.3048;

                    string connectedEnd = distToStart < distToEnd ? "START Point (Ujung Awal)" : "END Point (Ujung Akhir)";

                    sb.AppendLine($"       Start Point : ({ptStart.X * 0.3048:F3}, {ptStart.Y * 0.3048:F3}, {ptStart.Z * 0.3048:F3}) m");
                    sb.AppendLine($"       End Point   : ({ptEnd.X * 0.3048:F3}, {ptEnd.Y * 0.3048:F3}, {ptEnd.Z * 0.3048:F3}) m");
                    sb.AppendLine($"       Panjang     : {curve.Length * 0.3048:F3} m | Vector Dir: (dx={dir.X:F3}, dy={dir.Y:F3}, dz={dir.Z:F3})");
                    sb.AppendLine($"       Koneksi di  : {connectedEnd} (Jarak ke Start: {distToStart:F2}m, Jarak ke End: {distToEnd:F2}m)");

                    var bbBeam = el.get_BoundingBox(null);
                    if (bbBeam != null && bbConn != null)
                    {
                        sb.AppendLine($"       Beam BB Z   : {bbBeam.Min.Z * 0.3048:F3} m s/d {bbBeam.Max.Z * 0.3048:F3} m");
                        double diffTop = (bbConn.Min.Z - bbBeam.Max.Z) * 0.3048;
                        if (diffTop > 0.05)
                        {
                            sb.AppendLine($"       >>> [STATUS KONEKSI: TERBANG!] Bawah plat berada +{diffTop*1000:F0} mm di ATAS permukaan atas balok!");
                        }
                        else
                        {
                            sb.AppendLine($"       >>> [STATUS KONEKSI: NORMAL / TERDUDUK]");
                        }
                    }
                }
            }
        }

        // Cek definisi tipe koneksi
        sb.AppendLine("\\n--------------------------------------------------------------------------------");
        sb.AppendLine("DEFINISI TIPE KONEKSI TERKAIT");
        sb.AppendLine("--------------------------------------------------------------------------------");
        var connTypes = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructConnections)
            .WhereElementIsElementType()
            .Where(t => t.Name.IndexOf("WF350", StringComparison.OrdinalIgnoreCase) >= 0
                     || t.Name.IndexOf("Gusset", StringComparison.OrdinalIgnoreCase) >= 0
                     || t.Name.IndexOf("WF400", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        foreach (var ct in connTypes)
        {
            sb.AppendLine($"* Type Name: '{ct.Name}' (Id: {ct.Id.Value})");
            foreach (Parameter p in ct.Parameters)
            {
                if (p.HasValue && !string.IsNullOrWhiteSpace(p.AsValueString() ?? p.AsString()))
                {
                    sb.AppendLine($"    - {p.Definition.Name}: '{p.AsValueString() ?? p.AsString()}'");
                }
            }
        }

        return sb.ToString();
    }
'''

new_lines = lines[:start_idx] + [new_method + "\n}\n"]
with open(path, "w", encoding="utf-8") as f:
    f.writelines(new_lines)

print("Updated InspectSteelConnectionsInModel successfully.")

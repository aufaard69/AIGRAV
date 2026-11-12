using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public static class SloofS1AuditHelper
{
    public static string AuditSloofS1AndS1AFloors(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AUDIT DETAIL ELEVASI LANTAI DAN OVERLAP SLOOF S1 & S1-A ===");

        // 1. Audit Levels
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();

        sb.AppendLine("\n[1. DAFTAR LEVEL MODEL]");
        foreach (var l in levels)
        {
            double elevM = l.Elevation * 0.3048;
            sb.AppendLine($"  - Level: '{l.Name}' | Id: {l.Id.Value} | Elevation: {elevM:F3} m");
        }

        // 2. Audit Floors
        var floors = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .Cast<Floor>()
            .ToList();

        sb.AppendLine($"\n[2. DAFTAR PELAT LANTAI (Total: {floors.Count} unit)]");
        var floorInfoMap = new Dictionary<long, (string Name, string LevelName, double ElevM, double BboxMinZ, double BboxMaxZ)>();
        foreach (var fl in floors)
        {
            var bbox = fl.get_BoundingBox(null);
            var lvl = doc.GetElement(fl.LevelId) as Level;
            double lvlElevM = lvl != null ? lvl.Elevation * 0.3048 : 0;
            double offsetFt = fl.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0;
            double totalElevM = lvlElevM + (offsetFt * 0.3048);
            string tName = doc.GetElement(fl.GetTypeId())?.Name ?? fl.Name;
            double bMinZM = (bbox?.Min.Z ?? 0) * 0.3048;
            double bMaxZM = (bbox?.Max.Z ?? 0) * 0.3048;

            floorInfoMap[fl.Id.Value] = (tName, lvl?.Name ?? "No Level", totalElevM, bMinZM, bMaxZM);
        }

        // Group floors by total elevation
        var floorGroups = floorInfoMap.GroupBy(x => Math.Round(x.Value.ElevM, 2)).OrderBy(g => g.Key);
        foreach (var g in floorGroups)
        {
            sb.AppendLine($"  * Elevasi {g.Key:F2} m: {g.Count()} pelat lantai (Level: {string.Join(", ", g.Select(x => x.Value.LevelName).Distinct())})");
        }

        // 3. Sloof S1 & S1-A Overlap Pairs
        var framings = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(f => {
                string tn = doc.GetElement(f.GetTypeId())?.Name ?? f.Name;
                return tn.Contains("S1 200", StringComparison.OrdinalIgnoreCase) || tn.Contains("S1-A", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        sb.AppendLine($"\n[3. BALOK SLOOF S1 & S1-A (Total: {framings.Count} unit)]");
        var s1List = framings.Where(f => (doc.GetElement(f.GetTypeId())?.Name ?? f.Name).Contains("S1 200", StringComparison.OrdinalIgnoreCase)).ToList();
        var s1aList = framings.Where(f => (doc.GetElement(f.GetTypeId())?.Name ?? f.Name).Contains("S1-A", StringComparison.OrdinalIgnoreCase)).ToList();
        sb.AppendLine($"  - Tipe S1 200 X 300  : {s1List.Count} unit");
        sb.AppendLine($"  - Tipe S1-A 150 X 200: {s1aList.Count} unit");

        int s1PairsElev0 = 0;
        int s1PairsOtherElev = 0;
        int s1aPairsElev0 = 0;
        int s1aPairsOtherElev = 0;

        foreach (var fr in framings)
        {
            string tn = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;
            bool isS1 = tn.Contains("S1 200", StringComparison.OrdinalIgnoreCase);

            var joined = JoinGeometryUtils.GetJoinedElements(doc, fr);
            foreach (var jId in joined)
            {
                if (floorInfoMap.TryGetValue(jId.Value, out var flInfo))
                {
                    bool isElev0 = Math.Abs(flInfo.ElevM) < 0.05; // toleransi 5cm sekitar 0.00
                    if (isS1)
                    {
                        if (isElev0) s1PairsElev0++; else s1PairsOtherElev++;
                    }
                    else
                    {
                        if (isElev0) s1aPairsElev0++; else s1aPairsOtherElev++;
                    }
                }
            }
        }

        sb.AppendLine($"\n[4. PASANGAN OVERLAP TER-JOIN DENGAN PELAT]");
        sb.AppendLine($"  - S1 200 X 300  : {s1PairsElev0} pasang pada Pelat Elevasi 0.00 | {s1PairsOtherElev} pasang pada elevasi lain");
        sb.AppendLine($"  - S1-A 150 X 200: {s1aPairsElev0} pasang pada Pelat Elevasi 0.00 | {s1aPairsOtherElev} pasang pada elevasi lain");

        return sb.ToString();
    }
}

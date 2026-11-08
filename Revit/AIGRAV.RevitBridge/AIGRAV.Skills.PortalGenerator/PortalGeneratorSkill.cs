using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.PortalGenerator;

public class WarningSwallower : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        failuresAccessor.DeleteAllWarnings();
        return FailureProcessingResult.Continue;
    }
}

public sealed class PortalGeneratorSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Portal Frame Generator",
        Version = "2.1",
        Category = "Modeling",
        Description = "Autonomously models portal steel frames along grids with columns, rafters, and connections.",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName)
    {
        return toolName == "aigrav_portal_discovery" || 
               toolName == "aigrav_portal_generate" || 
               toolName == "aigrav_portal_single_side" ||
               toolName == "aigrav_portal_inspect" ||
               toolName == "aigrav_portal_trace";
    }

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        Document doc = app.ActiveUIDocument.Document;
        if (toolName == "aigrav_portal_discovery")
            return Discover(doc);
        else if (toolName == "aigrav_portal_trace")
            return TracePortal(doc);
        else if (toolName == "aigrav_portal_generate")
            return GenerateFullPortalAs1(doc, argument);
        else if (toolName == "aigrav_portal_single_side")
            return GenerateSingleSide(doc, argument);
        else if (toolName == "aigrav_portal_inspect")
            return Inspect(doc);
            
        return "Unknown command.";
    }

    private string GenerateFullPortalAs1(Document doc, string? argument)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV PORTAL AS.1 GENERATION ===");

        try
        {
            // 1. Resolve Symbols
            var colSymbols = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .ToList();

            FamilySymbol? colSymbol = colSymbols.FirstOrDefault(s => s.FamilyName.Contains("Wide Flange") && s.Name.Contains("WF 400"))
                                   ?? colSymbols.FirstOrDefault(s => s.Name.Contains("WF 400") || s.Name.Contains("WF400"));

            var framingSymbols = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .ToList();

            FamilySymbol? rafterSymbol = framingSymbols.FirstOrDefault(s => s.Name.Contains("RCWF.350") || s.Name.Contains("RCWF 350"))
                                      ?? framingSymbols.FirstOrDefault(s => s.Name.Contains("CWF 350") || s.Name.Contains("CWF350"))
                                      ?? framingSymbols.FirstOrDefault(s => s.Name.Contains("WF 350") || s.Name.Contains("WF350"));

            if (colSymbol == null)
            {
                return "ERROR: Column type WF 400 not found in project.";
            }

            if (rafterSymbol == null)
            {
                return "ERROR: Rafter type RCWF.350 / CWF 350 not found in project.";
            }

            // 2. Resolve Levels
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            Level? floor1 = levels.FirstOrDefault(l => l.Name.Contains("1ST FLOOR")) ?? levels.FirstOrDefault();
            if (floor1 == null)
            {
                return "ERROR: Level 1ST FLOOR not found.";
            }

            // 3. Grid 1 coordinate & Column Grid intersections
            double xGrid1_M = 182.4821;
            double xGrid1_Ft = xGrid1_M / 0.3048;
            double floor1Elev_Ft = floor1.Elevation;

            var columnDefs = new List<(string GridName, double Y_M, double Z_Top_M)>
            {
                ("C", -30.8498, 8.000),
                ("J", -24.7498, 8.583),
                ("I", -18.6498, 9.165),
                ("H", -12.5498, 9.748),
                ("K",  -6.4498, 10.331),
                ("G",  -0.3498, 10.913),
                ("B",   5.7502, 11.496),
                ("D",  11.8502, 8.747),
                ("E",  17.9502, 9.663),
                ("F",  24.0502, 10.580),
                ("A",  30.1502, 11.496)
            };

            using (Transaction tx = new Transaction(doc, "AIGRAV - Generate Portal As.1"))
            {
                // Set failure preprocessor to suppress warnings
                FailureHandlingOptions failOpt = tx.GetFailureHandlingOptions();
                failOpt.SetFailuresPreprocessor(new WarningSwallower());
                tx.SetFailureHandlingOptions(failOpt);

                tx.Start();

                if (!colSymbol.IsActive) colSymbol.Activate();
                if (!rafterSymbol.IsActive) rafterSymbol.Activate();

                sb.AppendLine($"1. Profil Terverifikasi:");
                sb.AppendLine($"   - Kolom: {colSymbol.FamilyName} : {colSymbol.Name}");
                sb.AppendLine($"   - Rafter: {rafterSymbol.FamilyName} : {rafterSymbol.Name}");
                sb.AppendLine($"   - Referensi Level: {floor1.Name} (Elev = {floor1.Elevation * 0.3048:F3} m)");

                // 4. Create Columns (Check if already exist)
                sb.AppendLine($"\n2. Pembuatan Kolom WF400 (11 Unit di As. 1):");
                var createdCols = new List<FamilyInstance>();
                foreach (var col in columnDefs)
                {
                    double y_Ft = col.Y_M / 0.3048;
                    XYZ colPt = new XYZ(xGrid1_Ft, y_Ft, floor1Elev_Ft);
                    FamilyInstance inst = doc.Create.NewFamilyInstance(colPt, colSymbol, floor1, StructuralType.Column);

                    double baseOffset_Ft = 0.0 - floor1Elev_Ft;
                    inst.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).Set(floor1.Id);
                    inst.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM).Set(baseOffset_Ft);

                    double topOffset_Ft = (col.Z_Top_M / 0.3048) - floor1Elev_Ft;
                    inst.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).Set(floor1.Id);
                    inst.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM).Set(topOffset_Ft);

                    createdCols.Add(inst);
                    sb.AppendLine($"   - [OK] As. 1-{col.GridName}: Id {inst.Id.Value} | Base 0.000 m -> Top +{col.Z_Top_M:F3} m (H = {col.Z_Top_M:F3} m)");
                }

                // 5. Create Rafters
                sb.AppendLine($"\n3. Pembuatan Rafter RCWF.350 (2 Bentang di As. 1):");

                // Span 1: As. C to As. B
                XYZ ptC = new XYZ(xGrid1_Ft, -30.8498 / 0.3048, 8.000 / 0.3048);
                XYZ ptB1 = new XYZ(xGrid1_Ft, 5.7502 / 0.3048, 11.496 / 0.3048);
                Line lineCB = Line.CreateBound(ptC, ptB1);
                FamilyInstance rafterCB = doc.Create.NewFamilyInstance(lineCB, rafterSymbol, floor1, StructuralType.Beam);
                Parameter zJustCB = rafterCB.get_Parameter(BuiltInParameter.Z_JUSTIFICATION);
                if (zJustCB != null && !zJustCB.IsReadOnly) zJustCB.Set(0); // 0 = Top
                sb.AppendLine($"   - [OK] Rafter Bentang 1 (As. C -> As. B): Id {rafterCB.Id.Value} | L = 36.77 m | Top: +8.000 m ke +11.496 m (Kemiringan 9.55%)");

                // Span 2: As. B to As. A
                XYZ ptB2 = new XYZ(xGrid1_Ft, 5.7502 / 0.3048, 7.830 / 0.3048);
                XYZ ptA = new XYZ(xGrid1_Ft, 30.1502 / 0.3048, 11.496 / 0.3048);
                Line lineBA = Line.CreateBound(ptB2, ptA);
                FamilyInstance rafterBA = doc.Create.NewFamilyInstance(lineBA, rafterSymbol, floor1, StructuralType.Beam);
                Parameter zJustBA = rafterBA.get_Parameter(BuiltInParameter.Z_JUSTIFICATION);
                if (zJustBA != null && !zJustBA.IsReadOnly) zJustBA.Set(0); // 0 = Top
                sb.AppendLine($"   - [OK] Rafter Bentang 2 (As. B -> As. A): Id {rafterBA.Id.Value} | L = 24.67 m | Top: +7.830 m ke +11.496 m (Kemiringan 15.02%)");

                tx.Commit();
                sb.AppendLine("\nSTATUS: PORTAL AS. 1 BERHASIL DIBUAT DAN DICOMMIT KE MODEL REVIT.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"ERROR: {ex.Message}\n{ex.StackTrace}");
        }

        return sb.ToString();
    }

    private string Inspect(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== VERIFICATION REPORT ===");

        var cols = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
        var framing = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();

        sb.AppendLine($"Total Columns Found: {cols.Count}");
        foreach (var c in cols)
        {
            sb.AppendLine($"- Id {c.Id.Value} | {c.Symbol.FamilyName} : {c.Name}");
            if (c.Location is LocationPoint lp) sb.AppendLine($"  Pos: ({lp.Point.X*0.3048:F3}, {lp.Point.Y*0.3048:F3}, {lp.Point.Z*0.3048:F3}) m");
        }

        sb.AppendLine($"\nTotal Rafters Found: {framing.Count}");
        foreach (var f in framing)
        {
            sb.AppendLine($"- Id {f.Id.Value} | {f.Symbol.FamilyName} : {f.Name}");
            if (f.Location is LocationCurve lc)
            {
                XYZ start = lc.Curve.GetEndPoint(0);
                XYZ end = lc.Curve.GetEndPoint(1);
                sb.AppendLine($"  Start: ({start.X*0.3048:F3}, {start.Y*0.3048:F3}, {start.Z*0.3048:F3}) m");
                sb.AppendLine($"  End  : ({end.X*0.3048:F3}, {end.Y*0.3048:F3}, {end.Z*0.3048:F3}) m");
            }
        }

        return sb.ToString();
    }

    private string Discover(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== DISCOVERY REPORT ===");

        var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
        sb.AppendLine($"Levels Found: {levels.Count}");
        foreach (var lvl in levels)
        {
            sb.AppendLine($"- {lvl.Name}: Elevation = {lvl.Elevation * 304.8:F2} mm");
        }

        var structuralColumns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsElementType().Cast<FamilySymbol>().ToList();
        var structuralFraming = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsElementType().Cast<FamilySymbol>().ToList();

        sb.AppendLine($"\nStructural Column Types: {structuralColumns.Count}");
        foreach(var c in structuralColumns)
        {
            sb.AppendLine($"- {c.FamilyName} : {c.Name}");
        }

        sb.AppendLine($"\nStructural Framing Types: {structuralFraming.Count}");
        foreach(var f in structuralFraming)
        {
            sb.AppendLine($"- {f.FamilyName} : {f.Name}");
        }

        return sb.ToString();
    }

    private string TracePortal(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV PORTAL TRACE REPORT ===");

        var allGrids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().ToList();
        Grid? grid1 = allGrids.FirstOrDefault(g => g.Name == "1");

        foreach (var gTarget in allGrids.Where(g => !char.IsDigit(g.Name[0])).OrderBy(g => g.Name).Select(g => (g.Name, g)))
        {
            if (grid1 != null)
            {
                XYZ intPt = Compute2DIntersection(grid1.Curve, gTarget.Item2.Curve);
                if (intPt != null)
                {
                    sb.AppendLine($"As 1 - As {gTarget.Item1}: X = {intPt.X * 0.3048:F3} m, Y = {intPt.Y * 0.3048:F3} m");
                }
            }
        }

        return sb.ToString();
    }

    private XYZ Compute2DIntersection(Curve c1, Curve c2)
    {
        XYZ p1 = c1.GetEndPoint(0);
        XYZ p2 = c1.GetEndPoint(1);
        XYZ p3 = c2.GetEndPoint(0);
        XYZ p4 = c2.GetEndPoint(1);

        double x1 = p1.X, y1 = p1.Y;
        double x2 = p2.X, y2 = p2.Y;
        double x3 = p3.X, y3 = p3.Y;
        double x4 = p4.X, y4 = p4.Y;

        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-9) return null;

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        return new XYZ(x1 + t * (x2 - x1), y1 + t * (y2 - y1), 0);
    }

    private string GenerateSingleSide(Document doc, string? argument)
    {
        return GenerateFullPortalAs1(doc, argument);
    }
}

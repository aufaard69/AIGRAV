using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AIGRAV.Contracts;
using System.IO;

namespace AIGRAV.Skills.ConnectionLibraryManager;

public sealed class ConnectionLibraryManagerSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Connection Library Manager",
        Version = "1.0",
        Category = "Modeling",
        Description = "Autonomously authors Native Revit Structural Connection Families (RFA) and loads them.",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName) => toolName == "aigrav_author_connections" || toolName == "aigrav_verify_connections";

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (toolName == "aigrav_author_connections")
            return AuthorConnections(app);
        if (toolName == "aigrav_verify_connections")
            return VerifyConnections(app.ActiveUIDocument.Document);
        return "Unknown";
    }

    private string AuthorConnections(UIApplication app)
    {
        StringBuilder sb = new StringBuilder();
        string libPath = @"C:\AIGRAV\ConnectionLibrary";
        string templatePath = @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\Metric Generic Model.rft";
        
        if (!File.Exists(templatePath))
            return $"ERROR: Template not found at {templatePath}";

        Document mainDoc = app.ActiveUIDocument.Document;
        
        string basePlatePath = Path.Combine(libPath, "AIGRAV_BasePlate_WF400.rfa");
        string endPlatePath = Path.Combine(libPath, "AIGRAV_EndPlate_WF400.rfa");

        // --- AUTHOR BASE PLATE ---
        if (File.Exists(basePlatePath)) File.Delete(basePlatePath);
        Document bpFamDoc = app.Application.NewFamilyDocument(templatePath);
        using (Transaction tx = new Transaction(bpFamDoc, "Author Base Plate"))
        {
            tx.Start();
            // Change Category to Structural Connections
            bpFamDoc.OwnerFamily.FamilyCategory = bpFamDoc.Settings.Categories.get_Item(BuiltInCategory.OST_StructConnections);
            
            // Build geometry
            double bpLx = 440.0 / 304.8;
            double bpLy = 300.0 / 304.8;
            double bpThk = 19.0 / 304.8;
            
            // Base plate extrusion
            CurveArrArray caa = new CurveArrArray();
            CurveArray ca = new CurveArray();
            ca.Append(Line.CreateBound(new XYZ(-bpLx/2, -bpLy/2, 0), new XYZ(bpLx/2, -bpLy/2, 0)));
            ca.Append(Line.CreateBound(new XYZ(bpLx/2, -bpLy/2, 0), new XYZ(bpLx/2, bpLy/2, 0)));
            ca.Append(Line.CreateBound(new XYZ(bpLx/2, bpLy/2, 0), new XYZ(-bpLx/2, bpLy/2, 0)));
            ca.Append(Line.CreateBound(new XYZ(-bpLx/2, bpLy/2, 0), new XYZ(-bpLx/2, -bpLy/2, 0)));
            caa.Append(ca);
            
            Plane sketchPlane = Plane.CreateByNormalAndOrigin(new XYZ(0,0,1), XYZ.Zero);
            SketchPlane sp = SketchPlane.Create(bpFamDoc, sketchPlane);
            
            Extrusion plate = bpFamDoc.FamilyCreate.NewExtrusion(true, caa, sp, bpThk);
            
            // Anchors
            double ex = (440.0/2 - 50.0) / 304.8;
            double ey = (300.0/2 - 50.0) / 304.8;
            double aLen = 700.0 / 304.8;
            double aRad = 11.0 / 304.8;
            XYZ[] anchors = {
                new XYZ(ex, ey, 0), new XYZ(-ex, ey, 0),
                new XYZ(ex, -ey, 0), new XYZ(-ex, -ey, 0)
            };
            foreach(var apt in anchors) {
                CurveArrArray acaa = new CurveArrArray();
                CurveArray aca = new CurveArray();
                aca.Append(Arc.Create(apt, aRad, 0, Math.PI, new XYZ(1,0,0), new XYZ(0,1,0)));
                aca.Append(Arc.Create(apt, aRad, Math.PI, Math.PI*2, new XYZ(1,0,0), new XYZ(0,1,0)));
                acaa.Append(aca);
                Extrusion anchor = bpFamDoc.FamilyCreate.NewExtrusion(true, acaa, sp, 50.0/304.8);
                // Also downward for the embedment
                Extrusion embed = bpFamDoc.FamilyCreate.NewExtrusion(true, acaa, sp, 1.0);
                embed.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(-aLen);
                embed.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).Set(0.0);
            }
            
            tx.Commit();
        }
        bpFamDoc.SaveAs(basePlatePath);
        bpFamDoc.Close();
        sb.AppendLine($"[REGISTERED] Base Plate Family saved to {basePlatePath}");

        // --- AUTHOR END PLATE ---
        if (File.Exists(endPlatePath)) File.Delete(endPlatePath);
        Document epFamDoc = app.Application.NewFamilyDocument(templatePath);
        using (Transaction tx = new Transaction(epFamDoc, "Author End Plate"))
        {
            tx.Start();
            epFamDoc.OwnerFamily.FamilyCategory = epFamDoc.Settings.Categories.get_Item(BuiltInCategory.OST_StructConnections);
            
            // Extrude along Z? Family origin is (0,0,0) face-to-face.
            // Let's draw it on X-Y plane (like a plate on the floor) and we will rotate the instance in the project!
            double epW = 200.0 / 304.8;
            double epH = 700.0 / 304.8;
            double epThk = 19.0 / 304.8;
            
            CurveArrArray caa = new CurveArrArray();
            CurveArray ca = new CurveArray();
            ca.Append(Line.CreateBound(new XYZ(-epW/2, -epH/2, 0), new XYZ(epW/2, -epH/2, 0)));
            ca.Append(Line.CreateBound(new XYZ(epW/2, -epH/2, 0), new XYZ(epW/2, epH/2, 0)));
            ca.Append(Line.CreateBound(new XYZ(epW/2, epH/2, 0), new XYZ(-epW/2, epH/2, 0)));
            ca.Append(Line.CreateBound(new XYZ(-epW/2, epH/2, 0), new XYZ(-epW/2, -epH/2, 0)));
            caa.Append(ca);
            
            Plane sketchPlane = Plane.CreateByNormalAndOrigin(new XYZ(0,0,1), XYZ.Zero);
            SketchPlane sp = SketchPlane.Create(epFamDoc, sketchPlane);
            
            Extrusion plate = epFamDoc.FamilyCreate.NewExtrusion(true, caa, sp, epThk);
            
            // Bolts (7 rows, 2 cols, gauge 120)
            double gauge = 120.0 / 304.8 / 2;
            double[] rowZs = { 300, 200, 100, 0, -100, -200, -300 };
            double bRad = 11.0 / 304.8;
            
            foreach(double rz in rowZs) {
                foreach(double g in new double[]{gauge, -gauge}) {
                    XYZ center = new XYZ(g, rz/304.8, 0);
                    CurveArrArray bcaa = new CurveArrArray();
                    CurveArray bca = new CurveArray();
                    bca.Append(Arc.Create(center, bRad, 0, Math.PI, new XYZ(1,0,0), new XYZ(0,1,0)));
                    bca.Append(Arc.Create(center, bRad, Math.PI, Math.PI*2, new XYZ(1,0,0), new XYZ(0,1,0)));
                    bcaa.Append(bca);
                    
                    Extrusion bolt = epFamDoc.FamilyCreate.NewExtrusion(true, bcaa, sp, 50.0/304.8);
                }
            }
            tx.Commit();
        }
        epFamDoc.SaveAs(endPlatePath);
        epFamDoc.Close();
        sb.AppendLine($"[REGISTERED] End Plate Family saved to {endPlatePath}");

        // --- LOAD AND PLACE ---
        using (Transaction tx = new Transaction(mainDoc, "Place Connections"))
        {
            tx.Start();
            
            Family bpFam, epFam;
            mainDoc.LoadFamily(basePlatePath, out bpFam);
            mainDoc.LoadFamily(endPlatePath, out epFam);
            
            FamilySymbol bpSymbol = (FamilySymbol)mainDoc.GetElement(bpFam.GetFamilySymbolIds().First());
            if (!bpSymbol.IsActive) bpSymbol.Activate();
            
            FamilySymbol epSymbol = (FamilySymbol)mainDoc.GetElement(epFam.GetFamilySymbolIds().First());
            if (!epSymbol.IsActive) epSymbol.Activate();

            var cols = new FilteredElementCollector(mainDoc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(c => c.Symbol.FamilyName.Contains("Wide Flange")).ToList();
            
            foreach (var col in cols)
            {
                if (col.Location is LocationPoint lp)
                {
                    // Base Plate
                    XYZ basePt = new XYZ(lp.Point.X, lp.Point.Y, 2000.0 / 304.8);
                    FamilyInstance bpInst = mainDoc.Create.NewFamilyInstance(basePt, bpSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    sb.AppendLine($"Placed Base Plate at {basePt}");
                    
                    // End Plate
                    bool isLeft = lp.Point.X < 12000.0/304.8;
                    double epX = isLeft ? lp.Point.X + (200.0/304.8) : lp.Point.X - (200.0/304.8);
                    XYZ epPt = new XYZ(epX, lp.Point.Y, 11800.0/304.8);
                    
                    FamilyInstance epInst = mainDoc.Create.NewFamilyInstance(epPt, epSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    
                    // Rotate End plate to be vertical
                    // The family was drawn on X-Y plane (flat), so its Z points up.
                    // We need it to be vertical. Let's rotate it 90 degrees around Y axis.
                    Line rotAxis = Line.CreateBound(epPt, epPt + new XYZ(0, 1, 0));
                    double angle = isLeft ? -Math.PI/2 : Math.PI/2;
                    ElementTransformUtils.RotateElement(mainDoc, epInst.Id, rotAxis, angle);
                    sb.AppendLine($"Placed End Plate at {epPt}");
                }
            }
            tx.Commit();
        }
        
        return sb.ToString();
    }
    
    private string VerifyConnections(Document doc)
    {
        var conns = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructConnections)
            .WhereElementIsNotElementType()
            .ToList();
            
        return $"=== NATIVE STRUCTURAL CONNECTIONS VERIFICATION ===\n" +
               $"Found {conns.Count} elements of Category: OST_StructConnections\n" +
               string.Join("\n", conns.Select(c => $"ID: {c.Id}, Name: {c.Name}, Category: {c.Category.Name}"));
    }
}

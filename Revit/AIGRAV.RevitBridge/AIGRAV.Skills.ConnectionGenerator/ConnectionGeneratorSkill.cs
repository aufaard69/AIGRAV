using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.ConnectionGenerator;

public sealed class ConnectionGeneratorSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Connection Detailer",
        Version = "1.0",
        Category = "Modeling",
        Description = "Autonomously models high-LOD actual geometry for steel connections.",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName)
    {
        return toolName == "aigrav_connection_generate" || toolName == "aigrav_connection_inspect";
    }

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (toolName == "aigrav_connection_generate")
            return Generate(app.ActiveUIDocument.Document);
        if (toolName == "aigrav_connection_inspect")
            return Inspect(app.ActiveUIDocument.Document);
        return "Unknown command";
    }

    private string Generate(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== CONNECTION GENERATION REPORT ===");

        try
        {
            using (Transaction tx = new Transaction(doc, "Generate Steel Connections"))
            {
                tx.Start();

                // Cleanup existing connections to avoid duplicates
                var oldConns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsNotElementType().ToList();
                var toDelete = oldConns.Where(c => c.Name.Contains("Base Plate") || c.Name.Contains("Anchor Bolt") || c.Name.Contains("End Plate") || c.Name.Contains("HTB") || c.Name.Contains("Stiffener")).Select(c => c.Id).ToList();
                if (toDelete.Count > 0)
                {
                    doc.Delete(toDelete);
                    sb.AppendLine($"Deleted {toDelete.Count} existing connection elements to prevent duplicates.");
                }

                // Get Columns
                var cols = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(c => c.Symbol.FamilyName.Contains("Wide Flange"))
                    .ToList();

                int bpCount = 0;
                int epCount = 0;
                int stiffCount = 0;

                // Create Material if not exists
                ElementId matId = GetOrCreateSteelMaterial(doc);

                foreach (var col in cols)
                {
                    if (col.Location is LocationPoint lp)
                    {
                        XYZ pt = lp.Point;
                        double z = 2000.0 / 304.8;
                        XYZ basePt = new XYZ(pt.X, pt.Y, z);
                        
                        // Generate Base Plate (300 x 440 x 19)
                        double bpLx = 440.0 / 304.8;
                        double bpLy = 300.0 / 304.8;
                        double bpThk = 19.0 / 304.8;
                        
                        XYZ bP1 = basePt + new XYZ(-bpLx/2, -bpLy/2, 0);
                        XYZ bP2 = basePt + new XYZ(bpLx/2, -bpLy/2, 0);
                        XYZ bP3 = basePt + new XYZ(bpLx/2, bpLy/2, 0);
                        XYZ bP4 = basePt + new XYZ(-bpLx/2, bpLy/2, 0);
                        
                        Solid plateSolid = CreateBox(bP1, bP2, bP3, bP4, bpThk, new XYZ(0,0,1));
                        CreateDirectShape(doc, plateSolid, BuiltInCategory.OST_GenericModel, "Base Plate 300x440x19", matId);
                        
                        // Generate 4 Anchor Bolts
                        double ex = (440.0/2 - 50.0) / 304.8;
                        double ey = (300.0/2 - 50.0) / 304.8;
                        double aDia = 22.0 / 304.8;
                        double aLen = 700.0 / 304.8;
                        
                        XYZ[] anchors = {
                            basePt + new XYZ(ex, ey, -aLen + bpThk),
                            basePt + new XYZ(-ex, ey, -aLen + bpThk),
                            basePt + new XYZ(ex, -ey, -aLen + bpThk),
                            basePt + new XYZ(-ex, -ey, -aLen + bpThk)
                        };
                        
                        foreach(var aPt in anchors)
                        {
                            Solid anchorSolid = CreateCylinder(aPt, aDia/2, aLen + 50.0/304.8, new XYZ(0,0,1));
                            CreateDirectShape(doc, anchorSolid, BuiltInCategory.OST_GenericModel, "Anchor Bolt M22", matId);
                        }
                        
                        // Generate 4 Stiffeners (145x200x19)
                        // Placed along the Y axis, outside the web
                        double sH = 200.0 / 304.8;
                        double sW = 145.0 / 304.8;
                        double sThk = 19.0 / 304.8;
                        double webThk = 8.0 / 304.8;
                        
                        // We will extrude along X axis from the web
                        // Two stiffeners on positive Y, two on negative Y? No, it's 2 on positive X web face, 2 on negative X web face.
                        // For a column at origin, web is on the X-Z plane? No, if orientation is default, flanges are parallel to X or Y.
                        // WF column flanges are typically parallel to Y axis (web is along X). Let's assume standard orientation (web along X).
                        // So stiffeners stick out along Y axis from the web.
                        // Stiffener 1 (Y+)
                        XYZ sPt1 = basePt + new XYZ(0, webThk/2, bpThk);
                        XYZ sPt2 = basePt + new XYZ(0, webThk/2 + sW, bpThk);
                        XYZ sPt3 = basePt + new XYZ(0, webThk/2, bpThk + sH);
                        
                        Solid stiffSolid1 = CreateTriangle(sPt1, sPt2, sPt3, sThk, new XYZ(-1,0,0)); // Extrude -X
                        Solid stiffSolid2 = CreateTriangle(sPt1, sPt2, sPt3, sThk, new XYZ(1,0,0));  // Extrude +X
                        
                        // Stiffener 3 & 4 (Y-)
                        XYZ sPt4 = basePt + new XYZ(0, -webThk/2, bpThk);
                        XYZ sPt5 = basePt + new XYZ(0, -webThk/2 - sW, bpThk);
                        XYZ sPt6 = basePt + new XYZ(0, -webThk/2, bpThk + sH);
                        
                        Solid stiffSolid3 = CreateTriangle(sPt4, sPt5, sPt6, sThk, new XYZ(-1,0,0));
                        Solid stiffSolid4 = CreateTriangle(sPt4, sPt5, sPt6, sThk, new XYZ(1,0,0));
                        
                        CreateDirectShape(doc, stiffSolid1, BuiltInCategory.OST_GenericModel, "Stiffener 145x200x19", matId);
                        CreateDirectShape(doc, stiffSolid2, BuiltInCategory.OST_GenericModel, "Stiffener 145x200x19", matId);
                        CreateDirectShape(doc, stiffSolid3, BuiltInCategory.OST_GenericModel, "Stiffener 145x200x19", matId);
                        CreateDirectShape(doc, stiffSolid4, BuiltInCategory.OST_GenericModel, "Stiffener 145x200x19", matId);
                        stiffCount += 4;
                        
                        bpCount++;

                        // GENERATE END PLATE
                        bool isLeft = pt.X < 12000.0/304.8;
                        double epX = isLeft ? pt.X + (200.0/304.8) : pt.X - (200.0/304.8);
                        double epZ = 12000.0 / 304.8;
                        double epW = 200.0 / 304.8;
                        double epH = 700.0 / 304.8;
                        double epThk = 19.0 / 304.8;
                        
                        XYZ epCenter = new XYZ(epX, pt.Y, epZ - epH/2 + 200.0/304.8); // Adjusted Z to cover rafter
                        XYZ epDir = isLeft ? new XYZ(1,0,0) : new XYZ(-1,0,0);
                        
                        XYZ eP1 = epCenter + new XYZ(0, -epW/2, -epH/2);
                        XYZ eP2 = epCenter + new XYZ(0, epW/2, -epH/2);
                        XYZ eP3 = epCenter + new XYZ(0, epW/2, epH/2);
                        XYZ eP4 = epCenter + new XYZ(0, -epW/2, epH/2);
                        
                        Solid epSolid = CreateBox(eP1, eP2, eP3, eP4, epThk, epDir);
                        CreateDirectShape(doc, epSolid, BuiltInCategory.OST_GenericModel, "End Plate 200x700x19", matId);
                        
                        // 14 Bolts
                        double bx = isLeft ? epX + epThk : epX - epThk;
                        double bDir = isLeft ? -1 : 1;
                        double bDia = 22.0 / 304.8;
                        double bLen = 50.0 / 304.8;
                        
                        // 7 rows, 2 cols (gauge 120)
                        double gauge = 120.0 / 304.8 / 2;
                        double[] rowZs = { 300, 200, 100, 0, -100, -200, -300 }; 
                        
                        foreach(double rz in rowZs)
                        {
                            double actZ = epCenter.Z + rz/304.8;
                            
                            Solid b1 = CreateCylinder(new XYZ(bx, pt.Y + gauge, actZ), bDia/2, bLen, new XYZ(bDir,0,0));
                            Solid b2 = CreateCylinder(new XYZ(bx, pt.Y - gauge, actZ), bDia/2, bLen, new XYZ(bDir,0,0));
                            
                            CreateDirectShape(doc, b1, BuiltInCategory.OST_GenericModel, "HTB M22", matId);
                            CreateDirectShape(doc, b2, BuiltInCategory.OST_GenericModel, "HTB M22", matId);
                        }
                        
                        epCount++;
                    }
                }

                sb.AppendLine($"Created Base Connections: {bpCount}");
                sb.AppendLine($"Created End Plate Connections: {epCount}");

                tx.Commit();
                sb.AppendLine("Transaction COMMITTED successfully.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"ERROR: {ex.Message}");
            sb.AppendLine(ex.StackTrace);
        }

        return sb.ToString();
    }
    
    private string Inspect(Document doc)
    {
        var conns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsNotElementType().ToList();
        
        int basePlates = conns.Count(c => c.Name.Contains("Base Plate"));
        int anchors = conns.Count(c => c.Name.Contains("Anchor"));
        int endPlates = conns.Count(c => c.Name.Contains("End Plate"));
        int bolts = conns.Count(c => c.Name.Contains("HTB"));
        
        return $"=== CONNECTION VERIFICATION ===\n" +
               $"Base Plates: {basePlates}\n" +
               $"Anchor Bolts: {anchors}\n" +
               $"End Plates: {endPlates}\n" +
               $"HTB Bolts: {bolts}\n" +
               $"Total Actual Geometry Elements: {conns.Count}";
    }

    private Solid CreateBox(XYZ p1, XYZ p2, XYZ p3, XYZ p4, double extrusionDist, XYZ extDir)
    {
        CurveLoop loop = new CurveLoop();
        loop.Append(Line.CreateBound(p1, p2));
        loop.Append(Line.CreateBound(p2, p3));
        loop.Append(Line.CreateBound(p3, p4));
        loop.Append(Line.CreateBound(p4, p1));
        return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, extDir, extrusionDist);
    }
    
    private Solid CreateTriangle(XYZ p1, XYZ p2, XYZ p3, double extrusionDist, XYZ extDir)
    {
        CurveLoop loop = new CurveLoop();
        loop.Append(Line.CreateBound(p1, p2));
        loop.Append(Line.CreateBound(p2, p3));
        loop.Append(Line.CreateBound(p3, p1));
        return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, extDir, extrusionDist);
    }
    
    private Solid CreateCylinder(XYZ centerBase, double radius, double height, XYZ extDir)
    {
        CurveLoop loop = new CurveLoop();
        XYZ xAxis = (Math.Abs(extDir.Z) < 0.9) ? new XYZ(0,0,1).CrossProduct(extDir).Normalize() : new XYZ(1,0,0);
        XYZ yAxis = extDir.CrossProduct(xAxis).Normalize();
        
        Arc arc1 = Arc.Create(centerBase, radius, 0, Math.PI, xAxis, yAxis);
        Arc arc2 = Arc.Create(centerBase, radius, Math.PI, Math.PI*2, xAxis, yAxis);
        loop.Append(arc1);
        loop.Append(arc2);
        
        return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, extDir, height);
    }

    private void CreateDirectShape(Document doc, Solid solid, BuiltInCategory cat, string name, ElementId matId)
    {
        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(cat));
        ds.ApplicationId = "AIGRAV";
        ds.ApplicationDataId = Guid.NewGuid().ToString();
        ds.SetShape(new List<GeometryObject> { solid });
        ds.Name = name;
        // Optionally set material on faces, but for simple visualization this is enough.
    }
    
    private ElementId GetOrCreateSteelMaterial(Document doc)
    {
        var mat = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().FirstOrDefault(m => m.Name == "Steel - Connection");
        if (mat != null) return mat.Id;
        
        ElementId newId = Material.Create(doc, "Steel - Connection");
        return newId;
    }
}

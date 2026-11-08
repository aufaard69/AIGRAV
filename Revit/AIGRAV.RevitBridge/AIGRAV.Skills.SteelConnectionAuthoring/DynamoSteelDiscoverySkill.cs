using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using AIGRAV.Contracts;
using System.Collections.Generic;

namespace AIGRAV.Skills.SteelConnectionAuthoring;

public sealed class DynamoSteelLoaderSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Dynamo Steel Loader",
        Version = "1.0",
        Category = "Modeling",
        Description = "Loads structural connection types via Dynamo nodes",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName) => toolName == "aigrav_test_dynamo_load" || toolName == "aigrav_test_base_plate_seed";

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (toolName == "aigrav_test_dynamo_load") return TestDynamoLoad(app.ActiveUIDocument.Document);
        if (toolName == "aigrav_test_base_plate_seed") return TestSeed(app.ActiveUIDocument.Document);
        return "Unknown";
    }

    private string TestDynamoLoad(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== DYNAMO CONNECTION LOAD TEST ===");

        string dllPath = @"C:\Program Files\Autodesk\Revit 2026\AddIns\DynamoForRevit\Revit\nodes\steel-pkg\bin\AdvanceSteelConnAutoNodes.dll";
        string libraryPath = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English-Imperial\US\Structural Connections\Steel\AISC Connections V15\AISC 14.3 V15.rvt";

        if (!File.Exists(dllPath)) return "AdvanceSteelConnAutoNodes.dll not found.";
        if (!File.Exists(libraryPath)) return "AISC 14.3 V15.rvt not found.";

        try
        {
            Assembly assem = Assembly.LoadFrom(dllPath);
            Type t = assem.GetType("AdvanceSteel.ConnectionAutomation.Nodes.ConnectionType");
            if (t == null) return "Type ConnectionType not found.";

            MethodInfo loadMethod = t.GetMethod("LoadFromFile", BindingFlags.Public | BindingFlags.Static);
            if (loadMethod == null) return "LoadFromFile method not found.";

            sb.AppendLine("Dynamo ConnectionType.LoadFromFile: FOUND");
            sb.AppendLine($"Actual implementation: {t.FullName}.LoadFromFile");
            
            int countBefore = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandlerType)).Count();
            sb.AppendLine($"Before: {countBefore}");

            try
            {
                using (TransactionGroup tg = new TransactionGroup(doc, "Dynamo Load"))
                {
                    tg.Start();
                    var result = loadMethod.Invoke(null, new object[] { libraryPath, "Base plate", null });
                    sb.AppendLine($"Invoked LoadFromFile. Result: {result}");
                    tg.Commit();
                }
            }
            catch (Exception innerEx)
            {
                sb.AppendLine($"Exception invoking LoadFromFile: {innerEx.InnerException?.Message ?? innerEx.Message}");
            }

            int countAfter = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandlerType)).Count();
            sb.AppendLine($"After: {countAfter}");
            
            var types = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandlerType)).Cast<StructuralConnectionHandlerType>().ToList();
            var bp = types.FirstOrDefault(x => x.Name.Contains("Base plate", StringComparison.OrdinalIgnoreCase));
            if (bp != null)
            {
                sb.AppendLine("Base Plate loaded: YES");
                sb.AppendLine($"Name: {bp.Name}");
                sb.AppendLine($"TypeId: {bp.Id}");
                sb.AppendLine("Autonomous loading capability: YES");
            }
            else
            {
                sb.AppendLine("Base Plate loaded: NO");
                sb.AppendLine("Autonomous loading capability: UNKNOWN");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Error: {ex.Message}");
        }

        return sb.ToString();
    }

    private string TestSeed(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        var types = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandlerType)).Cast<StructuralConnectionHandlerType>().ToList();
        var bp = types.FirstOrDefault(x => x.Name.Contains("Base plate", StringComparison.OrdinalIgnoreCase));

        if (bp == null) return "Standard Base Plate seed: FAIL (Type not loaded)";

        var col = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .FirstOrDefault();

        var foundation = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFoundation)
            .WhereElementIsNotElementType()
            .FirstOrDefault();

        if (col == null) return "Standard Base Plate seed: FAIL (No column)";

        List<ElementId> inputIds = new List<ElementId> { col.Id };
        if (foundation != null) inputIds.Add(foundation.Id);

        StructuralConnectionHandler seedHandler = null;
        try
        {
            using (Transaction tx = new Transaction(doc, "Create Seed"))
            {
                tx.Start();
                seedHandler = StructuralConnectionHandler.Create(doc, inputIds, bp.Id);
                tx.Commit();
            }
            doc.Regenerate();
        }
        catch (Exception ex)
        {
            return $"Standard Base Plate seed: FAIL ({ex.Message})";
        }

        if (seedHandler == null) return "Standard Base Plate seed: FAIL (Handler is null)";

        sb.AppendLine("Standard Base Plate seed: PASS");

        var subelements = seedHandler.GetSubelements();
        int plateCount = 0, anchorCount = 0, boltCount = 0, weldCount = 0;
        foreach (var subel in subelements)
        {
            if (subel == null) continue;
            BuiltInCategory cat = (BuiltInCategory)subel.Category.Id.Value;
            if (cat == BuiltInCategory.OST_StructConnectionPlates) plateCount++;
            else if (cat == BuiltInCategory.OST_StructConnectionAnchors) anchorCount++;
            else if (cat == BuiltInCategory.OST_StructConnectionBolts) boltCount++;
            else if (cat == BuiltInCategory.OST_StructConnectionWelds) weldCount++;
        }

        sb.AppendLine($"Native Plate: {plateCount}");
        sb.AppendLine($"Native Anchor: {anchorCount}");
        sb.AppendLine($"Native Bolt: {boltCount}");
        sb.AppendLine($"Native Weld: {weldCount}");

        return sb.ToString();
    }
}

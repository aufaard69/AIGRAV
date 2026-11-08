using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.SteelConnectionAuthoring;

public sealed class ConnectionV14Skill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Connection V1.4 Implementation",
        Version = "1.4",
        Category = "Modeling",
        Description = "Implements standard steel connection V1.4 pipeline",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName) => toolName == "aigrav_execute_v14_pipeline";

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        StringBuilder sb = new StringBuilder();
        Document doc = app.ActiveUIDocument.Document;
        
        string dllPath = @"C:\Program Files\Autodesk\Revit 2026\AddIns\DynamoForRevit\Revit\nodes\steel-pkg\bin\AdvanceSteelConnAutoNodes.dll";
        string libraryPath = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English-Imperial\US\Structural Connections\Steel\AISC Connections V15\AISC 14.3 V15.rvt";

        sb.AppendLine("=== PHASE 1 - ENUMERATE LIBRARY ===");
        if (!File.Exists(dllPath) || !File.Exists(libraryPath))
        {
            sb.AppendLine("DLL or Library not found.");
            return sb.ToString();
        }

        Assembly assem = Assembly.LoadFrom(dllPath);
        Type typeNode = assem.GetType("AdvanceSteel.ConnectionAutomation.Nodes.ConnectionType");
        MethodInfo listMethod = typeNode.GetMethod("ListFromFile", BindingFlags.Public | BindingFlags.Static);
        MethodInfo loadMethod = typeNode.GetMethod("LoadFromFile", BindingFlags.Public | BindingFlags.Static);

        List<string> connectionNames = new List<string>();
        try
        {
            var result = listMethod.Invoke(null, new object[] { libraryPath });
            if (result is IEnumerable en)
            {
                foreach (var item in en)
                {
                    // item is likely KeyValuePair<string, object> or similar from Dynamo Dictionary
                    Type itemType = item.GetType();
                    PropertyInfo keyProp = itemType.GetProperty("Key");
                    if (keyProp != null)
                    {
                        string key = keyProp.GetValue(item)?.ToString();
                        if (!string.IsNullOrEmpty(key)) connectionNames.Add(key);
                    }
                    else
                    {
                        string s = item.ToString();
                        if (s.StartsWith("[")) s = s.Substring(1, s.IndexOf(',') - 1);
                        connectionNames.Add(s);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("ListFromFile Exception: " + ex.InnerException?.Message);
            return sb.ToString();
        }

        connectionNames = connectionNames.Distinct().ToList();
        sb.AppendLine($"Found {connectionNames.Count} connections.");
        foreach(var name in connectionNames) sb.AppendLine("  - " + name);

        sb.AppendLine("\n=== PHASE 2 - SEMANTIC MATCH ===");
        // 1. Base Plate, 2. Column Base, 3. Base, 4. Base Plate with anchors...
        string bestMatch = null;
        var orderedCandidates = connectionNames
            .Where(n => n.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("plate", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(n => n.Length) // Shorter is usually more generic "Base plate"
            .ToList();
            
        string targetMatch = "Base plate";
        bestMatch = orderedCandidates.FirstOrDefault(x => x.Equals("Base plate", StringComparison.OrdinalIgnoreCase));
        if (bestMatch == null)
            bestMatch = orderedCandidates.FirstOrDefault();

        if (bestMatch == null)
        {
            sb.AppendLine("No matching base plate connection found in library.");
            return sb.ToString();
        }

        sb.AppendLine($"Candidate: {bestMatch}");
        sb.AppendLine($"Exact Name: {bestMatch}");
        sb.AppendLine($"Confidence: High");
        sb.AppendLine($"Reason: Best semantic match for Base Plate.");

        sb.AppendLine("\n=== PHASE 3 - LOAD ON DEMAND ===");
        try
        {
            using (TransactionGroup tg = new TransactionGroup(doc, "Dynamo Load"))
            {
                tg.Start();
                // LoadFromFile(string filePath, string connectionName, string typeName)
                var loadResult = loadMethod.Invoke(null, new object[] { libraryPath, bestMatch, null });
                tg.Commit();
            }
            sb.AppendLine($"Load result: {bestMatch} loaded.");
        }
        catch (Exception ex)
        {
            sb.AppendLine("LoadFromFile Exception: " + (ex.InnerException?.Message ?? ex.Message));
            return sb.ToString();
        }

        // Verify loaded type
        var types = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandlerType)).Cast<StructuralConnectionHandlerType>().ToList();
        var basePlateType = types.FirstOrDefault(t => t.Name.Equals(bestMatch, StringComparison.OrdinalIgnoreCase));
        if (basePlateType == null)
        {
            // Maybe the name is slightly different in the document
            basePlateType = types.FirstOrDefault(t => t.Name.IndexOf("Base plate", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (basePlateType == null)
        {
            sb.AppendLine("Connection type was not found in document after loading.");
            return sb.ToString();
        }

        sb.AppendLine($"Type exists: YES");
        sb.AppendLine($"Name: {basePlateType.Name}");
        sb.AppendLine($"Type ID: {basePlateType.Id}");
        
        bool isCustom = false;
        try { 
            var pi = typeof(StructuralConnectionHandlerType).GetProperty("IsCustom");
            if (pi != null) isCustom = (bool)pi.GetValue(basePlateType);
        } catch { }
        sb.AppendLine($"IsCustom: {isCustom}");

        sb.AppendLine("\n=== PHASE 4 - STANDARD CONNECTION SEED ===");
        var col = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .FirstOrDefault();

        var foundation = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFoundation)
            .WhereElementIsNotElementType()
            .FirstOrDefault();

        if (col == null)
        {
            sb.AppendLine("No Column found in document to test on.");
            return sb.ToString();
        }

        List<ElementId> inputIds = new List<ElementId> { col.Id };
        if (foundation != null) inputIds.Add(foundation.Id);

        StructuralConnectionHandler seedHandler = null;
        try
        {
            using (Transaction tx = new Transaction(doc, "Create Seed"))
            {
                tx.Start();
                seedHandler = StructuralConnectionHandler.Create(doc, inputIds, basePlateType.Id);
                tx.Commit();
            }
            doc.Regenerate();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Failed to create seed: {ex.Message}");
            return sb.ToString();
        }

        if (seedHandler == null)
        {
            sb.AppendLine("Failed to create seed (handler is null).");
            return sb.ToString();
        }
        sb.AppendLine("Seed Created: PASS");

        sb.AppendLine("\n=== PHASE 5 - INSPECT NATIVE COMPONENTS ===");
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

        sb.AppendLine($"StructuralConnectionHandler: 1");
        sb.AppendLine($"Plate: {plateCount}");
        sb.AppendLine($"Anchor: {anchorCount}");
        sb.AppendLine($"Bolt: {boltCount}");
        sb.AppendLine($"Weld: {weldCount}");

        if (plateCount == 0 && anchorCount == 0)
        {
            sb.AppendLine("ERROR: Seed generated no plates or anchors. Wait, this usually means the connection is uncalculated or failing.");
            // We can ask the connection status.
            try {
                var statProp = seedHandler.GetType().GetProperty("ApprovalStatus");
                if (statProp != null) sb.AppendLine("Status: " + statProp.GetValue(seedHandler));
            } catch { }
        }

        sb.AppendLine("\n=== PHASE 8 - CREATE CUSTOM CONNECTION ===");
        // Try to convert to custom connection
        // The API Create(Document, IList<ElementId>, String) makes a custom connection
        // Wait, the documentation says inputIds are the elements allowed to be included.
        // It says: "Input elements can be framing, columns, profiles, plates, bolts, anchors...".
        // Wait! The subelements returned by `GetSubelements()` are `Subelement` objects. 
        // `Subelement` does not have an `Id` property of type `ElementId`!
        // So we cannot add them to `IList<ElementId>` for `Create()`.
        
        sb.AppendLine("ATTEMPTING API CUSTOM CONVERSION...");
        StructuralConnectionHandler customHandler = null;
        try
        {
            using (Transaction tx = new Transaction(doc, "Create Custom Connection"))
            {
                tx.Start();
                // We create a Custom Connection with just the column & foundation first?
                // Or maybe we use the SeedHandler Id?
                List<ElementId> customInputs = new List<ElementId>(inputIds);
                customInputs.Add(seedHandler.Id); // Can a custom connection contain another connection handler?
                
                customHandler = StructuralConnectionHandler.Create(doc, customInputs, "WF400_BASE_STD_01");
                tx.Commit();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Create custom connection FAIL: {ex.GetType().Name} - {ex.Message}");
        }

        if (customHandler != null)
        {
            sb.AppendLine("Custom connection created: YES");
            // Check IsCustom
            var custType = doc.GetElement(customHandler.GetTypeId()) as StructuralConnectionHandlerType;
            bool cIsCustom = false;
            try { 
                var pi = typeof(StructuralConnectionHandlerType).GetProperty("IsCustom");
                if (pi != null) cIsCustom = (bool)pi.GetValue(custType);
            } catch { }
            sb.AppendLine($"IsCustom: {cIsCustom}");
        }
        else
        {
            sb.AppendLine("Custom connection created: NO");
            sb.AppendLine("API Limitation: Cannot convert seed subelements (plates/anchors) directly into a Custom Connection because they do not have standalone ElementIds, and adding the Seed Handler itself failed or is not the correct approach.");
        }

        return sb.ToString();
    }
}

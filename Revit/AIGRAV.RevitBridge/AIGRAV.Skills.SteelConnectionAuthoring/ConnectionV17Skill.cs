using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.SteelConnectionAuthoring;

public sealed class ConnectionV17Skill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Connection Custom Authoring V1.7",
        Version = "1.7",
        Category = "Modeling",
        Description = "Create Custom Connection from Native Subelements",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName) => toolName == "aigrav_execute_v17_pipeline";

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        StringBuilder sb = new StringBuilder();
        Document doc = app.ActiveUIDocument.Document;
        StructuralConnectionHandler stdHandler = new FilteredElementCollector(doc)
            .OfClass(typeof(StructuralConnectionHandler))
            .Cast<StructuralConnectionHandler>()
            .FirstOrDefault();

        if (stdHandler == null) return "FAIL: No StructuralConnectionHandler found in document.";
        sb.AppendLine($"Using Standard Handler ID: {stdHandler.Id}");

        sb.AppendLine("=== PHASE 1 & 2 - SUBELEMENT REFERENCES ===");
        var subelements = stdHandler.GetSubelements();
        List<Reference> createdReferences = new List<Reference>();
        
        sb.AppendLine($"Total subelements found: {subelements.Count}");
        foreach (var subel in subelements)
        {
            if (subel == null) continue;
            BuiltInCategory cat = (BuiltInCategory)subel.Category.Id.Value;
            
            // Try to get Reference
            Reference r = null;
            string refStr = "NOT FOUND";
            string rep = "N/A";
            
            try 
            {
                // Subelement.GetReference() or similar?
                var m = typeof(Subelement).GetMethod("GetReference");
                if (m != null)
                {
                    r = m.Invoke(subel, null) as Reference;
                }
                else
                {
                    // Fallback to Subelement.Id or just Reference constructor?
                    // There's Subelement.UniqueId or Subelement.GetBoundingBox()...
                    // Actually Subelement doesn't always have GetReference.
                    sb.AppendLine("GetReference() method not found on Subelement.");
                }

                if (r != null)
                {
                    refStr = "FOUND";
                    rep = r.ConvertToStableRepresentation(doc);
                    createdReferences.Add(r);
                }
            }
            catch (Exception ex)
            {
                refStr = "EXCEPTION: " + ex.Message;
            }

            sb.AppendLine($"Category: {cat} | Ref: {refStr} | Stable: {rep}");
        }

        sb.AppendLine("\n=== PHASE 3 - EMPTY CUSTOM CONNECTION ===");
        var connectedCols = stdHandler.GetConnectedElementIds();
        var validConnected = new List<ElementId>();
        validConnected.Add(stdHandler.Id);
        foreach(var cid in connectedCols) {
            var el = doc.GetElement(cid);
            if (el.Category.Id.Value != (int)BuiltInCategory.OST_StructuralFoundation) {
                validConnected.Add(cid);
            }
        }
        sb.AppendLine($"Filtered Foundation. Total inputs: {validConnected.Count}");
        
        StructuralConnectionHandler customHandler = null;
        try
        {
            using (Transaction tx = new Transaction(doc, "Create Empty Custom Connection"))
            {
                tx.Start();
                customHandler = StructuralConnectionHandler.Create(doc, validConnected, "WF400_BASE_STD_01");
                doc.Regenerate();
                tx.Commit();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"FAIL: {ex.Message}");
            return sb.ToString();
        }

        if (customHandler == null) {
            sb.AppendLine("FAIL: Custom Handler is null.");
            return sb.ToString();
        }
        sb.AppendLine($"Custom Handler Created: {customHandler.Id}");

        sb.AppendLine("\n=== PHASE 4 - CUSTOM TYPE INSPECTION ===");
        StructuralConnectionHandlerType customType = doc.GetElement(customHandler.GetTypeId()) as StructuralConnectionHandlerType;
        if (customType != null)
        {
            bool isCust = false;
            try { 
                var pi = typeof(StructuralConnectionHandlerType).GetProperty("IsCustom");
                if (pi != null) isCust = (bool)pi.GetValue(customType);
            } catch { }
            sb.AppendLine($"IsCustom: {isCust}");
            sb.AppendLine($"Type Name: {customType.Name}");
        }
        else
        {
            sb.AppendLine("Custom Type is null.");
        }

        sb.AppendLine("\n=== PHASE 5 & 6 - UPDATE CUSTOM CONNECTION ===");
        try
        {
            using (Transaction tx = new Transaction(doc, "Update Custom Connection"))
            {
                tx.Start();
                // IList<Reference> removedElements = new List<Reference>();
                // customType.UpdateCustomConnectionType(customHandler, createdReferences, removedElements);
                
                // Invoke via reflection in case method signature is different
                var mUpdate = typeof(StructuralConnectionHandlerType).GetMethod("UpdateCustomConnectionType");
                if (mUpdate != null)
                {
                    mUpdate.Invoke(customType, new object[] { customHandler, createdReferences, new List<Reference>() });
                    sb.AppendLine("UpdateCustomConnectionType: SUCCESS");
                }
                else
                {
                    sb.AppendLine("Method UpdateCustomConnectionType not found on StructuralConnectionHandlerType.");
                }
                tx.Commit();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Update FAILED.");
            sb.AppendLine($"Message: {ex.Message}");
            if (ex.InnerException != null) sb.AppendLine($"Inner: {ex.InnerException.Message}");
        }

        sb.AppendLine("\n=== PHASE 7 & 8 - VERIFY CUSTOM ===");
        var customSubels = customHandler.GetSubelements();
        int p = 0, a = 0, b = 0, w = 0, s = 0;
        foreach (var subel in customSubels)
        {
            if (subel == null) continue;
            BuiltInCategory cat = (BuiltInCategory)subel.Category.Id.Value;
            if (cat == BuiltInCategory.OST_StructConnectionPlates) p++;
            else if (cat == BuiltInCategory.OST_StructConnectionAnchors) a++;
            else if (cat == BuiltInCategory.OST_StructConnectionBolts) b++;
            else if (cat == BuiltInCategory.OST_StructConnectionWelds) w++;
        }
        sb.AppendLine($"Plates: {p}, Anchors: {a}, Bolts: {b}, Welds: {w}");
        
        sb.AppendLine("\n=== PHASE 9 - REGISTER ===");
        if (p > 0 || a > 0)
        {
            string dbPath = @"C:\AIGRAV\KnowledgeBase\AIGRAV.ConnectionDatabase.json";
            string json = "{\n  \"connectionId\": \"WF400_BASE_STD_01\",\n  \"category\": \"Base\",\n  \"profile\": \"WF400\",\n  \"nativeRevit\": true,\n  \"isCustom\": true,\n  \"isDetailed\": true,\n  \"source\": \"Company Steel Standard\",\n  \"version\": \"1.0\"\n}";
            try { File.WriteAllText(dbPath, json); sb.AppendLine("Registered to JSON."); } catch { }
        }
        else
        {
            sb.AppendLine("Not registering, verification failed.");
        }

        return sb.ToString();
    }
}

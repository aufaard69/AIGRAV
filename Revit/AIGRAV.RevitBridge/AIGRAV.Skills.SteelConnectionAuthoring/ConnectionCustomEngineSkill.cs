using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using AIGRAV.Contracts;
using Autodesk.Revit.UI.Events;

namespace AIGRAV.Skills.SteelConnectionAuthoring;

public class CustomConnectionJob
{
    public ElementId HandlerId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ConnectionCustomEngineSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Custom Connection Authoring V1.7.1",
        Version = "1.7.1",
        Category = "Modeling",
        Description = "Create Custom Connection using Event Engine",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName) => toolName == "aigrav_execute_v17_custom";

    private static CustomConnectionJob currentJob;
    private static UIApplication currentApp;
    private static bool isSubscribed = false;

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (isSubscribed) return "Already running.";
        
        Document doc = app.ActiveUIDocument.Document;
        currentApp = app;
        
        var types = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandlerType)).Cast<StructuralConnectionHandlerType>().ToList();
        var bpType = types.FirstOrDefault(t => t.Name.Equals("Base plate", StringComparison.OrdinalIgnoreCase));
        if (bpType == null) return "FAIL: Base plate not loaded.";

        var allCols = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
        if (allCols.Count == 0) return "FAIL: No columns found.";
        
        // Find a column with NO connections
        var allConnections = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandler)).Cast<StructuralConnectionHandler>().ToList();
        var connectedColIds = new HashSet<ElementId>();
        foreach(var c in allConnections) {
            foreach(var id in c.GetConnectedElementIds()) {
                connectedColIds.Add(id);
            }
        }
        
        var freeCol = allCols.FirstOrDefault(c => !connectedColIds.Contains(c.Id));
        if (freeCol == null) freeCol = allCols.First(); // Fallback
        
        StructuralConnectionHandler seedHandler = null;
        try
        {
            using (Transaction tx = new Transaction(doc, "Create Seed Without Foundation"))
            {
                tx.Start();
                // ONLY use column
                List<ElementId> inputIds = new List<ElementId> { freeCol.Id };
                seedHandler = StructuralConnectionHandler.Create(doc, inputIds, bpType.Id);
                tx.Commit();
            }
        }
        catch (Exception ex)
        {
            return $"FAIL: Could not create base plate without foundation. {ex.Message}";
        }

        currentJob = new CustomConnectionJob { HandlerId = seedHandler.Id, CreatedAt = DateTime.Now };
        app.Idling += OnIdling;
        isSubscribed = true;
        
        return $"Event Engine Started for Custom Connection. Seed ID: {seedHandler.Id}.";
    }

    private void OnIdling(object sender, IdlingEventArgs e)
    {
        try { File.AppendAllText(@"C:\AIGRAV\KnowledgeBase\IdlingDebug.txt", "T"); } catch {}
        if (currentJob == null) return;
        UIApplication app = sender as UIApplication;
        Document doc = app.ActiveUIDocument.Document;
        
        StructuralConnectionHandler handler = doc.GetElement(currentJob.HandlerId) as StructuralConnectionHandler;
        bool isReady = false;
        if (handler != null && handler.GetSubelements().Count > 0) isReady = true;
        bool timeout = (DateTime.Now - currentJob.CreatedAt).TotalSeconds > 20;

        if (isReady || timeout)
        {
            app.Idling -= OnIdling;
            isSubscribed = false;
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== CUSTOM CONNECTION PIPELINE ===");
            
            if (isReady)
            {
                var subels = handler.GetSubelements();
                List<Reference> createdReferences = new List<Reference>();
                foreach (var subel in subels)
                {
                    try {
                        var m = typeof(Subelement).GetMethod("GetReference");
                        if (m != null) {
                            Reference r = m.Invoke(subel, null) as Reference;
                            if (r != null) createdReferences.Add(r);
                        }
                    } catch {}
                }
                
                sb.AppendLine($"Seed Subelements Ready: {subels.Count}");
                sb.AppendLine($"Extracted References: {createdReferences.Count}");
                
                try
                {
                    StructuralConnectionHandler customHandler = null;
                    using (Transaction tx = new Transaction(doc, "Create Custom Connection"))
                    {
                        tx.Start();
                        customHandler = StructuralConnectionHandler.Create(doc, new List<ElementId> { handler.Id }, "WF400_BASE_STD_01");
                        doc.Regenerate();
                        
                        if (customHandler != null)
                        {
                            sb.AppendLine($"Custom Handler Created: {customHandler.Id}");
                            StructuralConnectionHandlerType customType = doc.GetElement(customHandler.GetTypeId()) as StructuralConnectionHandlerType;
                            
                            var mUpdate = typeof(StructuralConnectionHandlerType).GetMethod("UpdateCustomConnectionType");
                            if (mUpdate != null)
                            {
                                mUpdate.Invoke(customType, new object[] { customHandler, createdReferences, new List<Reference>() });
                                sb.AppendLine("UpdateCustomConnectionType: SUCCESS");
                            }
                            else
                            {
                                sb.AppendLine("Method UpdateCustomConnectionType not found.");
                            }
                        }
                        tx.Commit();
                    }
                    
                    if (customHandler != null) {
                        var cSubels = customHandler.GetSubelements();
                        sb.AppendLine($"Final Custom Subelements Count: {cSubels.Count}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"FAIL Custom Creation: {ex.Message}");
                }
            }
            else
            {
                sb.AppendLine("TIMEOUT: Seed connection did not generate subelements.");
            }
            
            try { File.WriteAllText(@"C:\AIGRAV\KnowledgeBase\CustomConnection_V1.7_Report.md", sb.ToString()); } catch { }
            currentJob = null;
        }
    }
}

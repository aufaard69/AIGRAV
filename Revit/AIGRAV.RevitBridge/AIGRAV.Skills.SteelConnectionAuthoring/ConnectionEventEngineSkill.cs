using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.DB.Structure;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.SteelConnectionAuthoring;

public class ConnectionJob
{
    public string JobId { get; set; }
    public ElementId HandlerId { get; set; }
    public string State { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastCheck { get; set; }
    public int RetryCount { get; set; }
    public string Error { get; set; }
}

public sealed class ConnectionEventEngineSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Connection Event Engine V1.6",
        Version = "1.6",
        Category = "Modeling",
        Description = "Event-driven architecture for async steel connections",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName) => toolName == "aigrav_start_event_engine";

    private static ConnectionJob currentJob;
    private static UIApplication currentApp;
    private static bool isSubscribed = false;

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (isSubscribed)
        {
            return "Event Engine is already running. Please wait.";
        }

        Document doc = app.ActiveUIDocument.Document;
        currentApp = app;

        // Ensure Base plate is loaded
        var types = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandlerType)).Cast<StructuralConnectionHandlerType>().ToList();
        var bpType = types.FirstOrDefault(t => t.Name.Equals("Base plate", StringComparison.OrdinalIgnoreCase));
        if (bpType == null) return "FAIL: Base plate not loaded in template.";

        // Find column
        var col = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().Cast<FamilyInstance>().FirstOrDefault(x => x.Symbol.FamilyName.Contains("WF") || x.Name.Contains("400"));
        if (col == null) col = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().Cast<FamilyInstance>().FirstOrDefault();
        if (col == null) return "FAIL: No column found.";

        // Find foundation
        var foundation = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation).WhereElementIsNotElementType().FirstOrDefault();

        StructuralConnectionHandler seedHandler = null;
        try
        {
            using (Transaction tx = new Transaction(doc, "Create Async Seed Connection"))
            {
                tx.Start();
                List<ElementId> inputIds = new List<ElementId> { col.Id };
                try {
                    seedHandler = StructuralConnectionHandler.Create(doc, inputIds, bpType.Id);
                } catch {
                    if (foundation != null) {
                        inputIds.Add(foundation.Id);
                        seedHandler = StructuralConnectionHandler.Create(doc, inputIds, bpType.Id);
                    }
                }
                tx.Commit();
            }
        }
        catch (Exception ex)
        {
            return $"FAIL: {ex.Message}";
        }

        if (seedHandler == null) return "FAIL: seedHandler is null.";

        currentJob = new ConnectionJob
        {
            JobId = "JOB_" + Guid.NewGuid().ToString().Substring(0, 8),
            HandlerId = seedHandler.Id,
            State = "CREATED",
            CreatedAt = DateTime.Now,
            LastCheck = DateTime.Now,
            RetryCount = 0
        };

        app.Idling += OnIdling;
        isSubscribed = true;

        return $"Event Engine Started. Handler ID: {seedHandler.Id}. Job ID: {currentJob.JobId}. Idling event subscribed. Returning control to Revit.";
    }

    private void OnIdling(object sender, IdlingEventArgs e)
    {
        if (currentJob == null) return;
        
        UIApplication app = sender as UIApplication;
        Document doc = app.ActiveUIDocument.Document;

        currentJob.RetryCount++;
        currentJob.LastCheck = DateTime.Now;
        currentJob.State = "WAITING";

        bool isReady = false;
        string inspectReport = "";

        StructuralConnectionHandler handler = doc.GetElement(currentJob.HandlerId) as StructuralConnectionHandler;
        if (handler != null)
        {
            var subels = handler.GetSubelements();
            if (subels != null && subels.Count > 0)
            {
                isReady = true;
                currentJob.State = "READY";
                
                int p = 0, a = 0, b = 0, w = 0, s = 0;
                StringBuilder sb = new StringBuilder();
                foreach (var subel in subels)
                {
                    if (subel == null) continue;
                    BuiltInCategory cat = (BuiltInCategory)subel.Category.Id.Value;
                    if (cat == BuiltInCategory.OST_StructConnectionPlates) p++;
                    else if (cat == BuiltInCategory.OST_StructConnectionAnchors) a++;
                    else if (cat == BuiltInCategory.OST_StructConnectionBolts) b++;
                    else if (cat == BuiltInCategory.OST_StructConnectionWelds) w++;
                    // Removed stiffener specific category check to avoid compilation issues, assumed under plates if applicable
                    sb.AppendLine($"- Cat: {cat}, Class: Subelement (no distinct API class exposed)");
                }
                inspectReport = $"Plate: {p}\nAnchor: {a}\nBolt: {b}\nStiffener: UNKNOWN (Grouped)\nWeld: {w}\n\nDetails:\n{sb.ToString()}";
            }
        }

        bool timeout = (DateTime.Now - currentJob.CreatedAt).TotalSeconds > 30;

        if (isReady || timeout)
        {
            // Unsubscribe immediately
            app.Idling -= OnIdling;
            isSubscribed = false;

            // Phase 6 Customize Investigation
            string customizeReport = InvestigateCustomizeAPI();

            // Write final report
            string reportPath = @"C:\AIGRAV\KnowledgeBase\SteelConnectionEvent_V1.6_Report.md";
            StringBuilder finalRep = new StringBuilder();
            finalRep.AppendLine("# === CONNECTION EVENT ENGINE V1.6 ===");
            finalRep.AppendLine();
            finalRep.AppendLine($"Handler: {currentJob.HandlerId}");
            finalRep.AppendLine($"Creation: PASS");
            finalRep.AppendLine($"Async state: {currentJob.State}");
            finalRep.AppendLine($"Idling event: PASS");
            finalRep.AppendLine($"Connection READY: {(isReady ? "TRUE" : "FALSE")}");
            finalRep.AppendLine($"Wait duration: {(DateTime.Now - currentJob.CreatedAt).TotalSeconds:F2} seconds");
            finalRep.AppendLine($"Retry count: {currentJob.RetryCount}");
            finalRep.AppendLine();
            finalRep.AppendLine("Subelements:");
            if (isReady)
                finalRep.AppendLine(inspectReport);
            else
                finalRep.AppendLine("None (Timeout)");
            finalRep.AppendLine();
            finalRep.AppendLine(customizeReport);
            finalRep.AppendLine();
            finalRep.AppendLine("Overall: " + (isReady ? "PASS" : "FAIL (Timeout or Calculation Error)"));

            try
            {
                File.WriteAllText(reportPath, finalRep.ToString());
            }
            catch { }
            
            currentJob = null;
        }
    }

    private string InvestigateCustomizeAPI()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Customize API:");
        bool apiFound = false;

        // Investigate StructuralConnectionHandlerType methods
        var t = typeof(StructuralConnectionHandlerType);
        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        
        string equiv = "";
        foreach(var m in methods)
        {
            if (m.Name.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.Name.IndexOf("Duplicate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.Name.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.Name.IndexOf("Copy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (m.Name == "UpdateCustomConnectionType") {
                    equiv += $"- {m.Name}\n";
                }
            }
        }

        // StructuralConnectionHandler methods
        var t2 = typeof(StructuralConnectionHandler);
        var methods2 = t2.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        foreach (var m in methods2)
        {
            if (m.Name.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (m.Name == "Create") // Has a Create that makes custom type if name doesn't exist
                {
                    equiv += $"- StructuralConnectionHandler.Create(..., typeName)\n";
                }
            }
        }

        if (equiv.Contains("UpdateCustomConnectionType")) apiFound = true;

        sb.AppendLine(apiFound ? "FOUND" : "NOT FOUND");
        sb.AppendLine("Customize equivalent:");
        sb.AppendLine(equiv);
        sb.AppendLine("Note: The UI 'Customize' button conceptually creates a new Custom Connection Type from a Standard Seed. " + 
                      "The API provides `StructuralConnectionHandler.Create(doc, inputIds, \"Name\")` to create a new Custom Connection Handler (empty), " + 
                      "and `StructuralConnectionHandlerType.UpdateCustomConnectionType()` to modify it. " + 
                      "However, there is no direct public API method like `StructuralConnectionHandlerType.Customize(seedHandlerId)` that perfectly replicates the UI button's behavior of automatically inheriting the seed's subelements.");

        return sb.ToString();
    }
}

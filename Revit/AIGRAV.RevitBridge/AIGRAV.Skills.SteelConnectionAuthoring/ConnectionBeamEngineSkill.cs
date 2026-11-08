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

public class CustomConnectionBeamJob
{
    public ElementId HandlerId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ConnectionBeamEngineSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Custom Connection Authoring V1.7.2",
        Version = "1.7.2",
        Category = "Modeling",
        Description = "Create Custom Beam Connection using Event Engine",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName) => toolName == "aigrav_execute_v17_beam";

    private static CustomConnectionBeamJob currentJob;
    private static bool isSubscribed = false;

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (isSubscribed) return "Already running.";
        
        Document doc = app.ActiveUIDocument.Document;
        
        var types = new FilteredElementCollector(doc).OfClass(typeof(StructuralConnectionHandlerType)).Cast<StructuralConnectionHandlerType>().ToList();
        var shearType = types.FirstOrDefault(t => (t.Name.Contains("Shear plate") || t.Name.Contains("Clip angle") || t.Name.Contains("Seated")) && !t.Name.Contains("Generic"));
        if (shearType == null) return "FAIL: No suitable detailed beam connection loaded.";

        var col = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().Cast<FamilyInstance>().FirstOrDefault();
        var beam = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().Cast<FamilyInstance>().FirstOrDefault();
        
        if (col == null || beam == null) return "FAIL: Missing column or beam.";

        StructuralConnectionHandler seedHandler = null;
        try
        {
            using (Transaction tx = new Transaction(doc, "Create Beam Connection Seed"))
            {
                tx.Start();
                List<ElementId> inputIds = new List<ElementId> { col.Id, beam.Id };
                seedHandler = StructuralConnectionHandler.Create(doc, inputIds, shearType.Id);
                tx.Commit();
            }
        }
        catch (Exception ex)
        {
            return $"FAIL: Could not create standard connection. {ex.Message}";
        }

        currentJob = new CustomConnectionBeamJob { HandlerId = seedHandler.Id, CreatedAt = DateTime.Now };
        app.Idling += OnIdling;
        isSubscribed = true;
        
        return $"Beam Event Engine Started. Seed ID: {seedHandler.Id}. Type: {shearType.Name}";
    }

    private void OnIdling(object sender, IdlingEventArgs e)
    {
        try {
            if (currentJob == null) return;
            UIApplication app = sender as UIApplication;
            Document doc = app.ActiveUIDocument.Document;
            
            StructuralConnectionHandler handler = doc.GetElement(currentJob.HandlerId) as StructuralConnectionHandler;
            bool isReady = false;
            if (handler != null) {
                try {
                    if (handler.GetSubelements().Count > 0) isReady = true;
                } catch {}
            }
            bool timeout = (DateTime.Now - currentJob.CreatedAt).TotalSeconds > 15;

            if (isReady || timeout)
            {
                app.Idling -= OnIdling;
                isSubscribed = false;
                
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== CUSTOM BEAM CONNECTION PIPELINE ===");
                
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
                            // ONLY PASS THE STANDARD HANDLER!
                            customHandler = StructuralConnectionHandler.Create(doc, new List<ElementId> { handler.Id }, "AIGRAV_CUSTOM_SHEAR_01");
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
                            }
                            tx.Commit();
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
                
                try { File.WriteAllText(@"C:\AIGRAV\KnowledgeBase\CustomBeamConnection_Report.md", sb.ToString()); } catch { }
                currentJob = null;
            }
        } catch (Exception ex) {
            try { File.WriteAllText(@"C:\AIGRAV\KnowledgeBase\CustomBeamConnection_Error.txt", ex.ToString()); } catch {}
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.SteelConnectionAuthoring;

public static class DeepSteelMacroExplorer
{
    public static string ExploreDeepAPIs(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== DEEP STEEL MACRO & JOURNALING API EXPLORATION ===");

        // 1. Inspect Autodesk.SteelConnectionsUI.Journaling
        var journalType = Type.GetType("Autodesk.SteelConnectionsUI.Journaling, Autodesk.SteelConnectionsUI");
        if (journalType != null)
        {
            sb.AppendLine($"\n[Type: {journalType.FullName}]");
            foreach (var m in journalType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
            }
            foreach (var f in journalType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                sb.AppendLine($"  field {f.FieldType.Name} {f.Name}");
            }
        }
        else
        {
            sb.AppendLine("Journaling type not found directly, searching loaded assemblies...");
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "Autodesk.SteelConnectionsUI"))
            {
                var t = a.GetType("Autodesk.SteelConnectionsUI.Journaling");
                if (t != null)
                {
                    sb.AppendLine($"  Found in {a.FullName}: {t.FullName}");
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    {
                        sb.AppendLine($"    {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    }
                }
            }
        }

        // 2. Search for any class named SteelConnectionParameters or containing Parameter in SteelConnections assemblies
        sb.AppendLine("\n[Searching for Parameter Data Classes in SteelConnections assemblies]");
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("Autodesk.SteelConnections") || a.GetName().Name.StartsWith("AsConnections")))
        {
            try
            {
                var types = a.GetTypes().Where(t => t.Name.Contains("Param") || t.Name.Contains("Prop") || t.Name.Contains("ChangeInfo") || t.Name.Contains("Macro") || t.Name.Contains("Data"));
                foreach (var t in types)
                {
                    if (t.Name.Contains("SteelConnectionParam") || t.Name.Contains("ChangeInfo_BasicChange") || t.Name.Contains("Joint") || t.Name.Contains("BasePlate"))
                    {
                        sb.AppendLine($"  [{a.GetName().Name}] {t.FullName}");
                        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Take(15))
                        {
                            sb.AppendLine($"    prop {p.PropertyType.Name} {p.Name}");
                        }
                        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Take(10))
                        {
                            sb.AppendLine($"    meth {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(param => $"{param.ParameterType.Name} {param.Name}"))})");
                        }
                    }
                }
            }
            catch { }
        }

        // 3. Inspect Autodesk.SteelConnectionsDB.SteelConnectionHandler methods & fields
        var dbHandlerType = Type.GetType("Autodesk.SteelConnectionsDB.SteelConnectionHandler, Autodesk.SteelConnectionsDB");
        if (dbHandlerType != null)
        {
            sb.AppendLine($"\n[Type: {dbHandlerType.FullName} (All Members)]");
            foreach (var m in dbHandlerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
            }
            foreach (var f in dbHandlerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                sb.AppendLine($"  field {f.FieldType.Name} {f.Name}");
            }
        }

        // 4. Test instantiation of Autodesk.SteelConnectionsDB.SteelConnectionHandler on our Handler 1668214
        try
        {
            var h = doc.GetElement(new ElementId(1668214)) as StructuralConnectionHandler;
            if (h != null && dbHandlerType != null)
            {
                sb.AppendLine($"\n[Testing SteelConnectionHandler instance on Handler 1668214]");
                // Guid is UniqueId or from connection
                Guid guid = Guid.NewGuid();
                // Check if constructor ctor(Document, Guid, StructuralConnectionHandler) works
                var ctor = dbHandlerType.GetConstructor(new[] { typeof(Document), typeof(Guid), typeof(StructuralConnectionHandler) });
                if (ctor != null)
                {
                    object dbHandlerInst = ctor.Invoke(new object[] { doc, guid, h });
                    sb.AppendLine($"  Successfully constructed dbHandler: {dbHandlerInst}");
                    var connGuidProp = dbHandlerType.GetProperty("ConnectionGuid");
                    var connGuidVal = connGuidProp?.GetValue(dbHandlerInst);
                    sb.AppendLine($"  ConnectionGuid: {connGuidVal}");
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error instantiating dbHandler: {ex.Message}");
        }

        return sb.ToString();
    }
}

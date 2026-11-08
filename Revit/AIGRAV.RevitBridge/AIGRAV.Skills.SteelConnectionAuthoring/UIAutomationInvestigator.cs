using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.ExtensibleStorage;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.SteelConnectionAuthoring;

public static class UIAutomationInvestigator
{
    public static string InvestigateUIAutomation(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== PHASE 1 & 3: STEEL CONNECTIONS UI AUTOMATION INVESTIGATION ===");

        // Locate Handler 1668214
        var handler = doc.GetElement(new ElementId(1668214)) as StructuralConnectionHandler;
        if (handler == null) return "FAIL: Handler 1668214 not found.";

        var connType = doc.GetElement(handler.GetTypeId()) as StructuralConnectionHandlerType;
        sb.AppendLine($"Target Handler: {handler.Id}");
        sb.AppendLine($"Target Type: {connType?.Name} (Id: {connType?.Id})");

        // 1. Inspect Autodesk.SteelConnectionsUI.SteelConnectionPagesHandlerModal
        var uiAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Autodesk.SteelConnectionsUI");
        var dbAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Autodesk.SteelConnectionsDB");

        if (uiAss != null)
        {
            var modalType = uiAss.GetType("Autodesk.SteelConnectionsUI.SteelConnectionPagesHandlerModal");
            if (modalType != null)
            {
                sb.AppendLine("\n[SteelConnectionPagesHandlerModal Signature & Overloads]");
                foreach (var c in modalType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    sb.AppendLine($"  ctor({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"))})");
                }
                foreach (var m in modalType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    sb.AppendLine($"  {m.ReturnType.FullName} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"))})");
                }
            }

            var modelessType = uiAss.GetType("Autodesk.SteelConnectionsUI.SteelConnectionPagesHandlerModeless");
            if (modelessType != null)
            {
                sb.AppendLine("\n[SteelConnectionPagesHandlerModeless Signature & Overloads]");
                foreach (var c in modelessType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    sb.AppendLine($"  ctor({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"))})");
                }
                foreach (var m in modelessType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    sb.AppendLine($"  {m.ReturnType.FullName} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"))})");
                }
            }
        }

        // 2. Inspect Autodesk.SteelConnectionsDB.SteelConnectionHandler
        if (dbAss != null)
        {
            var dbHandlerType = dbAss.GetType("Autodesk.SteelConnectionsDB.SteelConnectionHandler");
            if (dbHandlerType != null)
            {
                sb.AppendLine("\n[Autodesk.SteelConnectionsDB.SteelConnectionHandler Signature & Overloads]");
                foreach (var c in dbHandlerType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    sb.AppendLine($"  ctor({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"))})");
                }
                foreach (var m in dbHandlerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    sb.AppendLine($"  {m.ReturnType.FullName} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"))})");
                }
            }
        }

        // 3. Inspect parameters on the Type itself vs Instance
        sb.AppendLine("\n[Connection Type vs Instance Parameter Mechanism]");
        sb.AppendLine($"Handler IsCustom: {handler.IsCustom()}");
        sb.AppendLine($"Type IsCustom: {connType?.IsCustom()}");

        // Check if there is an extensible storage schema on handler or type
        sb.AppendLine("\n[Extensible Storage Schemas on Handler & Type]");
        var schemas = Schema.ListSchemas();
        sb.AppendLine($"Total Registered Schemas: {schemas.Count}");
        foreach (var s in schemas.Where(s => s.SchemaName.Contains("Steel") || s.SchemaName.Contains("Connection") || s.SchemaName.Contains("Advance")))
        {
            sb.AppendLine($"  Schema: \"{s.SchemaName}\" (GUID: {s.GUID})");
            try
            {
                var entityOnHandler = handler.GetEntity(s);
                if (entityOnHandler.IsValid()) sb.AppendLine($"    -> Attached to Handler {handler.Id}");
            }
            catch { }
            try
            {
                if (connType != null)
                {
                    var entityOnType = connType.GetEntity(s);
                    if (entityOnType.IsValid()) sb.AppendLine($"    -> Attached to Type {connType.Id}");
                }
            }
            catch { }
        }

        return sb.ToString();
    }
}

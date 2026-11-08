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

public static class SteelReflectionInspector
{
    public static string InspectSteelAPIs()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== STEEL APIS REFLECTION INSPECTION ===");

        // 1. Autodesk.Revit.DB.Steel.SteelConnectionUtil
        var steelUtilType = Type.GetType("Autodesk.Revit.DB.Steel.SteelConnectionUtil, RevitAPISteel");
        if (steelUtilType != null)
        {
            sb.AppendLine($"\n[Methods of {steelUtilType.FullName}]");
            foreach (var m in steelUtilType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
            }
        }
        else
        {
            sb.AppendLine("\nAutodesk.Revit.DB.Steel.SteelConnectionUtil not found by direct type lookup, scanning assemblies...");
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.Contains("RevitAPISteel")))
            {
                foreach (var t in a.GetExportedTypes())
                {
                    sb.AppendLine($"  RevitAPISteel Type: {t.FullName}");
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        sb.AppendLine($"    {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    }
                }
            }
        }

        // 2. Autodesk.SteelConnectionsDB
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "Autodesk.SteelConnectionsDB"))
        {
            sb.AppendLine($"\n[Autodesk.SteelConnectionsDB Types & Methods]");
            foreach (var t in a.GetExportedTypes().Where(t => t.Name.Contains("Connection") || t.Name.Contains("Param") || t.Name.Contains("Rule")))
            {
                sb.AppendLine($"  Type: {t.FullName}");
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Take(10))
                {
                    sb.AppendLine($"    {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                }
            }
        }

        return sb.ToString();
    }
}

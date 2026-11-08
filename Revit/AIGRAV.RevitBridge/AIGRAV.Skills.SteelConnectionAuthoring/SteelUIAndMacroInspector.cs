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

public static class SteelUIAndMacroInspector
{
    public static string InspectUIAndMacro(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== PHASE 1: STEEL CONNECTIONS UI & MACRO API INSPECTION ===");

        // 1. Inspect assemblies in AddIns\SteelConnections
        string steelAddInsDir = @"C:\Program Files\Autodesk\Revit 2026\AddIns\SteelConnections";
        sb.AppendLine($"Steel AddIns Directory: {steelAddInsDir}");

        // Load / inspect Autodesk.SteelConnectionsUI
        Assembly uiAss = null;
        try
        {
            uiAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Autodesk.SteelConnectionsUI");
            if (uiAss == null && File.Exists(Path.Combine(steelAddInsDir, "Autodesk.SteelConnectionsUI.dll")))
            {
                uiAss = Assembly.LoadFrom(Path.Combine(steelAddInsDir, "Autodesk.SteelConnectionsUI.dll"));
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Error loading Autodesk.SteelConnectionsUI: {ex.Message}");
        }

        if (uiAss != null)
        {
            sb.AppendLine($"\n[Assembly: {uiAss.FullName}]");
            var uiTypes = uiAss.GetExportedTypes();
            sb.AppendLine($"Total Exported Types: {uiTypes.Length}");
            foreach (var t in uiTypes)
            {
                sb.AppendLine($"\n  Class/Interface: {t.FullName}");
                // Constructors
                foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    sb.AppendLine($"    ctor({string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                }
                // Methods
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    sb.AppendLine($"    {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                }
                // Properties
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    sb.AppendLine($"    prop {p.PropertyType.Name} {p.Name} {{ get: {p.CanRead}, set: {p.CanWrite} }}");
                }
            }
        }
        else
        {
            sb.AppendLine("Autodesk.SteelConnectionsUI assembly NOT loaded.");
        }

        // 2. Load / inspect Autodesk.SteelConnectionsDB
        Assembly dbAss = null;
        try
        {
            dbAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Autodesk.SteelConnectionsDB");
            if (dbAss == null && File.Exists(Path.Combine(steelAddInsDir, "Autodesk.SteelConnectionsDB.dll")))
            {
                dbAss = Assembly.LoadFrom(Path.Combine(steelAddInsDir, "Autodesk.SteelConnectionsDB.dll"));
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Error loading Autodesk.SteelConnectionsDB: {ex.Message}");
        }

        if (dbAss != null)
        {
            sb.AppendLine($"\n[Assembly: {dbAss.FullName}]");
            var dbTypes = dbAss.GetExportedTypes();
            sb.AppendLine($"Total Exported Types: {dbTypes.Length}");
            foreach (var t in dbTypes)
            {
                sb.AppendLine($"\n  Class/Interface: {t.FullName}");
                foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    sb.AppendLine($"    ctor({string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                }
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    sb.AppendLine($"    {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                }
            }
        }

        // 3. Inspect AsConnectionsFramework / ASAutoConnectionMgd
        foreach (var dllName in new[] { "AsConnectionsFramework.dll", "ASAutoConnectionMgd.dll" })
        {
            try
            {
                string p = Path.Combine(steelAddInsDir, dllName);
                if (File.Exists(p))
                {
                    var a = Assembly.LoadFrom(p);
                    sb.AppendLine($"\n[Assembly: {a.FullName}]");
                    foreach (var t in a.GetExportedTypes().Where(t => t.Name.Contains("Connection") || t.Name.Contains("Macro") || t.Name.Contains("BasePlate") || t.Name.Contains("Page") || t.Name.Contains("Param")))
                    {
                        sb.AppendLine($"  Class: {t.FullName}");
                        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Take(10))
                        {
                            sb.AppendLine($"    {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(param => $"{param.ParameterType.Name} {param.Name}"))})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error loading {dllName}: {ex.Message}");
            }
        }

        // 4. Inspect C:\ProgramData\Autodesk\Revit Steel Connections 2026\
        string progDataDir = @"C:\ProgramData\Autodesk\Revit Steel Connections 2026";
        sb.AppendLine($"\n=== PHASE 2: INSTALLED MACRO DATA DIRECTORY INSPECTION ===");
        sb.AppendLine($"Directory: {progDataDir}");
        if (Directory.Exists(progDataDir))
        {
            var files = Directory.GetFiles(progDataDir, "*.*", SearchOption.AllDirectories);
            sb.AppendLine($"Total files found in ProgramData: {files.Length}");
            foreach (var f in files)
            {
                var fi = new FileInfo(f);
                sb.AppendLine($"  - {f.Substring(progDataDir.Length)} ({fi.Length} bytes, modified: {fi.LastWriteTime:yyyy-MM-dd})");
            }
        }
        else
        {
            sb.AppendLine("ProgramData Revit Steel Connections 2026 directory not found.");
        }

        return sb.ToString();
    }
}

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

public sealed class ConnectionLibraryScannerSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new AIGRAVSkillInfo
    {
        Name = "Connection Library Scanner",
        Version = "1.0",
        Category = "Discovery",
        Description = "Scans all RVT libraries for connection types",
        Source = "External",
        Priority = 500
    };

    public bool CanHandle(string toolName) => toolName == "aigrav_scan_all_libraries";

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        StringBuilder sb = new StringBuilder();
        string dllPath = @"C:\Program Files\Autodesk\Revit 2026\AddIns\DynamoForRevit\Revit\nodes\steel-pkg\bin\AdvanceSteelConnAutoNodes.dll";
        string folderPath = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English-Imperial\US\Structural Connections\Steel";

        Assembly assem = Assembly.LoadFrom(dllPath);
        Type typeNode = assem.GetType("AdvanceSteel.ConnectionAutomation.Nodes.ConnectionType");
        MethodInfo listMethod = typeNode.GetMethod("ListFromFile", BindingFlags.Public | BindingFlags.Static);

        var rvtFiles = Directory.GetFiles(folderPath, "*.rvt", SearchOption.AllDirectories);
        foreach (var rvt in rvtFiles)
        {
            try
            {
                var result = listMethod.Invoke(null, new object[] { rvt });
                if (result is IEnumerable en)
                {
                    List<string> cnames = new List<string>();
                    foreach (var item in en)
                    {
                        string s = item.ToString();
                        if (s.StartsWith("[")) s = s.Substring(1, s.IndexOf(',') - 1);
                        cnames.Add(s);
                    }
                    if (cnames.Any(n => n.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("plate", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        sb.AppendLine($"\nFOUND IN: {Path.GetFileName(rvt)}");
                        foreach(var n in cnames) sb.AppendLine("  - " + n);
                    }
                }
            }
            catch { }
        }
        
        if (sb.Length == 0) sb.AppendLine("No base plate found in any RVT library.");
        return sb.ToString();
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.SteelConnectionAuthoring;

public static class SteelFilerReader
{
    public static string ReadConnectionFiler(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== READING DETAILED PARAMETERS BUFFER ===");

        var connTypes = new FilteredElementCollector(doc)
            .OfClass(typeof(StructuralConnectionHandlerType))
            .Cast<StructuralConnectionHandlerType>()
            .ToList();

        var typeType = typeof(StructuralConnectionHandlerType);
        var getBufSizeMethod = typeType.GetMethod("GetDetailedParametersBufferSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var getParamsMethod = typeType.GetMethod("GetDetailedParameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var setParamsMethod = typeType.GetMethod("SetDetailedParameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        sb.AppendLine("Scanning all StructuralConnectionHandlerTypes:");
        foreach (var t in connTypes)
        {
            int size = -1;
            try { size = (int)(getBufSizeMethod?.Invoke(t, null) ?? -1); } catch { }
            sb.AppendLine($"  Type: '{t.Name}' (Id: {t.Id}), IsDetailed: {t.IsDetailed()}, BufferSize: {size}");
        }

        // Check StructuralConnectionHandler instance
        var handler = doc.GetElement(new ElementId(1668214)) as StructuralConnectionHandler;
        if (handler != null)
        {
            sb.AppendLine("\nScanning StructuralConnectionHandler (1668214) methods:");
            foreach (var m in typeof(StructuralConnectionHandler).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.Name.Contains("Buffer") || m.Name.Contains("Param") || m.Name.Contains("Detail") || m.Name.Contains("Filer") || m.Name.Contains("Data"))
                {
                    sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    if (m.GetParameters().Length == 0 && m.ReturnType == typeof(int))
                    {
                        try { sb.AppendLine($"    -> Result: {m.Invoke(handler, null)}"); } catch { }
                    }
                }
            }
        }

        return sb.ToString();
    }
}

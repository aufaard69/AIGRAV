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

public static class MethodBodyInspector
{
    public static string InspectMethodBodies()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== MEMORY FILER DEEP INSPECTION ===");

        var dbAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Autodesk.SteelConnectionsDB");
        if (dbAss == null) return "FAIL: Autodesk.SteelConnectionsDB not found.";

        var mfType = dbAss.GetType("Autodesk.SteelConnectionsDB.MemoryFiler");
        if (mfType == null) return "FAIL: MemoryFiler not found.";

        Action<MethodInfo?, string> dumpMethodCallsAndStrings = (m, name) =>
        {
            if (m == null) return;
            sb.AppendLine($"\nCalls and Strings inside {name}():");
            var body = m.GetMethodBody();
            if (body == null) return;
            var il = body.GetILAsByteArray();
            if (il == null) return;
            var module = m.Module;
            for (int i = 0; i < il.Length - 4; i++)
            {
                byte op = il[i];
                if (op == 0x28 || op == 0x6f || op == 0x73) // call, callvirt, newobj
                {
                    int token = BitConverter.ToInt32(il, i + 1);
                    try
                    {
                        var member = module.ResolveMember(token);
                        if (member != null) sb.AppendLine($"  call -> {member.DeclaringType?.Name}.{member.Name}");
                    } catch { }
                }
                else if (op == 0x72) // ldstr
                {
                    int token = BitConverter.ToInt32(il, i + 1);
                    try
                    {
                        var str = module.ResolveString(token);
                        if (str != null) sb.AppendLine($"  ldstr -> \"{str}\"");
                    } catch { }
                }
            }
        };

        dumpMethodCallsAndStrings(mfType.GetMethod("SetChanges", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), "MemoryFiler.SetChanges");
        dumpMethodCallsAndStrings(mfType.GetMethod("GetChanges", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), "MemoryFiler.GetChanges");
        dumpMethodCallsAndStrings(mfType.GetMethod("WriteItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), "MemoryFiler.WriteItem");

        return sb.ToString();
    }
}

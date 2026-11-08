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

public static class SteelReflectionTester
{
    public static string TestJournalingAndFiler(Document doc)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== INSPECTING ADVANCE STEEL JOINT OBJECT & FILER ===");

        var handler = doc.GetElement(new ElementId(1668214)) as StructuralConnectionHandler;
        if (handler == null) return "FAIL: Handler 1668214 not found.";

        var dbAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Autodesk.SteelConnectionsDB");
        if (dbAss == null) return "FAIL: Autodesk.SteelConnectionsDB not loaded.";

        var smmType = dbAss.GetType("Autodesk.SteelConnectionsDB.SteelModelManager");
        var instProp = smmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        object? smmInst = instProp?.GetValue(null);

        var getAttMethod = smmType?.GetMethod("GetAttachments", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Document) }, null);
        object? attDoc = getAttMethod?.Invoke(smmInst, new object[] { doc });

        var revitLinkProp = attDoc?.GetType().GetProperty("RevitLink", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        object? revitLink = revitLinkProp?.GetValue(attDoc);

        var getExtUid = revitLink?.GetType().GetMethod("GetExtUniqueId", new[] { typeof(ElementId) });
        var getExtHandle = revitLink?.GetType().GetMethod("GetExtHandle", new[] { typeof(ElementId) });

        Guid extGuid = (Guid)(getExtUid?.Invoke(revitLink, new object[] { handler.Id }) ?? Guid.Empty);
        string? extHandle = getExtHandle?.Invoke(revitLink, new object[] { handler.Id }) as string;

        sb.AppendLine($"Connection ExtUniqueId: {extGuid}");
        sb.AppendLine($"Connection ExtHandle: {extHandle}");

        var getASOverloads = smmType?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name == "GetASObject").ToList() ?? new List<MethodInfo>();

        var fabTxType = dbAss.GetType("Autodesk.SteelConnectionsDB.FabricationTransaction");
        IDisposable? fabTx = null;
        if (fabTxType != null)
        {
            var ctor = fabTxType.GetConstructor(new[] { typeof(Document), typeof(bool), typeof(string) });
            if (ctor != null)
            {
                try
                {
                    fabTx = ctor.Invoke(new object[] { doc, true, "Read AS Object" }) as IDisposable;
                    sb.AppendLine($"Started FabricationTransaction: {fabTx != null}");
                }
                catch (Exception fex)
                {
                    sb.AppendLine($"FabricationTransaction failed: {fex.Message}");
                }
            }
        }

        object? targetObj = null;
        try
        {
            var mDocStr = getASOverloads.FirstOrDefault(m => {
                var p = m.GetParameters();
                return p.Length == 2 && p[0].ParameterType == typeof(Document) && p[1].ParameterType == typeof(string);
            });
            if (mDocStr != null && extHandle != null)
            {
                targetObj = mDocStr.Invoke(mDocStr.IsStatic ? null : smmInst, new object[] { doc, extHandle });
            }

            if (targetObj != null)
            {
                sb.AppendLine($"Target AS Object: {targetObj.GetType().FullName}");

                // 1. Invoke Save() (0 params)
                var save0 = targetObj.GetType().GetMethod("Save", Type.EmptyTypes);
                if (save0 != null)
                {
                    sb.AppendLine("Calling targetObj.Save()...");
                    object? savedFiler = save0.Invoke(targetObj, null);
                    sb.AppendLine($"Saved Filer: {savedFiler?.GetType().FullName ?? "null"}");
                    if (savedFiler != null)
                    {
                        // Check if we can copy into MemoryFiler using UpdateFrom
                        var mfType = dbAss.GetType("Autodesk.SteelConnectionsDB.MemoryFiler");
                        if (mfType != null)
                        {
                            var mf = Activator.CreateInstance(mfType);
                            var updateFrom = mfType.GetMethod("UpdateFrom");
                            if (updateFrom != null)
                            {
                                try
                                {
                                    updateFrom.Invoke(mf, new object[] { savedFiler });
                                    var getItems = mfType.GetMethod("GetItems");
                                    var items = getItems?.Invoke(mf, null) as System.Collections.IDictionary;
                                    sb.AppendLine($"\n=== SUCCESS: MemoryFiler updated from Joint Filer! ===");
                                    sb.AppendLine($"Items count: {items?.Count ?? 0}");
                                    if (items != null)
                                    {
                                        int count = 0;
                                        foreach (System.Collections.DictionaryEntry de in items)
                                        {
                                            sb.AppendLine($"  [{de.Key}] ({de.Value?.GetType().Name}) = {de.Value}");
                                            count++;
                                            if (count > 100) { sb.AppendLine($"  ... and {items.Count - count} more items."); break; }
                                        }
                                    }

                                    var getChanges = mfType.GetMethod("GetChanges");
                                    string? changes = getChanges?.Invoke(mf, null) as string;
                                    sb.AppendLine($"\nGetChanges() length: {changes?.Length ?? 0}");
                                    if (!string.IsNullOrEmpty(changes))
                                    {
                                        sb.AppendLine($"Sample changes:\n{changes.Substring(0, Math.Min(500, changes.Length))}");
                                    }
                                }
                                catch (Exception uex)
                                {
                                    sb.AppendLine($"UpdateFrom error: {uex.Message}");
                                    if (uex.InnerException != null) sb.AppendLine($"Inner: {uex.InnerException.Message}");
                                }
                            }
                        }
                    }
                }

                // 2. Invoke get_ParameterSet()
                var getParamSet = targetObj.GetType().GetMethod("get_ParameterSet");
                if (getParamSet != null)
                {
                    try
                    {
                        object? paramSet = getParamSet.Invoke(targetObj, null);
                        sb.AppendLine($"\nRuleParameterSet: {paramSet?.GetType().FullName ?? "null"}");
                        if (paramSet != null)
                        {
                            foreach (var p in paramSet.GetType().GetProperties())
                            {
                                sb.AppendLine($"  prop {p.PropertyType.Name} {p.Name} = {p.GetValue(paramSet)}");
                            }
                        }
                    }
                    catch (Exception pex)
                    {
                        sb.AppendLine($"ParameterSet error: {pex.Message}");
                    }
                }
            }
        }
        finally
        {
            fabTx?.Dispose();
        }

        return sb.ToString();
    }
}

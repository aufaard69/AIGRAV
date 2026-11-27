using System.IO;
using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.QTO;

/// <summary>
/// Audits, auto-binds, and injects 'Type of Connection' + 'Lokasi' across ALL 10 Advance Steel categories
/// (including OST_StructConnectionProfiles / Voute) and unifies Manual/Exploded Steel Connections.
/// </summary>
public sealed class ConnectionParamInjectorSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "Steel Connection Parameter Injector",
        Version = "4.0.0",
        Category = "Quantity Takeoff",
        Description = "Auto-binds Type of Connection & Lokasi across all 10 Advance Steel categories and injects them from Parent Connections to 100% of subelements including Voutes.",
        Source = "External",
        Priority = 1950,
        IsEnabled = true
    };

    private static readonly BuiltInCategory[] AdvanceSteel10Categories =
    {
        BuiltInCategory.OST_StructConnections,
        BuiltInCategory.OST_StructConnectionPlates,
        BuiltInCategory.OST_StructConnectionBolts,
        BuiltInCategory.OST_StructConnectionAnchors,
        BuiltInCategory.OST_StructConnectionProfiles,
        BuiltInCategory.OST_StructConnectionWelds,
        BuiltInCategory.OST_StructConnectionShearStuds,
        BuiltInCategory.OST_StructConnectionModifiers,
        BuiltInCategory.OST_StructConnectionHoles,
        BuiltInCategory.OST_StructConnectionOthers
    };

    private static readonly BuiltInCategory[] LokasiAllCategories =
    {
        BuiltInCategory.OST_StructConnections,
        BuiltInCategory.OST_StructConnectionPlates,
        BuiltInCategory.OST_StructConnectionBolts,
        BuiltInCategory.OST_StructConnectionAnchors,
        BuiltInCategory.OST_StructConnectionProfiles,
        BuiltInCategory.OST_StructConnectionWelds,
        BuiltInCategory.OST_StructConnectionShearStuds,
        BuiltInCategory.OST_StructConnectionModifiers,
        BuiltInCategory.OST_StructConnectionHoles,
        BuiltInCategory.OST_StructConnectionOthers,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_StructuralFraming,
        BuiltInCategory.OST_StructuralFoundation,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_Walls,
        BuiltInCategory.OST_Roofs,
        BuiltInCategory.OST_Ceilings,
        BuiltInCategory.OST_Doors,
        BuiltInCategory.OST_Windows,
        BuiltInCategory.OST_Rebar,
        BuiltInCategory.OST_StairsRailing,
        BuiltInCategory.OST_PipeCurves,
        BuiltInCategory.OST_Fascia,
        BuiltInCategory.OST_Cornices,
        BuiltInCategory.OST_PlumbingFixtures,
        BuiltInCategory.OST_GenericModel
    };

    private static readonly BuiltInCategory[] SteelChildCategories =
    {
        BuiltInCategory.OST_StructConnectionPlates,
        BuiltInCategory.OST_StructConnectionBolts,
        BuiltInCategory.OST_StructConnectionAnchors,
        BuiltInCategory.OST_StructConnectionProfiles,
        BuiltInCategory.OST_StructConnectionWelds,
        BuiltInCategory.OST_StructConnectionShearStuds
    };

    private sealed class RequestArgs
    {
        public string Mode { get; set; } = "plan"; // "plan" (read-only) | "execute"
        public bool AutoBindSharedParams { get; set; } = true;
        public bool IncludeManualExploded { get; set; } = true;
        public bool ForceOverwrite { get; set; } = false;
        public string DefaultLokasi { get; set; } = string.Empty;
    }

    public bool CanHandle(string toolName) =>
        toolName.Equals("aigrav_qto_sync_connection_params", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("qto_sync_connection_params", StringComparison.OrdinalIgnoreCase);

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null)
            return JsonSerializer.Serialize(new { error = "No active Revit document." });

        RequestArgs req = ParseArgs(argument);
        bool isExecute = string.Equals(req.Mode, "execute", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (!isExecute)
                return RunPlanAudit(doc);

            return RunExecuteInjection(app!, doc, req);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                status = "Error",
                mode = req.Mode,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private static string RunPlanAudit(Document doc)
    {
        var bindingAudit = InspectBindings(doc);

        var handlers = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructConnections)
            .WhereElementIsNotElementType()
            .ToElements();

        ElementId? typeConnId = FindParamIdOnSample(handlers, "Type of Connection", "Type of connection");
        ElementId? lokasiId = FindParamIdOnSample(handlers, "Lokasi", "LOKASI");

        int handlersMissingType = 0;
        int handlersMissingLokasi = 0;
        int totalSubelements = 0;
        int subelementsMissingOrMismatchType = 0;
        int subelementsMissingLokasi = 0;
        Dictionary<string, int> subsByCategory = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> subsMissingTypeByCategory = new(StringComparer.OrdinalIgnoreCase);

        foreach (Element el in handlers)
        {
            string curHType = GetStringParam(el, "Type of Connection", "Type of connection");
            string hType = string.IsNullOrWhiteSpace(curHType) ? ResolveHandlerTypeName(doc, el) : curHType;
            string hLok = GetStringParam(el, "Lokasi", "LOKASI");
            if (string.IsNullOrWhiteSpace(curHType)) handlersMissingType++;
            if (string.IsNullOrWhiteSpace(hLok)) handlersMissingLokasi++;

            if (el is not StructuralConnectionHandler handler) continue;
            IList<Subelement>? subs = null;
            try { subs = handler.GetSubelements(); } catch { }
            if (subs == null) continue;

            foreach (Subelement sub in subs)
            {
                totalSubelements++;
                string catName = sub.Category?.Name ?? "Unknown";
                subsByCategory[catName] = subsByCategory.GetValueOrDefault(catName) + 1;

                bool needType = typeConnId == null || !sub.HasParameter(typeConnId);
                if (!needType)
                {
                    string sv = GetSubelementString(sub, typeConnId!);
                    if (string.IsNullOrWhiteSpace(sv) ||
                        (!string.IsNullOrWhiteSpace(hType) && !string.Equals(sv, hType, StringComparison.OrdinalIgnoreCase)))
                        needType = true;
                }
                if (needType)
                {
                    subelementsMissingOrMismatchType++;
                    subsMissingTypeByCategory[catName] = subsMissingTypeByCategory.GetValueOrDefault(catName) + 1;
                }

                if (!string.IsNullOrWhiteSpace(hLok))
                {
                    bool needLok = lokasiId == null || !sub.HasParameter(lokasiId) ||
                                   !string.Equals(GetSubelementString(sub, lokasiId), hLok, StringComparison.OrdinalIgnoreCase);
                    if (needLok) subelementsMissingLokasi++;
                }
            }
        }

        // Manual / Exploded standalone steel connection elements
        Dictionary<string, int> manualTotalByCat = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> manualMissingTypeByCat = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> manualWithCommentsCandidate = new(StringComparer.OrdinalIgnoreCase);
        List<object> sampleManualMissing = new();

        foreach (BuiltInCategory bic in SteelChildCategories)
        {
            var elems = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToElements();
            string label = bic.ToString();
            manualTotalByCat[label] = elems.Count;
            int missing = 0;
            int fromComments = 0;
            foreach (Element e in elems)
            {
                string toc = GetStringParam(e, "Type of Connection", "Type of connection");
                if (string.IsNullOrWhiteSpace(toc))
                {
                    missing++;
                    string comm = GetStringParam(e, "Comments");
                    if (!string.IsNullOrWhiteSpace(comm)) fromComments++;
                    if (sampleManualMissing.Count < 15)
                    {
                        sampleManualMissing.Add(new
                        {
                            id = e.Id.Value,
                            category = label,
                            comments = comm,
                            lokasi = GetStringParam(e, "Lokasi", "LOKASI")
                        });
                    }
                }
            }
            manualMissingTypeByCat[label] = missing;
            manualWithCommentsCandidate[label] = fromComments;
        }

        return JsonSerializer.Serialize(new
        {
            status = "PlanOnly (Read-Only)",
            document = doc.Title,
            parameter_bindings = bindingAudit,
            custom_connections = new
            {
                total_handlers = handlers.Count,
                handlers_missing_type_of_connection = handlersMissingType,
                handlers_missing_lokasi = handlersMissingLokasi,
                total_subelements = totalSubelements,
                subelements_by_category = subsByCategory,
                subelements_missing_or_mismatched_type = subelementsMissingOrMismatchType,
                subelements_missing_type_by_category = subsMissingTypeByCategory,
                subelements_missing_lokasi = subelementsMissingLokasi
            },
            manual_exploded_elements = new
            {
                total_by_category = manualTotalByCat,
                missing_type_of_connection = manualMissingTypeByCat,
                recoverable_from_comments = manualWithCommentsCandidate,
                sample_missing = sampleManualMissing
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string RunExecuteInjection(UIApplication app, Document doc, RequestArgs req)
    {
        List<string> boundActions = new();
        List<string> failedItems = new();

        if (req.AutoBindSharedParams)
        {
            EnsureSharedParamBound(app, doc, "Type of Connection", AdvanceSteel10Categories, boundActions, failedItems);
            EnsureSharedParamBound(app, doc, "Lokasi", LokasiAllCategories, boundActions, failedItems);
        }

        var handlers = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructConnections)
            .WhereElementIsNotElementType()
            .ToElements();

        ElementId? typeConnId = FindParamIdOnSample(handlers, "Type of Connection", "Type of connection")
                               ?? ResolveBindingDefinitionId(doc, "Type of Connection");
        ElementId? lokasiId = FindParamIdOnSample(handlers, "Lokasi", "LOKASI")
                             ?? ResolveBindingDefinitionId(doc, "Lokasi");

        int handlersUpdated = 0;
        int subUpdatedTotal = 0;
        Dictionary<string, int> subUpdatedByCategory = new(StringComparer.OrdinalIgnoreCase);
        int depUpdatedTotal = 0;
        int manualUpdatedTotal = 0;
        int manualStillBlank = 0;

        using var tg = new TransactionGroup(doc, "AIGRAV QTO v4 - Sync Connection Parameters");
        tg.Start();

        using (var tx = new Transaction(doc, "Inject Connection Parameters to Subelements & Exploded Elements"))
        {
            FailureHandlingOptions fho = tx.GetFailureHandlingOptions();
            fho.SetFailuresPreprocessor(new SilentWarningPreprocessor());
            fho.SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(fho);
            tx.Start();

            // 1. Custom Advance Steel Connection Handlers + 100% Subelements (including Voutes!) + DependentElements
            foreach (Element el in handlers)
            {
                string desiredType = ResolveHandlerTypeName(doc, el);
                string curType = GetStringParam(el, "Type of Connection", "Type of connection");
                if (req.ForceOverwrite || string.IsNullOrWhiteSpace(curType))
                {
                    if (!string.IsNullOrWhiteSpace(desiredType) &&
                        SetStringParam(el, desiredType, "Type of Connection", "Type of connection"))
                        handlersUpdated++;
                }
                else
                {
                    desiredType = curType;
                }

                string desiredLok = GetStringParam(el, "Lokasi", "LOKASI");
                if (string.IsNullOrWhiteSpace(desiredLok) && !string.IsNullOrWhiteSpace(req.DefaultLokasi))
                {
                    desiredLok = req.DefaultLokasi;
                    SetStringParam(el, desiredLok, "Lokasi", "LOKASI");
                }

                if (el is StructuralConnectionHandler handler)
                {
                    try
                    {
                        IList<Subelement>? subs = handler.GetSubelements();
                        if (subs != null)
                        {
                            var strType = !string.IsNullOrWhiteSpace(desiredType) ? new StringParameterValue(desiredType) : null;
                            var strLok = !string.IsNullOrWhiteSpace(desiredLok) ? new StringParameterValue(desiredLok) : null;

                            foreach (Subelement sub in subs)
                            {
                                bool touched = false;
                                if (strType != null && typeConnId != null && sub.HasParameter(typeConnId) && sub.IsParameterModifiable(typeConnId))
                                {
                                    string existing = GetSubelementString(sub, typeConnId);
                                    if (req.ForceOverwrite || !string.Equals(existing, desiredType, StringComparison.Ordinal))
                                    {
                                        sub.SetParameterValue(typeConnId, strType);
                                        touched = true;
                                    }
                                }
                                if (strLok != null && lokasiId != null && sub.HasParameter(lokasiId) && sub.IsParameterModifiable(lokasiId))
                                {
                                    string existingL = GetSubelementString(sub, lokasiId);
                                    if (req.ForceOverwrite || !string.Equals(existingL, desiredLok, StringComparison.Ordinal))
                                    {
                                        sub.SetParameterValue(lokasiId, strLok);
                                        touched = true;
                                    }
                                }

                                if (touched)
                                {
                                    subUpdatedTotal++;
                                    string cName = sub.Category?.Name ?? "Unknown";
                                    subUpdatedByCategory[cName] = subUpdatedByCategory.GetValueOrDefault(cName) + 1;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failedItems.Add($"Handler {el.Id.Value} subelements error: {ex.Message}");
                    }

                    try
                    {
                        foreach (ElementId depId in handler.GetDependentElements(null))
                        {
                            if (depId == el.Id) continue;
                            Element? child = doc.GetElement(depId);
                            if (child == null) continue;
                            bool depTouched = false;
                            if (!string.IsNullOrWhiteSpace(desiredType) && SetStringParam(child, desiredType, "Type of Connection", "Type of connection"))
                                depTouched = true;
                            if (!string.IsNullOrWhiteSpace(desiredLok) && SetStringParam(child, desiredLok, "Lokasi", "LOKASI"))
                                depTouched = true;
                            if (depTouched) depUpdatedTotal++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failedItems.Add($"Handler {el.Id.Value} dependent elements error: {ex.Message}");
                    }
                }
            }

            // 2. Manual / Exploded standalone Plates, Bolts, Anchors, Profiles
            if (req.IncludeManualExploded)
            {
                foreach (BuiltInCategory bic in SteelChildCategories)
                {
                    var elems = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();
                    foreach (Element e in elems)
                    {
                        string curToc = GetStringParam(e, "Type of Connection", "Type of connection");
                        if (!string.IsNullOrWhiteSpace(curToc)) continue;

                        string comm = GetStringParam(e, "Comments");
                        if (!string.IsNullOrWhiteSpace(comm))
                        {
                            if (SetStringParam(e, comm.Trim(), "Type of Connection", "Type of connection"))
                                manualUpdatedTotal++;
                            else
                                failedItems.Add($"Manual element {e.Id.Value} ({bic}): cannot set Type of Connection.");
                        }
                        else
                        {
                            manualStillBlank++;
                        }

                        if (!string.IsNullOrWhiteSpace(req.DefaultLokasi) &&
                            string.IsNullOrWhiteSpace(GetStringParam(e, "Lokasi", "LOKASI")))
                        {
                            SetStringParam(e, req.DefaultLokasi, "Lokasi", "LOKASI");
                        }
                    }
                }
            }

            doc.Regenerate();
            tx.Commit();
        }

        tg.Assimilate();

        return JsonSerializer.Serialize(new
        {
            status = failedItems.Count == 0 ? "Success" : "CompletedWithWarnings",
            mode = "execute",
            bindings_applied = boundActions,
            total_handlers = handlers.Count,
            handlers_updated = handlersUpdated,
            subelements_updated_total = subUpdatedTotal,
            subelements_updated_by_category = subUpdatedByCategory,
            dependent_elements_updated = depUpdatedTotal,
            manual_exploded_synced_from_comments = manualUpdatedTotal,
            manual_exploded_still_blank = manualStillBlank,
            total_failed = failedItems.Count,
            failed_items = failedItems.Take(25).ToList()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object InspectBindings(Document doc)
    {
        var result = new Dictionary<string, object>();
        foreach (var (paramName, requiredCats) in new[]
        {
            ("Type of Connection", AdvanceSteel10Categories),
            ("Lokasi", LokasiAllCategories)
        })
        {
            HashSet<long> boundCatIds = new();
            bool exists = false;
            DefinitionBindingMapIterator it = doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                Definition? def = it.Key;
                if (def == null || !def.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase)) continue;
                exists = true;
                if (it.Current is ElementBinding eb)
                {
                    foreach (Category c in eb.Categories)
                        boundCatIds.Add(c.Id.Value);
                }
                break;
            }

            List<string> missing = new();
            foreach (BuiltInCategory bic in requiredCats)
            {
                if (!boundCatIds.Contains((long)bic))
                    missing.Add(bic.ToString());
            }

            result[paramName] = new
            {
                bound = exists,
                bound_category_count = boundCatIds.Count,
                missing_required_categories = missing
            };
        }
        return result;
    }

    private static void EnsureSharedParamBound(
        UIApplication app,
        Document doc,
        string paramName,
        BuiltInCategory[] requiredCats,
        List<string> boundActions,
        List<string> failedItems)
    {
        Definition? existingDef = null;
        CategorySet catSet = app.Application.Create.NewCategorySet();
        bool needUpdate = false;
        HashSet<long> existingIds = new();

        DefinitionBindingMapIterator it = doc.ParameterBindings.ForwardIterator();
        while (it.MoveNext())
        {
            Definition? d = it.Key;
            if (d == null || !d.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase)) continue;
            existingDef = d;
            if (it.Current is ElementBinding eb)
            {
                foreach (Category c in eb.Categories)
                {
                    catSet.Insert(c);
                    existingIds.Add(c.Id.Value);
                }
            }
            break;
        }

        foreach (BuiltInCategory bic in requiredCats)
        {
            Category? cat = doc.Settings.Categories.get_Item(bic);
            if (cat != null && cat.AllowsBoundParameters && !existingIds.Contains(cat.Id.Value))
            {
                catSet.Insert(cat);
                needUpdate = true;
            }
        }

        if (existingDef != null && !needUpdate) return;

        string spDir = @"C:\AIGRAV\Databases\QTO";
        Directory.CreateDirectory(spDir);
        string spFile = Path.Combine(spDir, "AIGRAV_QTO_SharedParameters.txt");
        if (!File.Exists(spFile))
        {
            File.WriteAllText(spFile,
                "# AIGRAV QTO Shared Parameters\r\n*META\tVERSION\tMINVERSION\r\nMETA\t2\t1\r\n*GROUP\tID\tNAME\r\nGROUP\t1\tAIGRAV_QTO\r\n*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNODATA\r\n");
        }

        string originalSpFile = string.Empty;
        try { originalSpFile = app.Application.SharedParametersFilename; } catch { }

        try
        {
            Definition? defToBind = existingDef;
            if (defToBind == null)
            {
                app.Application.SharedParametersFilename = spFile;
                DefinitionFile? defFile = app.Application.OpenSharedParameterFile();
                if (defFile == null)
                {
                    failedItems.Add($"Cannot open shared parameter file {spFile} for '{paramName}'.");
                    return;
                }
                DefinitionGroup group = defFile.Groups.get_Item("AIGRAV_QTO") ?? defFile.Groups.Create("AIGRAV_QTO");
                defToBind = group.Definitions.get_Item(paramName);
                if (defToBind == null)
                {
                    var opt = new ExternalDefinitionCreationOptions(paramName, SpecTypeId.String.Text);
                    defToBind = group.Definitions.Create(opt);
                }
            }

            InstanceBinding binding = app.Application.Create.NewInstanceBinding(catSet);
            using var tx = new Transaction(doc, $"AIGRAV - Bind '{paramName}'");
            tx.Start();
            bool ok = existingDef == null
                ? doc.ParameterBindings.Insert(defToBind, binding, GroupTypeId.IdentityData)
                : doc.ParameterBindings.ReInsert(defToBind, binding, GroupTypeId.IdentityData);
            tx.Commit();

            if (ok) boundActions.Add($"Bound '{paramName}' across {catSet.Size} categories.");
            else failedItems.Add($"Binding Insert/ReInsert returned false for '{paramName}'.");
        }
        catch (Exception ex)
        {
            failedItems.Add($"Shared parameter binding failed for '{paramName}': {ex.Message}");
        }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(originalSpFile))
                    app.Application.SharedParametersFilename = originalSpFile;
            }
            catch { }
        }
    }

    private static ElementId? ResolveBindingDefinitionId(Document doc, string paramName)
    {
        var sample = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructConnections)
            .WhereElementIsNotElementType()
            .FirstElement();
        if (sample != null)
        {
            Parameter? p = sample.LookupParameter(paramName);
            if (p != null) return p.Id;
        }
        return null;
    }

    private static ElementId? FindParamIdOnSample(IList<Element> elems, params string[] names)
    {
        foreach (Element e in elems)
        {
            foreach (string n in names)
            {
                Parameter? p = e.LookupParameter(n);
                if (p != null) return p.Id;
            }
        }
        return null;
    }

    private static string ResolveHandlerTypeName(Document doc, Element conn)
    {
        ElementId tid = conn.GetTypeId();
        if (tid != ElementId.InvalidElementId && doc.GetElement(tid) is ElementType et)
        {
            if (!string.IsNullOrWhiteSpace(et.FamilyName)) return et.FamilyName.Trim();
            if (!string.IsNullOrWhiteSpace(et.Name)) return et.Name.Trim();
        }
        return conn.Name?.Trim() ?? string.Empty;
    }

    private static string GetStringParam(Element el, params string[] names)
    {
        foreach (string n in names)
        {
            Parameter? p = el.LookupParameter(n);
            if (p == null) continue;
            string? val = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
        }
        return string.Empty;
    }

    private static bool SetStringParam(Element el, string value, params string[] names)
    {
        foreach (string n in names)
        {
            Parameter? p = el.LookupParameter(n);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                return p.Set(value);
        }
        return false;
    }

    private static string GetSubelementString(Subelement sub, ElementId pid)
    {
        try
        {
            ParameterValue? pv = sub.GetParameterValue(pid);
            if (pv is StringParameterValue spv) return spv.Value?.Trim() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    private static RequestArgs ParseArgs(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return new RequestArgs();
        try
        {
            return JsonSerializer.Deserialize<RequestArgs>(argument, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new RequestArgs();
        }
        catch
        {
            return new RequestArgs();
        }
    }

    private sealed class SilentWarningPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (FailureMessageAccessor fma in failuresAccessor.GetFailureMessages())
            {
                if (fma.GetSeverity() == FailureSeverity.Warning)
                    failuresAccessor.DeleteWarning(fma);
            }
            return FailureProcessingResult.Continue;
        }
    }
}

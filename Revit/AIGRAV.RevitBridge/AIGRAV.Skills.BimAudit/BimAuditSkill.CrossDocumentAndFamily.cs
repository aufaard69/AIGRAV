using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class BimAuditSkill
{
    // Registry memori untuk dokumen yang dibuka secara background (RAM headless)
    private static readonly Dictionary<string, Document> _backgroundDocs = new(StringComparer.OrdinalIgnoreCase);

    // Custom Handler untuk menangani konflik duplikat nama tipe saat copy antar dokumen
    private class DuplicateTypeHandler : IDuplicateTypeNamesHandler
    {
        public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
        {
            return DuplicateTypeAction.UseDestinationTypes;
        }
    }

    // Helper cepat untuk membuka atau mengambil dokumen tanpa memuat workset/link berat
    private static Document? ResolveOrOpenDoc(UIApplication app, string filePath)
    {
        string title = Path.GetFileNameWithoutExtension(filePath);

        // 1. Cek dari memory cache
        if (_backgroundDocs.TryGetValue(filePath, out var cached) && cached.IsValidObject) return cached;
        if (_backgroundDocs.TryGetValue(title, out var cachedTitle) && cachedTitle.IsValidObject) return cachedTitle;

        // 2. Cek dari dokumen yang sudah ada di Revit session (by path atau by title)
        foreach (Document d in app.Application.Documents)
        {
            if (!d.IsValidObject) continue;
            if (!string.IsNullOrEmpty(d.PathName) && d.PathName.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            {
                _backgroundDocs[filePath] = d;
                _backgroundDocs[d.Title] = d;
                return d;
            }
            if (d.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
            {
                _backgroundDocs[filePath] = d;
                _backgroundDocs[d.Title] = d;
                return d;
            }
        }

        // 3. Jika belum terbuka, buka secara headless dengan OpenOptions cepat (tanpa memuat workset & link berat)
        if (filePath.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
        {
            var d = app.Application.OpenDocumentFile(filePath);
            _backgroundDocs[filePath] = d;
            _backgroundDocs[d.Title] = d;
            return d;
        }

        try
        {
            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
            OpenOptions openOpts = new OpenOptions
            {
                DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets
            };
            try
            {
                WorksetConfiguration wsConfig = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                openOpts.SetOpenWorksetsConfiguration(wsConfig);
            }
            catch {}

            var d = app.Application.OpenDocumentFile(modelPath, openOpts);
            _backgroundDocs[filePath] = d;
            _backgroundDocs[d.Title] = d;
            return d;
        }
        catch
        {
            var d = app.Application.OpenDocumentFile(filePath);
            _backgroundDocs[filePath] = d;
            _backgroundDocs[d.Title] = d;
            return d;
        }
    }

    // =========================================================================
    // SOLUSI 1: MULTI-DOCUMENT CONTEXT MANAGER
    // =========================================================================

    public static string ListOpenDocuments(UIApplication app, Document activeDoc, string? argument)
    {
        var docList = new List<object>();

        foreach (Document d in app.Application.Documents)
        {
            if (!d.IsValidObject) continue;
            bool isActive = activeDoc != null && d.IsValidObject && d.Title.Equals(activeDoc.Title, StringComparison.OrdinalIgnoreCase);
            bool isBgTracked = _backgroundDocs.ContainsKey(d.Title) || (!string.IsNullOrEmpty(d.PathName) && _backgroundDocs.ContainsKey(d.PathName));

            docList.Add(new
            {
                title = d.Title,
                path = string.IsNullOrEmpty(d.PathName) ? "(In-Memory / Unsaved)" : d.PathName,
                is_active = isActive,
                is_family_document = d.IsFamilyDocument,
                is_background = isBgTracked || !isActive,
                is_read_only = d.IsReadOnly
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            total_documents = docList.Count,
            active_document = activeDoc?.Title ?? "None",
            documents = docList
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string OpenBackgroundDocument(UIApplication app, Document activeDoc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "File path harus diisi." });
        }

        string filePath = argument.Trim('\"', '\'', ' ');
        try
        {
            using var jsonDoc = JsonDocument.Parse(argument);
            if (jsonDoc.RootElement.TryGetProperty("path", out var pathProp))
            {
                filePath = pathProp.GetString() ?? filePath;
            }
        }
        catch {}

        if (!File.Exists(filePath))
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"File tidak ditemukan: {filePath}" });
        }

        Document? targetDoc = ResolveOrOpenDoc(app, filePath);
        if (targetDoc == null)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal membuka dokumen: {filePath}" });
        }

        int draftingViewCount = new FilteredElementCollector(targetDoc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Count(v => !v.IsTemplate && v.ViewType == ViewType.DraftingView);

        int familyCount = new FilteredElementCollector(targetDoc)
            .OfClass(typeof(Family))
            .GetElementCount();

        int wallTypeCount = new FilteredElementCollector(targetDoc)
            .OfClass(typeof(WallType))
            .GetElementCount();

        return JsonSerializer.Serialize(new
        {
            status = "success",
            message = "Dokumen siap di RAM background.",
            title = targetDoc.Title,
            path = filePath,
            is_family_document = targetDoc.IsFamilyDocument,
            statistics = new
            {
                drafting_views_count = draftingViewCount,
                families_count = familyCount,
                wall_types_count = wallTypeCount
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string CloseBackgroundDocument(UIApplication app, Document activeDoc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Title atau path dokumen harus diisi." });
        }

        string identifier = argument.Trim('\"', '\'', ' ');
        try
        {
            using var jsonDoc = JsonDocument.Parse(argument);
            if (jsonDoc.RootElement.TryGetProperty("title", out var titleProp))
            {
                identifier = titleProp.GetString() ?? identifier;
            }
        }
        catch {}

        Document? foundDoc = null;
        if (_backgroundDocs.TryGetValue(identifier, out var doc))
        {
            if (doc != null && doc.IsValidObject)
            {
                foundDoc = doc;
            }
            else
            {
                _backgroundDocs.Remove(identifier);
            }
        }

        if (foundDoc == null)
        {
            foreach (Document d in app.Application.Documents)
            {
                if (!d.IsValidObject) continue;
                if (d.Title.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(d.PathName) && d.PathName.Equals(identifier, StringComparison.OrdinalIgnoreCase)))
                {
                    foundDoc = d;
                    break;
                }
            }
        }

        if (foundDoc == null)
        {
            _backgroundDocs.Remove(identifier);
            return JsonSerializer.Serialize(new { status = "success", message = $"Dokumen '{identifier}' sudah tidak aktif atau sudah ditutup sebelumnya." });
        }

        if (activeDoc != null && foundDoc.Title.Equals(activeDoc.Title, StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Dokumen yang sedang aktif di UI tidak boleh ditutup via background close." });
        }

        string closedTitle = foundDoc.Title;
        try
        {
            foundDoc.Close(false);
            _backgroundDocs.Remove(closedTitle);
            _backgroundDocs.Remove(identifier);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                message = $"Dokumen '{closedTitle}' berhasil ditutup dan dibersihkan dari RAM."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal menutup dokumen: {ex.Message}" });
        }
    }

    // =========================================================================
    // SOLUSI 2: CROSS-PROJECT TRANSFER ENGINE (COPY DARI PROYEK EKSTERNAL)
    // =========================================================================

    public static string InspectExternalDocument(UIApplication app, Document activeDoc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Path file external harus diisi." });
        }

        string filePath = argument.Trim('\"', '\'', ' ');
        string filterCat = "all";

        try
        {
            using var jsonDoc = JsonDocument.Parse(argument);
            if (jsonDoc.RootElement.TryGetProperty("path", out var pProp)) filePath = pProp.GetString() ?? filePath;
            if (jsonDoc.RootElement.TryGetProperty("category", out var cProp)) filterCat = cProp.GetString() ?? "all";
        }
        catch {}

        if (!File.Exists(filePath))
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"File external tidak ditemukan: {filePath}" });
        }

        Document? extDoc = ResolveOrOpenDoc(app, filePath);
        if (extDoc == null)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal membuka file external: {filePath}" });
        }

        try
        {
            var draftingViews = new List<object>();
            if (filterCat == "all" || filterCat == "drafting_views" || filterCat == "views")
            {
                var views = new FilteredElementCollector(extDoc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.ViewType == ViewType.DraftingView)
                    .ToList();

                foreach (var v in views)
                {
                    int elemCount = new FilteredElementCollector(extDoc, v.Id).WhereElementIsNotElementType().GetElementCount();
                    draftingViews.Add(new { id = v.Id.Value, name = v.Name, scale = v.Scale, detail_elements_count = elemCount });
                }
            }

            var wallTypes = new List<object>();
            if (filterCat == "all" || filterCat == "wall_types" || filterCat == "types")
            {
                var wts = new FilteredElementCollector(extDoc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .ToList();

                foreach (var wt in wts)
                {
                    wallTypes.Add(new { id = wt.Id.Value, name = wt.Name, kind = wt.Kind.ToString() });
                }
            }

            var families = new List<object>();
            if (filterCat == "all" || filterCat == "families")
            {
                var fams = new FilteredElementCollector(extDoc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .ToList();

                foreach (var f in fams)
                {
                    families.Add(new { id = f.Id.Value, name = f.Name, category = f.FamilyCategory?.Name ?? "General" });
                }
            }

            var sheets = new List<object>();
            if (filterCat == "all" || filterCat == "sheets")
            {
                var sList = new FilteredElementCollector(extDoc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .ToList();

                foreach (var s in sList)
                {
                    int vpCount = 0;
                    try { vpCount = s.GetAllViewports().Count; } catch {}
                    sheets.Add(new { number = s.SheetNumber, name = s.Name, viewports_count = vpCount });
                }
            }

            return JsonSerializer.Serialize(new
            {
                status = "success",
                source_document = extDoc.Title,
                source_path = filePath,
                sheets = sheets,
                drafting_views = draftingViews,
                wall_types = wallTypes,
                families = families
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Error saat inspeksi dokumen external: {ex.Message}" });
        }
    }

    public static string CopyFromExternalProject(UIApplication app, Document targetDoc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Argument JSON transfer harus diisi." });
        }

        string sourcePath = "";
        var draftingViewNames = new List<string>();
        var systemTypeNames = new List<string>();
        var familyNames = new List<string>();

        try
        {
            using var jsonDoc = JsonDocument.Parse(argument);
            var root = jsonDoc.RootElement;
            if (root.TryGetProperty("source_path", out var spProp)) sourcePath = spProp.GetString() ?? "";
            if (root.TryGetProperty("drafting_views", out var dvProp) && dvProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in dvProp.EnumerateArray()) draftingViewNames.Add(el.GetString() ?? "");
            }
            if (root.TryGetProperty("system_types", out var stProp) && stProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in stProp.EnumerateArray()) systemTypeNames.Add(el.GetString() ?? "");
            }
            if (root.TryGetProperty("families", out var famProp) && famProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in famProp.EnumerateArray()) familyNames.Add(el.GetString() ?? "");
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal mem-parsing payload JSON: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Source path file tidak valid atau tidak ditemukan: '{sourcePath}'" });
        }

        // Buka atau ambil source document
        Document? sourceDoc = ResolveOrOpenDoc(app, sourcePath);
        if (sourceDoc == null)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal membuka source document: {sourcePath}" });
        }

        var copiedViews = new List<string>();
        var copiedTypes = new List<string>();
        var copiedFamilies = new List<string>();
        var failedItems = new List<string>();

        CopyPasteOptions copyOpts = new CopyPasteOptions();
        copyOpts.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

        using (Transaction tx = new Transaction(targetDoc, "AIGRAV Cross-Project Transfer"))
        {
            var failOpt = tx.GetFailureHandlingOptions();
            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());
            failOpt.SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(failOpt);

            tx.Start();

            // 1. SALIN DRAFTING VIEWS BESERTA SELURUH DETAIL 2D ELEMENNYA
            if (draftingViewNames.Count > 0)
            {
                ViewFamilyType? draftingVft = new FilteredElementCollector(targetDoc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Drafting);

                if (draftingVft == null)
                {
                    failedItems.Add("Target document tidak memiliki ViewFamilyType DraftingView.");
                }
                else
                {
                    foreach (var vname in draftingViewNames)
                    {
                        try
                        {
                            ViewDrafting? srcView = new FilteredElementCollector(sourceDoc)
                                .OfClass(typeof(ViewDrafting))
                                .Cast<ViewDrafting>()
                                .FirstOrDefault(v => v.Name.Equals(vname, StringComparison.OrdinalIgnoreCase));

                            if (srcView == null)
                            {
                                failedItems.Add($"Drafting View '{vname}' tidak ditemukan di source document.");
                                continue;
                            }

                            // Buat target drafting view
                            string newViewName = srcView.Name;
                            int suffix = 1;
                            while (new FilteredElementCollector(targetDoc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>().Any(v => v.Name.Equals(newViewName, StringComparison.OrdinalIgnoreCase)))
                            {
                                newViewName = $"{srcView.Name} (Imported {suffix++})";
                            }

                            ViewDrafting targetDrafting = ViewDrafting.Create(targetDoc, draftingVft.Id);
                            targetDrafting.Name = newViewName;
                            targetDrafting.Scale = srcView.Scale;

                            var detailElements = new FilteredElementCollector(sourceDoc, srcView.Id)
                                .WhereElementIsNotElementType()
                                .Select(e => e.Id)
                        .ToList();

                            if (detailElements.Count > 0)
                            {
                                ElementTransformUtils.CopyElements(srcView, detailElements, targetDrafting, Transform.Identity, copyOpts);
                            }

                            copiedViews.Add($"{srcView.Name} -> {newViewName} ({detailElements.Count} elemen detail)");
                        }
                        catch (Exception ex)
                        {
                            failedItems.Add($"Gagal salin Drafting View '{vname}': {ex.Message}");
                        }
                    }
                }
            }

            // 2. SALIN SYSTEM TYPES (WallTypes, FloorTypes, dll)
            if (systemTypeNames.Count > 0)
            {
                foreach (var tname in systemTypeNames)
                {
                    try
                    {
                        ElementType? srcType = new FilteredElementCollector(sourceDoc)
                            .WhereElementIsElementType()
                            .Cast<ElementType>()
                            .FirstOrDefault(t => t.Name.Equals(tname, StringComparison.OrdinalIgnoreCase));

                        if (srcType == null)
                        {
                            failedItems.Add($"System Type '{tname}' tidak ditemukan di source document.");
                            continue;
                        }

                        ElementTransformUtils.CopyElements(sourceDoc, new List<ElementId> { srcType.Id }, targetDoc, Transform.Identity, copyOpts);
                        copiedTypes.Add(srcType.Name);
                    }
                    catch (Exception ex)
                    {
                        failedItems.Add($"Gagal salin System Type '{tname}': {ex.Message}");
                    }
                }
            }

            // 3. SALIN LOADABLE FAMILIES (Ekspor in-memory RFA & Load ke Target)
            if (familyNames.Count > 0)
            {
                foreach (var fname in familyNames)
                {
                    try
                    {
                        Family? srcFam = new FilteredElementCollector(sourceDoc)
                            .OfClass(typeof(Family))
                            .Cast<Family>()
                            .FirstOrDefault(f => f.Name.Equals(fname, StringComparison.OrdinalIgnoreCase));

                        if (srcFam == null)
                        {
                            failedItems.Add($"Family '{fname}' tidak ditemukan di source document.");
                            continue;
                        }

                        Document famDoc = sourceDoc.EditFamily(srcFam);
                        string tempRfaPath = Path.Combine(Path.GetTempPath(), $"{srcFam.Name}_{Guid.NewGuid():N}.rfa");
                        SaveAsOptions saveOpts = new SaveAsOptions { OverwriteExistingFile = true };
                        famDoc.SaveAs(tempRfaPath, saveOpts);
                        famDoc.Close(false);

                        bool loaded = targetDoc.LoadFamily(tempRfaPath, new FamilyLoadOpt(), out Family loadedFam);
                        try { File.Delete(tempRfaPath); } catch {}

                        if (loaded && loadedFam != null)
                        {
                            copiedFamilies.Add(loadedFam.Name);
                        }
                        else
                        {
                            copiedFamilies.Add($"{fname} (Re-loaded/Updated)");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedItems.Add($"Gagal salin Family '{fname}': {ex.Message}");
                    }
                }
            }

            tx.Commit();
        }

        return JsonSerializer.Serialize(new
        {
            status = failedItems.Count == 0 ? "success" : (copiedViews.Count + copiedTypes.Count + copiedFamilies.Count > 0 ? "partial_success" : "failed"),
            copied_drafting_views = copiedViews,
            copied_system_types = copiedTypes,
            copied_families = copiedFamilies,
            failed_items = failedItems
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // =========================================================================
    // SOLUSI 3: IN-MEMORY FAMILY AUTHORING & EDITING
    // =========================================================================

    public static string InspectFamilyInternals(UIApplication app, Document activeDoc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Nama family atau path file .rfa harus diisi." });
        }

        string identifier = argument.Trim('\"', '\'', ' ');
        try
        {
            using var jsonDoc = JsonDocument.Parse(argument);
            if (jsonDoc.RootElement.TryGetProperty("family_name", out var fnProp)) identifier = fnProp.GetString() ?? identifier;
            if (jsonDoc.RootElement.TryGetProperty("file_path", out var fpProp)) identifier = fpProp.GetString() ?? identifier;
            if (jsonDoc.RootElement.TryGetProperty("path", out var pProp)) identifier = pProp.GetString() ?? identifier;
            if (jsonDoc.RootElement.TryGetProperty("identifier", out var idProp)) identifier = idProp.GetString() ?? identifier;
            if (jsonDoc.RootElement.TryGetProperty("rfa_path", out var rfProp)) identifier = rfProp.GetString() ?? identifier;
        }
        catch {}

        Document? famDoc = null;
        bool isFromFile = File.Exists(identifier);

        if (isFromFile)
        {
            try
            {
                famDoc = app.Application.OpenDocumentFile(identifier);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { status = "error", message = $"Gagal membuka file family .rfa: {ex.Message}" });
            }
        }
        else
        {
            Family? fam = new FilteredElementCollector(activeDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name.Equals(identifier, StringComparison.OrdinalIgnoreCase));

            if (fam == null)
            {
                return JsonSerializer.Serialize(new { status = "error", message = $"Family '{identifier}' tidak ditemukan di active project." });
            }

            try
            {
                famDoc = activeDoc.EditFamily(fam);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { status = "error", message = $"Gagal membuka family '{identifier}' in-memory: {ex.Message}" });
            }
        }

        try
        {
            var fm = famDoc.FamilyManager;
            var paramList = new List<object>();

            foreach (FamilyParameter fp in fm.Parameters)
            {
                paramList.Add(new
                {
                    name = fp.Definition.Name,
                    is_instance = fp.IsInstance,
                    is_shared = fp.IsShared,
                    is_readonly = fp.IsReadOnly,
                    formula = fp.Formula ?? "",
                    storage_type = fp.StorageType.ToString()
                });
            }

            var typeList = new List<object>();
            foreach (FamilyType ft in fm.Types)
            {
                typeList.Add(new
                {
                    name = ft.Name
                });
            }

            string categoryName = famDoc.OwnerFamily?.FamilyCategory?.Name ?? "General";
            string famTitle = famDoc.Title;

            famDoc.Close(false);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                family_title = famTitle,
                category = categoryName,
                total_types = typeList.Count,
                types = typeList,
                total_parameters = paramList.Count,
                parameters = paramList
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            try { famDoc?.Close(false); } catch {}
            return JsonSerializer.Serialize(new { status = "error", message = $"Error saat membaca internal family: {ex.Message}" });
        }
    }

    public static string ModifyFamilyParameter(UIApplication app, Document activeDoc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Argument JSON modifikasi family harus diisi." });
        }

        string familyName = "";
        string paramName = "";
        string paramValue = "";
        string formula = "";
        bool isInstance = false;

        try
        {
            using var jsonDoc = JsonDocument.Parse(argument);
            var root = jsonDoc.RootElement;
            if (root.TryGetProperty("family_name", out var fn)) familyName = fn.GetString() ?? "";
            if (root.TryGetProperty("parameter_name", out var pn)) paramName = pn.GetString() ?? "";
            if (root.TryGetProperty("parameter_value", out var pv)) paramValue = pv.GetString() ?? "";
            if (root.TryGetProperty("formula", out var fmla)) formula = fmla.GetString() ?? "";
            if (root.TryGetProperty("is_instance", out var inst)) isInstance = inst.GetBoolean();
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal mem-parsing payload JSON: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(familyName) || string.IsNullOrWhiteSpace(paramName))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Field 'family_name' dan 'parameter_name' wajib diisi." });
        }

        Family? fam = new FilteredElementCollector(activeDoc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .FirstOrDefault(f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));

        if (fam == null)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Family '{familyName}' tidak ditemukan di dokumen aktif." });
        }

        Document famDoc;
        try
        {
            famDoc = activeDoc.EditFamily(fam);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal mengedit family '{familyName}': {ex.Message}" });
        }

        try
        {
            using (Transaction famTx = new Transaction(famDoc, "AIGRAV In-Memory Family Authoring"))
            {
                var failOpt = famTx.GetFailureHandlingOptions();
                failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());
                failOpt.SetClearAfterRollback(true);
                famTx.SetFailureHandlingOptions(failOpt);

                famTx.Start();

                var fm = famDoc.FamilyManager;
                FamilyParameter? fp = fm.get_Parameter(paramName);

                if (fp == null)
                {
                    try
                    {
                        fp = fm.AddParameter(paramName, GroupTypeId.General, SpecTypeId.String.Text, isInstance);
                    }
                    catch
                    {
                        fp = fm.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (fp != null)
                {
                    if (!string.IsNullOrEmpty(formula))
                    {
                        fm.SetFormula(fp, formula);
                    }
                    else if (!string.IsNullOrEmpty(paramValue))
                    {
                        if (fp.StorageType == StorageType.String)
                        {
                            fm.Set(fp, paramValue);
                        }
                        else if (fp.StorageType == StorageType.Double && double.TryParse(paramValue, out double dval))
                        {
                            fm.Set(fp, dval);
                        }
                        else if (fp.StorageType == StorageType.Integer && int.TryParse(paramValue, out int ival))
                        {
                            fm.Set(fp, ival);
                        }
                    }
                }

                famTx.Commit();
            }

            // Reload balik ke Active Document dengan override parameter values
            famDoc.LoadFamily(activeDoc, new FamilyLoadOpt());
            famDoc.Close(false);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                message = $"Family '{familyName}' berhasil dimodifikasi di RAM dan di-reload balik ke project aktif.",
                parameter_name = paramName,
                is_instance = isInstance,
                value = paramValue,
                formula = formula
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            try { famDoc.Close(false); } catch {}
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal memodifikasi Family: {ex.Message}" });
        }
    }

    public static string ReloadFamilyWithOverride(UIApplication app, Document activeDoc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Path file .rfa harus diisi." });
        }

        string rfaPath = argument.Trim('\"', '\'', ' ');
        try
        {
            using var jsonDoc = JsonDocument.Parse(argument);
            if (jsonDoc.RootElement.TryGetProperty("rfa_path", out var rpProp)) rfaPath = rpProp.GetString() ?? rfaPath;
        }
        catch {}

        if (!File.Exists(rfaPath))
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"File .rfa tidak ditemukan: {rfaPath}" });
        }

        try
        {
            Family? loadedFam = null;
            using (Transaction tx = new Transaction(activeDoc, "AIGRAV Reload Family"))
            {
                var failOpt = tx.GetFailureHandlingOptions();
                failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());
                failOpt.SetClearAfterRollback(true);
                tx.SetFailureHandlingOptions(failOpt);

                tx.Start();
                activeDoc.LoadFamily(rfaPath, new FamilyLoadOpt(), out loadedFam);
                tx.Commit();
            }

            return JsonSerializer.Serialize(new
            {
                status = "success",
                message = $"Family '{loadedFam?.Name ?? Path.GetFileNameWithoutExtension(rfaPath)}' berhasil di-reload dengan override parameter values.",
                file = rfaPath
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal me-reload family: {ex.Message}" });
        }
    }

    // =========================================================================
    // TRANSFER SHEET, VIEW, ANNOTATION & DETAIL ITEMS ANTAR-PROYEK
    // =========================================================================

    public static string CopySheetsAndViewsFromProject(UIApplication app, Document targetDoc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Argument JSON transfer sheet harus diisi." });
        }

        string sourcePath = "";
        var sheetNumbers = new List<string>();

        try
        {
            using var jsonDoc = JsonDocument.Parse(argument);
            var root = jsonDoc.RootElement;
            if (root.TryGetProperty("source_path", out var spProp)) sourcePath = spProp.GetString() ?? "";
            if (root.TryGetProperty("sheet_numbers", out var snProp) && snProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in snProp.EnumerateArray()) sheetNumbers.Add(el.GetString() ?? "");
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal mem-parsing payload JSON: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Source path file tidak valid atau tidak ditemukan: '{sourcePath}'" });
        }

        Document? sourceDoc = ResolveOrOpenDoc(app, sourcePath);
        if (sourceDoc == null)
        {
            return JsonSerializer.Serialize(new { status = "error", message = $"Gagal membuka source document: {sourcePath}" });
        }

        var candidateSheets = new FilteredElementCollector(sourceDoc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Where(s => !s.IsTemplate)
            .ToList();

        if (sheetNumbers.Count > 0 && !sheetNumbers.Contains("all", StringComparer.OrdinalIgnoreCase))
        {
            candidateSheets = candidateSheets
                .Where(s => sheetNumbers.Any(sn => sn.Equals(s.SheetNumber, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (candidateSheets.Count == 0)
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Tidak ada Sheet yang sesuai kriteria ditemukan di source document." });
        }

        ViewFamilyType? draftingVft = new FilteredElementCollector(targetDoc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(x => x.ViewFamily == ViewFamily.Drafting);

        if (draftingVft == null)
        {
            return JsonSerializer.Serialize(new { status = "error", message = "Target document tidak memiliki ViewFamilyType DraftingView." });
        }

        var copiedSheetsInfo = new List<object>();
        var failedSheets = new List<string>();

        CopyPasteOptions copyOpts = new CopyPasteOptions();
        copyOpts.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

        using (Transaction tx = new Transaction(targetDoc, "AIGRAV Transfer Sheets & Views"))
        {
            var failOpt = tx.GetFailureHandlingOptions();
            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());
            failOpt.SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(failOpt);

            tx.Start();

            foreach (var srcSheet in candidateSheets)
            {
                try
                {
                    // 1. Temukan atau Salin Titleblock
                    FamilyInstance? srcTb = new FilteredElementCollector(sourceDoc, srcSheet.Id)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .FirstOrDefault();

                    ElementId targetTbSymbolId = ElementId.InvalidElementId;
                    if (srcTb != null && srcTb.Symbol != null)
                    {
                        string tbFamName = srcTb.Symbol.FamilyName;
                        FamilySymbol? existingTb = new FilteredElementCollector(targetDoc)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .WhereElementIsElementType()
                            .Cast<FamilySymbol>()
                            .FirstOrDefault(s => s.FamilyName.Equals(tbFamName, StringComparison.OrdinalIgnoreCase));

                        if (existingTb != null)
                        {
                            targetTbSymbolId = existingTb.Id;
                        }
                        else
                        {
                            try
                            {
                                Document tbDoc = sourceDoc.EditFamily(srcTb.Symbol.Family);
                                string tempTb = Path.Combine(Path.GetTempPath(), $"{srcTb.Symbol.FamilyName}_{Guid.NewGuid():N}.rfa");
                                tbDoc.SaveAs(tempTb, new SaveAsOptions { OverwriteExistingFile = true });
                                tbDoc.Close(false);

                                bool loaded = targetDoc.LoadFamily(tempTb, new FamilyLoadOpt(), out Family loadedTbFam);
                                try { File.Delete(tempTb); } catch {}

                                if (loaded && loadedTbFam != null)
                                {
                                    var symId = loadedTbFam.GetFamilySymbolIds().FirstOrDefault();
                                    if (symId != null) targetTbSymbolId = symId;
                                }
                            }
                            catch {}
                        }
                    }

                    if (targetTbSymbolId == ElementId.InvalidElementId)
                    {
                        var defaultTb = new FilteredElementCollector(targetDoc)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .WhereElementIsElementType()
                            .FirstOrDefault();
                        if (defaultTb != null) targetTbSymbolId = defaultTb.Id;
                    }

                    if (targetTbSymbolId == ElementId.InvalidElementId)
                    {
                        failedSheets.Add($"Sheet '{srcSheet.SheetNumber}': Tidak ditemukan Titleblock valid di target document.");
                        continue;
                    }

                    // 2. Buat ViewSheet Baru di Target
                    string newSheetNum = srcSheet.SheetNumber;
                    int suffix = 1;
                    while (new FilteredElementCollector(targetDoc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Any(s => s.SheetNumber.Equals(newSheetNum, StringComparison.OrdinalIgnoreCase)))
                    {
                        newSheetNum = $"{srcSheet.SheetNumber}-IMP{suffix++}";
                    }

                    ViewSheet targetSheet = ViewSheet.Create(targetDoc, targetTbSymbolId);
                    targetSheet.SheetNumber = newSheetNum;
                    targetSheet.Name = srcSheet.Name;

                    // 3. Salin elemen yang langsung menempel pada Sheet (Annotations/Lines/Texts di Sheet Space)
                    var directSheetElemIds = new FilteredElementCollector(sourceDoc, srcSheet.Id)
                        .WhereElementIsNotElementType()
                        .Where(e => e.Category != null &&
                                    e.Category.Id.Value != (long)BuiltInCategory.OST_TitleBlocks &&
                                    e.Category.Id.Value != (long)BuiltInCategory.OST_Viewports &&
                                    e.Category.Id.Value != (long)BuiltInCategory.OST_ScheduleGraphics)
                        .Select(e => e.Id)
                        .ToList();

                    if (directSheetElemIds.Count > 0)
                    {
                        try
                        {
                            ElementTransformUtils.CopyElements(srcSheet, directSheetElemIds, targetSheet, Transform.Identity, copyOpts);
                        }
                        catch {}
                    }

                    // 4. Salin Viewports dan Isinya (Drafting Views / 2D Detail & Anotasi)
                    var viewports = new FilteredElementCollector(sourceDoc, srcSheet.Id)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .ToList();

                    int vpsTransferred = 0;
                    var transferredViewNames = new List<string>();

                    foreach (var vp in viewports)
                    {
                        View? srcView = sourceDoc.GetElement(vp.ViewId) as View;
                        if (srcView == null) continue;

                        XYZ boxCenter = vp.GetBoxCenter();

                        if (srcView.ViewType == ViewType.DraftingView)
                        {
                            string targetVName = srcView.Name;
                            int vSuffix = 1;
                            while (new FilteredElementCollector(targetDoc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>().Any(v => v.Name.Equals(targetVName, StringComparison.OrdinalIgnoreCase)))
                            {
                                targetVName = $"{srcView.Name} (Imp {vSuffix++})";
                            }

                            ViewDrafting targetDrafting = ViewDrafting.Create(targetDoc, draftingVft.Id);
                            targetDrafting.Name = targetVName;
                            targetDrafting.Scale = srcView.Scale;

                            var detailIds = new FilteredElementCollector(sourceDoc, srcView.Id)
                                .WhereElementIsNotElementType()
                                .Select(e => e.Id)
                        .ToList();

                            if (detailIds.Count > 0)
                            {
                                try
                                {
                                    ElementTransformUtils.CopyElements(srcView, detailIds, targetDrafting, Transform.Identity, copyOpts);
                                }
                                catch {}
                            }

                            try
                            {
                                Viewport.Create(targetDoc, targetSheet.Id, targetDrafting.Id, boxCenter);
                                vpsTransferred++;
                                transferredViewNames.Add($"{srcView.Name} (Drafting)");
                            }
                            catch {}
                        }
                        else
                        {
                            // Model Views (Denah / Tampak / Potongan):
                            // Ekstrak 2D Anotasi, Dimensi, Text Notes, Detail Lines, dan Detail Components ke Drafting View 2D
                            string repViewName = $"{srcView.Name} - Detail & Anotasi 2D";
                            int vSuffix = 1;
                            while (new FilteredElementCollector(targetDoc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>().Any(v => v.Name.Equals(repViewName, StringComparison.OrdinalIgnoreCase)))
                            {
                                repViewName = $"{srcView.Name} - Detail & Anotasi 2D (Imp {vSuffix++})";
                            }

                            ViewDrafting targetDrafting = ViewDrafting.Create(targetDoc, draftingVft.Id);
                            targetDrafting.Name = repViewName;
                            targetDrafting.Scale = srcView.Scale;

                            var annotationIds = new FilteredElementCollector(sourceDoc, srcView.Id)
                                .WhereElementIsNotElementType()
                                .Where(e => e is TextNote || e is Dimension || e is DetailCurve ||
                                            (e.Category != null &&
                                             (e.Category.Id.Value == (long)BuiltInCategory.OST_DetailComponents ||
                                              e.Category.Id.Value == (long)BuiltInCategory.OST_Lines ||
                                              e.Category.Id.Value == (long)BuiltInCategory.OST_GenericAnnotation)))
                                .Select(e => e.Id)
                        .ToList();

                            if (annotationIds.Count > 0)
                            {
                                try
                                {
                                    ElementTransformUtils.CopyElements(srcView, annotationIds, targetDrafting, Transform.Identity, copyOpts);
                                }
                                catch {}
                            }

                            try
                            {
                                Viewport.Create(targetDoc, targetSheet.Id, targetDrafting.Id, boxCenter);
                                vpsTransferred++;
                                transferredViewNames.Add($"{srcView.Name} -> {repViewName} ({annotationIds.Count} anotasi/detail)");
                            }
                            catch {}
                        }
                    }

                    copiedSheetsInfo.Add(new
                    {
                        original_number = srcSheet.SheetNumber,
                        transferred_number = newSheetNum,
                        name = srcSheet.Name,
                        viewports_transferred = vpsTransferred,
                        views = transferredViewNames
                    });
                }
                catch (Exception ex)
                {
                    failedSheets.Add($"Sheet '{srcSheet.SheetNumber}': {ex.Message}");
                }
            }

            tx.Commit();
        }

        return JsonSerializer.Serialize(new
        {
            status = failedSheets.Count == 0 ? "success" : (copiedSheetsInfo.Count > 0 ? "partial_success" : "failed"),
            total_transferred = copiedSheetsInfo.Count,
            sheets = copiedSheetsInfo,
            failed_sheets = failedSheets
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}

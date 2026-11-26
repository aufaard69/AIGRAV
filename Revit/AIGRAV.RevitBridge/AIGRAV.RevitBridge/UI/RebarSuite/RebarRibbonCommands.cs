using System;
using System.IO;
using System.Linq;
using AIGRAV.Contracts;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AIGRAV.RevitBridge.UI.RebarSuite;

[Transaction(TransactionMode.Manual)]
public sealed class OpenRebarStudioCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        RebarSuiteController.OpenOrFocusStudio(commandData.Application);
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class ToggleRebarBrowserCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        RebarSuiteController.ToggleBrowserPane(commandData.Application);
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class SplitRebarCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
        Document? doc = uiDoc?.Document;
        if (uiDoc == null || doc == null) return Result.Cancelled;

        var rebarIds = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .OfType<Rebar>()
            .Select(r => r.Id.Value)
            .ToList();

        if (rebarIds.Count == 0)
        {
            TaskDialog.Show("AIGRAV Split & Stagger", "Pilih satu atau lebih elemen Rebar lurus di Revit terlebih dulu untuk menjalankan Split & Stagger (<= 12m).");
            return Result.Cancelled;
        }

        var profile = RebarSettingsStore.LoadFromDocumentOrDefault(doc);
        var res = RebarSplitEngine.SplitRebars(doc, rebarIds, SplitMethod.Staggered, profile.Global);
        TaskDialog.Show("AIGRAV Split & Stagger", res.Message + (res.Warnings.Count > 0 ? "\n\nCatatan:\n- " + string.Join("\n- ", res.Warnings.Take(6)) : ""));
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class PropagateRebarCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
        Document? doc = uiDoc?.Document;
        if (uiDoc == null || doc == null) return Result.Cancelled;

        var selectedHosts = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .Where(HostGeometryExtractor.CanHostRebar)
            .Cast<Element>()
            .ToList();

        if (selectedHosts.Count < 2)
        {
            try
            {
                Reference srcRef = uiDoc.Selection.PickObject(ObjectType.Element, new StructuralHostSelectionFilter(), "1/2: Pilih Host SUMBER (template)");
                var targetRefs = uiDoc.Selection.PickObjects(ObjectType.Element, new StructuralHostSelectionFilter(), "2/2: Pilih Host TARGET (Finish untuk Propagate)");
                var profile = RebarSettingsStore.LoadFromDocumentOrDefault(doc);
                var req = new RebarGenerationRequest
                {
                    Global = profile.Global, Beam = profile.Beam, Column = profile.Column, Foundation = profile.Foundation, Floor = profile.Floor
                };
                var res = RebarSplitEngine.PropagateRebars(doc, srcRef.ElementId.Value, targetRefs.Select(r => r.ElementId.Value).ToList(), req);
                TaskDialog.Show("AIGRAV Propagate Rebar", res.Message);
                RebarSuiteController.RefreshBrowserPane(doc);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
        }
        else
        {
            var profile = RebarSettingsStore.LoadFromDocumentOrDefault(doc);
            var req = new RebarGenerationRequest
            {
                Global = profile.Global, Beam = profile.Beam, Column = profile.Column, Foundation = profile.Foundation, Floor = profile.Floor
            };
            var res = RebarSplitEngine.PropagateRebars(doc, selectedHosts[0].Id.Value, selectedHosts.Skip(1).Select(h => h.Id.Value).ToList(), req);
            TaskDialog.Show("AIGRAV Propagate Rebar", res.Message);
            RebarSuiteController.RefreshBrowserPane(doc);
            return Result.Succeeded;
        }
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class SmartMarkRebarCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Document? doc = commandData.Application.ActiveUIDocument?.Document;
        if (doc == null) return Result.Cancelled;

        var profile = RebarSettingsStore.LoadFromDocumentOrDefault(doc);
        using var tx = new Transaction(doc, "AIGRAV Smart Mark Numbering");
        TransactionFailureHandling.SuppressWarnings(tx);
        tx.Start();
        int updated = MarkNumberingEngine.AssignMarksInDocument(doc, profile.Global);
        tx.Commit();

        RebarSuiteController.RefreshBrowserPane(doc);
        TaskDialog.Show("AIGRAV Smart Mark", $"Penomoran pintar selesai: {updated} set Rebar diperbarui (Scope: {profile.Global.MarkScope}).");
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class AutoTagRebarCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
        Document? doc = uiDoc?.Document;
        if (uiDoc == null || doc == null || doc.ActiveView == null) return Result.Cancelled;

        var rebarIds = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .OfType<Rebar>()
            .Select(r => r.Id.Value)
            .ToList();

        if (rebarIds.Count == 0)
        {
            rebarIds = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(Rebar))
                .Select(e => e.Id.Value)
                .ToList();
        }

        var warnings = new System.Collections.Generic.List<string>();
        using var tx = new Transaction(doc, "AIGRAV Auto-Tag Rebar");
        TransactionFailureHandling.SuppressWarnings(tx);
        tx.Start();
        int tagged = RebarVisualAndBbsService.AutoTagRebarsInView(doc, doc.ActiveView, rebarIds, warnings);
        tx.Commit();

        TaskDialog.Show("AIGRAV Auto-Tag Rebar", $"Berhasil memberi tag pada {tagged} rebar." + (warnings.Count > 0 ? "\n\n" + string.Join("\n", warnings) : ""));
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class CreateBbsScheduleCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
        Document? doc = uiDoc?.Document;
        if (uiDoc == null || doc == null) return Result.Cancelled;

        var warnings = new System.Collections.Generic.List<string>();
        ElementId? schedId;
        using (var tx = new Transaction(doc, "AIGRAV Create Native BBS Schedule"))
        {
            TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            schedId = RebarVisualAndBbsService.CreateOrUpdateNativeBbsSchedule(doc, warnings: warnings);
            tx.Commit();
        }

        if (schedId != null && doc.GetElement(schedId) is ViewSchedule vs)
        {
            uiDoc.ActiveView = vs;
        }
        else
        {
            TaskDialog.Show("AIGRAV BBS Schedule", "Gagal membuat schedule: " + string.Join("; ", warnings));
        }
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class ExportBbsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
        Document? doc = uiDoc?.Document;
        if (doc == null) return Result.Cancelled;

        var profile = RebarSettingsStore.LoadFromDocumentOrDefault(doc);
        var rows = MarkNumberingEngine.ExtractBbsRowsFromModel(doc);
        if (rows.Count == 0)
        {
            TaskDialog.Show("AIGRAV Export BBS", "Belum ada elemen Rebar di model ini untuk diekspor.");
            return Result.Cancelled;
        }

        string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AIGRAV_Rebar_Exports");
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string csvPath = RebarVisualAndBbsService.ExportBbsToCsv(rows, Path.Combine(outDir, $"BBS_{stamp}.csv"));
        string bvbsPath = RebarVisualAndBbsService.ExportBvbsAscii(rows, profile.Global.SteelGrade, Path.Combine(outDir, $"BBS_{stamp}.abs"));

        TaskDialog.Show("AIGRAV Export BBS Sukses",
            $"Total {rows.Count} baris mark ({rows.Sum(r => r.TotalWeightKg):N2} kg).\n\nCSV: {csvPath}\nBVBS: {bvbsPath}");
        return Result.Succeeded;
    }
}

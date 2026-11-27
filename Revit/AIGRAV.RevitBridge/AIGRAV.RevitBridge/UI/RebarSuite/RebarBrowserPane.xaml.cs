using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AIGRAV.Contracts;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge.UI.RebarSuite;

public sealed class RebarBrowserRowVm
{
    public string LevelName { get; set; } = string.Empty;
    public long HostElementId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string MarkSummary { get; set; } = string.Empty;
    public int BarCount { get; set; }
    public double WeightKg { get; set; }
    public bool IsHostModified { get; set; }
    public string StatusLabel => IsHostModified ? "MODIFIED" : "OK";
}

public partial class RebarBrowserPane : UserControl, IDockablePaneProvider
{
    private List<RebarBrowserRowVm> _rows = new();

    public RebarBrowserPane()
    {
        InitializeComponent();
        BrowserGrid.MouseDoubleClick += (_, _) => OnSelectClick(this, new RoutedEventArgs());
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        data.FrameworkElement = this;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right,
            MinimumWidth = 320,
            MinimumHeight = 300
        };
    }

    public void UpdateNodes(List<RebarBrowserNodeDto> nodes)
    {
        _rows = nodes.Select(n => new RebarBrowserRowVm
        {
            LevelName = n.LevelName,
            HostElementId = n.HostElementId,
            HostName = n.HostName,
            MarkSummary = n.MarkSummary,
            BarCount = n.BarCount,
            WeightKg = n.WeightKg,
            IsHostModified = n.IsHostModified
        }).ToList();

        BrowserGrid.ItemsSource = _rows;
        int modified = _rows.Count(r => r.IsHostModified);
        double totalKg = _rows.Sum(r => r.WeightKg);
        SummaryText.Text = $"{_rows.Count} host • {totalKg:N1} kg • {modified} modified";
    }

    private List<long> GetSelectedHostIds()
    {
        return BrowserGrid.SelectedItems
            .OfType<RebarBrowserRowVm>()
            .Select(r => r.HostElementId)
            .Distinct()
            .ToList();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        RebarSuiteController.RaiseAction(app =>
        {
            RebarSuiteController.RefreshBrowserPane(app.ActiveUIDocument?.Document);
        });
    }

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        var ids = GetSelectedHostIds();
        if (ids.Count == 0) return;

        RebarSuiteController.RaiseAction(app =>
        {
            UIDocument? uiDoc = app.ActiveUIDocument;
            if (uiDoc == null) return;
            var elemIds = ids.Select(id => new ElementId(id)).ToList();
            uiDoc.Selection.SetElementIds(elemIds);
            uiDoc.ShowElements(elemIds);
        });
    }

    private void OnIsolateClick(object sender, RoutedEventArgs e)
    {
        var ids = GetSelectedHostIds();
        if (ids.Count == 0) return;

        RebarSuiteController.RaiseAction(app =>
        {
            UIDocument? uiDoc = app.ActiveUIDocument;
            Document? doc = uiDoc?.Document;
            if (doc == null || doc.ActiveView == null) return;

            var toIsolate = new List<ElementId>();
            foreach (long hid in ids)
            {
                toIsolate.Add(new ElementId(hid));
                foreach (var r in RebarIdempotencyManager.FindExistingAigravRebarsForHost(doc, hid))
                    toIsolate.Add(r.Id);
            }

            using var tx = new Transaction(doc, "AIGRAV Isolate Host & Rebar");
            TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            doc.ActiveView.IsolateElementsTemporary(toIsolate);
            tx.Commit();
            uiDoc!.ShowElements(toIsolate);
        });
    }

    private void OnOpenStudioClick(object sender, RoutedEventArgs e)
    {
        var ids = GetSelectedHostIds();
        RebarSuiteController.RaiseAction(app =>
        {
            UIDocument? uiDoc = app.ActiveUIDocument;
            if (uiDoc != null && ids.Count > 0)
                uiDoc.Selection.SetElementIds(ids.Select(id => new ElementId(id)).ToList());
            RebarSuiteController.OpenOrFocusStudio(app);
        });
    }

    private void OnUpdateModifiedClick(object sender, RoutedEventArgs e)
    {
        var modIds = _rows.Where(r => r.IsHostModified).Select(r => r.HostElementId).ToList();
        if (modIds.Count == 0)
        {
            StatusFooter.Text = "Semua host sudah sinkron (0 modified).";
            return;
        }

        RebarSuiteController.RaiseAction(app =>
        {
            Document? doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;
            var profile = RebarSettingsStore.LoadFromDocumentOrDefault(doc);
            var req = new RebarGenerationRequest
            {
                SelectedHostElementIds = modIds,
                Global = profile.Global,
                Beam = profile.Beam,
                Column = profile.Column,
                Foundation = profile.Foundation,
                Floor = profile.Floor
            };
            var res = RebarGenerationOrchestrator.GenerateSelected(doc, req);
            RebarSuiteController.RefreshBrowserPane(doc);
            Dispatcher.BeginInvoke(new Action(() => StatusFooter.Text = res.Message));
        });
    }
}

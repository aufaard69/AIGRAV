using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;

namespace AIGRAV.RevitBridge.UI.RebarSuite;

/// <summary>
/// Dedicated 100% offline ExternalEvent handler for AIGRAV Rebar Suite.
/// Completely decoupled from AIGRAVRuntime.IsEnabled / HTTP server so it works
/// even when AIGRAV AI Bridge is OFF.
/// </summary>
public sealed class RebarExternalEventHandler : IExternalEventHandler
{
    private readonly ConcurrentQueue<Action<UIApplication>> _actions = new();

    public void Enqueue(Action<UIApplication> action)
    {
        if (action != null) _actions.Enqueue(action);
    }

    public void Execute(UIApplication app)
    {
        while (_actions.TryDequeue(out var act))
        {
            try
            {
                act(app);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AIGRAV Rebar Suite Error", ex.Message);
            }
        }
    }

    public string GetName() => "AIGRAV Offline Rebar Suite Handler";
}

public sealed class StructuralHostSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem) => HostGeometryExtractor.CanHostRebar(elem);
    public bool AllowReference(Reference reference, XYZ position) => false;
}

/// <summary>
/// Singleton lifecycle controller connecting Revit Startup / SelectionChanged / DockablePane
/// with the modeless RebarStudioWindow without blocking Revit UI.
/// </summary>
public static class RebarSuiteController
{
    public static readonly DockablePaneId BrowserPaneId =
        new(new Guid("A9F4D812-3B7C-4E19-9D5A-6C2F084B7E11"));

    private static RebarExternalEventHandler? _handler;
    private static ExternalEvent? _externalEvent;
    private static RebarStudioWindow? _studioWindow;
    private static RebarBrowserPane? _browserPane;

    public static bool IsStudioOpen => _studioWindow != null && _studioWindow.IsLoaded && _studioWindow.IsVisible;

    public static void Initialize(UIControlledApplication application)
    {
        _handler = new RebarExternalEventHandler();
        _externalEvent = ExternalEvent.Create(_handler);

        // Register Dockable Reinforcement Browser Pane cleanly
        _browserPane = new RebarBrowserPane();
        try
        {
            if (!DockablePane.PaneIsRegistered(BrowserPaneId))
            {
                application.RegisterDockablePane(
                    BrowserPaneId,
                    "AIGRAV Reinforcement Browser",
                    _browserPane);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RebarSuiteController] RegisterDockablePane warning: {ex.Message}");
        }

        // Subscribe to live Revit SelectionChanged (near-zero cost when Studio is closed)
        application.SelectionChanged += OnRevitSelectionChanged;
    }

    public static void Shutdown(UIControlledApplication application)
    {
        try
        {
            application.SelectionChanged -= OnRevitSelectionChanged;
        }
        catch { /* shutting down */ }

        try
        {
            if (_studioWindow != null)
            {
                _studioWindow.Close();
                _studioWindow = null;
            }
        }
        catch { /* shutting down */ }

        _externalEvent?.Dispose();
        _externalEvent = null;
    }

    public static void RaiseAction(Action<UIApplication> action)
    {
        if (_handler == null || _externalEvent == null) return;
        _handler.Enqueue(action);
        _externalEvent.Raise();
    }

    public static void OpenOrFocusStudio(UIApplication app)
    {
        UIDocument? uiDoc = app.ActiveUIDocument;
        Document? doc = uiDoc?.Document;

        if (_studioWindow == null || !_studioWindow.IsLoaded)
        {
            RebarPresetProfile profile = RebarSettingsStore.LoadFromDocumentOrDefault(doc);
            _studioWindow = new RebarStudioWindow(profile);

            try
            {
                var helper = new WindowInteropHelper(_studioWindow);
                helper.Owner = app.MainWindowHandle;
            }
            catch { /* owner fallback */ }

            _studioWindow.Closed += (_, _) => _studioWindow = null;
            _studioWindow.Show();
        }
        else
        {
            if (_studioWindow.WindowState == WindowState.Minimized)
                _studioWindow.WindowState = WindowState.Normal;
            _studioWindow.Activate();
        }

        // Immediately sync current Revit selection & render initial 3D + 2D preview!
        if (uiDoc != null && doc != null)
        {
            var selIds = uiDoc.Selection.GetElementIds()
                .Where(id => HostGeometryExtractor.CanHostRebar(doc.GetElement(id)))
                .Select(id => id.Value)
                .ToList();

            PushSelectionPreviewToStudio(doc, selIds);
        }
    }

    public static void ToggleBrowserPane(UIApplication app)
    {
        try
        {
            if (!DockablePane.PaneExists(BrowserPaneId)) return;
            DockablePane pane = app.GetDockablePane(BrowserPaneId);
            if (pane.IsShown())
            {
                pane.Hide();
            }
            else
            {
                pane.Show();
                RefreshBrowserPane(app.ActiveUIDocument?.Document);
            }
        }
        catch (Exception ex)
        {
            TaskDialog.Show("AIGRAV Rebar Browser", $"Tidak dapat membuka pane: {ex.Message}");
        }
    }

    public static void RefreshBrowserPane(Document? doc)
    {
        if (_browserPane == null || doc == null || !doc.IsValidObject) return;
        var nodes = RebarGenerationOrchestrator.ScanReinforcedHosts(doc);
        _browserPane.Dispatcher.BeginInvoke(new Action(() => _browserPane.UpdateNodes(nodes)));
    }

    private static void OnRevitSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsStudioOpen || _studioWindow == null || !_studioWindow.AutoSyncSelectionEnabled)
            return;

        try
        {
            Document doc = e.GetDocument();
            if (doc == null || !doc.IsValidObject) return;

            var selectedHostIds = e.GetSelectedElements()
                .Where(id => HostGeometryExtractor.CanHostRebar(doc.GetElement(id)))
                .Select(id => id.Value)
                .ToList();

            PushSelectionPreviewToStudio(doc, selectedHostIds);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RebarSuiteController] OnSelectionChanged: {ex.Message}");
        }
    }

    public static void PushSelectionPreviewToStudio(Document doc, List<long> hostIds)
    {
        if (_studioWindow == null) return;
        try
        {
            var req = _studioWindow.BuildCurrentRequest(hostIds);
            var preview = RebarPreviewProjectionBuilder.BuildPreview(doc, req);
            _studioWindow.Dispatcher.BeginInvoke(new Action(() => _studioWindow.ApplyPreviewPackage(preview, updateSelectionQueue: true)));
        }
        catch (Exception ex)
        {
            _studioWindow.Dispatcher.BeginInvoke(new Action(() => _studioWindow.ShowStatus($"Preview error: {ex.Message}", isError: true)));
        }
    }

    public static void RequestPreviewRefreshForCheckedIds(List<long> checkedHostIds)
    {
        RaiseAction(app =>
        {
            Document? doc = app.ActiveUIDocument?.Document;
            if (doc == null || _studioWindow == null) return;
            var req = _studioWindow.BuildCurrentRequest(checkedHostIds);
            var preview = RebarPreviewProjectionBuilder.BuildPreview(doc, req);
            _studioWindow.Dispatcher.BeginInvoke(new Action(() => _studioWindow.ApplyPreviewPackage(preview, updateSelectionQueue: false)));
        });
    }

    public static void RequestPickElementsInRevit()
    {
        RaiseAction(app =>
        {
            UIDocument? uiDoc = app.ActiveUIDocument;
            if (uiDoc == null) return;
            try
            {
                var refs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    new StructuralHostSelectionFilter(),
                    "Pilih elemen Beam / Column / Foundation / Floor untuk AIGRAV Rebar Suite (Finish untuk konfirmasi)");

                var ids = refs.Select(r => r.ElementId).Distinct().ToList();
                uiDoc.Selection.SetElementIds(ids);
                PushSelectionPreviewToStudio(uiDoc.Document, ids.Select(id => id.Value).ToList());
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User pressed Esc cleanly
            }
        });
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using AIGRAV.Contracts;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using ComboBox = System.Windows.Controls.ComboBox;

namespace AIGRAV.RevitBridge.UI.RebarSuite;

public partial class RebarStudioWindow : Window
{
    private static readonly int[] StandardDiameters = { 8, 10, 12, 13, 16, 19, 22, 25, 29, 32 };

    private readonly RebarViewport3DRenderer _viewport3D = new();
    private readonly DispatcherTimer _debounceTimer;

    private List<RebarPresetProfile> _presets = new();
    private List<SelectedHostSummary> _selectionQueue = new();
    private RebarPreviewPackage? _currentPreview;
    private bool _isLoadingUi;

    private bool _isDraggingOrbit;
    private bool _isDraggingPan;
    private System.Windows.Point _lastMousePos;

    public bool AutoSyncSelectionEnabled => AutoSyncCheckBox.IsChecked == true;

    public RebarStudioWindow(RebarPresetProfile initialProfile)
    {
        _isLoadingUi = true;
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            TriggerPreviewRefresh();
        };

        try
        {
            InitializeComponent();
            PopulateDiameterCombos();
            LoadPresetsIntoCombo(initialProfile);
            LoadProfileIntoUi(initialProfile);
            _viewport3D.UpdateCamera(MainCamera3D);
        }
        finally
        {
            _isLoadingUi = false;
        }
    }

    private void PopulateDiameterCombos()
    {
        ComboBox[] combos =
        {
            BeamTopDiaCombo, BeamBotDiaCombo, BeamExtraTopDiaCombo, BeamExtraBotDiaCombo,
            BeamSkinDiaCombo, BeamStirrupDiaCombo,
            ColMainDiaCombo, ColTieDiaCombo,
            FoundBotXDiaCombo, FoundBotYDiaCombo, FoundTopXDiaCombo, FoundTopYDiaCombo,
            FloorBotXDiaCombo, FloorBotYDiaCombo, FloorTopXDiaCombo, FloorTopYDiaCombo
        };
        foreach (var cb in combos)
        {
            cb.Items.Clear();
            foreach (int d in StandardDiameters)
                cb.Items.Add(new ComboBoxItem { Content = $"D{d}", Tag = d });
        }
    }

    private void LoadPresetsIntoCombo(RebarPresetProfile activeProfile)
    {
        _isLoadingUi = true;
        _presets = RebarSettingsStore.LoadAllPresets();
        PresetComboBox.Items.Clear();
        foreach (var p in _presets)
            PresetComboBox.Items.Add(p.Name);

        int idx = _presets.FindIndex(p => p.Name.Equals(activeProfile.Name, StringComparison.OrdinalIgnoreCase));
        PresetComboBox.SelectedIndex = idx >= 0 ? idx : 0;
        _isLoadingUi = false;
    }

    private void LoadProfileIntoUi(RebarPresetProfile p)
    {
        _isLoadingUi = true;
        try
        {
            // Global
            MaxStockCombo.Text = ((int)p.Global.MaxStockLengthMm).ToString(CultureInfo.InvariantCulture);
            SelectTag(LapModeCombo, p.Global.LapMode.ToString());
            CustomLapBox.Text = ((int)p.Global.CustomLapMm).ToString(CultureInfo.InvariantCulture);
            SelectTag(BondCombo, p.Global.BondCondition.ToString());
            SelectTag(RoundingCombo, p.Global.RoundingMode.ToString());
            StaggerCheck.IsChecked = p.Global.StaggerSplices;
            MomentSafeCheck.IsChecked = p.Global.MomentSafeSplit;
            ConstraintsCheck.IsChecked = p.Global.UseRebarConstraints;
            ColorByDiaCheck.IsChecked = p.Global.ApplyColorByDiameter;
            LokasiBox.Text = p.Global.DefaultLokasi;

            // Beam
            BeamCoverBox.Text = p.Beam.CoverMm.ToString("0", CultureInfo.InvariantCulture);
            SelectIntTag(BeamTopDiaCombo, p.Beam.TopBarDiameterMm);
            BeamTopCountBox.Text = p.Beam.TopBarCount.ToString(CultureInfo.InvariantCulture);
            SelectIntTag(BeamBotDiaCombo, p.Beam.BottomBarDiameterMm);
            BeamBotCountBox.Text = p.Beam.BottomBarCount.ToString(CultureInfo.InvariantCulture);
            SelectTag(BeamJointHookCombo, p.Beam.ExteriorJointHook.ToString());
            SelectTag(BeamHookFormulaCombo, p.Beam.JointHookLegFormula);
            BeamExtraTopCheck.IsChecked = p.Beam.EnableExtraTopSupport;
            SelectIntTag(BeamExtraTopDiaCombo, p.Beam.ExtraTopSupportDiaMm);
            BeamExtraTopCountBox.Text = p.Beam.ExtraTopSupportCount.ToString(CultureInfo.InvariantCulture);
            BeamExtraBotCheck.IsChecked = p.Beam.EnableExtraBottomMidspan;
            SelectIntTag(BeamExtraBotDiaCombo, p.Beam.ExtraBottomMidspanDiaMm);
            BeamExtraBotCountBox.Text = p.Beam.ExtraBottomMidspanCount.ToString(CultureInfo.InvariantCulture);
            SelectTag(BeamCutoffModeCombo, p.Beam.CutoffMode.ToString());
            BeamSkinCheck.IsChecked = p.Beam.EnableSkinBars;
            SelectIntTag(BeamSkinDiaCombo, p.Beam.SkinBarDiaMm);
            BeamSkinPairsBox.Text = p.Beam.SkinBarPairs.ToString(CultureInfo.InvariantCulture);
            SelectIntTag(BeamStirrupDiaCombo, p.Beam.StirrupDiaMm);
            BeamStirrupTumpBox.Text = p.Beam.StirrupSupportSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            BeamStirrupLapBox.Text = p.Beam.StirrupMidspanSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            SelectTag(BeamStirrupZoneModeCombo, p.Beam.StirrupZoneMode.ToString());
            SelectContent(BeamStirrupLegsCombo, p.Beam.StirrupLegs.ToString(CultureInfo.InvariantCulture));
            BeamSeismic135Check.IsChecked = p.Beam.Use135SeismicHook;

            // Column
            ColCoverBox.Text = p.Column.CoverMm.ToString("0", CultureInfo.InvariantCulture);
            SelectIntTag(ColMainDiaCombo, p.Column.MainBarDiaMm);
            ColMainCountBox.Text = p.Column.TotalMainBars.ToString(CultureInfo.InvariantCulture);
            SelectIntTag(ColTieDiaCombo, p.Column.TieDiaMm);
            ColTieLoBox.Text = p.Column.TieLoSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            ColTieMidBox.Text = p.Column.TieMidSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            ColTieFoundBox.Text = p.Column.FoundationTieSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            SelectContent(ColTieLegsCombo, p.Column.TieLegs.ToString(CultureInfo.InvariantCulture));
            SelectTag(ColSpliceStrategyCombo, p.Column.SpliceStrategy.ToString());
            SelectTag(ColBaseBendCombo, p.Column.BaseFootingBend.ToString());
            ColBaseBendLegBox.Text = p.Column.BaseFootingBendLegMm.ToString("0", CultureInfo.InvariantCulture);
            ColFootEmbedBox.Text = p.Column.FootingEmbedCoverClearanceMm.ToString("0", CultureInfo.InvariantCulture);
            SelectTag(ColRoofHookCombo, p.Column.RoofTopHook.ToString());
            ColCrankCheck.IsChecked = p.Column.EnableCrankAtOffset;
            ColFoundTiesCheck.IsChecked = p.Column.IncludeFoundationConfiningTies;

            // Foundation
            FoundCoverBox.Text = p.Foundation.CoverMm.ToString("0", CultureInfo.InvariantCulture);
            SelectIntTag(FoundBotXDiaCombo, p.Foundation.BottomXDiaMm);
            FoundBotXSpBox.Text = p.Foundation.BottomXSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            SelectIntTag(FoundBotYDiaCombo, p.Foundation.BottomYDiaMm);
            FoundBotYSpBox.Text = p.Foundation.BottomYSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            FoundTopMeshCheck.IsChecked = p.Foundation.EnableTopMesh;
            SelectIntTag(FoundTopXDiaCombo, p.Foundation.TopXDiaMm);
            FoundTopXSpBox.Text = p.Foundation.TopXSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            SelectIntTag(FoundTopYDiaCombo, p.Foundation.TopYDiaMm);
            FoundTopYSpBox.Text = p.Foundation.TopYSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            SelectTag(FoundEdgeTermCombo, p.Foundation.EdgeTermination.ToString());
            FoundHookLegBox.Text = p.Foundation.HookLegMm.ToString("0", CultureInfo.InvariantCulture);
            FoundPerimBinderCheck.IsChecked = p.Foundation.IncludePerimeterBinder;

            // Floor
            FloorCoverBox.Text = p.Floor.CoverMm.ToString("0", CultureInfo.InvariantCulture);
            SelectTag(FloorLayoutModeCombo, p.Floor.MeshLayoutMode.ToString());
            SelectTag(FloorTopModeCombo, p.Floor.TopMode.ToString());
            SelectIntTag(FloorBotXDiaCombo, p.Floor.BottomXDiaMm);
            FloorBotXSpBox.Text = p.Floor.BottomXSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            SelectIntTag(FloorBotYDiaCombo, p.Floor.BottomYDiaMm);
            FloorBotYSpBox.Text = p.Floor.BottomYSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            SelectIntTag(FloorTopXDiaCombo, p.Floor.TopXDiaMm);
            FloorTopXSpBox.Text = p.Floor.TopXSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            SelectIntTag(FloorTopYDiaCombo, p.Floor.TopYDiaMm);
            FloorTopYSpBox.Text = p.Floor.TopYSpacingMm.ToString("0", CultureInfo.InvariantCulture);
            FloorEdgeHookCheck.IsChecked = p.Floor.EdgeHook90;
            FloorTrimVoidCheck.IsChecked = p.Floor.TrimAtVoidOpenings;
            FloorDiagTrimCheck.IsChecked = p.Floor.AddOpeningDiagonalTrimmers;
        }
        finally
        {
            _isLoadingUi = false;
        }
    }

    public RebarPresetProfile ReadProfileFromUi()
    {
        return new RebarPresetProfile
        {
            Name = PresetComboBox.SelectedItem?.ToString() ?? "Custom Profile",
            Global = new RebarGlobalSettings
            {
                MaxStockLengthMm = ParseDouble(MaxStockCombo.Text, 12000),
                LapMode = ParseEnum<LapMode>(GetSelectedTag(LapModeCombo), LapMode.D40),
                CustomLapMm = ParseDouble(CustomLapBox.Text, 800),
                BondCondition = ParseEnum<BondCondition>(GetSelectedTag(BondCombo), BondCondition.Good),
                RoundingMode = ParseEnum<LengthRoundingMode>(GetSelectedTag(RoundingCombo), LengthRoundingMode.Nearest25Mm),
                StaggerSplices = StaggerCheck.IsChecked == true,
                MomentSafeSplit = MomentSafeCheck.IsChecked == true,
                UseRebarConstraints = ConstraintsCheck.IsChecked == true,
                ApplyColorByDiameter = ColorByDiaCheck.IsChecked == true,
                DefaultLokasi = LokasiBox.Text.Trim()
            },
            Beam = new BeamConfig
            {
                CoverMm = ParseDouble(BeamCoverBox.Text, 40),
                TopBarDiameterMm = GetSelectedIntTag(BeamTopDiaCombo, 16),
                TopBarCount = ParseInt(BeamTopCountBox.Text, 3, 2, 12),
                BottomBarDiameterMm = GetSelectedIntTag(BeamBotDiaCombo, 16),
                BottomBarCount = ParseInt(BeamBotCountBox.Text, 3, 2, 12),
                ExteriorJointHook = ParseEnum<EndHookDirection>(GetSelectedTag(BeamJointHookCombo), EndHookDirection.Bend90Inward),
                JointHookLegFormula = GetSelectedTag(BeamHookFormulaCombo) ?? "h-2c",
                EnableExtraTopSupport = BeamExtraTopCheck.IsChecked == true,
                ExtraTopSupportDiaMm = GetSelectedIntTag(BeamExtraTopDiaCombo, 16),
                ExtraTopSupportCount = ParseInt(BeamExtraTopCountBox.Text, 2, 1, 8),
                EnableExtraBottomMidspan = BeamExtraBotCheck.IsChecked == true,
                ExtraBottomMidspanDiaMm = GetSelectedIntTag(BeamExtraBotDiaCombo, 16),
                ExtraBottomMidspanCount = ParseInt(BeamExtraBotCountBox.Text, 2, 1, 8),
                CutoffMode = ParseEnum<CutoffExtensionMode>(GetSelectedTag(BeamCutoffModeCombo), CutoffExtensionMode.SniMaxDOr12D),
                EnableSkinBars = BeamSkinCheck.IsChecked == true,
                SkinBarDiaMm = GetSelectedIntTag(BeamSkinDiaCombo, 12),
                SkinBarPairs = ParseInt(BeamSkinPairsBox.Text, 1, 1, 4),
                StirrupDiaMm = GetSelectedIntTag(BeamStirrupDiaCombo, 10),
                StirrupSupportSpacingMm = ParseDouble(BeamStirrupTumpBox.Text, 100),
                StirrupMidspanSpacingMm = ParseDouble(BeamStirrupLapBox.Text, 150),
                StirrupZoneMode = ParseEnum<StirrupZoneMode>(GetSelectedTag(BeamStirrupZoneModeCombo), StirrupZoneMode.QuarterSpan_L4_L2_L4),
                StirrupLegs = ParseInt((BeamStirrupLegsCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), 2, 2, 4),
                Use135SeismicHook = BeamSeismic135Check.IsChecked == true
            },
            Column = new ColumnConfig
            {
                CoverMm = ParseDouble(ColCoverBox.Text, 40),
                MainBarDiaMm = GetSelectedIntTag(ColMainDiaCombo, 19),
                TotalMainBars = ParseInt(ColMainCountBox.Text, 8, 4, 24),
                TieDiaMm = GetSelectedIntTag(ColTieDiaCombo, 10),
                TieLoSpacingMm = ParseDouble(ColTieLoBox.Text, 100),
                TieMidSpacingMm = ParseDouble(ColTieMidBox.Text, 150),
                FoundationTieSpacingMm = ParseDouble(ColTieFoundBox.Text, 150),
                TieLegs = ParseInt((ColTieLegsCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), 4, 2, 4),
                SpliceStrategy = ParseEnum<ColumnSpliceStrategy>(GetSelectedTag(ColSpliceStrategyCombo), ColumnSpliceStrategy.AutoFollowModelCut),
                BaseFootingBend = ParseEnum<EndHookDirection>(GetSelectedTag(ColBaseBendCombo), EndHookDirection.Bend90Outward),
                BaseFootingBendLegMm = ParseDouble(ColBaseBendLegBox.Text, 300),
                FootingEmbedCoverClearanceMm = ParseDouble(ColFootEmbedBox.Text, 100),
                RoofTopHook = ParseEnum<EndHookDirection>(GetSelectedTag(ColRoofHookCombo), EndHookDirection.Bend90Inward),
                EnableCrankAtOffset = ColCrankCheck.IsChecked == true,
                IncludeFoundationConfiningTies = ColFoundTiesCheck.IsChecked == true
            },
            Foundation = new FoundationConfig
            {
                CoverMm = ParseDouble(FoundCoverBox.Text, 75),
                BottomXDiaMm = GetSelectedIntTag(FoundBotXDiaCombo, 16),
                BottomXSpacingMm = ParseDouble(FoundBotXSpBox.Text, 150),
                BottomYDiaMm = GetSelectedIntTag(FoundBotYDiaCombo, 16),
                BottomYSpacingMm = ParseDouble(FoundBotYSpBox.Text, 150),
                EnableTopMesh = FoundTopMeshCheck.IsChecked == true,
                TopXDiaMm = GetSelectedIntTag(FoundTopXDiaCombo, 13),
                TopXSpacingMm = ParseDouble(FoundTopXSpBox.Text, 200),
                TopYDiaMm = GetSelectedIntTag(FoundTopYDiaCombo, 13),
                TopYSpacingMm = ParseDouble(FoundTopYSpBox.Text, 200),
                EdgeTermination = ParseEnum<EdgeTerminationMode>(GetSelectedTag(FoundEdgeTermCombo), EdgeTerminationMode.Bend90),
                HookLegMm = ParseDouble(FoundHookLegBox.Text, 250),
                IncludePerimeterBinder = FoundPerimBinderCheck.IsChecked == true
            },
            Floor = new FloorConfig
            {
                CoverMm = ParseDouble(FloorCoverBox.Text, 20),
                MeshLayoutMode = ParseEnum<FloorMeshLayoutMode>(GetSelectedTag(FloorLayoutModeCombo), FloorMeshLayoutMode.Segmented12mMax),
                TopMode = ParseEnum<FloorTopReinforcementMode>(GetSelectedTag(FloorTopModeCombo), FloorTopReinforcementMode.SupportStripL4),
                BottomXDiaMm = GetSelectedIntTag(FloorBotXDiaCombo, 10),
                BottomXSpacingMm = ParseDouble(FloorBotXSpBox.Text, 150),
                BottomYDiaMm = GetSelectedIntTag(FloorBotYDiaCombo, 10),
                BottomYSpacingMm = ParseDouble(FloorBotYSpBox.Text, 150),
                TopXDiaMm = GetSelectedIntTag(FloorTopXDiaCombo, 10),
                TopXSpacingMm = ParseDouble(FloorTopXSpBox.Text, 150),
                TopYDiaMm = GetSelectedIntTag(FloorTopYDiaCombo, 10),
                TopYSpacingMm = ParseDouble(FloorTopYSpBox.Text, 150),
                EdgeHook90 = FloorEdgeHookCheck.IsChecked == true,
                TrimAtVoidOpenings = FloorTrimVoidCheck.IsChecked == true,
                AddOpeningDiagonalTrimmers = FloorDiagTrimCheck.IsChecked == true
            }
        };
    }

    public RebarGenerationRequest BuildCurrentRequest(List<long> hostIds)
    {
        RebarPresetProfile p = Dispatcher.CheckAccess()
            ? ReadProfileFromUi()
            : Dispatcher.Invoke(ReadProfileFromUi);

        return new RebarGenerationRequest
        {
            SelectedHostElementIds = hostIds,
            Global = p.Global,
            Beam = p.Beam,
            Column = p.Column,
            Foundation = p.Foundation,
            Floor = p.Floor
        };
    }

    public void ApplyPreviewPackage(RebarPreviewPackage preview, bool updateSelectionQueue)
    {
        _currentPreview = preview;

        if (updateSelectionQueue)
        {
            _selectionQueue = preview.Hosts.ToList();
            SelectionQueueList.ItemsSource = null;
            SelectionQueueList.ItemsSource = _selectionQueue;
            SelectionCountBadge.Text = $" ({_selectionQueue.Count})";

            // Auto-switch category tab when selection focuses on one primary category
            var firstCat = _selectionQueue.FirstOrDefault()?.Category;
            if (firstCat.HasValue && _selectionQueue.All(h => h.Category == firstCat.Value))
            {
                CategoryTabControl.SelectedIndex = firstCat.Value switch
                {
                    StructuralHostCategory.Beam => 0,
                    StructuralHostCategory.Column => 1,
                    StructuralHostCategory.Foundation => 2,
                    StructuralHostCategory.Floor => 3,
                    _ => CategoryTabControl.SelectedIndex
                };
            }
        }

        // Update 5 KPI Cards
        double maxStock = ParseDouble(MaxStockCombo.Text, 12000);
        KpiSteelKgText.Text = $"{preview.TotalSteelWeightKg:N1} kg";
        KpiConcreteM3Text.Text = $"{preview.TotalConcreteVolumeM3:N2} m³";
        KpiRatioText.Text = $"{preview.SteelDensityKgPerM3:N1} kg/m³";
        KpiMaxBarText.Text = $"{preview.MaxSingleBarLengthMm:N0} mm";
        KpiMaxBarText.Foreground = new SolidColorBrush(preview.MaxSingleBarLengthMm <= maxStock + 1
            ? System.Windows.Media.Color.FromRgb(52, 211, 153)
            : System.Windows.Media.Color.FromRgb(244, 63, 94));
        KpiOverlapsText.Text = $"{preview.TotalOverlapCount} / {preview.TotalBarPieces} btg";

        BbsDataGrid.ItemsSource = preview.BbsRows;
        WarningsListBox.ItemsSource = preview.Warnings.Count > 0
            ? preview.Warnings
            : new List<string> { "Tidak ada warning — seluruh panjang potong <= Max Stock & bebas double-counting." };

        Rebuild3DScene(fitCamera: updateSelectionQueue);
        Redraw2DCadCanvases();

        ShowStatus(preview.Hosts.Count > 0
            ? $"Live Preview aktif: {preview.Hosts.Count} host terpilih • {preview.TotalBarPieces} batang • {preview.TotalSteelWeightKg:N1} kg ({preview.SteelDensityKgPerM3:N1} kg/m³)."
            : "Belum ada elemen struktur terpilih. Pilih Beam / Column / Foundation / Floor di Revit.");
    }

    private void Rebuild3DScene(bool fitCamera)
    {
        if (SceneModelVisual == null || MainCamera3D == null) return;
        if (_currentPreview == null)
        {
            SceneModelVisual.Content = null;
            return;
        }
        SceneModelVisual.Content = _viewport3D.BuildSceneModel(
            _currentPreview,
            ConcreteOpacitySlider.Value,
            ShowConcreteCheck.IsChecked == true,
            ShowRebarCheck.IsChecked == true,
            ShowStirrupsCheck.IsChecked == true);

        if (fitCamera) _viewport3D.FitCameraToScene(_currentPreview);
        _viewport3D.UpdateCamera(MainCamera3D);
    }

    private void Redraw2DCadCanvases()
    {
        if (ElevationCanvas == null || SectionPlanCanvas == null) return;
        Rebar2DCadRenderer.RenderElevation(ElevationCanvas, _currentPreview);
        Rebar2DCadRenderer.RenderSectionOrPlan(SectionPlanCanvas, _currentPreview);
    }

    public void ShowStatus(string message, bool isError = false)
    {
        if (StatusTextBlock == null) return;
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = new SolidColorBrush(isError
            ? System.Windows.Media.Color.FromRgb(248, 113, 113)
            : System.Windows.Media.Color.FromRgb(148, 163, 184));
    }

    private void SchedulePreviewRefresh()
    {
        if (_isLoadingUi || _debounceTimer == null) return;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void TriggerPreviewRefresh()
    {
        var activeIds = _selectionQueue.Where(h => h.IsIncluded).Select(h => h.ElementId).ToList();
        if (activeIds.Count == 0) return;
        RebarSuiteController.RequestPreviewRefreshForCheckedIds(activeIds);
    }

    // ---- UI EVENT HANDLERS ----

    private void OnRuleChanged(object sender, SelectionChangedEventArgs e) => SchedulePreviewRefresh();
    private void OnRuleTextLostFocus(object sender, RoutedEventArgs e) => SchedulePreviewRefresh();
    private void OnRuleCheckChanged(object sender, RoutedEventArgs e) => SchedulePreviewRefresh();
    private void OnHostIncludeToggled(object sender, RoutedEventArgs e) => SchedulePreviewRefresh();
    private void OnViewportDisplayOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUi || !IsLoaded) return;
        Rebuild3DScene(fitCamera: false);
    }
    private void OnConcreteOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingUi || !IsLoaded) return;
        Rebuild3DScene(fitCamera: false);
    }
    private void OnPreviewTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi || !IsLoaded) return;
        Redraw2DCadCanvases();
    }
    private void OnCadCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isLoadingUi) return;
        Redraw2DCadCanvases();
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi || PresetComboBox.SelectedIndex < 0 || PresetComboBox.SelectedIndex >= _presets.Count) return;
        LoadProfileIntoUi(_presets[PresetComboBox.SelectedIndex]);
        TriggerPreviewRefresh();
    }

    private void OnSavePresetClick(object sender, RoutedEventArgs e)
    {
        var profile = ReadProfileFromUi();
        RebarSettingsStore.SavePreset(profile);
        ShowStatus($"Content Pack '{profile.Name}' tersimpan ke %AppData%\\AIGRAV\\RebarSuite\\ContentPacks.json.");
    }

    private void OnSaveToModelClick(object sender, RoutedEventArgs e)
    {
        var profile = ReadProfileFromUi();
        RebarSuiteController.RaiseAction(app =>
        {
            Document? doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;
            using var tx = new Transaction(doc, "Save AIGRAV Rebar Suite Settings");
            TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            bool ok = RebarSettingsStore.SaveToDocument(doc, profile, out string? err);
            tx.Commit();
            Dispatcher.BeginInvoke(new Action(() => ShowStatus(ok ? "Settings tersimpan ke ExtensibleStorage model Revit." : $"Gagal simpan: {err}", !ok)));
        });
    }

    private void OnPickInRevitClick(object sender, RoutedEventArgs e) => RebarSuiteController.RequestPickElementsInRevit();

    private void OnSyncSelectionClick(object sender, RoutedEventArgs e)
    {
        RebarSuiteController.RaiseAction(app =>
        {
            UIDocument? uiDoc = app.ActiveUIDocument;
            Document? doc = uiDoc?.Document;
            if (uiDoc == null || doc == null) return;
            var ids = uiDoc.Selection.GetElementIds()
                .Where(id => HostGeometryExtractor.CanHostRebar(doc.GetElement(id)))
                .Select(id => id.Value)
                .ToList();
            RebarSuiteController.PushSelectionPreviewToStudio(doc, ids);
        });
    }

    private void OnGenerateRebarClick(object sender, RoutedEventArgs e)
    {
        var checkedIds = _selectionQueue.Where(h => h.IsIncluded).Select(h => h.ElementId).ToList();
        if (checkedIds.Count == 0)
        {
            ShowStatus("Centang minimal satu elemen host terpilih sebelum Generate Rebar.", isError: true);
            return;
        }

        var req = BuildCurrentRequest(checkedIds);
        ShowStatus($"Menjalankan transaksi Revit pada {checkedIds.Count} host terpilih...");

        RebarSuiteController.RaiseAction(app =>
        {
            Document? doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;
            var res = RebarGenerationOrchestrator.GenerateSelected(doc, req);
            RebarSuiteController.RefreshBrowserPane(doc);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowStatus(res.Message, isError: !res.Success);
                if (res.FailedItems.Count > 0)
                {
                    WarningsListBox.ItemsSource = res.FailedItems.Concat(res.Warnings).ToList();
                    PreviewTabControl.SelectedIndex = 3;
                }
            }));
        });
    }

    private void OnSmartMarkClick(object sender, RoutedEventArgs e)
    {
        var global = ReadProfileFromUi().Global;
        RebarSuiteController.RaiseAction(app =>
        {
            Document? doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;
            using var tx = new Transaction(doc, "AIGRAV Smart Mark");
            TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            int n = MarkNumberingEngine.AssignMarksInDocument(doc, global);
            tx.Commit();
            RebarSuiteController.RefreshBrowserPane(doc);
            Dispatcher.BeginInvoke(new Action(() => ShowStatus($"Smart Mark selesai: {n} rebar diperbarui.")));
        });
    }

    private void OnAutoTagClick(object sender, RoutedEventArgs e)
    {
        var hostIds = _selectionQueue.Where(h => h.IsIncluded).Select(h => h.ElementId).ToHashSet();
        RebarSuiteController.RaiseAction(app =>
        {
            Document? doc = app.ActiveUIDocument?.Document;
            if (doc == null || doc.ActiveView == null) return;
            var rebarIds = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .Where(r => hostIds.Count == 0 || hostIds.Contains(r.GetHostId().Value))
                .Select(r => r.Id.Value)
                .ToList();
            var warns = new List<string>();
            using var tx = new Transaction(doc, "AIGRAV Auto-Tag Rebar");
            TransactionFailureHandling.SuppressWarnings(tx);
            tx.Start();
            int tagged = RebarVisualAndBbsService.AutoTagRebarsInView(doc, doc.ActiveView, rebarIds, warns);
            tx.Commit();
            Dispatcher.BeginInvoke(new Action(() => ShowStatus(warns.Count > 0 ? warns[0] : $"Auto-Tag selesai: {tagged} tag dibuat.")));
        });
    }

    private void OnCreateBbsClick(object sender, RoutedEventArgs e)
    {
        RebarSuiteController.RaiseAction(app =>
        {
            UIDocument? uiDoc = app.ActiveUIDocument;
            Document? doc = uiDoc?.Document;
            if (uiDoc == null || doc == null) return;
            var warns = new List<string>();
            ElementId? sid;
            using (var tx = new Transaction(doc, "AIGRAV Native BBS Schedule"))
            {
                TransactionFailureHandling.SuppressWarnings(tx);
                tx.Start();
                sid = RebarVisualAndBbsService.CreateOrUpdateNativeBbsSchedule(doc, warnings: warns);
                tx.Commit();
            }
            if (sid != null && doc.GetElement(sid) is ViewSchedule vs)
            {
                uiDoc.ActiveView = vs;
                Dispatcher.BeginInvoke(new Action(() => ShowStatus($"Schedule '{vs.Name}' aktif.")));
            }
        });
    }

    private void OnExportBbsClick(object sender, RoutedEventArgs e)
    {
        var rows = _currentPreview?.BbsRows;
        if (rows == null || rows.Count == 0)
        {
            ShowStatus("Belum ada data BBS preview untuk diekspor.", isError: true);
            return;
        }
        string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AIGRAV_Rebar_Exports");
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string csv = RebarVisualAndBbsService.ExportBbsToCsv(rows, Path.Combine(outDir, $"BBS_Preview_{stamp}.csv"));
        string bvbs = RebarVisualAndBbsService.ExportBvbsAscii(rows, ReadProfileFromUi().Global.SteelGrade, Path.Combine(outDir, $"BBS_Preview_{stamp}.abs"));
        ShowStatus($"Diekspor: {Path.GetFileName(csv)} & {Path.GetFileName(bvbs)} di Documents\\AIGRAV_Rebar_Exports");
    }

    // ---- 3D CAMERA INTERACTION ----

    private void OnCameraPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string preset)
        {
            _viewport3D.ApplyCameraPreset(preset, _currentPreview);
            _viewport3D.UpdateCamera(MainCamera3D);
        }
    }

    private void OnFitCameraClick(object sender, RoutedEventArgs e)
    {
        if (_currentPreview != null) _viewport3D.FitCameraToScene(_currentPreview);
        _viewport3D.UpdateCamera(MainCamera3D);
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        _lastMousePos = e.GetPosition(ViewportHostBorder);
        if (e.LeftButton == MouseButtonState.Pressed) _isDraggingOrbit = true;
        else if (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed) _isDraggingPan = true;
        ViewportHostBorder.CaptureMouse();
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingOrbit && !_isDraggingPan) return;
        System.Windows.Point pos = e.GetPosition(ViewportHostBorder);
        double dx = pos.X - _lastMousePos.X;
        double dy = pos.Y - _lastMousePos.Y;
        _lastMousePos = pos;

        if (_isDraggingOrbit)
        {
            _viewport3D.AzimuthDeg -= dx * 0.55;
            _viewport3D.ElevationDeg = Math.Clamp(_viewport3D.ElevationDeg + dy * 0.45, -88.0, 88.0);
        }
        else if (_isDraggingPan)
        {
            double panScale = _viewport3D.DistanceMm * 0.0014;
            double az = _viewport3D.AzimuthDeg * Math.PI / 180.0;
            Vector3D right = new(Math.Cos(az), Math.Sin(az), 0);
            _viewport3D.PanOffset -= right * (dx * panScale);
            _viewport3D.PanOffset += new Vector3D(0, 0, dy * panScale);
        }

        _viewport3D.UpdateCamera(MainCamera3D);
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingOrbit = false;
        _isDraggingPan = false;
        ViewportHostBorder.ReleaseMouseCapture();
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 0.87 : 1.15;
        _viewport3D.DistanceMm = Math.Clamp(_viewport3D.DistanceMm * factor, 800.0, 90000.0);
        _viewport3D.UpdateCamera(MainCamera3D);
    }

    // ---- PARSING HELPERS ----

    private static double ParseDouble(string? s, double fallback)
        => double.TryParse(s?.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) && v > 0 ? v : fallback;

    private static int ParseInt(string? s, int fallback, int min, int max)
        => int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? Math.Clamp(v, min, max) : fallback;

    private static T ParseEnum<T>(string? tag, T fallback) where T : struct
        => Enum.TryParse<T>(tag, ignoreCase: true, out var res) ? res : fallback;

    private static string? GetSelectedTag(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private static int GetSelectedIntTag(ComboBox cb, int fallback)
        => (cb.SelectedItem as ComboBoxItem)?.Tag is int d ? d : fallback;

    private static void SelectTag(ComboBox cb, string tag)
    {
        foreach (var item in cb.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                cb.SelectedItem = item;
                return;
            }
        }
    }

    private static void SelectIntTag(ComboBox cb, int val)
    {
        foreach (var item in cb.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is int d && d == val)
            {
                cb.SelectedItem = item;
                return;
            }
        }
    }

    private static void SelectContent(ComboBox cb, string content)
    {
        foreach (var item in cb.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase))
            {
                cb.SelectedItem = item;
                return;
            }
        }
    }
}

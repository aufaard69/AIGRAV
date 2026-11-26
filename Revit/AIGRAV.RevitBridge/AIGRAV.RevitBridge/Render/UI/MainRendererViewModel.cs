using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AIGRAV.RevitBridge.Render.Core;
using AIGRAV.RevitBridge.Render.Engine;
using Autodesk.Revit.UI;
using Microsoft.Win32;

namespace Aigrav.RenderStudio.UI.Views;

public class MainRendererViewModel : INotifyPropertyChanged
{
    private readonly UIApplication _app;
    private readonly MainRendererWindow _window;
    private RenderJobContext? _currentContext;

    public ObservableCollection<MaterialMapDisplayItem> DetectedMaterials { get; set; } = new();
    public ObservableCollection<RevitViewDisplayItem> AvailableViews { get; set; } = new();

    private RevitViewDisplayItem? _selectedTargetView;
    public RevitViewDisplayItem? SelectedTargetView
    {
        get => _selectedTargetView;
        set
        {
            if (SetField(ref _selectedTargetView, value) && value != null)
            {
                ActiveViewName = value.DisplayName;
            }
        }
    }

    private string _activeViewName = "3D View";
    public string ActiveViewName
    {
        get => _activeViewName;
        set => SetField(ref _activeViewName, value);
    }

    private string _bridgeStatusText = "AIGRAV MCP Bridge: Connected (Port 8765)";
    public string BridgeStatusText
    {
        get => _bridgeStatusText;
        set => SetField(ref _bridgeStatusText, value);
    }

    private bool _isBridgeModeSelected = true;
    public bool IsBridgeModeSelected
    {
        get => _isBridgeModeSelected;
        set
        {
            if (SetField(ref _isBridgeModeSelected, value) && value)
            {
                IsDirectApiModeSelected = false;
            }
        }
    }

    private bool _isDirectApiModeSelected = false;
    public bool IsDirectApiModeSelected
    {
        get => _isDirectApiModeSelected;
        set
        {
            if (SetField(ref _isDirectApiModeSelected, value) && value)
            {
                IsBridgeModeSelected = false;
            }
        }
    }

    private ImageSource? _styleReferenceImageSource;
    public ImageSource? StyleReferenceImageSource
    {
        get => _styleReferenceImageSource;
        set => SetField(ref _styleReferenceImageSource, value);
    }

    private string? _styleReferencePath;
    public string? StyleReferencePath
    {
        get => _styleReferencePath;
        set => SetField(ref _styleReferencePath, value);
    }

    private string _globalSceneNotes = "Photorealistic architectural photography, 24mm tilt-corrected lens, realistic structural proportions, crisp PBR material fidelity, zero structural hallucination.";
    public string GlobalSceneNotes
    {
        get => _globalSceneNotes;
        set => SetField(ref _globalSceneNotes, value);
    }

    private ImageSource? _beforePreviewImage;
    public ImageSource? BeforePreviewImage
    {
        get => _beforePreviewImage;
        set => SetField(ref _beforePreviewImage, value);
    }

    private ImageSource? _afterRenderImage;
    public ImageSource? AfterRenderImage
    {
        get => _afterRenderImage;
        set => SetField(ref _afterRenderImage, value);
    }

    private ImageSource? _geometryPassThumbnail;
    public ImageSource? GeometryPassThumbnail
    {
        get => _geometryPassThumbnail;
        set => SetField(ref _geometryPassThumbnail, value);
    }

    private ImageSource? _lightingPassThumbnail;
    public ImageSource? LightingPassThumbnail
    {
        get => _lightingPassThumbnail;
        set => SetField(ref _lightingPassThumbnail, value);
    }

    private ImageSource? _materialIdPassThumbnail;
    public ImageSource? MaterialIdPassThumbnail
    {
        get => _materialIdPassThumbnail;
        set => SetField(ref _materialIdPassThumbnail, value);
    }

    private string _materialJsonPath = ".aigrav/render_jobs/latest/material_map.json";
    public string MaterialJsonPath
    {
        get => _materialJsonPath;
        set => SetField(ref _materialJsonPath, value);
    }

    private double _progressPercentage = 100;
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetField(ref _progressPercentage, value);
    }

    private string _statusMessage = "Ready — Model Revit aman (Transaction.RollBack terverifikasi).";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private bool _isBusy = false;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public RelayCommand BrowseStyleImageCommand { get; }
    public RelayCommand RefreshViewsCommand { get; }
    public RelayCommand ClearStyleImageCommand { get; }
    public RelayCommand AutoEnrichMaterialPromptsCommand { get; }
    public RelayCommand CaptureThreePassCommand { get; }
    public RelayCommand ExecuteAiRenderCommand { get; }
    public RelayCommand SaveOutputImageCommand { get; }

    public MainRendererViewModel(UIApplication app, MainRendererWindow window)
    {
        _app = app;
        _window = window;

        RefreshViewsCommand = new RelayCommand(RefreshViews);
        BrowseStyleImageCommand = new RelayCommand(BrowseStyleImage);
        ClearStyleImageCommand = new RelayCommand(ClearStyleImage);
        AutoEnrichMaterialPromptsCommand = new RelayCommand(AutoEnrichMaterialPrompts);
        CaptureThreePassCommand = new RelayCommand(async () => await CaptureThreePassAsync(), () => !IsBusy);
        ExecuteAiRenderCommand = new RelayCommand(async () => await ExecuteAiRenderAsync(), () => !IsBusy);
        SaveOutputImageCommand = new RelayCommand(SaveOutputImage, () => AfterRenderImage != null);

        InitializeState();
    }

    private void InitializeState()
    {
        RefreshViews();

        BridgeStatusText = AIGRAV.RevitBridge.AIGRAVHttpServer.IsRunning
            ? "AIGRAV MCP Bridge: Connected (Port 8765)"
            : "AIGRAV MCP Bridge: Offline (Start via Ribbon)";

        // Preload saved API key and intelligent default selection
        string? openaiKey = AigravKeyStorage.GetKey("openai");
        string? geminiKey = AigravKeyStorage.GetKey("gemini");
        string? grokKey = AigravKeyStorage.GetKey("grok");

        if (!string.IsNullOrEmpty(openaiKey) && string.IsNullOrEmpty(geminiKey))
        {
            if (_window.CmbAiModel != null)
            {
                _window.CmbAiModel.SelectedIndex = 2; // OpenAI gpt-image-2.6-sunburst
            }
            if (_window.PwdApiKey != null)
            {
                _window.PwdApiKey.Password = openaiKey;
            }
            IsDirectApiModeSelected = true;
            IsBridgeModeSelected = false;
        }
        else
        {
            string? savedKey = geminiKey ?? openaiKey ?? grokKey;
            if (!string.IsNullOrEmpty(savedKey) && _window.PwdApiKey != null)
            {
                _window.PwdApiKey.Password = savedKey;
            }
        }

        // Add default sample materials to start with clean UI preview
        if (DetectedMaterials.Count == 0)
        {
            LoadSampleMaterials();
        }
    }

    public void RefreshViews()
    {
        try
        {
            var uidoc = _app.ActiveUIDocument;
            if (uidoc == null) return;
            var doc = uidoc.Document;
            if (doc == null) return;

            Autodesk.Revit.DB.View? currentActive = uidoc.ActiveGraphicalView ?? uidoc.ActiveView ?? doc.ActiveView;
            long activeId = currentActive?.Id.Value ?? -1;

            var openViewIds = new System.Collections.Generic.HashSet<long>();
            try
            {
                foreach (var uv in uidoc.GetOpenUIViews())
                {
                    openViewIds.Add(uv.ViewId.Value);
                }
            }
            catch { }

            var allViews = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.View))
                .Cast<Autodesk.Revit.DB.View>()
                .Where(v => !v.IsTemplate)
                .Where(v => v.ViewType == Autodesk.Revit.DB.ViewType.ThreeD ||
                            v.ViewType == Autodesk.Revit.DB.ViewType.FloorPlan ||
                            v.ViewType == Autodesk.Revit.DB.ViewType.CeilingPlan ||
                            v.ViewType == Autodesk.Revit.DB.ViewType.Elevation ||
                            v.ViewType == Autodesk.Revit.DB.ViewType.Section ||
                            v.ViewType == Autodesk.Revit.DB.ViewType.Walkthrough ||
                            v.ViewType == Autodesk.Revit.DB.ViewType.Rendering ||
                            openViewIds.Contains(v.Id.Value))
                .OrderByDescending(v => v.Id.Value == activeId)
                .ThenByDescending(v => openViewIds.Contains(v.Id.Value))
                .ThenByDescending(v => v.ViewType == Autodesk.Revit.DB.ViewType.ThreeD)
                .ThenBy(v => v.Name)
                .ToList();

            AvailableViews.Clear();
            RevitViewDisplayItem? toSelect = null;

            foreach (var v in allViews)
            {
                bool isActive = v.Id.Value == activeId;
                var item = new RevitViewDisplayItem
                {
                    ViewId = v.Id.Value,
                    Name = v.Name,
                    ViewType = v.ViewType.ToString(),
                    IsActiveInUI = isActive
                };

                AvailableViews.Add(item);
                if (isActive && toSelect == null)
                {
                    toSelect = item;
                }
            }

            if (toSelect != null)
            {
                SelectedTargetView = toSelect;
            }
            else if (AvailableViews.Count > 0)
            {
                SelectedTargetView = AvailableViews[0];
            }
        }
        catch { }
    }

    private void LoadSampleMaterials()
    {
        DetectedMaterials.Clear();
        DetectedMaterials.Add(new MaterialMapDisplayItem
        {
            HexColor = "#E6194B",
            RevitName = "Concrete - Cast-in-Place Gray",
            MaterialClass = "Concrete",
            WasTransparent = false,
            AiPromptDescriptor = "weathered architectural board-formed concrete wall, subtle tie holes, matte finish"
        });
        DetectedMaterials.Add(new MaterialMapDisplayItem
        {
            HexColor = "#008080",
            RevitName = "Glass - Clear Low-E Glazing",
            MaterialClass = "Glass",
            WasTransparent = true,
            AiPromptDescriptor = "clear architectural double-glazed curtain wall glass, realistic sky reflection and interior depth"
        });
        DetectedMaterials.Add(new MaterialMapDisplayItem
        {
            HexColor = "#FFE119",
            RevitName = "Steel - ASTM A992 Structural",
            MaterialClass = "Metal",
            WasTransparent = false,
            AiPromptDescriptor = "dark charcoal powder-coated structural H-beam steel frame, industrial warehouse finish"
        });
        DetectedMaterials.Add(new MaterialMapDisplayItem
        {
            HexColor = "#911EB4",
            RevitName = "Metal Panel - Insulated Cladding",
            MaterialClass = "Metal",
            WasTransparent = false,
            AiPromptDescriptor = "corrugated silver-gray aluminum factory wall cladding panels, crisp vertical seams"
        });
    }

    private void BrowseStyleImage()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Pilih Gambar Referensi Gaya & Pencahayaan (Style Reference)",
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            StyleReferencePath = dlg.FileName;
            StyleReferenceImageSource = LoadBitmapUnlocked(dlg.FileName);
        }
    }

    private void ClearStyleImage()
    {
        StyleReferencePath = null;
        StyleReferenceImageSource = null;
    }

    private void AutoEnrichMaterialPrompts()
    {
        foreach (var item in DetectedMaterials)
        {
            string lower = item.RevitName.ToLowerInvariant();
            if (lower.Contains("concrete") || lower.Contains("beton"))
                item.AiPromptDescriptor = "architectural cast-in-place concrete with subtle formwork seams, natural aggregate texture, and matte surface";
            else if (lower.Contains("glass") || lower.Contains("kaca"))
                item.AiPromptDescriptor = "clear architectural low-iron double-glazed curtain wall glass with crisp environmental reflections";
            else if (lower.Contains("steel") || lower.Contains("baja") || lower.Contains("metal"))
                item.AiPromptDescriptor = "structural steel profile with matte dark charcoal powder-coating and clean micro-beveled edges";
            else if (lower.Contains("wood") || lower.Contains("kayu"))
                item.AiPromptDescriptor = "natural architectural grade timber cladding with vertical wood grain and satin protective oil finish";
            else if (lower.Contains("brick") || lower.Contains("bata"))
                item.AiPromptDescriptor = "textured red-brown clay facing brick masonry with sharp mortar joints and subtle tonal variation";
            else if (lower.Contains("grass") || lower.Contains("rumput"))
                item.AiPromptDescriptor = "lush manicured exterior landscape lawn with natural blade density and subtle morning dew";
            else if (lower.Contains("water") || lower.Contains("air"))
                item.AiPromptDescriptor = "calm architectural reflection pool water with gentle surface ripples and realistic sky reflections";
            else
                item.AiPromptDescriptor = $"photorealistic PBR material texture representing {item.RevitName}";
        }

        StatusMessage = "PBR Prompt descriptors updated successfully.";
    }

    public async Task<bool> CaptureThreePassAsync()
    {
        if (IsBusy) return false;

        // Auto-refresh views to lock currently active tab in Revit UI
        RefreshViews();

        IsBusy = true;
        ProgressPercentage = 15;
        StatusMessage = "Menginisialisasi capture 3-Pass 4K view aktif di Revit...";

        try
        {
            if (AIGRAV.RevitBridge.AIGRAVApp.RenderExportHandler == null ||
                AIGRAV.RevitBridge.AIGRAVApp.RenderExternalEvent == null)
            {
                StatusMessage = "Error: Render ExternalEvent belum terdaftar. Pastikan plugin AIGRAV aktif.";
                IsBusy = false;
                return false;
            }

            ProgressPercentage = 30;
            StatusMessage = "Mengekstrak 3-Pass (Material Asli, Filter Color Material, Shaded Color) via Transaction.RollBack()...";

            long? targetId = SelectedTargetView?.ViewId;
            var context = await AIGRAV.RevitBridge.AIGRAVApp.RenderExportHandler.EnqueueAsync(
                app => ThreePassViewExporter.ExportPasses(
                    app.ActiveUIDocument,
                    targetId.HasValue ? new Autodesk.Revit.DB.ElementId(targetId.Value) : null),
                AIGRAV.RevitBridge.AIGRAVApp.RenderExternalEvent);

            _currentContext = context;

            ProgressPercentage = 75;
            StatusMessage = "Memuat 3-pass 4K dan memetakan palet material PBR...";

            // Pass 1: View saat ini dengan material asli
            if (File.Exists(context.LightingPassPath))
            {
                LightingPassThumbnail = LoadBitmapUnlocked(context.LightingPassPath);
                BeforePreviewImage = LightingPassThumbnail;
            }

            // Pass 2: View dengan filter color material mask
            if (File.Exists(context.MaterialIdCleanPath))
                MaterialIdPassThumbnail = LoadBitmapUnlocked(context.MaterialIdCleanPath);

            // Pass 3: View dengan shaded color & shadows
            if (File.Exists(context.GeometryPassPath))
                GeometryPassThumbnail = LoadBitmapUnlocked(context.GeometryPassPath);

            // If output.png already exists (e.g. from previous or external render), load into After panel
            string existingOutput = Path.Combine(context.OutputDirectory, "output.png");
            if (File.Exists(existingOutput))
            {
                AfterRenderImage = LoadBitmapUnlocked(existingOutput);
            }

            // Populate DetectedMaterials
            DetectedMaterials.Clear();
            foreach (var mat in context.MaterialMap.Materials)
            {
                DetectedMaterials.Add(new MaterialMapDisplayItem
                {
                    HexColor = mat.HexColor,
                    RevitName = mat.Name,
                    MaterialClass = mat.Category,
                    WasTransparent = false,
                    AiPromptDescriptor = mat.AiDescription
                });
            }

            MaterialJsonPath = context.MaterialMapJsonPath;
            ProgressPercentage = 100;
            StatusMessage = $"Capture 3-Pass 4K Selesai! {context.MaterialMap.Materials.Count} material teridentifikasi. Model Revit 100% aman.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gagal mengekstrak 3-pass Revit: {ex.Message}";
            ProgressPercentage = 0;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExecuteAiRenderAsync()
    {
        if (IsBusy) return;

        // Auto-capture if passes haven't been captured yet
        if (_currentContext == null || !File.Exists(_currentContext.GeometryPassPath))
        {
            bool captured = await CaptureThreePassAsync();
            if (!captured || _currentContext == null) return;
        }

        IsBusy = true;
        ProgressPercentage = 10;

        try
        {
            // Sync user changes from DataGrid back to context materials
            foreach (var dispItem in DetectedMaterials)
            {
                var targetMat = _currentContext.MaterialMap.Materials
                    .FirstOrDefault(m => m.HexColor.Equals(dispItem.HexColor, StringComparison.OrdinalIgnoreCase));
                if (targetMat != null)
                {
                    targetMat.AiDescription = dispItem.AiPromptDescriptor;
                }
            }

            _currentContext.ExtraPrompt = GlobalSceneNotes;
            _currentContext.StyleReferencePath = StyleReferencePath;

            // Determine routing mode and model
            if (IsBridgeModeSelected)
            {
                _currentContext.EngineMode = RenderEngineMode.AntigravityBridge;
                StatusMessage = "Mengirim job render ke Antigravity Workspace Bridge (.aigrav/render_jobs)...";

                var progress = new Progress<string>(msg =>
                {
                    StatusMessage = msg;
                    ProgressPercentage = Math.Min(95, ProgressPercentage + 15);
                });

                var bridgeClient = new AntigravityBridgeRenderClient();
                var result = await bridgeClient.DispatchAsync(_currentContext, progress);

                HandleRenderResult(result);
            }
            else
            {
                _currentContext.EngineMode = RenderEngineMode.DirectApiKey;

                // Read selected model and API key from UI
                string selectedModel = "gemini-3-pro-image";
                if (_window.CmbAiModel?.SelectedItem is System.Windows.Controls.ComboBoxItem cbi && cbi.Tag is string tag)
                {
                    selectedModel = tag;
                }
                _currentContext.ModelName = selectedModel;

                string apiKey = _window.PwdApiKey?.Password?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _currentContext.ApiKey = apiKey;
                    // Persist encrypted with DPAPI
                    string providerKey = selectedModel.Contains("gemini") ? "gemini" :
                                         selectedModel.Contains("gpt") ? "openai" : "grok";
                    AigravKeyStorage.SetKey(providerKey, apiKey);
                }

                StatusMessage = $"Menghubungkan ke Direct API Router ({selectedModel})...";

                var progress = new Progress<string>(msg =>
                {
                    StatusMessage = msg;
                    ProgressPercentage = Math.Min(95, ProgressPercentage + 20);
                });

                var router = new DirectApiRenderRouter();
                var result = await router.DispatchAsync(_currentContext, progress);

                HandleRenderResult(result);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Eksekusi render gagal: {ex.Message}";
            ProgressPercentage = 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void HandleRenderResult(RenderResult result)
    {
        if (result.Success && !string.IsNullOrEmpty(result.OutputImagePath) && File.Exists(result.OutputImagePath))
        {
            AfterRenderImage = LoadBitmapUnlocked(result.OutputImagePath);
            ProgressPercentage = 100;
            StatusMessage = $"Render 4K selesai dalam {result.Duration.TotalSeconds:F1} detik! Gambar berhasil dimuat.";

            // Set slider to 50% split comparison
            if (_window.SplitCompareSlider != null)
            {
                _window.SplitCompareSlider.Value = 50;
            }
        }
        else
        {
            ProgressPercentage = 0;
            StatusMessage = $"Gagal me-render: {result.ErrorMessage ?? "Hasil gambar tidak ditemukan."}";
        }
    }

    private void SaveOutputImage()
    {
        if (_currentContext == null) return;
        string expectedOutput = Path.Combine(_currentContext.OutputDirectory, "output.png");
        if (!File.Exists(expectedOutput))
        {
            string jpgFallback = Path.Combine(_currentContext.OutputDirectory, "output.jpg");
            if (File.Exists(jpgFallback)) expectedOutput = jpgFallback;
            else
            {
                StatusMessage = "Belum ada hasil render 4K yang dapat disimpan.";
                return;
            }
        }

        var dlg = new SaveFileDialog
        {
            Title = "Simpan Hasil Render 4K AIGRAV",
            FileName = $"AIGRAV_Render_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                File.Copy(expectedOutput, dlg.FileName, overwrite: true);
                StatusMessage = $"Gambar 4K berhasil disimpan ke: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal menyimpan gambar: {ex.Message}";
            }
        }
    }

    private static BitmapImage LoadBitmapUnlocked(string filePath)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bmp.UriSource = new Uri(filePath, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class RevitViewDisplayItem
{
    public long ViewId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ViewType { get; set; } = string.Empty;
    public bool IsActiveInUI { get; set; }

    public string DisplayName => IsActiveInUI
        ? $"📌 [Active] {ViewType} — {Name}"
        : $"{ViewType} — {Name}";
}

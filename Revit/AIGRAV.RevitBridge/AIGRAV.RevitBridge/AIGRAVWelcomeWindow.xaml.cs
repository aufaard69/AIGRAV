using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public partial class AIGRAVWelcomeWindow : Window
{
    private VersionInfo? _cachedRemoteInfo;
    private static readonly Brush GreenBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#32D583"));
    private static readonly Brush GreenBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#132B20"));
    private static readonly Brush GreenBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B4D36"));
    private static readonly Brush GreenTextBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48F09B"));

    private static readonly Brush RedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F04438"));
    private static readonly Brush RedBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B1616"));
    private static readonly Brush RedBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C2020"));
    private static readonly Brush RedTextBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97066"));

    public AIGRAVWelcomeWindow()
    {
        InitializeComponent();
        Loaded += AIGRAVWelcomeWindow_Loaded;
        Unloaded += AIGRAVWelcomeWindow_Unloaded;
    }

    private async void AIGRAVWelcomeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            PortInput.Text = AIGRAVHttpServer.CurrentPort.ToString();

            // Subscribe to live state updates
            AIGRAVHttpServer.StateChanged += OnServerStateChanged;

            // Update UI state based on active bridge status
            RefreshUiState();

            // Version metadata
            var local = AIGRAVVersionChecker.GetLocalVersion();
            FooterVersionText.Text = $"AIGRAV v{local.version}  •  Gemini + MCP + Revit API • Aufa Ardianto";

            // Check for updates in background
            var (hasUpdate, remote) = await AIGRAVVersionChecker.CheckForUpdatesAsync();
            if (hasUpdate && remote != null)
            {
                _cachedRemoteInfo = remote;
                Dispatcher.Invoke(() =>
                {
                    UpdateTitleText.Text = $"✨ Pembaruan AIGRAV v{remote.version} Tersedia!";
                    UpdateSubText.Text = $"{remote.release_name} ({remote.release_date})";
                    UpdateBanner.Visibility = Visibility.Visible;
                });
            }
        }
        catch
        {
            // Background check failure must never affect UI
        }
    }

    private void AIGRAVWelcomeWindow_Unloaded(object sender, RoutedEventArgs e)
    {
        AIGRAVHttpServer.StateChanged -= OnServerStateChanged;
    }

    private void OnServerStateChanged()
    {
        Dispatcher.Invoke(RefreshUiState);
    }

    private void RefreshUiState()
    {
        bool isRunning = AIGRAVHttpServer.IsRunning;
        int pid = 0;
        try { pid = Process.GetCurrentProcess().Id; } catch { }

        if (isRunning)
        {
            StatusLabel.Text = "Connected";
            StatusLabel.Foreground = GreenBrush;
            StatusDot.Fill = GreenBrush;
            StatusBadgeBorder.Background = GreenBgBrush;
            StatusBadgeBorder.BorderBrush = GreenBorderBrush;

            FeedbackBorder.Background = GreenBgBrush;
            FeedbackBorder.BorderBrush = GreenBorderBrush;

            StatusDetailText.Text = $"🟢 Bridge Connected di http://127.0.0.1:{AIGRAVHttpServer.CurrentPort}/  •  Siap melayani instruksi AI Revit.";
            StatusDetailText.Foreground = GreenTextBrush;

            ToggleBridgeButton.Content = "Restart Bridge";
            ToggleBridgeButton.Foreground = (Brush)new BrushConverter().ConvertFromString("#54A8FF")!;
        }
        else
        {
            StatusLabel.Text = "Unconnected";
            StatusLabel.Foreground = RedBrush;
            StatusDot.Fill = RedBrush;
            StatusBadgeBorder.Background = RedBgBrush;
            StatusBadgeBorder.BorderBrush = RedBorderBrush;

            FeedbackBorder.Background = RedBgBrush;
            FeedbackBorder.BorderBrush = RedBorderBrush;

            string reason = AIGRAVHttpServer.LastError ?? $"Port {AIGRAVHttpServer.CurrentPort} tidak terhubung.";
            int nextFree = AIGRAVHttpServer.FindNextAvailablePort(AIGRAVHttpServer.CurrentPort + 1);

            StatusDetailText.Text = $"🔴 Bridge Unconnected: {reason}\n💡 Masukkan port baru (misal: {nextFree}) lalu klik 'Test Connection' untuk menghubungkan.";
            StatusDetailText.Foreground = RedTextBrush;

            ToggleBridgeButton.Content = "Start Bridge";
            ToggleBridgeButton.Foreground = GreenBrush;
        }

        MetricsText.Text = $"URL: http://127.0.0.1:{AIGRAVHttpServer.CurrentPort}/  •  Revit PID: {pid}  •  Requests: {AIGRAVHttpServer.TotalRequestsServed} served";
    }

    private void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        string raw = PortInput.Text.Trim();
        if (!int.TryParse(raw, out int port) || port < 1024 || port > 65535)
        {
            StatusDetailText.Text = "⚠️ Masukkan nomor port yang valid antara 1024 dan 65535.";
            StatusDetailText.Foreground = (Brush)new BrushConverter().ConvertFromString("#FDB022")!;
            return;
        }

        // Test availability and start listening
        bool started = AIGRAVHttpServer.Start(port, out string? errorMessage);
        if (started)
        {
            RefreshUiState();
            StatusDetailText.Text = $"🟢 Test Connection SUKSES! Bridge sekarang terhubung di port {port}.";
            StatusDetailText.Foreground = GreenTextBrush;
        }
        else
        {
            RefreshUiState();
            StatusDetailText.Text = $"🔴 Test Connection GAGAL: {errorMessage}";
            StatusDetailText.Foreground = RedTextBrush;
        }
    }

    private void AutoPortButton_Click(object sender, RoutedEventArgs e)
    {
        int current = AIGRAVHttpServer.CurrentPort;
        if (int.TryParse(PortInput.Text.Trim(), out int parsed) && parsed > 0)
        {
            current = parsed;
        }

        int nextFree = AIGRAVHttpServer.FindNextAvailablePort(current + 1);
        PortInput.Text = nextFree.ToString();

        // Automatically connect to the newly found free port
        TestConnectionButton_Click(sender, e);
    }

    private void ToggleBridgeButton_Click(object sender, RoutedEventArgs e)
    {
        if (AIGRAVHttpServer.IsRunning)
        {
            // Restart
            TestConnectionButton_Click(sender, e);
        }
        else
        {
            // Start
            TestConnectionButton_Click(sender, e);
        }
    }

    private void ViewUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cachedRemoteInfo == null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Versi Terbaru: v{_cachedRemoteInfo.version}");
        sb.AppendLine($"Rilis: {_cachedRemoteInfo.release_name} ({_cachedRemoteInfo.release_date})");
        sb.AppendLine();

        if (_cachedRemoteInfo.highlights != null && _cachedRemoteInfo.highlights.Count > 0)
        {
            sb.AppendLine("✨ Fitur Baru & Peningkatan:");
            foreach (var h in _cachedRemoteInfo.highlights)
            {
                sb.AppendLine($" • {h}");
            }
            sb.AppendLine();
        }

        if (_cachedRemoteInfo.fixes != null && _cachedRemoteInfo.fixes.Count > 0)
        {
            sb.AppendLine("🛠️ Perbaikan Bug:");
            foreach (var f in _cachedRemoteInfo.fixes)
            {
                sb.AppendLine($" • {f}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Apakah Anda ingin memperbarui AIGRAV sekarang?");

        TaskDialog dialog = new("Pembaruan AIGRAV")
        {
            MainInstruction = $"Pembaruan AIGRAV v{_cachedRemoteInfo.version} Tersedia",
            MainContent = sb.ToString(),
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
            DefaultButton = TaskDialogResult.Yes
        };

        TaskDialogResult result = dialog.Show();
        if (result == TaskDialogResult.Yes)
        {
            Hide();
            bool launched = AIGRAVVersionChecker.TriggerUpdateScript();
            if (launched)
            {
                TaskDialog.Show("AIGRAV Updater", "Skrip sinkronisasi sedang berjalan di latar belakang.\nRevit akan otomatis memuat pembaruan setelah selesai.");
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}

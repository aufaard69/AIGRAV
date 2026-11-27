using System;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public partial class AIGRAVWelcomeWindow : Window
{
    private VersionInfo? _cachedRemoteInfo;

    private static readonly Brush GreenBg = new SolidColorBrush(Color.FromRgb(0x0E, 0x29, 0x1D));
    private static readonly Brush GreenBorder = new SolidColorBrush(Color.FromRgb(0x1F, 0x5D, 0x3E));
    private static readonly Brush GreenDot = new SolidColorBrush(Color.FromRgb(0x32, 0xD5, 0x83));
    private static readonly Brush GreenText = new SolidColorBrush(Color.FromRgb(0x48, 0xF0, 0x9B));
    private static readonly Brush GreenBannerText = new SolidColorBrush(Color.FromRgb(0x6C, 0xE9, 0xA6));

    private static readonly Brush RedBg = new SolidColorBrush(Color.FromRgb(0x2D, 0x14, 0x18));
    private static readonly Brush RedBorder = new SolidColorBrush(Color.FromRgb(0x7A, 0x27, 0x32));
    private static readonly Brush RedDot = new SolidColorBrush(Color.FromRgb(0xF9, 0x70, 0x66));
    private static readonly Brush RedText = new SolidColorBrush(Color.FromRgb(0xFD, 0xA2, 0x9B));

    public AIGRAVWelcomeWindow()
    {
        InitializeComponent();
        Loaded += AIGRAVWelcomeWindow_Loaded;
        Closed += AIGRAVWelcomeWindow_Closed;
        Activated += (_, _) => RefreshBridgeState();
        AIGRAVHttpServer.StateChanged += OnServerStateChanged;
    }

    private void OnServerStateChanged()
    {
        try
        {
            if (!Dispatcher.HasShutdownStarted && IsLoaded)
            {
                Dispatcher.BeginInvoke(() => RefreshBridgeState());
            }
        }
        catch
        {
            // Ignore shutdown dispatch races
        }
    }

    private void AIGRAVWelcomeWindow_Closed(object? sender, EventArgs e)
    {
        AIGRAVHttpServer.StateChanged -= OnServerStateChanged;
    }

    private async void AIGRAVWelcomeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshBridgeState();

        try
        {
            var local = AIGRAVVersionChecker.GetLocalVersion();
            FooterVersionText.Text = $"AIGRAV v{local.version}  •  Gemini + MCP + Revit API • Aufa Ardianto";

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

    public void RefreshBridgeState(string? overrideStatusMessage = null, bool? overrideSuccess = null)
    {
        bool running = AIGRAVHttpServer.IsRunning;
        int port = AIGRAVHttpServer.ActivePort;
        string url = port > 0 ? AIGRAVHttpServer.ActiveUrl : "-";
        long requests = AIGRAVHttpServer.RequestsServed;
        int pid = Environment.ProcessId;

        if (!PortTextBox.IsFocused && port > 0)
        {
            PortTextBox.Text = port.ToString();
        }

        bool isOk = overrideSuccess ?? running;
        SetConnectionBadge(running);

        string statusMsg = overrideStatusMessage
            ?? (running
                ? $"Bridge Connected di {url} • Siap melayani instruksi AI Revit."
                : "HTTP Server Bridge tidak aktif. Klik Restart Bridge atau Cari Port Bebas (+1).");

        SetStatusBanner(isOk, statusMsg);
        TelemetryText.Text = $"URL: {url} • Revit PID: {pid} • Requests: {requests} served";
    }

    private void SetConnectionBadge(bool connected)
    {
        ConnectionBadgeBorder.Background = connected ? GreenBg : RedBg;
        ConnectionBadgeBorder.BorderBrush = connected ? GreenBorder : RedBorder;
        ConnectionBadgeDot.Fill = connected ? GreenDot : RedDot;
        ConnectionBadgeText.Text = connected ? "Connected" : "Disconnected";
        ConnectionBadgeText.Foreground = connected ? GreenText : RedText;
    }

    private void SetStatusBanner(bool ok, string message)
    {
        BridgeStatusBanner.Background = ok ? GreenBg : RedBg;
        BridgeStatusBanner.BorderBrush = ok ? GreenBorder : RedBorder;
        StatusBannerDot.Fill = ok ? GreenDot : RedDot;
        StatusBannerText.Foreground = ok ? GreenBannerText : RedText;
        StatusBannerText.Text = message;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        TestConnectionButton.IsEnabled = false;
        try
        {
            int? targetPort = int.TryParse(PortTextBox.Text.Trim(), out int parsed) ? parsed : null;
            if (targetPort.HasValue && targetPort.Value != AIGRAVHttpServer.ActivePort)
            {
                var (switched, _, switchMsg) = AIGRAVHttpServer.Restart(targetPort.Value);
                if (!switched)
                {
                    RefreshBridgeState(switchMsg, false);
                    AIGRAVApp.RefreshRibbonStatus();
                    return;
                }
            }

            var (ok, msg) = await AIGRAVHttpServer.TestSelfConnectionAsync();
            RefreshBridgeState(msg, ok);
            AIGRAVApp.RefreshRibbonStatus();
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
        }
    }

    private async void FindFreePortButton_Click(object sender, RoutedEventArgs e)
    {
        FindFreePortButton.IsEnabled = false;
        try
        {
            var (ok, newPort, msg) = AIGRAVHttpServer.SwitchToNextFreePort();
            if (ok)
            {
                PortTextBox.Text = newPort.ToString();
                var (testOk, testMsg) = await AIGRAVHttpServer.TestSelfConnectionAsync(newPort);
                RefreshBridgeState(testOk ? testMsg : msg, testOk);
            }
            else
            {
                RefreshBridgeState(msg, false);
            }
            AIGRAVApp.RefreshRibbonStatus();
        }
        finally
        {
            FindFreePortButton.IsEnabled = true;
        }
    }

    private async void RestartBridgeButton_Click(object sender, RoutedEventArgs e)
    {
        RestartBridgeButton.IsEnabled = false;
        try
        {
            int? targetPort = int.TryParse(PortTextBox.Text.Trim(), out int parsed) ? parsed : null;
            var (ok, newPort, msg) = AIGRAVHttpServer.Restart(targetPort);
            if (ok)
            {
                PortTextBox.Text = newPort.ToString();
                var (testOk, testMsg) = await AIGRAVHttpServer.TestSelfConnectionAsync(newPort);
                RefreshBridgeState(testOk ? testMsg : msg, testOk);
            }
            else
            {
                RefreshBridgeState(msg, false);
            }
            AIGRAVApp.RefreshRibbonStatus();
        }
        finally
        {
            RestartBridgeButton.IsEnabled = true;
        }
    }

    private void PortTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RestartBridgeButton_Click(sender, e);
            e.Handled = true;
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
                TaskDialog.Show(
                    "AIGRAV Updater",
                    "Jendela updater PowerShell telah dibuka untuk mengunduh pembaruan dari GitHub.\n\n" +
                    "PENTING: Tutup Autodesk Revit setelah ini agar skrip updater dapat mengkompilasi ulang AIGRAV.RevitBridge.dll (UI & Bridge), lalu buka kembali Revit.");
            }
            else
            {
                TaskDialog.Show(
                    "AIGRAV Updater",
                    "Tidak dapat menjalankan sync-aigrav.ps1 secara otomatis. Tutup Revit lalu jalankan sync-aigrav.ps1 secara manual di folder AIGRAV.");
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

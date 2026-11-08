using System;
using System.Text;
using System.Windows;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public partial class AIGRAVWelcomeWindow : Window
{
    private VersionInfo? _cachedRemoteInfo;

    public AIGRAVWelcomeWindow()
    {
        InitializeComponent();
        Loaded += AIGRAVWelcomeWindow_Loaded;
    }

    private async void AIGRAVWelcomeWindow_Loaded(object sender, RoutedEventArgs e)
    {
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

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIGRAV.RevitBridge.Render.Engine;
using Autodesk.Revit.UI;

namespace Aigrav.RenderStudio.UI.Views
{
    public partial class MainRendererWindow : Window
    {
        public MainRendererViewModel ViewModel { get; }

        public MainRendererWindow(UIApplication app)
        {
            InitializeComponent();
            ViewModel = new MainRendererViewModel(app, this);
            DataContext = ViewModel;

            if (CmbAiModel != null)
            {
                CmbAiModel.SelectionChanged += CmbAiModel_SelectionChanged;
            }

            StateChanged += (_, _) => UpdateMaximizeIcons();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                }
            };

            Loaded += (_, _) =>
            {
                UpdateBeforeAfterSplitClip();
                UpdateMaximizeIcons();
                ExecutionMode_Changed(this, new RoutedEventArgs());
            };
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, e);
            }
            else if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
            UpdateMaximizeIcons();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateMaximizeIcons()
        {
            if (IconSquareNormal == null || IconSquareMaximized == null) return;
            if (WindowState == WindowState.Maximized)
            {
                IconSquareNormal.Visibility = Visibility.Collapsed;
                IconSquareMaximized.Visibility = Visibility.Visible;
            }
            else
            {
                IconSquareNormal.Visibility = Visibility.Visible;
                IconSquareMaximized.Visibility = Visibility.Collapsed;
            }
        }

        private void CmbAiModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbAiModel?.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag && PwdApiKey != null)
            {
                string provider = tag.Contains("gemini") ? "gemini" :
                                  tag.Contains("gpt") ? "openai" : "grok";
                string? key = AigravKeyStorage.GetKey(provider);
                if (!string.IsNullOrEmpty(key))
                {
                    PwdApiKey.Password = key;
                }
            }
        }

        private void SplitCompareSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateBeforeAfterSplitClip();
        }

        private void ViewportCompareContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBeforeAfterSplitClip();
        }

        private void UpdateBeforeAfterSplitClip()
        {
            if (ViewportCompareContainer == null || BeforeClipGeometry == null || SplitDividerLine == null)
                return;

            double totalWidth = ViewportCompareContainer.ActualWidth;
            double totalHeight = ViewportCompareContainer.ActualHeight;
            if (totalWidth <= 0 || totalHeight <= 0) return;

            double splitRatio = SplitCompareSlider.Value / 100.0;
            double clipWidth = totalWidth * splitRatio;

            BeforeClipGeometry.Rect = new Rect(0, 0, clipWidth, totalHeight);
            Canvas.SetLeft(SplitDividerLine, clipWidth);
        }

        private void ExecutionMode_Changed(object sender, RoutedEventArgs e)
        {
            if (DirectApiConfigPanel == null || RbDirectApiMode == null)
                return;

            bool isDirect = RbDirectApiMode.IsChecked == true;
            DirectApiConfigPanel.Opacity = isDirect ? 1.0 : 0.6;
            DirectApiConfigPanel.IsEnabled = isDirect;
        }
    }

    public class MaterialMapDisplayItem
    {
        public string HexColor { get; set; } = string.Empty;
        public string RevitName { get; set; } = string.Empty;
        public string MaterialClass { get; set; } = string.Empty;
        public bool WasTransparent { get; set; }
        public string AiPromptDescriptor { get; set; } = string.Empty;
    }
}

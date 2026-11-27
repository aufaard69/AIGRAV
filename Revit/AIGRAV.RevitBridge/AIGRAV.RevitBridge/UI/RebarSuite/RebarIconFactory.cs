using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AIGRAV.RevitBridge.UI.RebarSuite;

/// <summary>
/// Loads bespoke supersampled PNG Ribbon icons from Assets\RebarSuite\
/// with an automatic in-memory vector fallback if a PNG asset is missing.
/// </summary>
public static class RebarIconFactory
{
    public static ImageSource LoadIcon(string iconName, string fallbackGlyph, string accentHex, int size = 32)
    {
        try
        {
            string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? AppContext.BaseDirectory;
            string path = Path.Combine(asmDir, "Assets", "RebarSuite", $"{iconName}_{size}.png");
            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RebarIconFactory] Failed loading '{iconName}_{size}.png': {ex.Message}");
        }

        return CreateFallbackIcon(fallbackGlyph, accentHex, size);
    }

    public static ImageSource CreateFallbackIcon(string glyph, string accentHex, int size = 32)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            var bgBrush = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#132238")!,
                (Color)ColorConverter.ConvertFromString("#0A1220")!,
                45.0);
            var accentColor = (Color)ColorConverter.ConvertFromString(accentHex)!;
            var borderPen = new Pen(new SolidColorBrush(accentColor), size >= 24 ? 1.8 : 1.2);
            double r = size >= 24 ? 6.0 : 3.5;

            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(1, 1, size - 2, size - 2), r, r);

            var ft = new FormattedText(
                glyph,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                size >= 24 ? 11.5 : 8.0,
                new SolidColorBrush(accentColor),
                1.0);

            dc.DrawText(ft, new Point((size - ft.Width) * 0.5, (size - ft.Height) * 0.5));
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}

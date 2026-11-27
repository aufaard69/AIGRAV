using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AIGRAV.Skills.RebarExtension.Models;

namespace AIGRAV.RevitBridge.UI.RebarSuite;

/// <summary>
/// Orthographic 2D CAD Blueprint renderer drawing crisp Longitudinal Elevation (X-Z / Y-Z)
/// and Cross-Section / Plan projections directly from the solved 3D RebarPreviewPackage.
/// </summary>
public static class Rebar2DCadRenderer
{
    public static void RenderElevation(Canvas canvas, RebarPreviewPackage? preview)
    {
        canvas.Children.Clear();
        double cw = canvas.ActualWidth > 40 ? canvas.ActualWidth : 780;
        double ch = canvas.ActualHeight > 40 ? canvas.ActualHeight : 360;
        DrawCadGrid(canvas, cw, ch);

        if (preview == null || preview.ConcreteMeshes.Count == 0)
        {
            DrawEmptyBanner(canvas, cw, ch, "Pilih elemen Beam / Column / Foundation / Floor di Revit untuk melihat 2D Potongan Memanjang");
            return;
        }

        // Determine dominant horizontal axis (X vs Y) so longitudinal beams oriented along Y render full-length too!
        double minX = preview.ConcreteMeshes.Min(m => m.MinMm.X);
        double maxX = preview.ConcreteMeshes.Max(m => m.MaxMm.X);
        double minY = preview.ConcreteMeshes.Min(m => m.MinMm.Y);
        double maxY = preview.ConcreteMeshes.Max(m => m.MaxMm.Y);
        double minZ = preview.ConcreteMeshes.Min(m => m.MinMm.Z);
        double maxZ = preview.ConcreteMeshes.Max(m => m.MaxMm.Z);

        // Include rebar extents (e.g., column footing bends extending below column BaseZ!)
        foreach (var bar in preview.RebarBars)
        {
            foreach (var poly in bar.PolylinesMm)
            {
                foreach (var p in poly)
                {
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                    minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
                }
            }
        }

        bool useYAsHoriz = (maxY - minY) > (maxX - minX) * 1.15;
        double hMin = useYAsHoriz ? minY : minX;
        double hMax = useYAsHoriz ? maxY : maxX;
        double hSpan = Math.Max(600.0, hMax - hMin);
        double vSpan = Math.Max(400.0, maxZ - minZ);

        double pad = 44.0;
        double scale = Math.Min((cw - pad * 2) / hSpan, (ch - pad * 2) / vSpan);
        double offX = (cw - hSpan * scale) * 0.5 - hMin * scale;
        double offY = (ch - vSpan * scale) * 0.5 + maxZ * scale;

        Point Map(Point3DData p) => new((useYAsHoriz ? p.Y : p.X) * scale + offX, -p.Z * scale + offY);

        // 1. Concrete outlines
        foreach (var cm in preview.ConcreteMeshes)
        {
            double x0 = (useYAsHoriz ? cm.MinMm.Y : cm.MinMm.X) * scale + offX;
            double x1 = (useYAsHoriz ? cm.MaxMm.Y : cm.MaxMm.X) * scale + offX;
            double yTop = -cm.MaxMm.Z * scale + offY;
            double yBot = -cm.MinMm.Z * scale + offY;

            var rect = new Rectangle
            {
                Width = Math.Max(2, x1 - x0),
                Height = Math.Max(2, yBot - yTop),
                Fill = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)),
                Stroke = new SolidColorBrush(Color.FromArgb(170, 148, 163, 184)),
                StrokeThickness = 1.2
            };
            Canvas.SetLeft(rect, x0);
            Canvas.SetTop(rect, yTop);
            canvas.Children.Add(rect);
        }

        // 2. Rebars (stirrups first, main bars & extras on top)
        foreach (var bar in preview.RebarBars.OrderByDescending(b => b.IsStirrup))
        {
            Color col = ParseColor(bar.ColorHex);
            var strokeBrush = new SolidColorBrush(bar.IsStirrup ? Color.FromArgb(145, col.R, col.G, col.B) : col);
            double thickness = bar.IsStirrup ? 1.1 : (bar.Role.Contains("Extra", StringComparison.OrdinalIgnoreCase) ? 2.6 : 2.2);

            foreach (var poly in bar.PolylinesMm)
            {
                if (poly.Count < 2) continue;
                var pl = new Polyline
                {
                    Stroke = strokeBrush,
                    StrokeThickness = thickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                foreach (var pt in poly) pl.Points.Add(Map(pt));
                canvas.Children.Add(pl);
            }
        }

        // Dimension Header
        DrawCadLabel(canvas, 14, 10,
            $"POTONGAN MEMANJANG / ELEVASI ({(useYAsHoriz ? "Y-Z" : "X-Z")})  •  Bentang: {hSpan:N0} mm  ×  Tinggi: {vSpan:N0} mm",
            "#94A3B8");
    }

    public static void RenderSectionOrPlan(Canvas canvas, RebarPreviewPackage? preview)
    {
        canvas.Children.Clear();
        double cw = canvas.ActualWidth > 40 ? canvas.ActualWidth : 780;
        double ch = canvas.ActualHeight > 40 ? canvas.ActualHeight : 360;
        DrawCadGrid(canvas, cw, ch);

        if (preview == null || preview.ConcreteMeshes.Count == 0)
        {
            DrawEmptyBanner(canvas, cw, ch, "Pilih elemen di Revit untuk melihat 2D Potongan Penampang / Denah");
            return;
        }

        var primaryCat = preview.Hosts.FirstOrDefault()?.Category ?? StructuralHostCategory.Beam;
        bool isPlanMode = primaryCat is StructuralHostCategory.Floor or StructuralHostCategory.Foundation or StructuralHostCategory.Column;

        if (isPlanMode)
        {
            RenderTopPlan(canvas, cw, ch, preview, primaryCat);
        }
        else
        {
            RenderBeamCrossSection(canvas, cw, ch, preview);
        }
    }

    private static void RenderTopPlan(Canvas canvas, double cw, double ch, RebarPreviewPackage preview, StructuralHostCategory cat)
    {
        double minX = preview.ConcreteMeshes.Min(m => m.MinMm.X);
        double maxX = preview.ConcreteMeshes.Max(m => m.MaxMm.X);
        double minY = preview.ConcreteMeshes.Min(m => m.MinMm.Y);
        double maxY = preview.ConcreteMeshes.Max(m => m.MaxMm.Y);

        foreach (var b in preview.RebarBars)
            foreach (var pl in b.PolylinesMm)
                foreach (var p in pl)
                {
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                }

        double spanX = Math.Max(400, maxX - minX);
        double spanY = Math.Max(400, maxY - minY);
        double pad = 44.0;
        double scale = Math.Min((cw - pad * 2) / spanX, (ch - pad * 2) / spanY);
        double offX = (cw - spanX * scale) * 0.5 - minX * scale;
        double offY = (ch - spanY * scale) * 0.5 + maxY * scale;

        Point Map(Point3DData p) => new(p.X * scale + offX, -p.Y * scale + offY);

        foreach (var cm in preview.ConcreteMeshes)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(2, (cm.MaxMm.X - cm.MinMm.X) * scale),
                Height = Math.Max(2, (cm.MaxMm.Y - cm.MinMm.Y) * scale),
                Fill = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)),
                Stroke = new SolidColorBrush(Color.FromArgb(190, 148, 163, 184)),
                StrokeThickness = 1.4
            };
            Canvas.SetLeft(rect, cm.MinMm.X * scale + offX);
            Canvas.SetTop(rect, -cm.MaxMm.Y * scale + offY);
            canvas.Children.Add(rect);
        }

        foreach (var bar in preview.RebarBars)
        {
            Color col = ParseColor(bar.ColorHex);
            var brush = new SolidColorBrush(col);

            foreach (var poly in bar.PolylinesMm)
            {
                if (poly.Count < 2) continue;
                var pl = new Polyline { Stroke = brush, StrokeThickness = bar.IsStirrup ? 1.5 : 1.7 };
                foreach (var pt in poly) pl.Points.Add(Map(pt));
                canvas.Children.Add(pl);

                // For vertical column bars, draw filled dot at bar XY position
                if (cat == StructuralHostCategory.Column && !bar.IsStirrup)
                {
                    Point c = Map(poly[0]);
                    var dot = new Ellipse { Width = 7, Height = 7, Fill = brush };
                    Canvas.SetLeft(dot, c.X - 3.5);
                    Canvas.SetTop(dot, c.Y - 3.5);
                    canvas.Children.Add(dot);
                }
            }
        }

        string title = cat == StructuralHostCategory.Column
            ? $"DENAH PENAMPANG KOLOM (X-Y)  •  {spanX:N0} × {spanY:N0} mm"
            : $"DENAH TULANGAN {cat.ToString().ToUpperInvariant()} (X-Y)  •  {spanX:N0} × {spanY:N0} mm";
        DrawCadLabel(canvas, 14, 10, title, "#94A3B8");
    }

    private static void RenderBeamCrossSection(Canvas canvas, double cw, double ch, RebarPreviewPackage preview)
    {
        var firstHost = preview.Hosts.FirstOrDefault(h => h.Category == StructuralHostCategory.Beam);
        var cm = preview.ConcreteMeshes.FirstOrDefault(m => m.ElementId == firstHost?.ElementId) ?? preview.ConcreteMeshes[0];

        double spanX = cm.MaxMm.X - cm.MinMm.X;
        double spanY = cm.MaxMm.Y - cm.MinMm.Y;
        bool beamAlongX = spanX >= spanY;

        double secMin = beamAlongX ? cm.MinMm.Y : cm.MinMm.X;
        double secMax = beamAlongX ? cm.MaxMm.Y : cm.MaxMm.X;
        double minZ = cm.MinMm.Z;
        double maxZ = cm.MaxMm.Z;

        double w = Math.Max(150, secMax - secMin);
        double h = Math.Max(200, maxZ - minZ);
        double pad = 52.0;
        double scale = Math.Min((cw * 0.45 - pad) / w, (ch - pad * 2) / h);
        double offX = cw * 0.5 - (secMin + w * 0.5) * scale;
        double offY = (ch - h * scale) * 0.5 + maxZ * scale;

        var rect = new Rectangle
        {
            Width = w * scale,
            Height = h * scale,
            Fill = new SolidColorBrush(Color.FromArgb(42, 148, 163, 184)),
            Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            StrokeThickness = 1.6
        };
        Canvas.SetLeft(rect, secMin * scale + offX);
        Canvas.SetTop(rect, -maxZ * scale + offY);
        canvas.Children.Add(rect);

        // Draw first stirrup loop + longitudinal bar cross-section circles
        var firstStirrup = preview.RebarBars.FirstOrDefault(b => b.HostElementId == cm.ElementId && b.IsStirrup && b.PolylinesMm.Count > 0);
        if (firstStirrup != null)
        {
            var pl = new Polyline
            {
                Stroke = new SolidColorBrush(ParseColor(firstStirrup.ColorHex)),
                StrokeThickness = 2.2
            };
            foreach (var p in firstStirrup.PolylinesMm[0])
            {
                double t = beamAlongX ? p.Y : p.X;
                pl.Points.Add(new Point(t * scale + offX, -p.Z * scale + offY));
            }
            canvas.Children.Add(pl);
        }

        foreach (var bar in preview.RebarBars.Where(b => b.HostElementId == cm.ElementId && !b.IsStirrup))
        {
            if (bar.PolylinesMm.Count == 0 || bar.PolylinesMm[0].Count < 2) continue;
            // Pick midpoint along longitudinal segment (avoid hook leg tip)
            var poly = bar.PolylinesMm[0];
            var pt = poly[poly.Count / 2];
            double sx = (beamAlongX ? pt.Y : pt.X) * scale + offX;
            double sy = -pt.Z * scale + offY;
            double r = Math.Clamp(bar.DiameterMm * scale * 0.55, 4.0, 9.5);
            var dot = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Fill = new SolidColorBrush(bar.Role.Contains("Extra", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromRgb(251, 191, 36)
                    : ParseColor(bar.ColorHex)),
                Stroke = Brushes.Black,
                StrokeThickness = 0.7
            };
            Canvas.SetLeft(dot, sx - r);
            Canvas.SetTop(dot, sy - r);
            canvas.Children.Add(dot);
        }

        DrawCadLabel(canvas, 14, 10, $"POTONGAN MELINTANG BALOK (A-A)  •  {w:N0} × {h:N0} mm  (Kuning = Tulangan Extra)", "#94A3B8");
    }

    private static void DrawCadGrid(Canvas canvas, double cw, double ch)
    {
        var penBrush = new SolidColorBrush(Color.FromArgb(24, 148, 163, 184));
        const double step = 32.0;
        for (double x = 0; x < cw; x += step)
            canvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = ch, Stroke = penBrush, StrokeThickness = 0.6 });
        for (double y = 0; y < ch; y += step)
            canvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = cw, Y2 = y, Stroke = penBrush, StrokeThickness = 0.6 });
    }

    private static void DrawEmptyBanner(Canvas canvas, double cw, double ch, string message)
    {
        var tb = new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 13,
            FontWeight = FontWeights.Medium
        };
        Canvas.SetLeft(tb, Math.Max(20, cw * 0.5 - 240));
        Canvas.SetTop(tb, ch * 0.5 - 12);
        canvas.Children.Add(tb);
    }

    private static void DrawCadLabel(Canvas canvas, double x, double y, string text, string hex)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(ParseColor(hex)),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas, Segoe UI")
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        canvas.Children.Add(tb);
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex)!; }
        catch { return Color.FromRgb(56, 189, 248); }
    }
}

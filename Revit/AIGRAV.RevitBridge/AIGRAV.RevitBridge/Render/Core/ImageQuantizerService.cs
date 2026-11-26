using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AIGRAV.RevitBridge.Render.Core;

public static class ImageQuantizerService
{
    private record struct PaletteColor(byte R, byte G, byte B);

    public static void QuantizeMaterialIdPass(
        string rawImagePath,
        string cleanOutputPath,
        IEnumerable<MaterialMappingItem> materials,
        int[] backgroundRgb)
    {
        if (!File.Exists(rawImagePath))
        {
            throw new FileNotFoundException("Raw Material ID pass image not found.", rawImagePath);
        }

        var palette = new List<PaletteColor>();

        byte bgR = (byte)Math.Clamp(backgroundRgb.Length > 0 ? backgroundRgb[0] : 255, 0, 255);
        byte bgG = (byte)Math.Clamp(backgroundRgb.Length > 1 ? backgroundRgb[1] : 255, 0, 255);
        byte bgB = (byte)Math.Clamp(backgroundRgb.Length > 2 ? backgroundRgb[2] : 255, 0, 255);
        var bgColor = new PaletteColor(bgR, bgG, bgB);
        palette.Add(bgColor);

        foreach (var mat in materials)
        {
            var pc = new PaletteColor(mat.R, mat.G, mat.B);
            if (!palette.Contains(pc))
            {
                palette.Add(pc);
            }
        }

        var paletteArray = palette.ToArray();

        using Image<Rgba32> image = Image.Load<Rgba32>(rawImagePath);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    ref Rgba32 pixel = ref row[x];
                    int pr = pixel.R;
                    int pg = pixel.G;
                    int pb = pixel.B;

                    int minDistanceSq = int.MaxValue;
                    PaletteColor nearest = bgColor;

                    for (int i = 0; i < paletteArray.Length; i++)
                    {
                        var c = paletteArray[i];
                        int dr = pr - c.R;
                        int dg = pg - c.G;
                        int db = pb - c.B;
                        int distSq = (dr * dr) + (dg * dg) + (db * db);

                        if (distSq < minDistanceSq)
                        {
                            minDistanceSq = distSq;
                            nearest = c;

                            if (distSq == 0) break;
                        }
                    }

                    pixel.R = nearest.R;
                    pixel.G = nearest.G;
                    pixel.B = nearest.B;
                    pixel.A = 255;
                }
            }
        });

        string? dir = Path.GetDirectoryName(cleanOutputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        image.SaveAsPng(cleanOutputPath);
    }

    public static void SaveMaterialMap(MaterialMapDocument doc, string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(doc, options);
        File.WriteAllText(filePath, json);
    }
}

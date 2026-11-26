using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge.Render.Core;

public static class ThreePassViewExporter
{
    public static RenderJobContext ExportPasses(
        UIDocument uidoc,
        ElementId? targetViewId = null,
        string? baseOutputDir = null,
        int pixelWidth = 3840)
    {
        Document doc = uidoc.Document;
        View? view = null;

        if (targetViewId != null && targetViewId != ElementId.InvalidElementId)
        {
            view = doc.GetElement(targetViewId) as View;
        }

        if (view == null || view.IsTemplate)
        {
            view = uidoc.ActiveGraphicalView ?? uidoc.ActiveView ?? doc.ActiveView;
        }

        if (view == null || view.IsTemplate)
        {
            view = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate);
        }

        if (view == null)
        {
            throw new InvalidOperationException("Tidak ada view 3D atau model view yang valid untuk di-render. Silakan buka view 3D di Revit.");
        }

        // Programmatically bring target view to focus in Revit UI if needed
        if (uidoc.ActiveView == null || uidoc.ActiveView.Id != view.Id)
        {
            try
            {
                uidoc.ActiveView = view;
            }
            catch
            {
                // Ignore if view switching is constrained by active transaction
            }
        }

        string jobId = DateTime.Now.ToString("yyyyMMdd_HHmmss_") + Guid.NewGuid().ToString("N")[..6];

        string outputDir = baseOutputDir ?? Path.Combine(
            GetProjectOrLocalWorkspace(doc),
            ".aigrav",
            "render_jobs",
            jobId);

        Directory.CreateDirectory(outputDir);

        string materialPassPath = Path.Combine(outputDir, "pass1_material_current.png");
        string rawMaterialPassPath = Path.Combine(outputDir, "pass2_material_filter_raw.png");
        string cleanMaterialPassPath = Path.Combine(outputDir, "pass2_material_filter.png");
        string shadedPassPath = Path.Combine(outputDir, "pass3_shaded_color.png");
        string materialMapJsonPath = Path.Combine(outputDir, "material_map.json");
        string renderPromptMdPath = Path.Combine(outputDir, "render_prompt.md");

        var context = new RenderJobContext
        {
            JobId = jobId,
            ViewName = view.Name,
            ViewId = view.Id.Value,
            OutputDirectory = outputDir,
            GeometryPassPath = shadedPassPath,            // Pass 3: View dengan Shaded Color
            LightingPassPath = materialPassPath,          // Pass 1: View saat ini dengan Material
            MaterialIdRawPath = rawMaterialPassPath,
            MaterialIdCleanPath = cleanMaterialPassPath,  // Pass 2: View dengan Filter Color Material
            MaterialMapJsonPath = materialMapJsonPath,
            RenderPromptMdPath = renderPromptMdPath
        };

        var mappingList = new List<MaterialMappingItem>();

        using (Transaction trans = new(doc, "AIGRAV_Temp_Material_Map"))
        {
            try
            {
                trans.Start();

                // 1. Detach View Template if attached so view settings can be safely adapted
                if (view.ViewTemplateId != ElementId.InvalidElementId)
                {
                    view.ViewTemplateId = ElementId.InvalidElementId;
                }

                // 2. Hide Non-Geometric Annotation Categories
                HideAnnotationCategories(doc, view);

                // 3. PASS 1: View saat ini dengan material (Realistic / Baseline Materials)
                try
                {
                    view.DisplayStyle = DisplayStyle.Realistic;
                }
                catch
                {
                    view.DisplayStyle = DisplayStyle.ShadingWithEdges;
                }
                SetViewShadows(view, castShadows: true, ambientShadows: true);
                ExportViewToPng(doc, view, materialPassPath, pixelWidth);

                // 4. PASS 3: View dengan shaded color (Pure Shaded Geometry + Crisp Shadows & Depth)
                view.DisplayStyle = DisplayStyle.ShadingWithEdges;
                SetViewShadows(view, castShadows: true, ambientShadows: true);
                ExportViewToPng(doc, view, shadedPassPath, pixelWidth);

                // 5. PASS 2: View dengan filter color material (Semantic Material ID Override, Flat Colors, Zero Transparency, No Shadows)
                mappingList = PrepareAndApplyMaterialIdOverrides(doc, view);
                view.DisplayStyle = DisplayStyle.FlatColors;
                SetViewShadows(view, castShadows: false, ambientShadows: false);
                ExportViewToPng(doc, view, rawMaterialPassPath, pixelWidth);
            }
            finally
            {
                // CRITICAL REQUIREMENT: Guaranteed RollBack to leave user's model 100% untouched
                if (trans.HasStarted() && !trans.HasEnded())
                {
                    trans.RollBack();
                }
            }
        }

        // 6. Post-Process Pass 2 with Euclidean RGB Nearest-Neighbor Quantizer to wipe edge anti-aliasing
        int[] bgRgb = [255, 255, 255];
        ImageQuantizerService.QuantizeMaterialIdPass(rawMaterialPassPath, cleanMaterialPassPath, mappingList, bgRgb);

        // 7. Save Material Map Document
        var mapDoc = new MaterialMapDocument
        {
            JobId = jobId,
            ViewName = view.Name,
            ViewId = view.Id.Value,
            Timestamp = DateTime.UtcNow.ToString("o"),
            BackgroundRgb = bgRgb,
            Materials = mappingList
        };
        ImageQuantizerService.SaveMaterialMap(mapDoc, materialMapJsonPath);
        context.MaterialMap = mapDoc;

        // 8. Generate Structured Prompt Markdown
        string promptContent = RenderPromptBuilder.BuildPrompt(context);
        File.WriteAllText(renderPromptMdPath, promptContent);

        return context;
    }

    private static void HideAnnotationCategories(Document doc, View view)
    {
        try
        {
            view.AreAnnotationCategoriesHidden = true;
        }
        catch
        {
            // Fallback for views that don't support AreAnnotationCategoriesHidden
        }

        BuiltInCategory[] categoriesToHide =
        [
            BuiltInCategory.OST_Grids,
            BuiltInCategory.OST_Levels,
            BuiltInCategory.OST_SectionBox,
            BuiltInCategory.OST_Dimensions,
            BuiltInCategory.OST_TextNotes,
            BuiltInCategory.OST_Viewers,
            BuiltInCategory.OST_ReferenceLines,
            BuiltInCategory.OST_ReferencePoints,
            BuiltInCategory.OST_CLines
        ];

        foreach (var bic in categoriesToHide)
        {
            try
            {
                Category? cat = Category.GetCategory(doc, bic);
                if (cat != null && view.CanCategoryBeHidden(cat.Id) && !view.GetCategoryHidden(cat.Id))
                {
                    view.SetCategoryHidden(cat.Id, true);
                }
            }
            catch
            {
                // Ignore categories that cannot be hidden in specific view types
            }
        }
    }

    private static void SetViewShadows(View view, bool castShadows, bool ambientShadows)
    {
        try
        {
            view.LookupParameter("Cast Shadows")?.Set(castShadows ? 1 : 0);
        }
        catch { }

        try
        {
            view.LookupParameter("Ambient Shadows")?.Set(ambientShadows ? 1 : 0);
        }
        catch { }
    }

    private static List<MaterialMappingItem> PrepareAndApplyMaterialIdOverrides(Document doc, View view)
    {
        var visibleElements = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .ToElements();

        var distinctMaterialIds = new HashSet<ElementId>();

        foreach (var elem in visibleElements)
        {
            try
            {
                var matIds = elem.GetMaterialIds(false);
                if (matIds != null)
                {
                    foreach (var mid in matIds) distinctMaterialIds.Add(mid);
                }

                var paintIds = elem.GetMaterialIds(true);
                if (paintIds != null)
                {
                    foreach (var mid in paintIds) distinctMaterialIds.Add(mid);
                }
            }
            catch
            {
                // Some structural elements throw when querying material IDs
            }
        }

        // Also query materials in document to have complete coverage
        var allDocMaterials = new FilteredElementCollector(doc)
            .OfClass(typeof(Material))
            .Cast<Material>()
            .ToList();

        var activeMaterials = distinctMaterialIds
            .Select(id => doc.GetElement(id) as Material)
            .Where(m => m != null)
            .Cast<Material>()
            .ToList();

        if (activeMaterials.Count == 0)
        {
            activeMaterials = allDocMaterials.Take(25).ToList();
        }

        var solidPattern = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

        var palette = GenerateHighContrastPalette(activeMaterials.Count);
        var result = new List<MaterialMappingItem>();

        for (int i = 0; i < activeMaterials.Count; i++)
        {
            var mat = activeMaterials[i];
            var (r, g, b) = palette[i % palette.Count];

            try
            {
                mat.Color = new Color(r, g, b);
                mat.Transparency = 0;
                mat.Shininess = 0;
                mat.Smoothness = 0;
                mat.UseRenderAppearanceForShading = false;

                if (solidPattern != null)
                {
                    mat.SurfaceForegroundPatternId = solidPattern.Id;
                    mat.SurfaceForegroundPatternColor = new Color(r, g, b);
                }
            }
            catch
            {
                // Material parameter override fallback
            }

            string hex = $"#{r:X2}{g:X2}{b:X2}";
            string aiDesc = GenerateDefaultAiDescription(mat.Name, mat.MaterialCategory);

            result.Add(new MaterialMappingItem
            {
                MaterialId = mat.Id.Value,
                Name = mat.Name,
                Category = mat.MaterialCategory ?? "General",
                HexColor = hex,
                Rgb = [r, g, b],
                AiDescription = aiDesc
            });
        }

        return result;
    }

    private static string ExportViewToPng(Document doc, View view, string targetFilePath, int pixelWidth)
    {
        string outputDir = Path.GetDirectoryName(targetFilePath)!;
        Directory.CreateDirectory(outputDir);
        string filePrefix = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(targetFilePath));

        if (File.Exists(targetFilePath))
        {
            try { File.Delete(targetFilePath); } catch { }
        }

        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = filePrefix,
            FitDirection = FitDirectionType.Horizontal,
            PixelSize = pixelWidth,
            ImageResolution = ImageResolution.DPI_300,
            ShadowViewsFileType = ImageFileType.PNG,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ZoomType = ZoomFitType.FitToPage
        };
        options.SetViewsAndSheets(new List<ElementId> { view.Id });

        doc.ExportImage(options);

        string baseName = Path.GetFileNameWithoutExtension(targetFilePath);
        var matches = Directory.GetFiles(outputDir, $"{baseName}*.png");

        if (matches.Length > 0)
        {
            string matched = matches[0];
            if (!matched.Equals(targetFilePath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
                File.Move(matched, targetFilePath);
            }
            return targetFilePath;
        }

        throw new FileNotFoundException($"Revit failed to produce exported view file for: {targetFilePath}");
    }

    private static List<(byte R, byte G, byte B)> GenerateHighContrastPalette(int count)
    {
        var basePalette = new (byte R, byte G, byte B)[]
        {
            (230, 25, 75),   // Red
            (60, 180, 75),   // Green
            (255, 225, 25),  // Yellow
            (0, 130, 200),   // Blue
            (245, 130, 48),  // Orange
            (145, 30, 180),  // Purple
            (70, 240, 240),  // Cyan
            (240, 50, 230),  // Magenta
            (210, 245, 60),  // Lime
            (250, 190, 212), // Pink
            (0, 128, 128),   // Teal
            (220, 190, 255), // Lavender
            (170, 110, 40),  // Brown
            (255, 250, 200), // Beige
            (128, 0, 0),     // Maroon
            (170, 255, 195), // Mint
            (128, 128, 0),   // Olive
            (255, 215, 180), // Coral
            (0, 0, 128),     // Navy
            (128, 128, 128)  // Grey
        };

        var result = new List<(byte, byte, byte)>();

        for (int i = 0; i < count; i++)
        {
            if (i < basePalette.Length)
            {
                result.Add(basePalette[i]);
            }
            else
            {
                double hue = ((i - basePalette.Length) * 137.508) % 360.0;
                result.Add(HsvToRgb(hue, 0.85, 0.92));
            }
        }

        return result;
    }

    private static (byte R, byte G, byte B) HsvToRgb(double hue, double saturation, double value)
    {
        int hi = (int)(Math.Floor(hue / 60)) % 6;
        double f = (hue / 60) - Math.Floor(hue / 60);

        value *= 255;
        byte v = (byte)Math.Clamp(value, 0, 255);
        byte p = (byte)Math.Clamp(value * (1 - saturation), 0, 255);
        byte q = (byte)Math.Clamp(value * (1 - (f * saturation)), 0, 255);
        byte t = (byte)Math.Clamp(value * (1 - ((1 - f) * saturation)), 0, 255);

        return hi switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };
    }

    private static string GenerateDefaultAiDescription(string matName, string? category)
    {
        string lower = matName.ToLowerInvariant();

        if (lower.Contains("concrete") || lower.Contains("beton"))
            return "Architectural cast-in-place concrete with subtle formwork seams, natural aggregate texture, and matte surface.";
        if (lower.Contains("glass") || lower.Contains("kaca"))
            return "Clear architectural low-iron double-glazed curtain wall glass with crisp environmental reflections.";
        if (lower.Contains("steel") || lower.Contains("baja") || lower.Contains("metal") || lower.Contains("besi"))
            return "Structural steel profile with matte dark charcoal powder-coating and clean micro-beveled edges.";
        if (lower.Contains("wood") || lower.Contains("kayu") || lower.Contains("timber"))
            return "Natural architectural grade timber cladding with vertical wood grain and satin protective oil finish.";
        if (lower.Contains("brick") || lower.Contains("bata"))
            return "Textured red-brown clay facing brick masonry with sharp mortar joints and subtle tonal variation.";
        if (lower.Contains("grass") || lower.Contains("rumput") || lower.Contains("lawn"))
            return "Lush manicured exterior landscape lawn with natural blade density and subtle morning dew.";
        if (lower.Contains("water") || lower.Contains("air") || lower.Contains("pool"))
            return "Calm architectural reflection pool water with gentle surface ripples and realistic sky reflections.";
        if (lower.Contains("aluminum") || lower.Contains("aluminium"))
            return "Anodized architectural aluminum extrusion with brushed satin metallic sheen.";
        if (lower.Contains("paint") || lower.Contains("cat") || lower.Contains("plaster") || lower.Contains("aci"))
            return "Smooth architectural white matte gypsum plaster wall finish with soft diffuse light absorption.";

        return $"Photorealistic architectural material texture representing {matName} ({category ?? "Architectural Finish"}).";
    }

    private static string GetProjectOrLocalWorkspace(Document doc)
    {
        try
        {
            if (!string.IsNullOrEmpty(doc.PathName))
            {
                string? docFolder = Path.GetDirectoryName(doc.PathName);
                if (!string.IsNullOrEmpty(docFolder) && Directory.Exists(docFolder))
                {
                    return docFolder;
                }
            }
        }
        catch { }

        // Workspace fallback
        if (Directory.Exists(@"C:\AIGRAV"))
        {
            return @"C:\AIGRAV";
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIGRAV");
    }
}

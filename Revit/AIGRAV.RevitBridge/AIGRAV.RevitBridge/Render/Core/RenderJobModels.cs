using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace AIGRAV.RevitBridge.Render.Core;

public class MaterialMappingItem
{
    [JsonPropertyName("material_id")]
    public long MaterialId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("hex_color")]
    public string HexColor { get; set; } = "#FFFFFF";

    [JsonPropertyName("rgb")]
    public int[] Rgb { get; set; } = [255, 255, 255];

    [JsonPropertyName("ai_description")]
    public string AiDescription { get; set; } = string.Empty;

    [JsonIgnore]
    public byte R => (byte)(Rgb.Length > 0 ? Math.Clamp(Rgb[0], 0, 255) : 255);

    [JsonIgnore]
    public byte G => (byte)(Rgb.Length > 1 ? Math.Clamp(Rgb[1], 0, 255) : 255);

    [JsonIgnore]
    public byte B => (byte)(Rgb.Length > 2 ? Math.Clamp(Rgb[2], 0, 255) : 255);

    [JsonIgnore]
    public SolidColorBrush ColorBrush => new(System.Windows.Media.Color.FromRgb(R, G, B));
}

public class MaterialMapDocument
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("view_name")]
    public string ViewName { get; set; } = string.Empty;

    [JsonPropertyName("view_id")]
    public long ViewId { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("background_rgb")]
    public int[] BackgroundRgb { get; set; } = [255, 255, 255];

    [JsonPropertyName("materials")]
    public List<MaterialMappingItem> Materials { get; set; } = new();
}

public class StylePreset
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PromptDescription { get; set; } = string.Empty;

    public static List<StylePreset> GetDefaultPresets()
    {
        return new List<StylePreset>
        {
            new()
            {
                Id = "industrial_warehouse",
                DisplayName = "Industrial Warehouse (Cinematic Cool Metal)",
                PromptDescription = "Industrial modern architectural photography. Atmospheric diffuse skylight illuminating polished concrete slab, dark charcoal steel trusses with subtle fabrication markings, realistic reflections on metal panel cladding, exposed MEP conduits, ambient warehouse dust motes in sunbeams."
            },
            new()
            {
                Id = "modern_facade_daylight",
                DisplayName = "Modern Facade Daylight (Crisp High-Key)",
                PromptDescription = "Contemporary high-end commercial architecture exterior. Bright crisp 10:00 AM natural daylight, vivid sky reflection in low-iron double-glazed curtain wall, ultra-clean shadow falloff, realistic honed stone and travertine facade textures, subtle surrounding foliage."
            },
            new()
            {
                Id = "golden_hour",
                DisplayName = "Golden Hour / Sunset (Warm Architectural Dramatic)",
                PromptDescription = "Dramatic sunset golden hour architectural render. Low-angle warm amber sunlight casting long cinematic shadows across facade and plaza, warm 2700K interior architectural illumination glowing softly through glass openings, rich atmospheric depth."
            },
            new()
            {
                Id = "interior_archviz",
                DisplayName = "Interior Archviz (Scandinavian Photorealism)",
                PromptDescription = "High-end Scandinavian interior architecture photography. Soft diffused northern window daylight, warm indirect cove lighting, realistic micro-textures on oak wood flooring, soft linen textiles, photorealistic specular highlights on matte fixtures."
            }
        };
    }
}

public enum RenderEngineMode
{
    AntigravityBridge,
    DirectApiKey
}

public enum DirectApiProvider
{
    Gemini,
    OpenAI,
    Grok
}

public class RenderJobContext
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string ViewName { get; set; } = string.Empty;
    public long ViewId { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;

    public string GeometryPassPath { get; set; } = string.Empty;
    public string LightingPassPath { get; set; } = string.Empty;
    public string MaterialIdRawPath { get; set; } = string.Empty;
    public string MaterialIdCleanPath { get; set; } = string.Empty;
    public string MaterialMapJsonPath { get; set; } = string.Empty;
    public string RenderPromptMdPath { get; set; } = string.Empty;
    public string? StyleReferencePath { get; set; }

    public MaterialMapDocument MaterialMap { get; set; } = new();
    public StylePreset SelectedPreset { get; set; } = StylePreset.GetDefaultPresets()[0];
    public string ExtraPrompt { get; set; } = string.Empty;

    public RenderEngineMode EngineMode { get; set; } = RenderEngineMode.AntigravityBridge;
    public DirectApiProvider Provider { get; set; } = DirectApiProvider.Gemini;
    public string ModelName { get; set; } = "gemini-3-pro-image";
    public string? ApiKey { get; set; }
}

public class RenderResult
{
    public bool Success { get; set; }
    public string? OutputImagePath { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }

    public static RenderResult Ok(string imagePath, TimeSpan duration) =>
        new() { Success = true, OutputImagePath = imagePath, Duration = duration };

    public static RenderResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public static class RenderPromptBuilder
{
    public static string BuildPrompt(RenderJobContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ABSOLUTE ARCHITECTURAL DIRECTIVE: ZERO-HALLUCINATION & 100% GEOMETRIC FREEZE");
        sb.AppendLine();
        sb.AppendLine("You are an ultra-precise deterministic architectural photorealism renderer. Your ONLY purpose is to apply high-fidelity PBR material textures, photorealistic reflections, and natural architectural lighting to the EXACT building surfaces provided in the 3 synchronized Revit reference passes.");
        sb.AppendLine();
        sb.AppendLine("## ⚠️ CRITICAL ZERO-HALLUCINATION MANDATE (STRICTLY ENFORCED):");
        sb.AppendLine("1. ABSOLUTELY ZERO ADDED ELEMENTS: You are STRICTLY FORBIDDEN from generating or adding ANY objects not explicitly visible in the reference passes. DO NOT add people, crowds, pedestrians, silhouettes. DO NOT add trees, shrubs, bushes, grass, or outdoor vegetation unless already modeled. DO NOT add cars, trucks, or vehicles. DO NOT add fictional furniture, extra columns, random stairs, balconies, or skylights.");
        sb.AppendLine("2. ABSOLUTELY ZERO REMOVED ELEMENTS: Every structural column, beam, truss, slab edge, wall partition, window frame, and foundation footing present in the 3 passes MUST remain in its exact spatial coordinate location and proportion. Do NOT omit or blur structural joints.");
        sb.AppendLine("3. ROLE DEFINITION: You are a material finishing and lighting engine, NOT a generative concept designer. Do not invent new architecture. Maintain 100% 1:1 pixel alignment with the underlying geometric structure.");
        sb.AppendLine();
        sb.AppendLine("## 1. Tri-Pass Synchronized Reference Guide");
        sb.AppendLine("- `Pass 1 (View Saat Ini dengan Material)`: Real-world Revit baseline materials, glass transparency, and primary camera perspective. Use this as your base canvas.");
        sb.AppendLine("- `Pass 2 (View dengan Filter Color Material)`: Exact Semantic RGB Material ID segmentation mask. Each flat color corresponds strictly to the physical PBR material in the lookup table below.");
        sb.AppendLine("- `Pass 3 (View dengan Shaded Color)`: Pure structural geometry, clean edges, and authentic sunlight/shadow angles establishing true architectural scale.");
        sb.AppendLine();
        sb.AppendLine("## 2. Material Assignment Matrix (RGB Segmentation Lookup)");
        sb.AppendLine("| Mask Color | RGB | Revit Material | Target PBR Material & Finishing |");
        sb.AppendLine("| :--- | :--- | :--- | :--- |");

        foreach (var mat in context.MaterialMap.Materials)
        {
            string rgbStr = $"({mat.R}, {mat.G}, {mat.B})";
            string desc = string.IsNullOrWhiteSpace(mat.AiDescription)
                ? $"Photorealistic architectural material for {mat.Name}"
                : mat.AiDescription;
            sb.AppendLine($"| `{mat.HexColor}` | {rgbStr} | **{mat.Name}** | {desc} |");
        }

        sb.AppendLine();
        sb.AppendLine("## 3. Atmospheric Style & Lighting Preset");
        sb.AppendLine($"**Preset**: {context.SelectedPreset.DisplayName}");
        sb.AppendLine(context.SelectedPreset.PromptDescription);

        if (!string.IsNullOrWhiteSpace(context.ExtraPrompt))
        {
            sb.AppendLine();
            sb.AppendLine("### Additional User Directives");
            sb.AppendLine(context.ExtraPrompt.Trim());
        }

        if (!string.IsNullOrWhiteSpace(context.StyleReferencePath))
        {
            sb.AppendLine();
            sb.AppendLine("### Style Reference Directive");
            sb.AppendLine("Adopt the color grading, material mood, atmospheric contrast, and glass reflection characteristics from the attached style reference image while strictly preserving the building geometry from the passes.");
        }

        sb.AppendLine();
        sb.AppendLine("## 4. Final Quality Standards");
        sb.AppendLine("- 4K UHD Ultra-High Definition photorealism.");
        sb.AppendLine("- Natural ambient occlusion in crevices and steel joints.");
        sb.AppendLine("- True Fresnel glass reflection and subtle interior transparency.");
        sb.AppendLine("- Zero watermarks, zero blur, sharp architectural lines, 100% frozen structural geometry.");

        return sb.ToString();
    }
}

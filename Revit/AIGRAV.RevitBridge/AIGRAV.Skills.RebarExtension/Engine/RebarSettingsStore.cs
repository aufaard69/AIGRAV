using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace AIGRAV.Skills.RebarExtension.Engine;

/// <summary>
/// Manages local JSON Content Packs (%AppData%\AIGRAV\RebarSuite\ContentPacks.json)
/// and per-document Revit ExtensibleStorage persistence so the plugin remains 100% offline.
/// </summary>
public static class RebarSettingsStore
{
    private static readonly Guid SchemaGuid = new("D7A4F281-5A18-4D9C-B842-81F29E44A901");
    private const string SchemaName = "AIGRAV_RebarSuite_ProjectSettings_V1";
    private const string FieldJsonName = "SettingsJson";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AIGRAV",
        "RebarSuite");

    public static string PresetsFilePath => Path.Combine(SettingsDirectory, "ContentPacks.json");

    public static List<RebarPresetProfile> GetBuiltInPresets()
    {
        return new List<RebarPresetProfile>
        {
            new()
            {
                Name = "SNI 2847-2019 (SRPMK)",
                Description = "Rangka pemikul momen khusus daktail: l_o = 2h, kait sengkang 135 deg, selimut fondasi 75 mm.",
                Global = new RebarGlobalSettings { MaxStockLengthMm = 12000, LapMode = LapMode.D40, BondCondition = BondCondition.Good, SteelGrade = "BJTS 420B" },
                Beam = new BeamConfig { CoverMm = 40, StirrupSupportSpacingMm = 100, StirrupMidspanSpacingMm = 150, CutoffMode = CutoffExtensionMode.SniMaxDOr12D, Use135SeismicHook = true },
                Column = new ColumnConfig { CoverMm = 40, TieLoSpacingMm = 100, TieMidSpacingMm = 150, TieLegs = 4, SpliceStrategy = ColumnSpliceStrategy.AutoFollowModelCut, BaseFootingBend = EndHookDirection.Bend90Outward },
                Foundation = new FoundationConfig { CoverMm = 75, BottomXDiaMm = 16, BottomYDiaMm = 16, EnableTopMesh = true },
                Floor = new FloorConfig { CoverMm = 20, TopMode = FloorTopReinforcementMode.SupportStripL4, MeshLayoutMode = FloorMeshLayoutMode.Segmented12mMax }
            },
            new()
            {
                Name = "SNI 2847-2019 (SRPMM)",
                Description = "Rangka pemikul momen menengah: jarak sengkang tumpuan 125 mm, lapangan 200 mm.",
                Global = new RebarGlobalSettings { MaxStockLengthMm = 12000, LapMode = LapMode.D40, BondCondition = BondCondition.Good, SteelGrade = "BJTS 420B" },
                Beam = new BeamConfig { CoverMm = 40, StirrupSupportSpacingMm = 125, StirrupMidspanSpacingMm = 200, CutoffMode = CutoffExtensionMode.SniMaxDOr12D, Use135SeismicHook = true },
                Column = new ColumnConfig { CoverMm = 40, TieLoSpacingMm = 125, TieMidSpacingMm = 200, TieLegs = 2, SpliceStrategy = ColumnSpliceStrategy.AutoFollowModelCut, BaseFootingBend = EndHookDirection.Bend90Outward },
                Foundation = new FoundationConfig { CoverMm = 75, BottomXDiaMm = 16, BottomYDiaMm = 16, EnableTopMesh = false },
                Floor = new FloorConfig { CoverMm = 20, TopMode = FloorTopReinforcementMode.SupportStripL4, MeshLayoutMode = FloorMeshLayoutMode.Segmented12mMax }
            },
            new()
            {
                Name = "ACI 318M-19 (Class B Lap 50D)",
                Description = "ACI metric with Class B splice factor 50D, Grade 420, 12m stock bars.",
                Global = new RebarGlobalSettings { MaxStockLengthMm = 12000, LapMode = LapMode.D50, BondCondition = BondCondition.Good, SteelGrade = "ASTM A615 Gr.60" },
                Beam = new BeamConfig { CoverMm = 40, CutoffMode = CutoffExtensionMode.SniMaxDOr12D, JointHookLegFormula = "12D" },
                Column = new ColumnConfig { CoverMm = 40, SpliceStrategy = ColumnSpliceStrategy.AutoFollowModelCut },
                Foundation = new FoundationConfig { CoverMm = 75 },
                Floor = new FloorConfig { CoverMm = 20 }
            },
            new()
            {
                Name = "Gudang / Industrial (Heavy 20D Cutoff)",
                Description = "Profil heavy industrial dengan ekstensi pemutusan 20D, D22 utama, double mesh pelat tebal.",
                Global = new RebarGlobalSettings { MaxStockLengthMm = 12000, LapMode = LapMode.D50, RoundingMode = LengthRoundingMode.Nearest50Mm, SteelGrade = "BJTS 420B" },
                Beam = new BeamConfig { CoverMm = 50, TopBarDiameterMm = 22, BottomBarDiameterMm = 22, ExtraTopSupportDiaMm = 22, ExtraBottomMidspanDiaMm = 22, CutoffMode = CutoffExtensionMode.L20D, EnableSkinBars = true },
                Column = new ColumnConfig { CoverMm = 50, MainBarDiaMm = 22, TotalMainBars = 12, TieDiaMm = 10, TieLegs = 4 },
                Foundation = new FoundationConfig { CoverMm = 75, BottomXDiaMm = 19, BottomYDiaMm = 19, BottomXSpacingMm = 125, BottomYSpacingMm = 125, EnableTopMesh = true, IncludePerimeterBinder = true },
                Floor = new FloorConfig { CoverMm = 25, TopMode = FloorTopReinforcementMode.FullDoubleMesh, BottomXDiaMm = 13, BottomYDiaMm = 13, TopXDiaMm = 13, TopYDiaMm = 13 }
            }
        };
    }

    public static List<RebarPresetProfile> LoadAllPresets()
    {
        var list = GetBuiltInPresets();
        try
        {
            if (File.Exists(PresetsFilePath))
            {
                string json = File.ReadAllText(PresetsFilePath);
                var custom = JsonSerializer.Deserialize<List<RebarPresetProfile>>(json, JsonOpts);
                if (custom != null)
                {
                    foreach (var p in custom)
                    {
                        var idx = list.FindIndex(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) list[idx] = p;
                        else list.Add(p);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RebarSettingsStore] LoadAllPresets warning: {ex.Message}");
        }
        return list;
    }

    public static void SavePreset(RebarPresetProfile preset)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var existing = LoadAllPresets();
        int idx = existing.FindIndex(x => x.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) existing[idx] = preset;
        else existing.Add(preset);

        string json = JsonSerializer.Serialize(existing, JsonOpts);
        File.WriteAllText(PresetsFilePath, json);
    }

    public static RebarPresetProfile LoadFromDocumentOrDefault(Document? doc)
    {
        var fallback = GetBuiltInPresets().First();
        if (doc == null || !doc.IsValidObject) return fallback;

        try
        {
            Schema? schema = Schema.Lookup(SchemaGuid);
            if (schema == null || doc.ProjectInformation == null) return fallback;

            Entity entity = doc.ProjectInformation.GetEntity(schema);
            if (!entity.IsValid()) return fallback;

            string json = entity.Get<string>(FieldJsonName);
            if (string.IsNullOrWhiteSpace(json)) return fallback;

            return JsonSerializer.Deserialize<RebarPresetProfile>(json, JsonOpts) ?? fallback;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RebarSettingsStore] LoadFromDocument failed: {ex.Message}");
            return fallback;
        }
    }

    public static bool SaveToDocument(Document doc, RebarPresetProfile profile, out string? error)
    {
        error = null;
        if (doc == null || !doc.IsValidObject || doc.ProjectInformation == null)
        {
            error = "Active document or ProjectInformation is unavailable.";
            return false;
        }

        try
        {
            Schema schema = Schema.Lookup(SchemaGuid) ?? BuildSchema();
            Entity entity = new(schema);
            string json = JsonSerializer.Serialize(profile, JsonOpts);
            entity.Set(FieldJsonName, json);
            doc.ProjectInformation.SetEntity(entity);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static Schema BuildSchema()
    {
        SchemaBuilder sb = new(SchemaGuid);
        sb.SetReadAccessLevel(AccessLevel.Public);
        sb.SetWriteAccessLevel(AccessLevel.Public);
        sb.SetSchemaName(SchemaName);
        sb.AddSimpleField(FieldJsonName, typeof(string));
        return sb.Finish();
    }
}

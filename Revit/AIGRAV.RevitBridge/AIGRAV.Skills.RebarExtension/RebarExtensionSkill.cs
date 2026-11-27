using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGRAV.Contracts;
using AIGRAV.Skills.RebarExtension.Engine;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.RebarExtension;

/// <summary>
/// Optional IAIGRAVSkill bridge entrypoint exposing the offline deterministic Rebar Suite
/// operations to headless/batch callers without any AI/LLM dependency.
/// Primary interactive UI calls RebarPreviewProjectionBuilder / RebarGenerationOrchestrator directly.
/// </summary>
public sealed class RebarExtensionSkill : IAIGRAVSkill
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> SupportedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "rebar_preview_selected",
        "rebar_generate_selected",
        "rebar_split_selected",
        "rebar_propagate",
        "rebar_assign_marks",
        "rebar_export_bbs"
    };

    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "RebarExtension",
        Version = "1.0.0",
        Category = "Reinforcement",
        Description = "100% Offline Deterministic Reinforcement Modeling, Stock Splice, Vertical Column Chain, and BBS Engine",
        Source = "External",
        Priority = 1800
    };

    public bool CanHandle(string toolName) => SupportedTools.Contains(toolName);

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        UIDocument? uiDoc = app?.ActiveUIDocument;
        Document? doc = uiDoc?.Document;
        if (doc == null)
            return JsonSerializer.Serialize(new { success = false, error = "No active Revit document." }, JsonOpts);

        RebarPresetProfile activeProfile = RebarSettingsStore.LoadFromDocumentOrDefault(doc);
        List<long> selectedIds = uiDoc!.Selection.GetElementIds().Select(id => id.Value).ToList();

        switch (toolName.ToLowerInvariant())
        {
            case "rebar_preview_selected":
            {
                var req = BuildRequest(selectedIds, activeProfile, argument);
                var preview = RebarPreviewProjectionBuilder.BuildPreview(doc, req);
                return JsonSerializer.Serialize(preview, JsonOpts);
            }
            case "rebar_generate_selected":
            {
                var req = BuildRequest(selectedIds, activeProfile, argument);
                var res = RebarGenerationOrchestrator.GenerateSelected(doc, req);
                return JsonSerializer.Serialize(res, JsonOpts);
            }
            case "rebar_assign_marks":
            {
                using var tx = new Transaction(doc, "AIGRAV Smart Mark Numbering");
                TransactionFailureHandling.SuppressWarnings(tx);
                tx.Start();
                int count = MarkNumberingEngine.AssignMarksInDocument(doc, activeProfile.Global);
                tx.Commit();
                return JsonSerializer.Serialize(new { success = true, updatedMarksCount = count }, JsonOpts);
            }
            case "rebar_split_selected":
            {
                var rebars = uiDoc.Selection.GetElementIds()
                    .Select(id => doc.GetElement(id))
                    .OfType<Rebar>()
                    .Select(r => r.Id.Value)
                    .ToList();
                var res = RebarSplitEngine.SplitRebars(doc, rebars, SplitMethod.Staggered, activeProfile.Global);
                return JsonSerializer.Serialize(res, JsonOpts);
            }
            case "rebar_export_bbs":
            {
                var rows = MarkNumberingEngine.ExtractBbsRowsFromModel(doc, selectedIds);
                return JsonSerializer.Serialize(new { success = true, rowsCount = rows.Count, rows }, JsonOpts);
            }
            default:
                return JsonSerializer.Serialize(new { success = false, error = $"Unknown tool '{toolName}'." }, JsonOpts);
        }
    }

    private static RebarGenerationRequest BuildRequest(List<long> fallbackSelectedIds, RebarPresetProfile profile, string? argument)
    {
        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<RebarGenerationRequest>(argument, JsonOpts);
                if (parsed != null)
                {
                    if (parsed.SelectedHostElementIds.Count == 0)
                        parsed.SelectedHostElementIds.AddRange(fallbackSelectedIds);
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RebarExtensionSkill] BuildRequest fallback: {ex.Message}");
            }
        }
        return new RebarGenerationRequest
        {
            SelectedHostElementIds = fallbackSelectedIds,
            Global = profile.Global,
            Beam = profile.Beam,
            Column = profile.Column,
            Foundation = profile.Foundation,
            Floor = profile.Floor
        };
    }
}

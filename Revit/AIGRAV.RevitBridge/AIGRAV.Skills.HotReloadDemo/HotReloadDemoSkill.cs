using Autodesk.Revit.UI;
using AIGRAV.Contracts;

namespace AIGRAV.Skills.HotReloadDemo;

/// <summary>
/// Small external skill used only to prove that AIGRAV can load and reload
/// a skill assembly while Revit remains open.
/// </summary>
public sealed class HotReloadDemoSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "Hot Reload Demo",
        Version = "2.0",
        Category = "Development",
        Description = "Proof-of-concept external skill for AIGRAV hot reload.",
        Source = "External",
        Priority = 1000
    };

    public bool CanHandle(string toolName)
    {
        return toolName.Equals(
            "aigrav_hot_reload_test",
            StringComparison.OrdinalIgnoreCase);
    }

    public string Execute(
        string toolName,
        UIApplication app,
        string? argument = null)
    {
        return
            "AIGRAV HOT RELOAD TEST PASS\n\n" +
            $"Skill: {Info.Name}\n" +
            $"Version: {Info.Version}\n" +
            $"Argument: {argument ?? ""}\n" +
            "Revit remained open while this external skill was executed.";
    }
}

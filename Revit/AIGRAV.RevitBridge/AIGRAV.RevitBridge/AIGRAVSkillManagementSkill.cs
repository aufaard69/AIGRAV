using AIGRAV.Contracts;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

/// <summary>
/// Built-in management skill used to inspect and reload external skills without restarting Revit.
/// </summary>
public sealed class AIGRAVSkillManagementSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "Skill Management",
        Version = "1.0",
        Category = "System",
        Description = "Controls the AIGRAV external skill registry and hot reload lifecycle.",
        Source = "Built-in",
        Priority = 2000
    };

    public bool CanHandle(string toolName)
    {
        return toolName.Equals("aigrav_skill_status", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_reload_skills", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_discover_skills", StringComparison.OrdinalIgnoreCase);
    }

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (toolName.Equals("aigrav_skill_status", StringComparison.OrdinalIgnoreCase))
        {
            return AIGRAVSkillLoader.GetStatus();
        }

        if (toolName.Equals("aigrav_discover_skills", StringComparison.OrdinalIgnoreCase))
        {
            return AIGRAVSkillLoader.GetDiscoveryReport();
        }

        if (toolName.Equals("aigrav_reload_skills", StringComparison.OrdinalIgnoreCase))
        {
            return AIGRAVSkillLoader.Reload();
        }

        return $"Unknown skill management operation: {toolName}";
    }
}

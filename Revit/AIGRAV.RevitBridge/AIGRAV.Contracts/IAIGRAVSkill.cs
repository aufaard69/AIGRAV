using Autodesk.Revit.UI;

namespace AIGRAV.Contracts;

/// <summary>
/// Contract for an AIGRAV skill.
/// A skill may handle one or more AIGRAV tool names and execute them inside Revit's API context.
/// </summary>
public interface IAIGRAVSkill
{
    AIGRAVSkillInfo Info { get; }

    bool CanHandle(string toolName);

    string Execute(string toolName, UIApplication app, string? argument = null);
}

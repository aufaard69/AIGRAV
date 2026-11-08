using Autodesk.Revit.UI;
using AIGRAV.Contracts;

namespace AIGRAV.RevitBridge;

public static class AIGRAVSkillRegistry
{
    private static readonly object Sync = new();

    private static readonly Dictionary<string, IAIGRAVSkill> Skills =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            Skills.Clear();
            Register(new AIGRAVLegacyBimSkill());
            Register(new AIGRAVSkillManagementSkill());
            _initialized = true;
        }
    }

    public static void Register(IAIGRAVSkill skill)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.Info.Name))
        {
            return;
        }

        lock (Sync)
        {
            Skills[skill.Info.Name] = skill;
        }
    }

    public static bool Remove(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        lock (Sync)
        {
            return Skills.Remove(name);
        }
    }

    public static IReadOnlyList<string> RemoveExternalSkills()
    {
        lock (Sync)
        {
            List<string> names = Skills.Values
                .Where(skill =>
                    string.Equals(skill.Info.Source, "External", StringComparison.OrdinalIgnoreCase))
                .Select(skill => skill.Info.Name)
                .ToList();

            foreach (string name in names)
            {
                Skills.Remove(name);
            }

            return names;
        }
    }

    public static bool TryGet(string name, out IAIGRAVSkill? skill)
    {
        lock (Sync)
        {
            return Skills.TryGetValue(name, out skill);
        }
    }

    public static IReadOnlyList<IAIGRAVSkill> GetAll()
    {
        lock (Sync)
        {
            return Skills.Values
                .OrderByDescending(x => x.Info.Priority)
                .ThenBy(x => x.Info.Category)
                .ThenBy(x => x.Info.Name)
                .ToList();
        }
    }

    public static int Count
    {
        get
        {
            lock (Sync)
            {
                return Skills.Count;
            }
        }
    }

    public static string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (!_initialized)
        {
            Initialize();
        }

        IAIGRAVSkill[] skills;
        lock (Sync)
        {
            skills = Skills.Values
                .OrderByDescending(x => x.Info.Priority)
                .ThenBy(x => x.Info.Name)
                .ToArray();
        }

        foreach (IAIGRAVSkill skill in skills)
        {
            if (!skill.Info.IsEnabled)
            {
                continue;
            }

            bool canHandle;
            try
            {
                canHandle = skill.CanHandle(toolName);
            }
            catch (Exception ex)
            {
                return $"AIGRAV Skill '{skill.Info.Name}' failed while checking tool '{toolName}': {ex.Message}";
            }

            if (!canHandle)
            {
                continue;
            }

            try
            {
                return skill.Execute(toolName, app, argument);
            }
            catch (Exception ex)
            {
                return $"AIGRAV Skill '{skill.Info.Name}' failed to execute '{toolName}': {ex}";
            }
        }

        return $"AIGRAV Skill Router: no skill can handle tool '{toolName}'.";
    }

    public static string GetStatus()
    {
        IAIGRAVSkill[] skills;
        lock (Sync)
        {
            skills = Skills.Values
                .OrderByDescending(x => x.Info.Priority)
                .ThenBy(x => x.Info.Name)
                .ToArray();
        }

        if (skills.Length == 0)
        {
            return "No AIGRAV skills registered.";
        }

        return string.Join(
            Environment.NewLine,
            skills.Select(skill =>
                $"- {skill.Info.Name} | v{skill.Info.Version} | {skill.Info.Category} | " +
                $"Priority {skill.Info.Priority} | " +
                (skill.Info.IsEnabled ? "ACTIVE" : "OFF") +
                $" | {skill.Info.Source}"));
    }
}

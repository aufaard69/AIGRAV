using System;

namespace AIGRAV.Contracts;

public sealed class AIGRAVSkillInfo
{
    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = "1.0.0";

    public string Category { get; init; } = "General";

    public string Description { get; init; } = string.Empty;

    public string Source { get; init; } = "Built-in";

    public int Priority { get; init; } = 0;

    public bool IsEnabled { get; set; } = true;

    public DateTime RegisteredAt { get; init; } = DateTime.Now;
}

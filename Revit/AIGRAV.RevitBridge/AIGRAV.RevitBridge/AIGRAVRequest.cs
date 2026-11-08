using System.Threading.Tasks;

namespace AIGRAV.RevitBridge;

public class AIGRAVRequest
{
    public string ToolName { get; set; } = string.Empty;

    public string? Argument { get; set; }

    public string Result { get; set; } = string.Empty;

    public TaskCompletionSource<string> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
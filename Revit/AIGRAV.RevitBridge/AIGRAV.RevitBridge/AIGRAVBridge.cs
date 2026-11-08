namespace AIGRAV.RevitBridge;

public static class AIGRAVBridge
{
    public static async Task<string> ExecuteToolAsync(
        string toolName,
        string? argument = null,
        int timeoutMilliseconds = 120000)
    {
        if (!AIGRAVRuntime.IsInitialized)
        {
            return "AIGRAV Runtime is not initialized.";
        }

        if (!AIGRAVRuntime.IsEnabled)
        {
            return "AIGRAV is OFF. Enable AIGRAV before executing Revit tools.";
        }

        if (AIGRAVApp.ExternalEvent == null)
        {
            return "AIGRAV ExternalEvent is not available.";
        }

        AIGRAVRequest request = new()
        {
            ToolName = toolName,
            Argument = argument
        };

        AIGRAVRequestManager.Enqueue(request);

        try
        {
            AIGRAVApp.ExternalEvent.Raise();
        }
        catch (Exception ex)
        {
            return $"Failed to raise ExternalEvent: {ex.Message}";
        }

        Task timeoutTask =
            Task.Delay(timeoutMilliseconds);

        Task completedTask =
            await Task.WhenAny(
                request.Completion.Task,
                timeoutTask);

        if (completedTask == timeoutTask)
        {
            return "AIGRAV request timed out.";
        }

        try
        {
            return await request.Completion.Task;
        }
        catch (Exception ex)
        {
            return
                $"AIGRAV tool execution failed: {ex.Message}";
        }
    }
}

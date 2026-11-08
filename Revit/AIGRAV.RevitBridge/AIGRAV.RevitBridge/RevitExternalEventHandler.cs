using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public class RevitExternalEventHandler : IExternalEventHandler
{
    public void Execute(UIApplication app)
    {
        RevitContext.Initialize(app);

        while (AIGRAVRequestManager.TryDequeue(
            out AIGRAVRequest? request))
        {
            if (request == null)
            {
                continue;
            }

            try
            {
                if (!AIGRAVRuntime.IsEnabled)
                {
                    request.Completion.TrySetResult(
                        "AIGRAV is OFF. Request was not executed.");

                    continue;
                }

                string result =
                    AIGRAVSkillRegistry.Execute(
                        request.ToolName,
                        app,
                        request.Argument);

                request.Completion.TrySetResult(result);
            }
            catch (Exception ex)
            {
                request.Completion.TrySetException(ex);
            }
        }
    }

    public string GetName()
    {
        return "AIGRAV Revit External Event Handler";
    }
}

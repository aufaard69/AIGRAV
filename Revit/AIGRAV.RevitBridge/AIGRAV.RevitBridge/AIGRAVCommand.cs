using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

[Transaction(TransactionMode.Manual)]
public class AIGRAVCommand : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        Autodesk.Revit.DB.ElementSet elements)
    {
        AIGRAVApp.ToggleAIGRAV();

        return Result.Succeeded;
    }
}

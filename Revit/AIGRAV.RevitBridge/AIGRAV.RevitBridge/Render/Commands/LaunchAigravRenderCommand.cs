using System;
using Aigrav.RenderStudio.UI.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge.Render.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class LaunchAigravRenderCommand : IExternalCommand
{
    private static MainRendererWindow? _studioWindow;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (_studioWindow != null && _studioWindow.IsLoaded)
            {
                _studioWindow.Activate();
                return Result.Succeeded;
            }

            _studioWindow = new MainRendererWindow(commandData.Application);
            _studioWindow.Closed += (_, _) => _studioWindow = null;
            _studioWindow.Show();
            _studioWindow.Activate();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.ToString();
            return Result.Failed;
        }
    }
}

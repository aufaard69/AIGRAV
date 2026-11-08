using Autodesk.Revit.DB;

namespace AIGRAV.RevitBridge;

public static class RevitViewTool
{
    public static string GetActiveView(Document document)
    {
        if (document == null)
        {
            return "No active Revit document.";
        }

        View? view = document.ActiveView;

        if (view == null)
        {
            return "No active view.";
        }

        return
            $"View Name: {view.Name}\n" +
            $"View Type: {view.ViewType}\n" +
            $"View Id: {view.Id.Value}";
    }
}
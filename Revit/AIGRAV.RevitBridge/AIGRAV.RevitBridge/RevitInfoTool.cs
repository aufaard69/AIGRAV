using Autodesk.Revit.DB;

namespace AIGRAV.RevitBridge;

public static class RevitInfoTool
{
    public static string GetInfo(Document document)
    {
        if (document == null)
        {
            return "No active Revit document.";
        }

        string projectName = document.Title;
        string activeView = document.ActiveView?.Name ?? "Unknown";
        string filePath = document.PathName;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            filePath = "Unsaved Project";
        }

        return
            $"Project: {projectName}\n" +
            $"Active View: {activeView}\n" +
            $"File Path: {filePath}";
    }
}
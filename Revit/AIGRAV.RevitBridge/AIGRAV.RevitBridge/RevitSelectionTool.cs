using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text;

namespace AIGRAV.RevitBridge;

public static class RevitSelectionTool
{
    public static string GetSelectedElements(UIDocument uiDocument)
    {
        if (uiDocument == null)
        {
            return "No active Revit UI document.";
        }

        Document document = uiDocument.Document;

        ICollection<ElementId> selectedIds =
            uiDocument.Selection.GetElementIds();

        if (selectedIds.Count == 0)
        {
            return "No elements are currently selected.";
        }

        StringBuilder result = new();

        result.AppendLine($"Selected Elements: {selectedIds.Count}");

        foreach (ElementId id in selectedIds)
        {
            Element? element = document.GetElement(id);

            if (element == null)
            {
                continue;
            }

            string category =
                element.Category?.Name ?? "No Category";

            string name =
                element.Name ?? "Unnamed";

            result.AppendLine(
                $"- Id: {id.Value} | " +
                $"Category: {category} | " +
                $"Name: {name}"
            );
        }

        return result.ToString();
    }
}
using Autodesk.Revit.DB;
using System.Text;

namespace AIGRAV.RevitBridge;

public static class RevitElementTool
{
    public static string GetElement(
        Document document,
        long elementId)
    {
        if (document == null)
        {
            return "No active Revit document.";
        }

        ElementId id = new ElementId(elementId);

        Element? element = document.GetElement(id);

        if (element == null)
        {
            return $"Element with Id {elementId} was not found.";
        }

        StringBuilder result = new();

        string category =
            element.Category?.Name ?? "No Category";

        string name =
            element.Name ?? "Unnamed";

        result.AppendLine($"Element Id: {elementId}");
        result.AppendLine($"Category: {category}");
        result.AppendLine($"Name: {name}");
        result.AppendLine($"Class: {element.GetType().Name}");

        Parameter? typeParameter =
            element.get_Parameter(
                BuiltInParameter.ELEM_TYPE_PARAM);

        if (typeParameter != null &&
            typeParameter.HasValue &&
            typeParameter.StorageType == StorageType.ElementId)
        {
            ElementId typeId =
                typeParameter.AsElementId();

            if (typeId != ElementId.InvalidElementId)
            {
                Element? typeElement =
                    document.GetElement(typeId);

                if (typeElement != null)
                {
                    result.AppendLine(
                        $"Type: {typeElement.Name}"
                    );
                }
            }
        }

        return result.ToString();
    }
}
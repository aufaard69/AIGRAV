using Autodesk.Revit.DB;
using System.Text;

namespace AIGRAV.RevitBridge;

public static class RevitElementDataTool
{
    public static string GetParameters(
        Document document,
        long elementId)
    {
        Element? element =
            document.GetElement(
                new ElementId(elementId));

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        StringBuilder result = new();

        result.AppendLine(
            $"Element Id: {elementId}");

        result.AppendLine(
            $"Name: {element.Name}");

        result.AppendLine(
            $"Category: {element.Category?.Name ?? "No Category"}");

        foreach (Parameter parameter in element.Parameters)
        {
            string value;

            try
            {
                value =
                    parameter.StorageType switch
                    {
                        StorageType.String =>
                            parameter.AsString() ?? "",

                        StorageType.Integer =>
                            parameter.AsInteger().ToString(),

                        StorageType.Double =>
                            parameter.AsDouble().ToString(
                                System.Globalization.CultureInfo.InvariantCulture),

                        StorageType.ElementId =>
                            parameter.AsElementId().Value.ToString(),

                        _ =>
                            ""
                    };
            }
            catch
            {
                value = "";
            }

            string parameterName =
                parameter.Definition?.Name ??
                "Unnamed";

            result.AppendLine(
                $"{parameterName} = {value}");
        }

        return result.ToString();
    }

    public static string GetLocation(
        Document document,
        long elementId)
    {
        Element? element =
            document.GetElement(
                new ElementId(elementId));

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        Location? location =
            element.Location;

        if (location == null)
        {
            return "Element has no Location.";
        }

        if (location is LocationPoint point)
        {
            return
                $"Location Point: " +
                $"X={ToMeters(point.Point.X):F3} m, " +
                $"Y={ToMeters(point.Point.Y):F3} m, " +
                $"Z={ToMeters(point.Point.Z):F3} m";
        }

        if (location is LocationCurve curve)
        {
            XYZ start =
                curve.Curve.GetEndPoint(0);

            XYZ end =
                curve.Curve.GetEndPoint(1);

            return
                $"Curve Start: {PointText(start)}\n" +
                $"Curve End: {PointText(end)}\n" +
                $"Length: {ToMeters(curve.Curve.Length):F3} m";
        }

        return
            $"Location Type: {location.GetType().Name}";
    }

    public static string GetBoundingBox(
        Document document,
        long elementId)
    {
        Element? element =
            document.GetElement(
                new ElementId(elementId));

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        BoundingBoxXYZ? bbox =
            element.get_BoundingBox(null);

        if (bbox == null)
        {
            return "Bounding box unavailable.";
        }

        return
            $"Min: {PointText(bbox.Min)}\n" +
            $"Max: {PointText(bbox.Max)}";
    }

    public static string GetType(
        Document document,
        long elementId)
    {
        Element? element =
            document.GetElement(
                new ElementId(elementId));

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        ElementId typeId =
            element.GetTypeId();

        if (typeId == ElementId.InvalidElementId)
        {
            return "Element has no type.";
        }

        Element? type =
            document.GetElement(typeId);

        if (type == null)
        {
            return $"Type Id: {typeId.Value}";
        }

        return
            $"Type Id: {typeId.Value}\n" +
            $"Type Name: {type.Name}\n" +
            $"Type Class: {type.GetType().Name}";
    }

    public static string GetFamily(
        Document document,
        long elementId)
    {
        Element? element =
            document.GetElement(
                new ElementId(elementId));

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        if (element is FamilyInstance familyInstance)
        {
            FamilySymbol? symbol =
                familyInstance.Symbol;

            if (symbol != null)
            {
                Family? family =
                    symbol.Family;

                if (family != null)
                {
                    return
                        $"Family: {family.Name}\n" +
                        $"Symbol: {symbol.Name}";
                }

                return
                    $"Symbol: {symbol.Name}";
            }
        }

        ElementId typeId =
            element.GetTypeId();

        Element? type =
            document.GetElement(typeId);

        return type?.Name != null
            ? $"Type: {type.Name}"
            : "Family information unavailable.";
    }

    public static string GetLevel(
        Document document,
        long elementId)
    {
        Element? element =
            document.GetElement(
                new ElementId(elementId));

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        Parameter? levelParameter =
            element.get_Parameter(
                BuiltInParameter.LEVEL_PARAM);

        if (levelParameter != null &&
            levelParameter.HasValue &&
            levelParameter.StorageType ==
                StorageType.ElementId)
        {
            ElementId levelId =
                levelParameter.AsElementId();

            Element? level =
                document.GetElement(levelId);

            if (level != null)
            {
                return
                    $"Level Id: {levelId.Value}\n" +
                    $"Level Name: {level.Name}";
            }
        }

        if (element is FamilyInstance familyInstance)
        {
            Level? level =
                familyInstance
                    .Host
                    ?.Document
                    .GetElement(
                        familyInstance.LevelId)
                as Level;

            if (level != null)
            {
                return
                    $"Level Id: {level.Id.Value}\n" +
                    $"Level Name: {level.Name}";
            }
        }

        return "Level information unavailable.";
    }

    public static string GetDimensions(
        Document document,
        long elementId)
    {
        Element? element =
            document.GetElement(
                new ElementId(elementId));

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        BoundingBoxXYZ? bbox =
            element.get_BoundingBox(null);

        if (bbox == null)
        {
            return "Dimensions unavailable.";
        }

        double width =
            Math.Abs(
                bbox.Max.X -
                bbox.Min.X);

        double depth =
            Math.Abs(
                bbox.Max.Y -
                bbox.Min.Y);

        double height =
            Math.Abs(
                bbox.Max.Z -
                bbox.Min.Z);

        return
            $"Bounding Width: {ToMeters(width):F3} m\n" +
            $"Bounding Depth: {ToMeters(depth):F3} m\n" +
            $"Bounding Height: {ToMeters(height):F3} m";
    }

    public static string GetMaterials(
        Document document,
        long elementId)
    {
        Element? element =
            document.GetElement(
                new ElementId(elementId));

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        ICollection<ElementId> materialIds =
            element.GetMaterialIds(false);

        if (materialIds.Count == 0)
        {
            return "No materials found.";
        }

        StringBuilder result = new();

        result.AppendLine(
            $"Materials: {materialIds.Count}");

        foreach (ElementId materialId in materialIds)
        {
            Material? material =
                document.GetElement(materialId)
                as Material;

            if (material != null)
            {
                result.AppendLine(
                    $"- {materialId.Value} | " +
                    material.Name);
            }
        }

        return result.ToString();
    }

    private static double ToMeters(
        double internalValue)
    {
        return UnitUtils.ConvertFromInternalUnits(
            internalValue,
            UnitTypeId.Meters);
    }

    private static string PointText(
        XYZ point)
    {
        return
            $"({ToMeters(point.X):F3}, " +
            $"{ToMeters(point.Y):F3}, " +
            $"{ToMeters(point.Z):F3}) m";
    }
}
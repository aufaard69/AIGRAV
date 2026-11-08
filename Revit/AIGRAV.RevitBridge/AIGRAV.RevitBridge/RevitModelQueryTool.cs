using Autodesk.Revit.DB;
using System.Text;

namespace AIGRAV.RevitBridge;

public static class RevitModelQueryTool
{
    public static string GetLevels(Document document)
    {
        var levels =
            new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .ToList();

        StringBuilder result = new();

        result.AppendLine($"Levels: {levels.Count}");

        foreach (Level level in levels)
        {
            result.AppendLine(
                $"- Id: {level.Id.Value} | " +
                $"Name: {level.Name} | " +
                $"Elevation: {level.Elevation:F3} ft"
            );
        }

        return result.ToString();
    }


    public static string GetGrids(Document document)
    {
        var grids =
            new FilteredElementCollector(document)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .OrderBy(x => x.Name)
                .ToList();

        StringBuilder result = new();

        result.AppendLine($"Grids: {grids.Count}");

        foreach (Grid grid in grids)
        {
            result.AppendLine(
                $"- Id: {grid.Id.Value} | " +
                $"Name: {grid.Name}"
            );
        }

        return result.ToString();
    }


    public static string GetWalls(Document document)
    {
        var walls =
            new FilteredElementCollector(document)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

        StringBuilder result = new();

        result.AppendLine($"Walls: {walls.Count}");

        foreach (Wall wall in walls)
        {
            string typeName =
                document.GetElement(wall.GetTypeId())?.Name
                ?? "Unknown";

            result.AppendLine(
                $"- Id: {wall.Id.Value} | " +
                $"Name: {wall.Name} | " +
                $"Type: {typeName}"
            );
        }

        return result.ToString();
    }


    public static string GetFloors(Document document)
    {
        var floors =
            new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .ToElements();

        StringBuilder result = new();

        result.AppendLine($"Floors: {floors.Count}");

        foreach (Element floor in floors)
        {
            result.AppendLine(
                $"- Id: {floor.Id.Value} | " +
                $"Name: {floor.Name}"
            );
        }

        return result.ToString();
    }


    public static string GetStructuralColumns(
        Document document)
    {
        var columns =
            new FilteredElementCollector(document)
                .OfCategory(
                    BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .ToElements();

        StringBuilder result = new();

        result.AppendLine(
            $"Structural Columns: {columns.Count}"
        );

        foreach (Element column in columns)
        {
            result.AppendLine(
                $"- Id: {column.Id.Value} | " +
                $"Name: {column.Name}"
            );
        }

        return result.ToString();
    }


    public static string GetStructuralFraming(
        Document document)
    {
        var framing =
            new FilteredElementCollector(document)
                .OfCategory(
                    BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .ToElements();

        StringBuilder result = new();

        result.AppendLine(
            $"Structural Framing: {framing.Count}"
        );

        foreach (Element beam in framing)
        {
            result.AppendLine(
                $"- Id: {beam.Id.Value} | " +
                $"Name: {beam.Name}"
            );
        }

        return result.ToString();
    }


    public static string GetDoors(Document document)
    {
        var doors =
            new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .ToElements();

        StringBuilder result = new();

        result.AppendLine($"Doors: {doors.Count}");

        foreach (Element door in doors)
        {
            result.AppendLine(
                $"- Id: {door.Id.Value} | " +
                $"Name: {door.Name}"
            );
        }

        return result.ToString();
    }


    public static string GetWindows(Document document)
    {
        var windows =
            new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType()
                .ToElements();

        StringBuilder result = new();

        result.AppendLine($"Windows: {windows.Count}");

        foreach (Element window in windows)
        {
            result.AppendLine(
                $"- Id: {window.Id.Value} | " +
                $"Name: {window.Name}"
            );
        }

        return result.ToString();
    }


    public static string GetRooms(Document document)
    {
        var rooms =
            new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .ToElements();

        StringBuilder result = new();

        result.AppendLine($"Rooms: {rooms.Count}");

        foreach (Element room in rooms)
        {
            result.AppendLine(
                $"- Id: {room.Id.Value} | " +
                $"Name: {room.Name}"
            );
        }

        return result.ToString();
    }


    public static string GetViews(Document document)
    {
        var views =
            new FilteredElementCollector(document)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .OrderBy(v => v.ViewType)
                .ThenBy(v => v.Name)
                .ToList();

        StringBuilder result = new();

        result.AppendLine($"Views: {views.Count}");

        foreach (View view in views)
        {
            result.AppendLine(
                $"- Id: {view.Id.Value} | " +
                $"Name: {view.Name} | " +
                $"Type: {view.ViewType}"
            );
        }

        return result.ToString();
    }
}
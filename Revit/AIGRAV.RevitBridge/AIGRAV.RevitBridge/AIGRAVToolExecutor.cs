using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Globalization;
using System.Text;
using System.Text.Json;

// Revit StructuralType yang benar untuk Revit 2026.
using StructuralType =
    Autodesk.Revit.DB.Structure.StructuralType;

namespace AIGRAV.RevitBridge;

public static class AIGRAVToolExecutor
{
    // ============================================================
    // PUBLIC ENTRY POINT
    // ============================================================

    public static string Execute(
        string toolName,
        UIApplication app,
        string? argument = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return "AIGRAV Error: Tool name is empty.";
            }

            return Run(
                toolName.Trim().ToLowerInvariant(),
                app,
                argument);
        }
        catch (Exception ex)
        {
            return
                $"AIGRAV tool execution failed:\n{ex}";
        }
    }

    // ============================================================
    // MAIN ROUTER
    // ============================================================

    private static string Run(
        string op,
        UIApplication app,
        string? argument)
    {
        UIDocument? uiDocument =
            app.ActiveUIDocument;

        Document? document =
            uiDocument?.Document;

        if (document == null)
        {
            return "No active Revit document.";
        }

        if (uiDocument == null)
        {
            return "No active Revit UI document.";
        }

        // ========================================================
        // BASIC CONTEXT
        // ========================================================

        switch (op)
        {
            case "aigrav_get_revit_info":
            case "get_project_info":
            case "get_revit_info":

                return RevitInfoTool.GetInfo(
                    document);

            case "aigrav_get_active_view":
            case "get_active_view":

                return RevitViewTool.GetActiveView(
                    document);

            case "aigrav_get_selection":
            case "get_selection":

                return RevitSelectionTool.GetSelectedElements(
                    uiDocument);

            case "aigrav_get_element":
            case "get_element":

                if (!TryParseLong(
                        argument,
                        out long elementId))
                {
                    return "Invalid element id.";
                }

                return RevitElementTool.GetElement(
                    document,
                    elementId);
        }

        // ========================================================
        // ELEMENT INTELLIGENCE
        // ========================================================

        if (op.StartsWith("get_element_"))
        {
            if (!TryParseLong(
                    argument,
                    out long elementId))
            {
                return "Invalid element id.";
            }

            return op switch
            {
                "get_element_parameters" =>
                    GetElementParameters(
                        document,
                        elementId),

                "get_element_location" =>
                    GetElementLocation(
                        document,
                        elementId),

                "get_element_bbox" =>
                    GetElementBoundingBox(
                        document,
                        elementId),

                "get_element_type" =>
                    GetElementType(
                        document,
                        elementId),

                "get_element_family" =>
                    GetElementFamily(
                        document,
                        elementId),

                "get_element_level" =>
                    GetElementLevel(
                        document,
                        elementId),

                "get_element_dimensions" =>
                    GetElementDimensions(
                        document,
                        elementId),

                "get_element_materials" =>
                    GetElementMaterials(
                        document,
                        elementId),

                _ =>
                    $"Element tool '{op}' is registered but specialized implementation is pending."
            };
        }

        // ========================================================
        // MODEL QUERY
        // ========================================================

        if (CategoryMap.TryGetValue(
                op,
                out BuiltInCategory category))
        {
            return QueryCategory(
                document,
                category,
                PrettyName(op));
        }

        switch (op)
        {
            case "get_levels":
                return GetLevels(document);

            case "get_grids":
                return GetGrids(document);

            case "get_families":
                return GetFamilies(document);

            case "get_family_types":
                return GetFamilyTypes(document);

            case "get_walls":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Walls,
                    "Walls");

            case "get_floors":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Floors,
                    "Floors");

            case "get_roofs":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Roofs,
                    "Roofs");

            case "get_ceilings":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Ceilings,
                    "Ceilings");

            case "get_doors":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Doors,
                    "Doors");

            case "get_windows":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Windows,
                    "Windows");

            case "get_rooms":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Rooms,
                    "Rooms");

            case "get_spaces":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_MEPSpaces,
                    "Spaces");

            case "get_structural_columns":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_StructuralColumns,
                    "Structural Columns");

            case "get_structural_framing":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_StructuralFraming,
                    "Structural Framing");

            case "get_foundations":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_StructuralFoundation,
                    "Structural Foundations");

            case "get_rebar":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Rebar,
                    "Rebar");

            case "get_stairs":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Stairs,
                    "Stairs");

            case "get_ramps":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Ramps,
                    "Ramps");

            case "get_railings":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_StairsRailing,
                    "Railings");

            case "get_mep_equipment":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_MechanicalEquipment,
                    "Mechanical Equipment");

            case "get_ducts":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_DuctCurves,
                    "Ducts");

            case "get_pipes":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_PipeCurves,
                    "Pipes");

            case "get_cable_trays":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_CableTray,
                    "Cable Trays");

            case "get_conduits":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_Conduit,
                    "Conduits");

            case "get_lighting_fixtures":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_LightingFixtures,
                    "Lighting Fixtures");

            case "get_electrical_equipment":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_ElectricalEquipment,
                    "Electrical Equipment");

            case "get_plumbing_fixtures":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_PlumbingFixtures,
                    "Plumbing Fixtures");

            case "get_fire_alarm_devices":
                return QueryCategory(
                    document,
                    BuiltInCategory.OST_FireAlarmDevices,
                    "Fire Alarm Devices");
        }

        // ========================================================
        // DOCUMENT
        // ========================================================

        switch (op)
        {
            case "get_document_units":

                return
                    "AIGRAV geometry inputs use meters. " +
                    "Angles use degrees. " +
                    "Revit internally uses feet/radians.";

            case "get_worksharing_info":

                return document.IsWorkshared
                    ? "Document is workshared."
                    : "Document is not workshared.";

            case "get_project_location":

                return
                    $"Project Location: " +
                    $"{document.ActiveProjectLocation.Name}";

            case "get_phases":

                return GetPhases(document);

            case "get_design_options":

                return GetDesignOptions(document);

            case "get_worksets":

                return GetWorksets(document);

            case "get_linked_documents":

                return GetLinkedDocuments(document);

            case "regenerate":

                document.Regenerate();

                return "Document regenerated.";

            case "save_document":

                document.Save();

                return "Document saved.";

            case "get_warnings":

                return GetWarnings(document);

            case "count_elements":

                return CountElements(
                    document,
                    argument);
        }

        // ========================================================
        // VIEW
        // ========================================================

        switch (op)
        {
            case "activate_view":

                return ActivateView(
                    app,
                    argument);

            case "rename_view":

                return RenameView(
                    document,
                    argument);

            case "duplicate_view":

                return DuplicateView(
                    document,
                    argument);

            case "set_view_scale":

                return SetViewScale(
                    document,
                    argument);

            case "set_view_detail_level":

                return SetViewDetailLevel(
                    document,
                    argument);

            case "hide_elements_in_view":

                return SetViewVisibility(
                    document,
                    argument,
                    true);

            case "unhide_elements_in_view":

                return SetViewVisibility(
                    document,
                    argument,
                    false);

            case "isolate_elements_in_view":

                return IsolateElements(
                    document,
                    argument);

            case "get_view_elements":

                return GetViewElements(
                    document,
                    argument);
        }

        // ========================================================
        // CREATE
        // ========================================================

        switch (op)
        {
            case "create_level":

                return CreateLevel(
                    document,
                    argument);

            case "create_grid":

                return CreateGrid(
                    document,
                    argument);

            case "create_wall":

                return CreateWall(
                    document,
                    argument);

            case "create_floor":

                return CreateFloor(
                    document,
                    argument);

            case "create_column":

                return CreateFamilyInstance(
                    document,
                    argument,
                    BuiltInCategory.OST_StructuralColumns,
                    StructuralType.Column);

            case "create_beam":

                return CreateFamilyInstance(
                    document,
                    argument,
                    BuiltInCategory.OST_StructuralFraming,
                    StructuralType.Beam);

            case "place_family_instance":

                return CreateFamilyInstance(
                    document,
                    argument,
                    GetRequestedCategory(argument),
                    StructuralType.NonStructural);

            case "create_generic_model":

                return CreateFamilyInstance(
                    document,
                    argument,
                    BuiltInCategory.OST_GenericModel,
                    StructuralType.NonStructural);

            case "create_sheet":
            case "aigrav_create_sheet":

                return CreateSheet(
                    document,
                    argument);

            case "create_schedule":
            case "aigrav_create_schedule":

                return CreateSchedule(
                    document,
                    argument);
        }

        // ========================================================
        // MODIFY
        // ========================================================

        switch (op)
        {
            case "set_parameter":

                return SetParameter(
                    document,
                    argument);

            case "move_element":

                return MoveElement(
                    document,
                    argument);

            case "move_elements":

                return MoveElements(
                    document,
                    argument);

            case "offset_element":

                return MoveElement(
                    document,
                    argument);

            case "copy_element":

                return CopyElement(
                    document,
                    argument);

            case "copy_elements":

                return CopyElements(
                    document,
                    argument);

            case "rotate_element":

                return RotateElement(
                    document,
                    argument);

            case "rotate_elements":

                return RotateElements(
                    document,
                    argument);

            case "mirror_element":

                return MirrorElements(
                    document,
                    argument,
                    false);

            case "mirror_elements":

                return MirrorElements(
                    document,
                    argument,
                    true);

            case "delete_element":

                return DeleteElement(
                    document,
                    argument);

            case "delete_elements":

                return DeleteElements(
                    document,
                    argument);

            case "pin_element":

                return SetPinned(
                    document,
                    argument,
                    true);

            case "unpin_element":

                return SetPinned(
                    document,
                    argument,
                    false);

            case "join_geometry":

                return JoinGeometry(
                    document,
                    argument,
                    true);

            case "unjoin_geometry":

                return JoinGeometry(
                    document,
                    argument,
                    false);

            case "switch_join_order":

                return SwitchJoinOrder(
                    document,
                    argument);

            case "are_elements_joined":

                return AreElementsJoined(
                    document,
                    argument);

            case "allow_join_at_end":

                return SetWallJoin(
                    document,
                    argument,
                    true);

            case "disallow_join_at_end":

                return SetWallJoin(
                    document,
                    argument,
                    false);

            case "trim_extend_curves":

                return
                    "Trim/Extend is registered. " +
                    "Revit 2026 geometry implementation is pending.";

            case "change_type":

                return ChangeElementType(
                    document,
                    argument);

            case "change_level":

                return ChangeElementLevel(
                    document,
                    argument);
        }

        // ========================================================
        // ANALYSIS
        // ========================================================

        switch (op)
        {
            case "measure_distance":

                return MeasureDistance(
                    argument);

            case "measure_element_length":

                return MeasureElementLength(
                    document,
                    argument);

            case "get_curve_endpoints":

                return GetCurveEndpoints(
                    document,
                    argument);

            case "validate_element_exists":

                return ValidateElements(
                    document,
                    argument);

            case "validate_levels":

                return ValidateLevels(
                    document);

            case "get_element_count":

                return CountElements(
                    document,
                    argument);
        }

        // ========================================================
        // REGISTERED BUT SPECIALIZED IMPLEMENTATION PENDING
        // ========================================================

        return
            $"AIGRAV tool '{op}' is registered, " +
            "but its specialized Revit API implementation " +
            "is currently pending.";
    }

    // ============================================================
    // CATEGORY MAP
    // ============================================================

    private static readonly
        Dictionary<string, BuiltInCategory> CategoryMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["get_walls"] =
                BuiltInCategory.OST_Walls,

            ["get_sheets"] =
                BuiltInCategory.OST_Sheets,

            ["get_floors"] =
                BuiltInCategory.OST_Floors,

            ["get_roofs"] =
                BuiltInCategory.OST_Roofs,

            ["get_ceilings"] =
                BuiltInCategory.OST_Ceilings,

            ["get_doors"] =
                BuiltInCategory.OST_Doors,

            ["get_windows"] =
                BuiltInCategory.OST_Windows,

            ["get_rooms"] =
                BuiltInCategory.OST_Rooms,

            ["get_spaces"] =
                BuiltInCategory.OST_MEPSpaces,

            ["get_areas"] =
                BuiltInCategory.OST_Areas,

            ["get_structural_columns"] =
                BuiltInCategory.OST_StructuralColumns,

            ["get_structural_framing"] =
                BuiltInCategory.OST_StructuralFraming,

            ["get_foundations"] =
                BuiltInCategory.OST_StructuralFoundation,

            ["get_rebar"] =
                BuiltInCategory.OST_Rebar,

            ["get_stairs"] =
                BuiltInCategory.OST_Stairs,

            ["get_ramps"] =
                BuiltInCategory.OST_Ramps,

            ["get_railings"] =
                BuiltInCategory.OST_StairsRailing,

            ["get_mep_equipment"] =
                BuiltInCategory.OST_MechanicalEquipment,

            ["get_ducts"] =
                BuiltInCategory.OST_DuctCurves,

            ["get_pipes"] =
                BuiltInCategory.OST_PipeCurves,

            ["get_cable_trays"] =
                BuiltInCategory.OST_CableTray,

            ["get_conduits"] =
                BuiltInCategory.OST_Conduit,

            ["get_lighting_fixtures"] =
                BuiltInCategory.OST_LightingFixtures,

            ["get_electrical_equipment"] =
                BuiltInCategory.OST_ElectricalEquipment,

            ["get_plumbing_fixtures"] =
                BuiltInCategory.OST_PlumbingFixtures,

            ["get_fire_alarm_devices"] =
                BuiltInCategory.OST_FireAlarmDevices
        };

    // ============================================================
    // ELEMENT DATA
    // ============================================================

    private static string GetElementParameters(
        Document document,
        long elementId)
    {
        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        StringBuilder result =
            new();

        result.AppendLine(
            $"Element Id: {elementId}");

        result.AppendLine(
            $"Name: {element.Name}");

        result.AppendLine(
            $"Category: " +
            $"{element.Category?.Name ?? "No Category"}");

        result.AppendLine(
            $"Class: {element.GetType().Name}");

        result.AppendLine();

        foreach (Parameter parameter in element.Parameters)
        {
            string name =
                parameter.Definition?.Name ??
                "Unnamed";

            string value;

            try
            {
                value =
                    parameter.StorageType switch
                    {
                        StorageType.String =>
                            parameter.AsString() ?? "",

                        StorageType.Integer =>
                            parameter.AsInteger()
                                .ToString(
                                    CultureInfo.InvariantCulture),

                        StorageType.Double =>
                            parameter.AsDouble()
                                .ToString(
                                    CultureInfo.InvariantCulture),

                        StorageType.ElementId =>
                            parameter.AsElementId()
                                .Value
                                .ToString(
                                    CultureInfo.InvariantCulture),

                        _ => ""
                    };
            }
            catch
            {
                value = "";
            }

            result.AppendLine(
                $"{name} = {value}");
        }

        return result.ToString();
    }

    private static string GetElementLocation(
        Document document,
        long elementId)
    {
        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        Location? location =
            element.Location;

        if (location == null)
        {
            return "Element has no location.";
        }

        if (location is LocationPoint point)
        {
            return
                $"Location Point: {PointText(point.Point)}";
        }

        if (location is LocationCurve curve)
        {
            return
                $"Start: " +
                $"{PointText(curve.Curve.GetEndPoint(0))}\n" +
                $"End: " +
                $"{PointText(curve.Curve.GetEndPoint(1))}\n" +
                $"Length: " +
                $"{ToMeters(curve.Curve.Length):F3} m";
        }

        return
            $"Location Type: {location.GetType().Name}";
    }

    private static string GetElementBoundingBox(
        Document document,
        long elementId)
    {
        Element? element =
            GetElement(
                document,
                elementId);

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

    private static string GetElementType(
        Document document,
        long elementId)
    {
        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        ElementId typeId =
            element.GetTypeId();

        if (typeId ==
            ElementId.InvalidElementId)
        {
            return "Element has no type.";
        }

        Element? type =
            document.GetElement(typeId);

        if (type == null)
        {
            return
                $"Type Id: {typeId.Value}";
        }

        return
            $"Type Id: {typeId.Value}\n" +
            $"Type Name: {type.Name}\n" +
            $"Type Class: {type.GetType().Name}";
    }

    private static string GetElementFamily(
        Document document,
        long elementId)
    {
        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        if (element is FamilyInstance instance)
        {
            FamilySymbol? symbol =
                instance.Symbol;

            if (symbol != null)
            {
                Family? family =
                    symbol.Family;

                if (family != null)
                {
                    return
                        $"Family: {family.Name}\n" +
                        $"Type: {symbol.Name}";
                }

                return
                    $"Type: {symbol.Name}";
            }
        }

        ElementId typeId =
            element.GetTypeId();

        Element? type =
            document.GetElement(typeId);

        return type != null
            ? $"Type: {type.Name}"
            : "Family information unavailable.";
    }

    private static string GetElementLevel(
        Document document,
        long elementId)
    {
        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        if (element is FamilyInstance familyInstance)
        {
            ElementId levelId =
                familyInstance.LevelId;

            if (levelId !=
                ElementId.InvalidElementId)
            {
                Level? level =
                    document.GetElement(levelId)
                    as Level;

                if (level != null)
                {
                    return
                        $"Level Id: {level.Id.Value}\n" +
                        $"Level Name: {level.Name}";
                }
            }
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

            Level? level =
                document.GetElement(levelId)
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

    private static string GetElementDimensions(
        Document document,
        long elementId)
    {
        Element? element =
            GetElement(
                document,
                elementId);

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

        double x =
            Math.Abs(
                bbox.Max.X -
                bbox.Min.X);

        double y =
            Math.Abs(
                bbox.Max.Y -
                bbox.Min.Y);

        double z =
            Math.Abs(
                bbox.Max.Z -
                bbox.Min.Z);

        return
            $"X: {ToMeters(x):F3} m\n" +
            $"Y: {ToMeters(y):F3} m\n" +
            $"Z: {ToMeters(z):F3} m";
    }

    private static string GetElementMaterials(
        Document document,
        long elementId)
    {
        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return $"Element {elementId} not found.";
        }

        ICollection<ElementId> ids =
            element.GetMaterialIds(false);

        if (ids.Count == 0)
        {
            return "No materials found.";
        }

        StringBuilder result =
            new();

        result.AppendLine(
            $"Materials: {ids.Count}");

        foreach (ElementId id in ids)
        {
            Material? material =
                document.GetElement(id)
                as Material;

            if (material != null)
            {
                result.AppendLine(
                    $"- {id.Value} | " +
                    material.Name);
            }
        }

        return result.ToString();
    }

    // ============================================================
    // MODEL QUERY
    // ============================================================

    private static string QueryCategory(
        Document document,
        BuiltInCategory category,
        string label)
    {
        IList<Element> elements =
            new FilteredElementCollector(document)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();

        StringBuilder result =
            new();

        result.AppendLine(
            $"{label}: {elements.Count}");

        foreach (Element element in elements)
        {
            result.AppendLine(
                $"- Id: {element.Id.Value} | " +
                $"Name: {element.Name ?? "Unnamed"} | " +
                $"TypeId: {element.GetTypeId().Value}");
        }

        return result.ToString();
    }

    private static string GetLevels(
        Document document)
    {
        var levels =
            new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(
                    x => x.Elevation)
                .ToList();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Levels: {levels.Count}");

        foreach (Level level in levels)
        {
            result.AppendLine(
                $"- Id: {level.Id.Value} | " +
                $"Name: {level.Name} | " +
                $"Elevation: " +
                $"{ToMeters(level.Elevation):F3} m");
        }

        return result.ToString();
    }

    private static string GetGrids(
        Document document)
    {
        var grids =
            new FilteredElementCollector(document)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Grids: {grids.Count}");

        foreach (Grid grid in grids)
        {
            result.AppendLine(
                $"- Id: {grid.Id.Value} | " +
                $"Name: {grid.Name}");
        }

        return result.ToString();
    }

    private static string GetFamilies(
        Document document)
    {
        var families =
            new FilteredElementCollector(document)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Families: {families.Count}");

        foreach (Family family in families)
        {
            result.AppendLine(
                $"- Id: {family.Id.Value} | " +
                $"{family.Name}");
        }

        return result.ToString();
    }

    private static string GetFamilyTypes(
        Document document)
    {
        var symbols =
            new FilteredElementCollector(document)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Family Types: {symbols.Count}");

        foreach (FamilySymbol symbol in symbols)
        {
            string familyName =
                symbol.Family?.Name ??
                "Unknown Family";

            result.AppendLine(
                $"- Id: {symbol.Id.Value} | " +
                $"{familyName} : {symbol.Name}");
        }

        return result.ToString();
    }

    // ============================================================
    // VIEW
    // ============================================================

    private static string ActivateView(
        UIApplication app,
        string? argument)
    {
        if (!TryParseLong(
                argument,
                out long viewId))
        {
            return "Invalid view id.";
        }

        View? view =
            app.ActiveUIDocument?
                .Document
                .GetElement(
                    new ElementId(viewId))
            as View;

        if (view == null)
        {
            return "View not found.";
        }

        app.ActiveUIDocument!.ActiveView =
            view;

        return
            $"Activated view: {view.Name}";
    }

    private static string RenameView(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long viewId =
            GetLong(
                data,
                "viewId");

        string name =
            GetString(
                data,
                "name");

        View? view =
            document.GetElement(
                new ElementId(viewId))
            as View;

        if (view == null)
        {
            return "View not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Rename View");

        transaction.Start();

        view.Name = name;

        transaction.Commit();

        return
            $"View renamed to '{name}'.";
    }

    private static string DuplicateView(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long viewId =
            GetLong(
                data,
                "viewId");

        View? view =
            document.GetElement(
                new ElementId(viewId))
            as View;

        if (view == null)
        {
            return "View not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Duplicate View");

        transaction.Start();

        ElementId duplicateId =
            view.Duplicate(
                ViewDuplicateOption.Duplicate);

        transaction.Commit();

        return
            $"View duplicated. New Id: " +
            $"{duplicateId.Value}";
    }

    private static string SetViewScale(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long viewId =
            GetLong(
                data,
                "viewId");

        int scale =
            GetInt(
                data,
                "scale");

        View? view =
            document.GetElement(
                new ElementId(viewId))
            as View;

        if (view == null)
        {
            return "View not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Set View Scale");

        transaction.Start();

        view.Scale = scale;

        transaction.Commit();

        return
            $"View scale set to 1:{scale}.";
    }

    private static string SetViewDetailLevel(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long viewId =
            GetLong(
                data,
                "viewId");

        string detailText =
            GetString(
                data,
                "detailLevel");

        if (!Enum.TryParse(
                detailText,
                true,
                out ViewDetailLevel detailLevel))
        {
            return
                $"Invalid detail level: {detailText}";
        }

        View? view =
            document.GetElement(
                new ElementId(viewId))
            as View;

        if (view == null)
        {
            return "View not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Detail Level");

        transaction.Start();

        view.DetailLevel =
            detailLevel;

        transaction.Commit();

        return
            $"Detail level set to {detailLevel}.";
    }

    private static string SetViewVisibility(
        Document document,
        string? argument,
        bool hide)
    {
        JsonElement data =
            ParseJson(argument);

        long viewId =
            GetLong(
                data,
                "viewId");

        View? view =
            document.GetElement(
                new ElementId(viewId))
            as View;

        if (view == null)
        {
            return "View not found.";
        }

        List<ElementId> ids =
            GetElementIds(
                data,
                "elementIds");

        if (ids.Count == 0)
        {
            return "No element ids supplied.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - View Visibility");

        transaction.Start();

        if (hide)
        {
            view.HideElements(ids);
        }
        else
        {
            view.UnhideElements(ids);
        }

        transaction.Commit();

        return
            $"{(hide ? "Hidden" : "Unhidden")} " +
            $"{ids.Count} elements.";
    }

    private static string IsolateElements(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long viewId =
            GetLong(
                data,
                "viewId");

        View? view =
            document.GetElement(
                new ElementId(viewId))
            as View;

        if (view == null)
        {
            return "View not found.";
        }

        List<ElementId> ids =
            GetElementIds(
                data,
                "elementIds");

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Temporary Isolate");

        transaction.Start();

        view.IsolateElementsTemporary(ids);

        transaction.Commit();

        return
            $"Temporarily isolated {ids.Count} elements.";
    }

    private static string GetViewElements(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long viewId =
            GetLong(
                data,
                "viewId");

        View? view =
            document.GetElement(
                new ElementId(viewId))
            as View;

        if (view == null)
        {
            return "View not found.";
        }

        var elements =
            new FilteredElementCollector(
                document,
                view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

        StringBuilder result =
            new();

        result.AppendLine(
            $"View: {view.Name}");

        result.AppendLine(
            $"Elements: {elements.Count}");

        foreach (Element element in elements)
        {
            result.AppendLine(
                $"- {element.Id.Value} | " +
                $"{element.Category?.Name ?? "No Category"} | " +
                $"{element.Name ?? "Unnamed"}");
        }

        return result.ToString();
    }

    // ============================================================
    // CREATE LEVEL
    // ============================================================

    private static string CreateLevel(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        double elevationM =
            GetDouble(
                data,
                "elevationM");

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Create Level");

        transaction.Start();

        Level level =
            Level.Create(
                document,
                ToInternalUnits(
                    elevationM));

        if (TryGetString(
                data,
                "name",
                out string? name) &&
            !string.IsNullOrWhiteSpace(name))
        {
            level.Name = name;
        }

        transaction.Commit();

        return
            $"Created Level: " +
            $"{level.Name} " +
            $"(Id {level.Id.Value})";
    }

    // ============================================================
    // CREATE GRID
    // ============================================================

    private static string CreateGrid(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        double x1 =
            GetDouble(data, "x1M");

        double y1 =
            GetDouble(data, "y1M");

        double x2 =
            GetDouble(data, "x2M");

        double y2 =
            GetDouble(data, "y2M");

        double z =
            TryGetDouble(
                data,
                "zM",
                0);

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Create Grid");

        transaction.Start();

        Grid grid =
            Grid.Create(
                document,
                Line.CreateBound(
                    Point(
                        x1,
                        y1,
                        z),
                    Point(
                        x2,
                        y2,
                        z)));

        if (TryGetString(
                data,
                "name",
                out string? name) &&
            !string.IsNullOrWhiteSpace(name))
        {
            grid.Name = name;
        }

        transaction.Commit();

        return
            $"Created Grid: " +
            $"{grid.Name} " +
            $"(Id {grid.Id.Value})";
    }

    // ============================================================
    // CREATE WALL
    // ============================================================

    private static string CreateWall(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        string typeName =
            GetString(
                data,
                "typeName");

        long levelId =
            GetLong(
                data,
                "levelId");

        double x1 =
            GetDouble(
                data,
                "x1M");

        double y1 =
            GetDouble(
                data,
                "y1M");

        double x2 =
            GetDouble(
                data,
                "x2M");

        double y2 =
            GetDouble(
                data,
                "y2M");

        double heightM =
            TryGetDouble(
                data,
                "heightM",
                3.0);

        bool structural =
            TryGetBool(
                data,
                "structural",
                false);

        WallType? wallType =
            new FilteredElementCollector(document)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(
                    x =>
                        x.Name.Equals(
                            typeName,
                            StringComparison.OrdinalIgnoreCase));

        if (wallType == null)
        {
            return
                $"Wall type '{typeName}' not found.";
        }

        Level? level =
            document.GetElement(
                new ElementId(levelId))
            as Level;

        if (level == null)
        {
            return
                $"Level {levelId} not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Create Wall");

        transaction.Start();

        Wall wall =
            Wall.Create(
                document,
                Line.CreateBound(
                    Point(x1, y1, 0),
                    Point(x2, y2, 0)),
                wallType.Id,
                level.Id,
                ToInternalUnits(heightM),
                0,
                false,
                structural);

        transaction.Commit();

        return
            $"Created Wall Id: {wall.Id.Value}";
    }

    // ============================================================
    // CREATE FLOOR
    // ============================================================

    private static string CreateFloor(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        string typeName =
            GetString(
                data,
                "typeName");

        long levelId =
            GetLong(
                data,
                "levelId");

        FloorType? floorType =
            new FilteredElementCollector(document)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(
                    x =>
                        x.Name.Equals(
                            typeName,
                            StringComparison.OrdinalIgnoreCase));

        if (floorType == null)
        {
            return
                $"Floor type '{typeName}' not found.";
        }

        Level? level =
            document.GetElement(
                new ElementId(levelId))
            as Level;

        if (level == null)
        {
            return
                $"Level {levelId} not found.";
        }

        if (!data.TryGetProperty(
                "points",
                out JsonElement points))
        {
            return
                "Floor requires 'points'.";
        }

        List<JsonElement> pointList =
            points
                .EnumerateArray()
                .ToList();

        if (pointList.Count < 3)
        {
            return
                "Floor requires at least 3 points.";
        }

        CurveLoop loop =
            new();

        for (int i = 0;
             i < pointList.Count;
             i++)
        {
            JsonElement p1 =
                pointList[i];

            JsonElement p2 =
                pointList[
                    (i + 1) %
                    pointList.Count];

            double x1 =
                GetDouble(
                    p1,
                    "x");

            double y1 =
                GetDouble(
                    p1,
                    "y");

            double x2 =
                GetDouble(
                    p2,
                    "x");

            double y2 =
                GetDouble(
                    p2,
                    "y");

            loop.Append(
                Line.CreateBound(
                    Point(x1, y1, 0),
                    Point(x2, y2, 0)));
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Create Floor");

        transaction.Start();

        Floor floor =
            Floor.Create(
                document,
                new List<CurveLoop>
                {
                    loop
                },
                floorType.Id,
                level.Id);

        transaction.Commit();

        return
            $"Created Floor Id: {floor.Id.Value}";
    }

    // ============================================================
    // CREATE FAMILY INSTANCE
    // ============================================================

    private static string CreateFamilyInstance(
        Document document,
        string? argument,
        BuiltInCategory category,
        StructuralType structuralType)
    {
        JsonElement data =
            ParseJson(argument);

        string typeName =
            GetString(
                data,
                "typeName");

        long levelId =
            GetLong(
                data,
                "levelId");

        double x =
            GetDouble(
                data,
                "xM");

        double y =
            GetDouble(
                data,
                "yM");

        double z =
            TryGetDouble(
                data,
                "zM",
                0);

        FamilySymbol? symbol =
            new FilteredElementCollector(document)
                .OfCategory(category)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(
                    s =>
                        s.Name.Equals(
                            typeName,
                            StringComparison.OrdinalIgnoreCase));

        if (symbol == null)
        {
            return
                $"Family type '{typeName}' not found " +
                $"in category {category}.";
        }

        Level? level =
            document.GetElement(
                new ElementId(levelId))
            as Level;

        if (level == null)
        {
            return
                $"Level {levelId} not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Create Family Instance");

        transaction.Start();

        if (!symbol.IsActive)
        {
            symbol.Activate();

            document.Regenerate();
        }

        FamilyInstance instance =
            document.Create.NewFamilyInstance(
                Point(x, y, z),
                symbol,
                level,
                structuralType);

        transaction.Commit();

        return
            $"Created FamilyInstance Id: " +
            $"{instance.Id.Value}\n" +
            $"Family: " +
            $"{symbol.Family?.Name ?? "Unknown"}\n" +
            $"Type: {symbol.Name}";
    }

    // ============================================================
    // CREATE SHEET
    // ============================================================

    private static string CreateSheet(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        string sheetNumber =
            GetString(
                data,
                "sheetNumber");

        string sheetName =
            GetString(
                data,
                "sheetName");

        ElementId titleBlockId = ElementId.InvalidElementId;

        if (data.TryGetProperty("titleBlockId", out JsonElement tbElem) &&
            tbElem.ValueKind == JsonValueKind.Number)
        {
            titleBlockId = new ElementId(tbElem.GetInt64());
        }
        else
        {
            var titleBlock =
                new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsElementType()
                    .FirstOrDefault();

            if (titleBlock != null)
            {
                titleBlockId = titleBlock.Id;
            }
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Create Sheet");

        transaction.Start();

        ViewSheet sheet =
            ViewSheet.Create(
                document,
                titleBlockId);

        if (!string.IsNullOrWhiteSpace(sheetNumber))
        {
            sheet.SheetNumber = sheetNumber;
        }

        if (!string.IsNullOrWhiteSpace(sheetName))
        {
            sheet.Name = sheetName;
        }

        transaction.Commit();

        return
            $"Created Sheet: {sheet.SheetNumber} - {sheet.Name} (Id {sheet.Id.Value})";
    }

    // ============================================================
    // CREATE SCHEDULE
    // ============================================================

    private static string CreateSchedule(
        Document document,
        string? argument)
    {
        JsonElement data = ParseJson(argument);

        string scheduleName = GetString(data, "name");
        string categoryStr = GetString(data, "category");

        BuiltInCategory category = categoryStr.ToLowerInvariant() switch
        {
            "structuralcolumns" or "columns" or "structural_columns" => BuiltInCategory.OST_StructuralColumns,
            "structuralframing" or "framing" or "structural_framing" or "beams" => BuiltInCategory.OST_StructuralFraming,
            "floors" or "floor" => BuiltInCategory.OST_Floors,
            "walls" or "wall" => BuiltInCategory.OST_Walls,
            _ => BuiltInCategory.OST_StructuralColumns
        };

        using Transaction transaction = new(document, "AIGRAV - Create Schedule");
        transaction.Start();

        ViewSchedule schedule = ViewSchedule.CreateSchedule(
            document,
            new ElementId(category));

        if (!string.IsNullOrWhiteSpace(scheduleName))
        {
            schedule.Name = scheduleName;
        }

        ScheduleDefinition definition = schedule.Definition;

        // Collect schedulable fields map
        var schedulableFields = definition.GetSchedulableFields();
        var fieldMap = new Dictionary<string, SchedulableField>(StringComparer.OrdinalIgnoreCase);
        foreach (SchedulableField sf in schedulableFields)
        {
            string name = sf.GetName(document);
            if (!fieldMap.ContainsKey(name))
            {
                fieldMap[name] = sf;
            }
        }

        ScheduleField? itemField = null;
        ScheduleField? materialField = null;
        ScheduleField? countField = null;
        ScheduleField? volumeField = null;

        // 1. Column: Uraian Item Pekerjaan (Family and Type)
        if (fieldMap.TryGetValue("Family and Type", out SchedulableField? sfItem) ||
            fieldMap.TryGetValue("Type", out sfItem))
        {
            itemField = definition.AddField(sfItem);
            itemField.ColumnHeading = "Uraian Item Pekerjaan";
        }

        // 2. Column: Material Mutu Beton (Structural Material / Material: Name / Comments)
        if (fieldMap.TryGetValue("Structural Material", out SchedulableField? sfMat) ||
            fieldMap.TryGetValue("Material", out sfMat) ||
            fieldMap.TryGetValue("Comments", out sfMat))
        {
            materialField = definition.AddField(sfMat);
            materialField.ColumnHeading = "Material Mutu Beton";
        }

        // 3. Column: Jumlah (Unit) (Count)
        if (fieldMap.TryGetValue("Count", out SchedulableField? sfCount))
        {
            countField = definition.AddField(sfCount);
            countField.ColumnHeading = "Jumlah (Unit)";
            countField.DisplayType = ScheduleFieldDisplayType.Totals;
        }

        // 4. Column: Volume (m3) (Volume)
        if (fieldMap.TryGetValue("Volume", out SchedulableField? sfVol))
        {
            volumeField = definition.AddField(sfVol);
            volumeField.ColumnHeading = "Volume (m3)";
            volumeField.DisplayType = ScheduleFieldDisplayType.Totals;
        }

        // Configure Sorting by Family and Type
        if (itemField != null)
        {
            ScheduleSortGroupField sortGroup = new(itemField.FieldId, ScheduleSortOrder.Ascending);
            definition.AddSortGroupField(sortGroup);
        }

        // Configure Filter for Structural Framing (filter out steel like CNP/UNP)
        if (category == BuiltInCategory.OST_StructuralFraming && materialField != null)
        {
            // Material 54292 = Concrete, Cast-in-Place gray
            ScheduleFilter filter = new(materialField.FieldId, ScheduleFilterType.Equal, new ElementId(54292));
            definition.AddFilter(filter);
        }

        // Do not itemize every instance (group by type)
        definition.IsItemized = false;
        definition.ShowGrandTotal = true;
        definition.ShowGrandTotalTitle = true;
        definition.ShowGrandTotalCount = true;

        transaction.Commit();

        return $"Created Schedule: {schedule.Name} (Id {schedule.Id.Value})";
    }

    // ============================================================
    // PARAMETER
    // ============================================================

    private static string SetParameter(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long elementId =
            GetLong(
                data,
                "elementId");

        string parameterName =
            GetString(
                data,
                "parameterName");

        string value =
            GetString(
                data,
                "value");

        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return
                $"Element {elementId} not found.";
        }

        Parameter? parameter =
            element.LookupParameter(
                parameterName);

        if (parameter == null)
        {
            return
                $"Parameter '{parameterName}' not found.";
        }

        if (parameter.IsReadOnly)
        {
            return
                $"Parameter '{parameterName}' is read-only.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Set Parameter");

        transaction.Start();

        switch (parameter.StorageType)
        {
            case StorageType.String:

                parameter.Set(value);

                break;

            case StorageType.Integer:

                if (!int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int intValue))
                {
                    transaction.RollBack();

                    return
                        "Invalid integer value.";
                }

                parameter.Set(intValue);

                break;

            case StorageType.Double:

                if (!double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double doubleValue))
                {
                    transaction.RollBack();

                    return
                        "Invalid double value.";
                }

                parameter.Set(doubleValue);

                break;

            case StorageType.ElementId:

                if (!long.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out long idValue))
                {
                    transaction.RollBack();

                    return
                        "Invalid ElementId value.";
                }

                parameter.Set(
                    new ElementId(idValue));

                break;

            default:

                transaction.RollBack();

                return
                    "Unsupported parameter storage type.";
        }

        transaction.Commit();

        return
            $"Parameter '{parameterName}' updated.";
    }

    // ============================================================
    // MOVE
    // ============================================================

    private static string MoveElement(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long elementId =
            GetLong(
                data,
                "elementId");

        XYZ translation =
            Point(
                GetDouble(data, "dxM"),
                GetDouble(data, "dyM"),
                GetDouble(data, "dzM"));

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Move Element");

        transaction.Start();

        ElementTransformUtils.MoveElement(
            document,
            new ElementId(elementId),
            translation);

        transaction.Commit();

        return
            $"Moved element {elementId}.";
    }

    // ============================================================
    // MOVE MANY
    // ============================================================

    private static string MoveElements(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        List<ElementId> ids =
            GetElementIds(
                data,
                "elementIds");

        XYZ translation =
            Point(
                GetDouble(data, "dxM"),
                GetDouble(data, "dyM"),
                GetDouble(data, "dzM"));

        if (ids.Count == 0)
        {
            return "No element ids supplied.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Move Elements");

        transaction.Start();

        ElementTransformUtils.MoveElements(
            document,
            ids,
            translation);

        transaction.Commit();

        return
            $"Moved {ids.Count} elements.";
    }

    // ============================================================
    // COPY ONE
    // ============================================================

    private static string CopyElement(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long elementId =
            GetLong(
                data,
                "elementId");

        XYZ translation =
            Point(
                GetDouble(data, "dxM"),
                GetDouble(data, "dyM"),
                GetDouble(data, "dzM"));

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Copy Element");

        transaction.Start();

        ICollection<ElementId> copied =
            ElementTransformUtils.CopyElement(
                document,
                new ElementId(elementId),
                translation);

        transaction.Commit();

        return
            $"Copied element {elementId}.\n" +
            $"New Ids: " +
            $"{string.Join(
                ", ",
                copied.Select(x => x.Value))}";
    }

    // ============================================================
    // COPY MANY
    // ============================================================

    private static string CopyElements(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        List<ElementId> ids =
            GetElementIds(
                data,
                "elementIds");

        XYZ translation =
            Point(
                GetDouble(data, "dxM"),
                GetDouble(data, "dyM"),
                GetDouble(data, "dzM"));

        if (ids.Count == 0)
        {
            return "No element ids supplied.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Copy Elements");

        transaction.Start();

        ICollection<ElementId> copied =
            ElementTransformUtils.CopyElements(
                document,
                ids,
                translation);

        transaction.Commit();

        return
            $"Copied {ids.Count} elements.\n" +
            $"New Ids: " +
            $"{string.Join(
                ", ",
                copied.Select(x => x.Value))}";
    }

    // ============================================================
    // ROTATE
    // ============================================================

    private static string RotateElement(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long elementId =
            GetLong(
                data,
                "elementId");

        XYZ origin =
            Point(
                GetDouble(data, "xM"),
                GetDouble(data, "yM"),
                GetDouble(data, "zM"));

        double angleDeg =
            GetDouble(
                data,
                "angleDeg");

        double angleRad =
            angleDeg *
            Math.PI /
            180.0;

        Line axis =
            Line.CreateBound(
                origin,
                origin + XYZ.BasisZ);

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Rotate Element");

        transaction.Start();

        ElementTransformUtils.RotateElement(
            document,
            new ElementId(elementId),
            axis,
            angleRad);

        transaction.Commit();

        return
            $"Rotated element {elementId} " +
            $"by {angleDeg} degrees.";
    }

    private static string RotateElements(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        List<ElementId> ids =
            GetElementIds(
                data,
                "elementIds");

        XYZ origin =
            Point(
                GetDouble(data, "xM"),
                GetDouble(data, "yM"),
                GetDouble(data, "zM"));

        double angleRad =
            GetDouble(
                data,
                "angleDeg") *
            Math.PI /
            180.0;

        Line axis =
            Line.CreateBound(
                origin,
                origin + XYZ.BasisZ);

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Rotate Elements");

        transaction.Start();

        ElementTransformUtils.RotateElements(
            document,
            ids,
            axis,
            angleRad);

        transaction.Commit();

        return
            $"Rotated {ids.Count} elements.";
    }

    // ============================================================
    // MIRROR
    // ============================================================

    private static string MirrorElements(
        Document document,
        string? argument,
        bool multiple)
    {
        JsonElement data =
            ParseJson(argument);

        List<ElementId> ids;

        if (multiple)
        {
            ids =
                GetElementIds(
                    data,
                    "elementIds");
        }
        else
        {
            long elementId =
                GetLong(
                    data,
                    "elementId");

            ids =
                new List<ElementId>
                {
                    new ElementId(elementId)
                };
        }

        XYZ p1 =
            Point(
                GetDouble(data, "x1M"),
                GetDouble(data, "y1M"),
                GetDouble(data, "z1M"));

        XYZ p2 =
            Point(
                GetDouble(data, "x2M"),
                GetDouble(data, "y2M"),
                GetDouble(data, "z2M"));

        XYZ direction =
            (p2 - p1).Normalize();

        XYZ origin =
            (p1 + p2) / 2.0;

        Plane plane =
            Plane.CreateByNormalAndOrigin(
                direction,
                origin);

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Mirror Elements");

        transaction.Start();

        IList<ElementId> mirrored =
            ElementTransformUtils.MirrorElements(
                document,
                ids,
                plane,
                true);

        transaction.Commit();

        return
            $"Mirrored {ids.Count} elements.\n" +
            $"Result Ids: " +
            $"{string.Join(
                ", ",
                mirrored.Select(x => x.Value))}";
    }

    // ============================================================
    // DELETE
    // ============================================================

    private static string DeleteElement(
        Document document,
        string? argument)
    {
        if (!TryParseLong(
                argument,
                out long elementId))
        {
            return "Invalid element id.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Delete Element");

        transaction.Start();

        ICollection<ElementId> deleted =
            document.Delete(
                new ElementId(elementId));

        transaction.Commit();

        return
            $"Deleted element {elementId}.\n" +
            $"Affected ids: {deleted.Count}";
    }

    private static string DeleteElements(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        List<ElementId> ids =
            GetElementIds(
                data,
                "elementIds");

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Delete Elements");

        transaction.Start();

        ICollection<ElementId> deleted =
            document.Delete(ids);

        transaction.Commit();

        return
            $"Deleted {ids.Count} requested elements.\n" +
            $"Affected ids: {deleted.Count}";
    }

    // ============================================================
    // PIN
    // ============================================================

    private static string SetPinned(
        Document document,
        string? argument,
        bool pinned)
    {
        if (!TryParseLong(
                argument,
                out long elementId))
        {
            return "Invalid element id.";
        }

        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return
                $"Element {elementId} not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Pin Element");

        transaction.Start();

        element.Pinned =
            pinned;

        transaction.Commit();

        return
            $"{(pinned ? "Pinned" : "Unpinned")} " +
            $"element {elementId}.";
    }

    // ============================================================
    // JOIN GEOMETRY
    // ============================================================

    private static string JoinGeometry(
        Document document,
        string? argument,
        bool join)
    {
        JsonElement data =
            ParseJson(argument);

        long id1 =
            GetLong(
                data,
                "elementId1");

        long id2 =
            GetLong(
                data,
                "elementId2");

        Element? element1 =
            GetElement(
                document,
                id1);

        Element? element2 =
            GetElement(
                document,
                id2);

        if (element1 == null ||
            element2 == null)
        {
            return
                "One or both elements were not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Join Geometry");

        transaction.Start();

        if (join)
        {
            JoinGeometryUtils.JoinGeometry(
                document,
                element1,
                element2);
        }
        else
        {
            JoinGeometryUtils.UnjoinGeometry(
                document,
                element1,
                element2);
        }

        transaction.Commit();

        return
            join
                ? "Geometry joined."
                : "Geometry unjoined.";
    }

    private static string SwitchJoinOrder(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        Element? element1 =
            GetElement(
                document,
                GetLong(
                    data,
                    "elementId1"));

        Element? element2 =
            GetElement(
                document,
                GetLong(
                    data,
                    "elementId2"));

        if (element1 == null ||
            element2 == null)
        {
            return
                "One or both elements were not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Switch Join Order");

        transaction.Start();

        JoinGeometryUtils.SwitchJoinOrder(
            document,
            element1,
            element2);

        transaction.Commit();

        return
            "Join order switched.";
    }

    private static string AreElementsJoined(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        Element? element1 =
            GetElement(
                document,
                GetLong(
                    data,
                    "elementId1"));

        Element? element2 =
            GetElement(
                document,
                GetLong(
                    data,
                    "elementId2"));

        if (element1 == null ||
            element2 == null)
        {
            return
                "One or both elements were not found.";
        }

        bool joined =
            JoinGeometryUtils.AreElementsJoined(
                document,
                element1,
                element2);

        return
            joined
                ? "Elements are joined."
                : "Elements are not joined.";
    }

    // ============================================================
    // WALL JOIN
    // ============================================================

    private static string SetWallJoin(
        Document document,
        string? argument,
        bool allow)
    {
        JsonElement data =
            ParseJson(argument);

        long elementId =
            GetLong(
                data,
                "elementId");

        int end =
            GetInt(
                data,
                "end");

        Wall? wall =
            GetElement(
                document,
                elementId)
            as Wall;

        if (wall == null)
        {
            return
                $"Element {elementId} is not a Wall.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Wall Join");

        transaction.Start();

        if (allow)
        {
            WallUtils.AllowWallJoinAtEnd(
                wall,
                end);
        }
        else
        {
            WallUtils.DisallowWallJoinAtEnd(
                wall,
                end);
        }

        transaction.Commit();

        return
            $"{(allow ? "Allowed" : "Disallowed")} " +
            $"wall join at end {end}.";
    }

    // ============================================================
    // CHANGE TYPE
    // ============================================================

    private static string ChangeElementType(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long elementId =
            GetLong(
                data,
                "elementId");

        long typeId =
            GetLong(
                data,
                "typeId");

        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return "Element not found.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Change Type");

        transaction.Start();

        element.ChangeTypeId(
            new ElementId(typeId));

        transaction.Commit();

        return
            $"Element {elementId} type changed.";
    }

    // ============================================================
    // CHANGE LEVEL
    // ============================================================

    private static string ChangeElementLevel(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        long elementId =
            GetLong(
                data,
                "elementId");

        long levelId =
            GetLong(
                data,
                "levelId");

        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return "Element not found.";
        }

        Parameter? parameter =
            element.get_Parameter(
                BuiltInParameter.LEVEL_PARAM);

        if (parameter == null ||
            parameter.IsReadOnly)
        {
            return
                "Level parameter is unavailable or read-only.";
        }

        using Transaction transaction =
            new(
                document,
                "AIGRAV - Change Level");

        transaction.Start();

        parameter.Set(
            new ElementId(levelId));

        transaction.Commit();

        return
            $"Element {elementId} level changed.";
    }

    // ============================================================
    // ANALYSIS
    // ============================================================

    private static string MeasureDistance(
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        XYZ p1 =
            Point(
                GetDouble(data, "x1M"),
                GetDouble(data, "y1M"),
                GetDouble(data, "z1M"));

        XYZ p2 =
            Point(
                GetDouble(data, "x2M"),
                GetDouble(data, "y2M"),
                GetDouble(data, "z2M"));

        double distance =
            p1.DistanceTo(p2);

        return
            $"Distance: " +
            $"{ToMeters(distance):F3} m";
    }

    private static string MeasureElementLength(
        Document document,
        string? argument)
    {
        if (!TryParseLong(
                argument,
                out long elementId))
        {
            return "Invalid element id.";
        }

        Element? element =
            GetElement(
                document,
                elementId);

        if (element == null)
        {
            return "Element not found.";
        }

        if (element.Location
            is LocationCurve locationCurve)
        {
            return
                $"Length: " +
                $"{ToMeters(
                    locationCurve.Curve.Length):F3} m";
        }

        return
            "Element does not have a curve location.";
    }

    private static string GetCurveEndpoints(
        Document document,
        string? argument)
    {
        if (!TryParseLong(
                argument,
                out long elementId))
        {
            return "Invalid element id.";
        }

        Element? element =
            GetElement(
                document,
                elementId);

        if (element?.Location
            is not LocationCurve locationCurve)
        {
            return
                "Element does not have a curve location.";
        }

        XYZ start =
            locationCurve.Curve
                .GetEndPoint(0);

        XYZ end =
            locationCurve.Curve
                .GetEndPoint(1);

        return
            $"Start: {PointText(start)}\n" +
            $"End: {PointText(end)}\n" +
            $"Length: " +
            $"{ToMeters(
                locationCurve.Curve.Length):F3} m";
    }

    private static string ValidateElements(
        Document document,
        string? argument)
    {
        JsonElement data =
            ParseJson(argument);

        List<ElementId> ids =
            GetElementIds(
                data,
                "elementIds");

        StringBuilder result =
            new();

        foreach (ElementId id in ids)
        {
            Element? element =
                document.GetElement(id);

            result.AppendLine(
                $"{id.Value}: " +
                $"{(element == null
                    ? "MISSING"
                    : "FOUND")}");
        }

        return result.ToString();
    }

    private static string ValidateLevels(
        Document document)
    {
        var levels =
            new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(
                    x => x.Elevation)
                .ToList();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Level Count: {levels.Count}");

        foreach (Level level in levels)
        {
            result.AppendLine(
                $"{level.Id.Value} | " +
                $"{level.Name} | " +
                $"{ToMeters(
                    level.Elevation):F3} m");
        }

        return result.ToString();
    }

    // ============================================================
    // DOCUMENT INFORMATION
    // ============================================================

    private static string GetPhases(
        Document document)
    {
        var phases =
            new FilteredElementCollector(document)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .ToList();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Phases: {phases.Count}");

        foreach (Phase phase in phases)
        {
            result.AppendLine(
                $"- {phase.Id.Value} | " +
                phase.Name);
        }

        return result.ToString();
    }

    private static string GetDesignOptions(
        Document document)
    {
        var options =
            new FilteredElementCollector(document)
                .OfClass(typeof(DesignOption))
                .Cast<DesignOption>()
                .ToList();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Design Options: {options.Count}");

        foreach (DesignOption option in options)
        {
            result.AppendLine(
                $"- {option.Id.Value} | " +
                option.Name);
        }

        return result.ToString();
    }

    private static string GetWorksets(
        Document document)
    {
        if (!document.IsWorkshared)
        {
            return
                "Document is not workshared.";
        }

        var worksets =
            new FilteredWorksetCollector(
                document)
                .OfKind(
                    WorksetKind.UserWorkset)
                .ToWorksets();

        StringBuilder result =
            new();

        result.AppendLine(
            $"User Worksets: {worksets.Count}");

        foreach (Workset workset in worksets)
        {
            result.AppendLine(
                $"- {workset.Id.IntegerValue} | " +
                workset.Name);
        }

        return result.ToString();
    }

    private static string GetLinkedDocuments(
        Document document)
    {
        var links =
            new FilteredElementCollector(document)
                .OfClass(
                    typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Revit Links: {links.Count}");

        foreach (
            RevitLinkInstance link
            in links)
        {
            Document? linked =
                link.GetLinkDocument();

            result.AppendLine(
                $"- Id: {link.Id.Value} | " +
                $"Name: {link.Name} | " +
                $"Loaded: {linked != null}");
        }

        return result.ToString();
    }

    private static string GetWarnings(
        Document document)
    {
        IList<FailureMessage> warnings =
            document.GetWarnings();

        StringBuilder result =
            new();

        result.AppendLine(
            $"Warnings: {warnings.Count}");

        foreach (
            FailureMessage warning
            in warnings)
        {
            result.AppendLine(
                "- " +
                warning.GetDescriptionText());
        }

        return result.ToString();
    }

    private static string CountElements(
        Document document,
        string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            int count =
                new FilteredElementCollector(
                    document)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Count;

            return
                $"Total model elements: {count}";
        }

        JsonElement data =
            ParseJson(argument);

        if (!TryGetString(
                data,
                "category",
                out string? categoryText))
        {
            return
                "Category not supplied.";
        }

        if (!Enum.TryParse(
                categoryText,
                true,
                out BuiltInCategory category))
        {
            return
                $"Unknown category: {categoryText}";
        }

        int categoryCount =
            new FilteredElementCollector(
                document)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements()
                .Count;

        return
            $"{categoryText}: {categoryCount}";
    }

    // ============================================================
    // UTILITIES
    // ============================================================

    private static Element? GetElement(
        Document document,
        long elementId)
    {
        return document.GetElement(
            new ElementId(elementId));
    }

    private static BuiltInCategory
        GetRequestedCategory(
            string? argument)
    {
        if (string.IsNullOrWhiteSpace(
                argument))
        {
            return
                BuiltInCategory.OST_GenericModel;
        }

        try
        {
            JsonElement data =
                ParseJson(argument);

            if (TryGetString(
                    data,
                    "category",
                    out string? categoryText) &&
                Enum.TryParse(
                    categoryText,
                    true,
                    out BuiltInCategory category))
            {
                return category;
            }
        }
        catch
        {
        }

        return
            BuiltInCategory.OST_GenericModel;
    }

    private static string PrettyName(
        string value)
    {
        return value
            .Replace("get_", "")
            .Replace("_", " ");
    }

    private static JsonElement ParseJson(
        string? argument)
    {
        if (string.IsNullOrWhiteSpace(
                argument))
        {
            throw new ArgumentException(
                "Tool argument is required.");
        }

        return JsonSerializer
            .Deserialize<JsonElement>(
                argument);
    }

    private static bool TryParseLong(
        string? value,
        out long result)
    {
        return long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static string GetString(
        JsonElement data,
        string property)
    {
        if (!data.TryGetProperty(
                property,
                out JsonElement value))
        {
            throw new ArgumentException(
                $"Missing property '{property}'.");
        }

        return value.GetString() ?? "";
    }

    private static bool TryGetString(
        JsonElement data,
        string property,
        out string? value)
    {
        if (data.TryGetProperty(
                property,
                out JsonElement element) &&
            element.ValueKind ==
                JsonValueKind.String)
        {
            value =
                element.GetString();

            return true;
        }

        value = null;

        return false;
    }

    private static long GetLong(
        JsonElement data,
        string property)
    {
        if (!data.TryGetProperty(
                property,
                out JsonElement value))
        {
            throw new ArgumentException(
                $"Missing property '{property}'.");
        }

        return value.GetInt64();
    }

    private static int GetInt(
        JsonElement data,
        string property)
    {
        if (!data.TryGetProperty(
                property,
                out JsonElement value))
        {
            throw new ArgumentException(
                $"Missing property '{property}'.");
        }

        return value.GetInt32();
    }

    private static double GetDouble(
        JsonElement data,
        string property)
    {
        if (!data.TryGetProperty(
                property,
                out JsonElement value))
        {
            throw new ArgumentException(
                $"Missing property '{property}'.");
        }

        return value.GetDouble();
    }

    private static double TryGetDouble(
        JsonElement data,
        string property,
        double defaultValue)
    {
        if (!data.TryGetProperty(
                property,
                out JsonElement value))
        {
            return defaultValue;
        }

        return value.GetDouble();
    }

    private static bool TryGetBool(
        JsonElement data,
        string property,
        bool defaultValue)
    {
        if (!data.TryGetProperty(
                property,
                out JsonElement value))
        {
            return defaultValue;
        }

        return value.GetBoolean();
    }

    private static List<ElementId>
        GetElementIds(
            JsonElement data,
            string property)
    {
        if (!data.TryGetProperty(
                property,
                out JsonElement values))
        {
            throw new ArgumentException(
                $"Missing property '{property}'.");
        }

        return values
            .EnumerateArray()
            .Select(
                x =>
                    new ElementId(
                        x.GetInt64()))
            .ToList();
    }

    private static XYZ Point(
        double xMeters,
        double yMeters,
        double zMeters)
    {
        return new XYZ(
            ToInternalUnits(xMeters),
            ToInternalUnits(yMeters),
            ToInternalUnits(zMeters));
    }

    private static double ToInternalUnits(
        double meters)
    {
        return UnitUtils.ConvertToInternalUnits(
            meters,
            UnitTypeId.Meters);
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
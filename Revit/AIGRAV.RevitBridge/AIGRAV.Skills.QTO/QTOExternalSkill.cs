using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIGRAV.Contracts;
using AIGRAV.Skills.QTO.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.QTO;

public sealed class QTOExternalSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "QTO Engine",
        Version = "3.1",
        Category = "Quantity Takeoff",
        Description = "QTO measurement and classification engine with steel weight calculation from the user-provided AIGRAV steel database.",
        Source = "External",
        Priority = 1500
    };

    private static readonly object DatabaseSync = new();
    private static SteelDatabase? _database;
    private static string _databasePath = string.Empty;

    public bool CanHandle(string toolName) =>
        toolName.Equals("aigrav_qto", StringComparison.OrdinalIgnoreCase);

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        if (!toolName.Equals("aigrav_qto", StringComparison.OrdinalIgnoreCase))
            return $"Unknown QTO operation: {toolName}";

        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null)
            return "QTO failed: no active Revit document.";

        try
        {
            QTORequest request = ParseRequest(argument);
            if (string.IsNullOrWhiteSpace(request.Category))
                return BuildUsage();

            if (!Enum.TryParse(request.Category, true, out BuiltInCategory category))
                return $"QTO failed: unknown BuiltInCategory '{request.Category}'.";

            QTOOutputTemplate template = QTOTemplates.Resolve(request.Template);
            QTOOutputMode mode = ResolveOutputMode(template);

            SteelDatabase? database = null;
            string databaseMessage = string.Empty;
            if (mode == QTOOutputMode.Steel)
                database = LoadSteelDatabase(out databaseMessage);

            List<QTORecord> records = CollectRecords(doc, category, mode, database);
            List<QTORecord> grouped = GroupRecords(records, request, template, mode);

            string renderText = RenderResult(doc, template, category, mode, records, grouped, database, databaseMessage);

            if (!string.IsNullOrWhiteSpace(request.ScheduleName))
            {
                string scheduleResult = CreateRevitSchedule(doc, category, request.ScheduleName);
                renderText += "\n\n" + scheduleResult;
            }

            return renderText;
        }
        catch (JsonException ex)
        {
            return "QTO failed: invalid JSON argument.\n\n" + ex.Message;
        }
        catch (Exception ex)
        {
            return "QTO failed:\n\n" + ex;
        }
    }

    private static List<QTORecord> CollectRecords(Document doc, BuiltInCategory category, QTOOutputMode mode, SteelDatabase? database)
    {
        List<Element> elements = new FilteredElementCollector(doc)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();

        List<QTORecord> records = new(elements.Count);
        foreach (Element element in elements)
            records.Add(BuildRecord(doc, element, mode, database));

        return records;
    }

    private static QTORecord BuildRecord(Document doc, Element element, QTOOutputMode mode, SteelDatabase? database)
    {
        Element? typeElement = null;
        ElementId typeId = element.GetTypeId();
        if (typeId != ElementId.InvalidElementId)
            typeElement = doc.GetElement(typeId);

        string comments = FirstNonEmpty(
            GetStringParameter(element, "Comments"),
            GetStringParameter(typeElement, "Comments"));

        string typeName = FirstNonEmpty(
            GetStringParameter(element, "Type"),
            typeElement?.Name,
            element.Name);

        string profile = FirstNonEmpty(
            GetStringParameter(element, "Profile"),
            GetStringParameter(typeElement, "Profile"),
            typeName);

        QTORecord record = new()
        {
            Comments = comments,
            Type = typeName,
            Profile = profile,
            Level = GetLevelName(doc, element),
            LengthMm = FirstNonNull(
                GetLengthMm(element, "Cut Length"),
                GetLengthMm(element, "Length"),
                GetLocationCurveLengthMm(element)),
            AreaM2 = GetAreaM2(element, "Area"),
            VolumeM3 = GetVolumeM3(element, "Volume"),
            Count = 1,
            Source = "Revit",
            Notes = $"ElementId={element.Id.Value}"
        };

        if (mode == QTOOutputMode.Steel && database != null)
        {
            SteelMatch? match = database.Find(profile, typeName);
            if (match != null)
            {
                record.ReferenceWeightKgPerMeter = match.WeightPerMeterKg;
                if (record.LengthMm.HasValue)
                    record.CalibratedWeightKg = record.LengthMm.Value / 1000.0 * match.WeightPerMeterKg;

                record.Notes += $"; SteelMatch={match.Family}:{match.Profile}; kg/m={match.WeightPerMeterKg.ToString("0.######", CultureInfo.InvariantCulture)}";
            }
            else
            {
                record.Notes += "; SteelMatch=NOT_FOUND";
            }
        }

        return record;
    }

    private static QTOOutputMode ResolveOutputMode(QTOOutputTemplate template)
    {
        if (template.Name.Contains("Steel", StringComparison.OrdinalIgnoreCase)) return QTOOutputMode.Steel;
        if (template.Name.Contains("Concrete", StringComparison.OrdinalIgnoreCase)) return QTOOutputMode.Concrete;
        if (template.Name.Contains("Area", StringComparison.OrdinalIgnoreCase)) return QTOOutputMode.Area;
        return QTOOutputMode.Generic;
    }

    private static List<QTORecord> GroupRecords(List<QTORecord> records, QTORequest request, QTOOutputTemplate template, QTOOutputMode mode)
    {
        List<string> groupBy = request.GroupBy.Count > 0
            ? request.GroupBy
            : mode == QTOOutputMode.Steel
                ? new List<string> { "Comments", "Profile" }
                : template.GroupBy;

        if (groupBy.Count == 0) return records;

        return records
            .GroupBy(record => BuildGroupKey(record, groupBy))
            .Select(group => AggregateGroup(group, groupBy, mode))
            .OrderBy(record => record.Comments)
            .ThenBy(record => record.Profile)
            .ThenBy(record => record.Type)
            .ThenBy(record => record.Level)
            .ToList();
    }

    private static string BuildGroupKey(QTORecord record, IReadOnlyList<string> groupBy) =>
        string.Join("¦", groupBy.Select(field => GetFieldText(record, field)));

    private static QTORecord AggregateGroup(IEnumerable<QTORecord> group, IReadOnlyList<string> groupBy, QTOOutputMode mode)
    {
        List<QTORecord> items = group.ToList();
        QTORecord first = items[0];

        bool hasLength = items.Any(x => x.LengthMm.HasValue);
        bool hasArea = items.Any(x => x.AreaM2.HasValue);
        bool hasVolume = items.Any(x => x.VolumeM3.HasValue);
        bool hasWeight = items.Any(x => x.CalibratedWeightKg.HasValue);
        bool hasReferenceWeight = items.Any(x => x.ReferenceWeightKgPerMeter.HasValue);

        return new QTORecord
        {
            Comments = ContainsField(groupBy, "Comments") ? first.Comments : string.Empty,
            Type = first.Type,
            Profile = first.Profile,
            Level = ContainsField(groupBy, "Level") ? first.Level : string.Empty,
            Count = items.Sum(x => x.Count),
            LengthMm = hasLength ? items.Sum(x => x.LengthMm ?? 0) : null,
            AreaM2 = hasArea ? items.Sum(x => x.AreaM2 ?? 0) : null,
            VolumeM3 = hasVolume ? items.Sum(x => x.VolumeM3 ?? 0) : null,
            ReferenceWeightKgPerMeter = hasReferenceWeight ? ResolveGroupReferenceWeight(items) : null,
            CalibratedWeightKg = mode == QTOOutputMode.Steel && hasWeight ? items.Sum(x => x.CalibratedWeightKg ?? 0) : null,
            Source = "Revit / Grouped",
            Notes = $"Grouped from {items.Count} element(s); mode={mode}."
        };
    }

    private static double? ResolveGroupReferenceWeight(IReadOnlyList<QTORecord> items)
    {
        double first = items.Where(x => x.ReferenceWeightKgPerMeter.HasValue)
            .Select(x => x.ReferenceWeightKgPerMeter!.Value).FirstOrDefault();
        if (first <= 0) return null;

        bool consistent = items.Where(x => x.ReferenceWeightKgPerMeter.HasValue)
            .All(x => Math.Abs(x.ReferenceWeightKgPerMeter!.Value - first) < 0.000001);
        return consistent ? first : null;
    }

    private static string RenderResult(Document doc, QTOOutputTemplate template, BuiltInCategory category, QTOOutputMode mode,
        IReadOnlyList<QTORecord> rawRecords, IReadOnlyList<QTORecord> groupedRecords, SteelDatabase? database, string databaseMessage)
    {
        StringBuilder sb = new();
        sb.AppendLine("AIGRAV QTO RESULT");
        sb.AppendLine();
        sb.AppendLine($"Project       : {doc.Title}");
        sb.AppendLine($"Category      : {category}");
        sb.AppendLine($"Template      : {template.Name}");
        sb.AppendLine($"Mode          : {mode}");
        sb.AppendLine($"Raw Elements  : {rawRecords.Count}");
        sb.AppendLine($"QTO Groups    : {groupedRecords.Count}");
        sb.AppendLine($"Grouping      : {string.Join(" + ", mode == QTOOutputMode.Steel ? new[] { "Comments", "Profile" } : template.GroupBy)}");

        if (mode == QTOOutputMode.Steel)
        {
            sb.AppendLine($"Steel DB      : {(database == null ? "NOT LOADED" : "LOADED")}");
            sb.AppendLine($"Steel DB Path : {_databasePath}");
            sb.AppendLine($"Steel DB Rows : {database?.Records.Count ?? 0}");
            if (!string.IsNullOrWhiteSpace(databaseMessage)) sb.AppendLine($"Steel DB Info : {databaseMessage}");
        }

        sb.AppendLine();
        AppendModeTable(sb, mode, groupedRecords);

        if (mode == QTOOutputMode.Steel)
        {
            int matched = rawRecords.Count(x => x.ReferenceWeightKgPerMeter.HasValue);
            int unmatched = rawRecords.Count - matched;
            sb.AppendLine();
            sb.AppendLine($"Steel match   : {matched} matched / {unmatched} unmatched");
            sb.AppendLine("Steel note    : Weight = actual Revit length × user steel-table kg/m. Volume is hidden.");
        }
        else if (mode == QTOOutputMode.Concrete)
        {
            sb.AppendLine();
            sb.AppendLine("Concrete note: Volume is shown in m³ with 2 decimal places.");
        }
        else if (mode == QTOOutputMode.Area)
        {
            sb.AppendLine();
            sb.AppendLine("Area note: Area is shown in m² with 2 decimal places.");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendModeTable(StringBuilder sb, QTOOutputMode mode, IReadOnlyList<QTORecord> records)
    {
        if (mode == QTOOutputMode.Steel)
        {
            sb.AppendLine("| Comments / Fungsi | Type / Profil | Total Panjang (m) | Qty (Unit) | Berat (kg) |");
            sb.AppendLine("|---|---|---:|---:|---:|");
            foreach (QTORecord record in records)
                sb.AppendLine($"| {Safe(record.Comments)} | {Safe(record.Profile)} | {FormatMeters(record.LengthMm)} | {record.Count.ToString("0", CultureInfo.InvariantCulture)} | {FormatNumber(record.CalibratedWeightKg)} |");
            return;
        }

        if (mode == QTOOutputMode.Concrete)
        {
            sb.AppendLine("| Comments / Fungsi | Type | Total Panjang (m) | Qty (Unit) | Volume (m³) |");
            sb.AppendLine("|---|---|---:|---:|---:|");
            foreach (QTORecord record in records)
                sb.AppendLine($"| {Safe(record.Comments)} | {Safe(record.Type)} | {FormatMeters(record.LengthMm)} | {record.Count.ToString("0", CultureInfo.InvariantCulture)} | {FormatNumber(record.VolumeM3)} |");
            return;
        }

        if (mode == QTOOutputMode.Area)
        {
            sb.AppendLine("| Comments / Fungsi | Type | Area (m²) | Qty (Unit) |");
            sb.AppendLine("|---|---|---:|---:|");
            foreach (QTORecord record in records)
                sb.AppendLine($"| {Safe(record.Comments)} | {Safe(record.Type)} | {FormatNumber(record.AreaM2)} | {record.Count.ToString("0", CultureInfo.InvariantCulture)} |");
            return;
        }

        sb.AppendLine("| Comments / Fungsi | Type | Total Panjang (m) | Qty (Unit) | Volume (m³) | Area (m²) |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (QTORecord record in records)
            sb.AppendLine($"| {Safe(record.Comments)} | {Safe(record.Type)} | {FormatMeters(record.LengthMm)} | {record.Count.ToString("0", CultureInfo.InvariantCulture)} | {FormatNumber(record.VolumeM3)} | {FormatNumber(record.AreaM2)} |");
    }

    private static SteelDatabase? LoadSteelDatabase(out string message)
    {
        lock (DatabaseSync)
        {
            string path = ResolveSteelDatabasePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _database = null;
                _databasePath = path;
                message = "Steel database JSON not found.";
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                SteelDatabase? database = JsonSerializer.Deserialize<SteelDatabase>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (database == null || database.Records.Count == 0)
                {
                    _database = null;
                    _databasePath = path;
                    message = "Steel database is empty.";
                    return null;
                }

                database.BuildIndex();
                _database = database;
                _databasePath = path;
                message = "Loaded successfully.";
                return _database;
            }
            catch (Exception ex)
            {
                _database = null;
                _databasePath = path;
                message = $"Load error: {ex.Message}";
                return null;
            }
        }
    }

    private static string ResolveSteelDatabasePath()
    {
        string? environmentPath = Environment.GetEnvironmentVariable("AIGRAV_STEEL_DATABASE");
        if (!string.IsNullOrWhiteSpace(environmentPath)) return Path.GetFullPath(environmentPath);

        Assembly? hostAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, "AIGRAV.RevitBridge", StringComparison.OrdinalIgnoreCase));

        string? hostLocation = hostAssembly?.Location;
        if (!string.IsNullOrWhiteSpace(hostLocation))
        {
            string? hostDirectory = Path.GetDirectoryName(hostLocation);
            if (!string.IsNullOrWhiteSpace(hostDirectory))
                return Path.Combine(hostDirectory, "Skills", "SteelDatabase", "AIGRAV.SteelDatabase.json");
        }

        return Path.Combine(AppContext.BaseDirectory, "Skills", "SteelDatabase", "AIGRAV.SteelDatabase.json");
    }

    private static string FormatMeters(double? lengthMm) =>
        lengthMm.HasValue ? (lengthMm.Value / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) : "-";

    private static string FormatNumber(double? value) =>
        value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) : "-";

    private static string Safe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Replace("|", "/").Trim();

    private static string GetFieldText(QTORecord record, string field)
    {
        if (field.Equals("Comments", StringComparison.OrdinalIgnoreCase)) return record.Comments;
        if (field.Equals("Type", StringComparison.OrdinalIgnoreCase)) return record.Type;
        if (field.Equals("Profile", StringComparison.OrdinalIgnoreCase)) return record.Profile;
        if (field.Equals("Level", StringComparison.OrdinalIgnoreCase)) return record.Level;
        return string.Empty;
    }

    private static Parameter? FindParameter(Element? element, string parameterName) =>
        element?.LookupParameter(parameterName);

    private static string? GetStringParameter(Element? element, string parameterName)
    {
        Parameter? parameter = FindParameter(element, parameterName);
        if (parameter == null) return null;
        return parameter.StorageType == StorageType.String ? parameter.AsString() : parameter.AsValueString();
    }

    private static double? GetLengthMm(Element element, string parameterName)
    {
        Parameter? parameter = FindParameter(element, parameterName);
        if (parameter == null || parameter.StorageType != StorageType.Double) return null;
        return UnitUtils.ConvertFromInternalUnits(parameter.AsDouble(), UnitTypeId.Millimeters);
    }

    private static double? GetLocationCurveLengthMm(Element element)
    {
        if (element.Location is not LocationCurve locationCurve || locationCurve.Curve == null) return null;
        return UnitUtils.ConvertFromInternalUnits(locationCurve.Curve.Length, UnitTypeId.Millimeters);
    }

    private static double? GetAreaM2(Element element, string parameterName)
    {
        Parameter? parameter = FindParameter(element, parameterName);
        if (parameter == null || parameter.StorageType != StorageType.Double) return null;
        return UnitUtils.ConvertFromInternalUnits(parameter.AsDouble(), UnitTypeId.SquareMeters);
    }

    private static double? GetVolumeM3(Element element, string parameterName)
    {
        Parameter? parameter = FindParameter(element, parameterName);
        if (parameter == null || parameter.StorageType != StorageType.Double) return null;
        return UnitUtils.ConvertFromInternalUnits(parameter.AsDouble(), UnitTypeId.CubicMeters);
    }

    private static string GetLevelName(Document doc, Element element)
    {
        ElementId levelId = element.LevelId;
        if (levelId == ElementId.InvalidElementId)
        {
            Parameter? levelParameter = element.LookupParameter("Reference Level");
            if (levelParameter != null && levelParameter.StorageType == StorageType.ElementId)
                levelId = levelParameter.AsElementId();
        }
        if (levelId == ElementId.InvalidElementId) return string.Empty;
        return doc.GetElement(levelId)?.Name ?? string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static double? FirstNonNull(params double?[] values) =>
        values.FirstOrDefault(value => value.HasValue);

    private static bool ContainsField(IReadOnlyList<string> fields, string target) =>
        fields.Any(field => field.Equals(target, StringComparison.OrdinalIgnoreCase));

    private static QTORequest ParseRequest(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return new QTORequest { Category = string.Empty };
        return JsonSerializer.Deserialize<QTORequest>(argument, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new QTORequest();
    }

    private static string BuildUsage() =>
        "AIGRAV QTO requires a Revit BuiltInCategory.\n\n" +
        "Steel:\n" +
        "{\"category\":\"OST_StructuralFraming\",\"template\":\"StructuralSteel\"}\n\n" +
        "Concrete:\n" +
        "{\"category\":\"OST_StructuralColumns\",\"template\":\"Concrete\"}\n\n" +
        "Area:\n" +
        "{\"category\":\"OST_Walls\",\"template\":\"FinishingArea\"}";

    private enum QTOOutputMode { Generic, Steel, Concrete, Area }

    private sealed class SteelDatabase
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string SourceOfTruth { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public string WeightUnit { get; set; } = "kg/m";
        public List<SteelRecord> Records { get; set; } = new();

        private Dictionary<string, List<SteelRecord>> DimensionIndex { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void BuildIndex()
        {
            DimensionIndex = new Dictionary<string, List<SteelRecord>>(StringComparer.OrdinalIgnoreCase);
            foreach (SteelRecord record in Records)
            {
                string key = NormalizeDimensions(record.Profile);
                if (!DimensionIndex.TryGetValue(key, out List<SteelRecord>? list))
                {
                    list = new List<SteelRecord>();
                    DimensionIndex[key] = list;
                }
                list.Add(record);
            }
        }

        public SteelMatch? Find(string? profile, string? type)
        {
            string profileText = profile ?? string.Empty;
            string typeText = type ?? string.Empty;

            string dimensions = NormalizeDimensions(profileText);
            if (string.IsNullOrWhiteSpace(dimensions))
                dimensions = NormalizeDimensions(typeText);

            if (string.IsNullOrWhiteSpace(dimensions)) return null;
            if (!DimensionIndex.TryGetValue(dimensions, out List<SteelRecord>? candidates)) return null;

            SteelRecord? familyMatch = FindBestFamilyMatch(candidates, $"{profileText} {typeText}");
            if (familyMatch != null)
                return new SteelMatch(familyMatch.Family, familyMatch.Profile, familyMatch.WeightPerMeterKg);

            // If multiple steel families share the same dimensions, do not guess.
            return candidates.Count == 1
                ? new SteelMatch(candidates[0].Family, candidates[0].Profile, candidates[0].WeightPerMeterKg)
                : null;
        }

        private static SteelRecord? FindBestFamilyMatch(IEnumerable<SteelRecord> candidates, string text)
        {
            string normalized = NormalizeFamily(text);
            foreach (SteelRecord candidate in candidates)
            {
                string family = NormalizeFamily(candidate.Family);
                if (normalized.Contains(family, StringComparison.OrdinalIgnoreCase)) return candidate;
            }
            return candidates.FirstOrDefault();
        }
    }

    private sealed class SteelRecord
    {
        public string Family { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public string ProfileKey { get; set; } = string.Empty;
        public double WeightPerMeterKg { get; set; }
        public double StandardLengthM { get; set; }
    }

    private sealed record SteelMatch(string Family, string Profile, double WeightPerMeterKg);

    private static string NormalizeFamily(string? text)
    {
        string value = (text ?? string.Empty).ToUpperInvariant()
            .Replace("×", "X").Replace("*", "X");
        value = Regex.Replace(value, @"\s+", " ").Trim();

        if (value.Contains("H-BEAM") || value.Contains("H BEAM") || value.StartsWith("H")) return "HBEAM";
        if (value.Contains("WIDE FLANGE") || value.StartsWith("WF") || value.StartsWith("W ")) return "WF";
        if (value.Contains("CNP")) return "CNP";
        if (value.Contains("UNP")) return "UNP";
        if (value.Contains("INP")) return "INP";
        if (value.Contains("SIKU") || value.Contains("ANGLE") || value.Contains("L ")) return "SIKU";
        if (value.Contains("SEAMLESS")) return "SEAMLESS";
        if (value.Contains("STAAL-BUIS")) return "PIPA STAAL-BUIS";
        if (value.Contains("STK41")) return "PIPA HITAM STK41";
        if (value.Contains("PIPA")) return "PIPA";
        return value.Replace(" ", string.Empty);
    }

    private static string NormalizeDimensions(string? text)
    {
        string value = (text ?? string.Empty).ToUpperInvariant()
            .Replace("×", "X").Replace("*", "X").Replace(",", ".");

        MatchCollection matches = Regex.Matches(value, @"\d+(?:\.\d+)?");
        if (matches.Count == 0) return string.Empty;

        List<string> numbers = new();
        foreach (Match match in matches)
        {
            if (double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                numbers.Add(number.ToString("0.######", CultureInfo.InvariantCulture));
        }
        return string.Join("X", numbers);
    }

    private static string CreateRevitSchedule(Document doc, BuiltInCategory category, string scheduleName)
    {
        try
        {
            using Transaction transaction = new(doc, "AIGRAV - Create QTO Schedule");
            transaction.Start();

            ViewSchedule schedule = ViewSchedule.CreateSchedule(doc, new ElementId(category));
            schedule.Name = scheduleName;

            ScheduleDefinition definition = schedule.Definition;
            var schedulableFields = definition.GetSchedulableFields();
            var fieldMap = new Dictionary<string, SchedulableField>(StringComparer.OrdinalIgnoreCase);

            foreach (SchedulableField sf in schedulableFields)
            {
                string name = sf.GetName(doc);
                if (!fieldMap.ContainsKey(name))
                    fieldMap[name] = sf;
            }

            ScheduleField? commentsField = null;
            if (fieldMap.TryGetValue("Comments", out SchedulableField? sfComm))
            {
                commentsField = definition.AddField(sfComm);
                commentsField.ColumnHeading = "Comments / Fungsi";
            }

            ScheduleField? typeField = null;
            if (fieldMap.TryGetValue("Family and Type", out SchedulableField? sfType) ||
                fieldMap.TryGetValue("Type", out sfType))
            {
                typeField = definition.AddField(sfType);
                typeField.ColumnHeading = "Type / Profil";
            }

            ScheduleField? lengthField = null;
            if (fieldMap.TryGetValue("Cut Length", out SchedulableField? sfLen) ||
                fieldMap.TryGetValue("Length", out sfLen))
            {
                lengthField = definition.AddField(sfLen);
                lengthField.ColumnHeading = "Total Panjang (m)";
                lengthField.DisplayType = ScheduleFieldDisplayType.Totals;
            }

            ScheduleField? countField = null;
            if (fieldMap.TryGetValue("Count", out SchedulableField? sfCount))
            {
                countField = definition.AddField(sfCount);
                countField.ColumnHeading = "Qty (Unit)";
                countField.DisplayType = ScheduleFieldDisplayType.Totals;
            }

            if (commentsField != null)
                definition.AddSortGroupField(new ScheduleSortGroupField(commentsField.FieldId, ScheduleSortOrder.Ascending));

            if (typeField != null)
                definition.AddSortGroupField(new ScheduleSortGroupField(typeField.FieldId, ScheduleSortOrder.Ascending));

            definition.IsItemized = false;
            definition.ShowGrandTotal = true;
            definition.ShowGrandTotalTitle = true;
            definition.ShowGrandTotalCount = true;

            transaction.Commit();

            return $"Schedule Created in Revit: '{schedule.Name}' (View ID: {schedule.Id.Value})";
        }
        catch (Exception ex)
        {
            return $"Failed to create Revit Schedule: {ex.Message}";
        }
    }
}

using System.Globalization;
using System.Text;
using System.Text.Json;
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
        Version = "4.0",
        Category = "Quantity Takeoff",
        Description = "Universal multi-discipline QTO measurement engine with cleaned SNI/JIS steel table calibration, solid-volume plate/voute weight, surface painting area, and Lokasi grouping.",
        Source = "External",
        Priority = 1500
    };

    private const double SteelDensityKgPerM3 = 7850.0;

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
            QTOOutputMode mode = ResolveOutputMode(template, category);

            SteelDatabaseProvider? database = mode == QTOOutputMode.Steel ? SteelDatabaseProvider.LoadDefault() : null;

            List<QTORecord> records = CollectRecords(doc, category, mode, database, request);
            List<QTORecord> grouped = GroupRecords(records, request, template, mode);

            string renderText = RenderResult(doc, template, category, mode, records, grouped, database);

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

    private static List<QTORecord> CollectRecords(Document doc, BuiltInCategory category, QTOOutputMode mode, SteelDatabaseProvider? database, QTORequest request)
    {
        List<Element> elements = new FilteredElementCollector(doc)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();

        List<QTORecord> records = new(elements.Count);
        foreach (Element element in elements)
        {
            QTORecord rec = BuildRecord(doc, element, category, mode, database);
            if (!string.IsNullOrWhiteSpace(request.LokasiFilter) &&
                !string.Equals(rec.Lokasi, request.LokasiFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(request.CommentsFilter) &&
                !rec.Comments.Contains(request.CommentsFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            records.Add(rec);
        }

        return records;
    }

    private static QTORecord BuildRecord(Document doc, Element element, BuiltInCategory category, QTOOutputMode mode, SteelDatabaseProvider? database)
    {
        ElementType? typeElement = doc.GetElement(element.GetTypeId()) as ElementType;

        string lokasi = FirstNonEmpty(
            GetStringParameter(element, "Lokasi"),
            GetStringParameter(element, "LOKASI"));

        string comments = FirstNonEmpty(
            GetStringParameter(element, "Comments"),
            GetStringParameter(element, "Type of Connection"),
            GetStringParameter(typeElement, "Comments"));

        string familyName = typeElement?.FamilyName ?? string.Empty;
        string typeName = FirstNonEmpty(
            GetStringParameter(element, "Type"),
            typeElement?.Name,
            element.Name);

        string profile = FirstNonEmpty(
            GetStringParameter(element, "Profile"),
            GetStringParameter(typeElement, "Profile"),
            typeName);

        double? lengthMm = FirstNonNull(
            GetLengthMm(element, "Cut Length"),
            GetLengthMm(element, "Length"),
            GetLocationCurveLengthMm(element));
        double? volumeM3 = GetVolumeM3(element, "Volume");

        QTORecord record = new()
        {
            Lokasi = lokasi,
            Comments = comments,
            Type = typeName,
            Profile = profile,
            Level = GetLevelName(doc, element),
            LengthMm = lengthMm,
            AreaM2 = GetAreaM2(element, "Area"),
            VolumeM3 = volumeM3,
            Count = 1,
            Source = "Revit",
            Notes = $"ElementId={element.Id.Value}"
        };

        if (mode == QTOOutputMode.Steel)
        {
            // Plates & Voutes use net solid volume * 7850 kg/m3
            if (category == BuiltInCategory.OST_StructConnectionPlates ||
                category == BuiltInCategory.OST_StructConnectionProfiles)
            {
                double? exactKg = GetNumericParam(element, "Exact Weight") ?? GetNumericParam(element, "Weight");
                if (!exactKg.HasValue && volumeM3.HasValue)
                    exactKg = volumeM3.Value * SteelDensityKgPerM3;

                record.CalibratedWeightKg = exactKg;
                record.SteelMatchStatus = "SOLID_VOLUME";
                record.Notes += "; SteelMatch=SOLID_VOLUME_7850";
            }
            else if (database != null)
            {
                SteelProfileMatch match = database.TryMatch(familyName, typeName, profile);
                record.SteelMatchStatus = match.MatchStatus;
                if (match.MatchStatus == "EXACT" || match.MatchStatus == "ALIAS")
                {
                    record.ReferenceWeightKgPerMeter = match.WeightPerMeterKg;
                    if (lengthMm.HasValue)
                    {
                        double lenM = lengthMm.Value / 1000.0;
                        record.CalibratedWeightKg = lenM * match.WeightPerMeterKg;
                        if (match.SurfaceAreaM2PerM > 0)
                            record.SurfaceAreaM2 = lenM * match.SurfaceAreaM2PerM;
                    }
                    record.Notes += $"; SteelMatch={match.Family}:{match.Profile}; kg/m={match.WeightPerMeterKg.ToString("0.######", CultureInfo.InvariantCulture)}";
                }
                else
                {
                    record.Notes += $"; SteelMatch={match.MatchStatus}";
                }
            }
        }

        return record;
    }

    private static QTOOutputMode ResolveOutputMode(QTOOutputTemplate template, BuiltInCategory category)
    {
        if (category == BuiltInCategory.OST_StructConnectionPlates ||
            category == BuiltInCategory.OST_StructConnectionProfiles)
            return QTOOutputMode.Steel;
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
                ? new List<string> { "Lokasi", "Comments", "Profile" }
                : template.GroupBy;

        if (groupBy.Count == 0) return records;

        return records
            .GroupBy(record => string.Join("¦", groupBy.Select(field => GetFieldText(record, field))))
            .Select(group => AggregateGroup(group, groupBy, mode))
            .OrderBy(record => record.Lokasi)
            .ThenBy(record => record.Comments)
            .ThenBy(record => record.Profile)
            .ThenBy(record => record.Type)
            .ToList();
    }

    private static QTORecord AggregateGroup(IEnumerable<QTORecord> group, IReadOnlyList<string> groupBy, QTOOutputMode mode)
    {
        List<QTORecord> items = group.ToList();
        QTORecord first = items[0];

        bool hasLength = items.Any(x => x.LengthMm.HasValue);
        bool hasArea = items.Any(x => x.AreaM2.HasValue);
        bool hasSurface = items.Any(x => x.SurfaceAreaM2.HasValue);
        bool hasVolume = items.Any(x => x.VolumeM3.HasValue);
        bool hasWeight = items.Any(x => x.CalibratedWeightKg.HasValue);

        return new QTORecord
        {
            Lokasi = ContainsField(groupBy, "Lokasi") ? first.Lokasi : string.Empty,
            Comments = ContainsField(groupBy, "Comments") ? first.Comments : string.Empty,
            Type = first.Type,
            Profile = first.Profile,
            Level = ContainsField(groupBy, "Level") ? first.Level : string.Empty,
            Count = items.Sum(x => x.Count),
            LengthMm = hasLength ? items.Sum(x => x.LengthMm ?? 0) : null,
            AreaM2 = hasArea ? items.Sum(x => x.AreaM2 ?? 0) : null,
            SurfaceAreaM2 = hasSurface ? items.Sum(x => x.SurfaceAreaM2 ?? 0) : null,
            VolumeM3 = hasVolume ? items.Sum(x => x.VolumeM3 ?? 0) : null,
            CalibratedWeightKg = mode == QTOOutputMode.Steel && hasWeight ? items.Sum(x => x.CalibratedWeightKg ?? 0) : null,
            Source = "Revit / Grouped",
            Notes = $"Grouped from {items.Count} element(s); mode={mode}."
        };
    }

    private static string RenderResult(Document doc, QTOOutputTemplate template, BuiltInCategory category, QTOOutputMode mode,
        IReadOnlyList<QTORecord> rawRecords, IReadOnlyList<QTORecord> groupedRecords, SteelDatabaseProvider? database)
    {
        StringBuilder sb = new();
        sb.AppendLine("AIGRAV QTO v4.0 RESULT");
        sb.AppendLine();
        sb.AppendLine($"Project       : {doc.Title}");
        sb.AppendLine($"Category      : {category}");
        sb.AppendLine($"Template      : {template.Name}");
        sb.AppendLine($"Mode          : {mode}");
        sb.AppendLine($"Raw Elements  : {rawRecords.Count}");
        sb.AppendLine($"QTO Groups    : {groupedRecords.Count}");

        if (mode == QTOOutputMode.Steel && database != null)
        {
            sb.AppendLine($"Steel DB      : v{database.Version} ({database.Records.Count} clean rows) -> {database.LoadedPath}");
        }

        sb.AppendLine();
        AppendModeTable(sb, mode, groupedRecords);

        if (mode == QTOOutputMode.Steel)
        {
            int matched = rawRecords.Count(x => x.SteelMatchStatus is "EXACT" or "ALIAS" or "SOLID_VOLUME");
            int ambiguous = rawRecords.Count(x => x.SteelMatchStatus == "AMBIGUOUS");
            int unmatched = rawRecords.Count - matched - ambiguous;
            sb.AppendLine();
            sb.AppendLine($"Steel match   : {matched} matched / {ambiguous} ambiguous / {unmatched} unmatched");
            sb.AppendLine($"Grand Weight  : {groupedRecords.Sum(x => x.CalibratedWeightKg ?? 0):N2} kg");
        }
        else if (mode == QTOOutputMode.Concrete)
        {
            sb.AppendLine();
            sb.AppendLine($"Grand Volume  : {groupedRecords.Sum(x => x.VolumeM3 ?? 0):N2} m³");
        }
        else if (mode == QTOOutputMode.Area)
        {
            sb.AppendLine();
            sb.AppendLine($"Grand Area    : {groupedRecords.Sum(x => x.AreaM2 ?? 0):N2} m²");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendModeTable(StringBuilder sb, QTOOutputMode mode, IReadOnlyList<QTORecord> records)
    {
        if (mode == QTOOutputMode.Steel)
        {
            sb.AppendLine("| Lokasi | Comments / Fungsi | Type / Profil | Total Panjang (m) | Qty | Berat (kg) | Luas Cat (m²) |");
            sb.AppendLine("|---|---|---|---:|---:|---:|---:|");
            foreach (QTORecord r in records)
                sb.AppendLine($"| {Safe(r.Lokasi)} | {Safe(r.Comments)} | {Safe(r.Profile)} | {FormatMeters(r.LengthMm)} | {r.Count:0} | {FormatNumber(r.CalibratedWeightKg)} | {FormatNumber(r.SurfaceAreaM2)} |");
            return;
        }

        if (mode == QTOOutputMode.Concrete)
        {
            sb.AppendLine("| Lokasi | Comments / Fungsi | Type | Total Panjang (m) | Qty | Volume (m³) |");
            sb.AppendLine("|---|---|---|---:|---:|---:|");
            foreach (QTORecord r in records)
                sb.AppendLine($"| {Safe(r.Lokasi)} | {Safe(r.Comments)} | {Safe(r.Type)} | {FormatMeters(r.LengthMm)} | {r.Count:0} | {FormatNumber(r.VolumeM3)} |");
            return;
        }

        if (mode == QTOOutputMode.Area)
        {
            sb.AppendLine("| Lokasi | Comments / Fungsi | Type | Area (m²) | Qty |");
            sb.AppendLine("|---|---|---|---:|---:|");
            foreach (QTORecord r in records)
                sb.AppendLine($"| {Safe(r.Lokasi)} | {Safe(r.Comments)} | {Safe(r.Type)} | {FormatNumber(r.AreaM2)} | {r.Count:0} |");
            return;
        }

        sb.AppendLine("| Lokasi | Comments / Fungsi | Type | Total Panjang (m) | Qty | Volume (m³) | Area (m²) |");
        sb.AppendLine("|---|---|---|---:|---:|---:|---:|");
        foreach (QTORecord r in records)
            sb.AppendLine($"| {Safe(r.Lokasi)} | {Safe(r.Comments)} | {Safe(r.Type)} | {FormatMeters(r.LengthMm)} | {r.Count:0} | {FormatNumber(r.VolumeM3)} | {FormatNumber(r.AreaM2)} |");
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
            var fieldMap = new Dictionary<string, SchedulableField>(StringComparer.OrdinalIgnoreCase);
            foreach (SchedulableField sf in definition.GetSchedulableFields())
            {
                string name = sf.GetName(doc);
                if (!fieldMap.ContainsKey(name)) fieldMap[name] = sf;
            }

            // Column A = Lokasi mandatory
            if (fieldMap.TryGetValue("Lokasi", out SchedulableField? sfLok))
            {
                var fLok = definition.AddField(sfLok);
                fLok.ColumnHeading = "Lokasi";
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

            if (fieldMap.TryGetValue("Cut Length", out SchedulableField? sfLen) ||
                fieldMap.TryGetValue("Length", out sfLen))
            {
                var lf = definition.AddField(sfLen);
                lf.ColumnHeading = "Total Panjang (m)";
                lf.DisplayType = ScheduleFieldDisplayType.Totals;
            }

            if (fieldMap.TryGetValue("Count", out SchedulableField? sfCount))
            {
                var cf = definition.AddField(sfCount);
                cf.ColumnHeading = "Qty (Unit)";
                cf.DisplayType = ScheduleFieldDisplayType.Totals;
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

    private static string FormatMeters(double? lengthMm) =>
        lengthMm.HasValue ? (lengthMm.Value / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) : "-";

    private static string FormatNumber(double? value) =>
        value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) : "-";

    private static string Safe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Replace("|", "/").Trim();

    private static string GetFieldText(QTORecord record, string field)
    {
        if (field.Equals("Lokasi", StringComparison.OrdinalIgnoreCase)) return record.Lokasi;
        if (field.Equals("Comments", StringComparison.OrdinalIgnoreCase)) return record.Comments;
        if (field.Equals("Type", StringComparison.OrdinalIgnoreCase)) return record.Type;
        if (field.Equals("Profile", StringComparison.OrdinalIgnoreCase)) return record.Profile;
        if (field.Equals("Level", StringComparison.OrdinalIgnoreCase)) return record.Level;
        return string.Empty;
    }

    private static string? GetStringParameter(Element? element, string parameterName)
    {
        Parameter? p = element?.LookupParameter(parameterName);
        if (p == null) return null;
        return p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
    }

    private static double? GetNumericParam(Element element, string parameterName)
    {
        Parameter? p = element.LookupParameter(parameterName);
        if (p == null || p.StorageType != StorageType.Double) return null;
        return p.AsDouble();
    }

    private static double? GetLengthMm(Element element, string parameterName)
    {
        Parameter? p = element.LookupParameter(parameterName);
        if (p == null || p.StorageType != StorageType.Double) return null;
        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
    }

    private static double? GetLocationCurveLengthMm(Element element)
    {
        if (element.Location is not LocationCurve locationCurve || locationCurve.Curve == null) return null;
        return UnitUtils.ConvertFromInternalUnits(locationCurve.Curve.Length, UnitTypeId.Millimeters);
    }

    private static double? GetAreaM2(Element element, string parameterName)
    {
        Parameter? p = element.LookupParameter(parameterName);
        if (p == null || p.StorageType != StorageType.Double) return null;
        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.SquareMeters);
    }

    private static double? GetVolumeM3(Element element, string parameterName)
    {
        Parameter? p = element.LookupParameter(parameterName);
        if (p == null || p.StorageType != StorageType.Double) return null;
        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.CubicMeters);
    }

    private static string GetLevelName(Document doc, Element element)
    {
        ElementId levelId = element.LevelId;
        if (levelId == ElementId.InvalidElementId)
        {
            Parameter? lp = element.LookupParameter("Reference Level");
            if (lp != null && lp.StorageType == StorageType.ElementId) levelId = lp.AsElementId();
        }
        return levelId == ElementId.InvalidElementId ? string.Empty : doc.GetElement(levelId)?.Name ?? string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static double? FirstNonNull(params double?[] values) =>
        values.FirstOrDefault(v => v.HasValue);

    private static bool ContainsField(IReadOnlyList<string> fields, string target) =>
        fields.Any(f => f.Equals(target, StringComparison.OrdinalIgnoreCase));

    private static QTORequest ParseRequest(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return new QTORequest();
        return JsonSerializer.Deserialize<QTORequest>(argument, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new QTORequest();
    }

    private static string BuildUsage() =>
        "AIGRAV QTO v4.0 requires a Revit BuiltInCategory.\n" +
        "Examples:\n" +
        "{\"category\":\"OST_StructuralFraming\",\"template\":\"StructuralSteel\"}\n" +
        "{\"category\":\"OST_StructuralColumns\",\"template\":\"Concrete\"}\n" +
        "{\"category\":\"OST_Walls\",\"template\":\"FinishingArea\"}";

    private enum QTOOutputMode { Generic, Steel, Concrete, Area }
}

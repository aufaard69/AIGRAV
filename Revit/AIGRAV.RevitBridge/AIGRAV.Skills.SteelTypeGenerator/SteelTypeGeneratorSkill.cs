using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.SteelTypeGenerator;

/// <summary>
/// Batch 5C: creates/validates Revit Structural Framing types from the
/// AIGRAV Steel Master Database. The skill intentionally does not create or
/// overwrite Family files. It works with families already loaded in the project.
/// </summary>
public sealed class SteelTypeGeneratorSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "Steel Type Generator",
        Version = "1.0",
        Category = "Steel Automation",
        Description = "Creates and validates Revit Structural Framing types from the AIGRAV Steel Master Database.",
        Source = "External",
        Priority = 1400
    };

    private static readonly object DatabaseSync = new();
    private static SteelMasterDatabase? _database;
    private static string _databasePath = string.Empty;

    private static readonly Dictionary<string, string[]> FamilyAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["WF"] = ["WF", "W-WIDE FLANGE", "WIDE FLANGE", "WIDE-FLANGE"],
            ["HBEAM"] = ["H-BEAM", "H BEAM", "HBEAM"],
            ["INP"] = ["INP", "I-BEAM", "I BEAM", "I SECTION"],
            ["UNP"] = ["UNP", "U-CHANNEL", "U CHANNEL", "CHANNEL"],
            ["CNP"] = ["CNP", "C-CHANNEL", "C CHANNEL", "CHANNEL"],
            ["SIKU"] = ["SIKU", "L-ANGLE", "L ANGLE", "ANGLE"]
        };

    public bool CanHandle(string toolName) =>
        toolName.Equals("aigrav_steel_types", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("aigrav_steel_scan", StringComparison.OrdinalIgnoreCase);

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null)
            return "Steel Type Generator failed: no active Revit document.";

        try
        {
            SteelTypeRequest request = ParseRequest(argument);

            if (toolName.Equals("aigrav_steel_scan", StringComparison.OrdinalIgnoreCase) ||
                request.Operation.Equals("scan", StringComparison.OrdinalIgnoreCase))
            {
                return ScanFamilies(doc);
            }

            return request.Operation.ToLowerInvariant() switch
            {
                "validate" => ValidateDatabase(doc, request),
                "create" => CreateTypes(doc, request),
                "load_library" => LoadLibraryFamily(doc, request),
                _ => BuildUsage()
            };
        }
        catch (JsonException ex)
        {
            return "Steel Type Generator failed: invalid JSON argument.\n\n" + ex.Message;
        }
        catch (Exception ex)
        {
            return "Steel Type Generator failed:\n\n" + ex;
        }
    }

    private static SteelTypeRequest ParseRequest(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return new SteelTypeRequest();

        SteelTypeRequest? request = JsonSerializer.Deserialize<SteelTypeRequest>(
            argument,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return request ?? new SteelTypeRequest();
    }

    private static string BuildUsage() =>
        "AIGRAV Steel Type Generator\n\n" +
        "Tool: aigrav_steel_types\n\n" +
        "Operations:\n" +
        "- scan\n" +
        "- validate\n" +
        "- create\n\n" +
        "Safe example:\n" +
        "{\"operation\":\"create\",\"family\":\"WF\",\"profile\":\"WF 400x200x8x13\",\"dryRun\":true}\n\n" +
        "Create all profiles of one family:\n" +
        "{\"operation\":\"create\",\"family\":\"WF\",\"all\":true,\"dryRun\":true}\n\n" +
        "For real creation set dryRun=false. Use revitFamily/baseType when auto-detection is ambiguous.";

    private static string ScanFamilies(Document doc)
    {
        List<FamilySymbol> symbols = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .OrderBy(x => x.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symbols.Count == 0)
            return "AIGRAV Steel Type Generator\n\nNo Structural Framing Family Types are loaded in the active project.";

        StringBuilder sb = new();
        sb.AppendLine("AIGRAV STEEL FAMILY SCAN");
        sb.AppendLine($"Structural Framing Types: {symbols.Count}");
        sb.AppendLine();

        foreach (IGrouping<string, FamilySymbol> family in symbols.GroupBy(x => x.FamilyName, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"FAMILY: {family.Key}");
            foreach (FamilySymbol symbol in family.Take(30))
                sb.AppendLine($"  - {symbol.Name}");
            if (family.Count() > 30)
                sb.AppendLine($"  ... +{family.Count() - 30} more");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string ValidateDatabase(Document doc, SteelTypeRequest request)
    {
        SteelMasterDatabase database = LoadDatabase();
        IEnumerable<SteelRecord> records = SelectRecords(database.Records, request);
        List<FamilySymbol> symbols = GetStructuralFramingSymbols(doc);

        StringBuilder sb = new();
        sb.AppendLine("AIGRAV STEEL TYPE VALIDATION");
        sb.AppendLine($"Database: {_databasePath}");
        sb.AppendLine($"Records selected: {records.Count()}");
        sb.AppendLine();

        foreach (SteelRecord record in records)
        {
            FamilyResolution resolution = ResolveFamily(symbols, record.Family, request.RevitFamily);
            sb.AppendLine($"{record.Profile} | Family={record.Family} | " +
                          (resolution.Success
                              ? $"RevitFamily={resolution.FamilyName}"
                              : $"STATUS={resolution.Message}"));
        }

        return sb.ToString().TrimEnd();
    }

    private static string LoadLibraryFamily(Document doc, SteelTypeRequest request)
    {
        string path = request.FamilyPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return $"LoadLibraryFamily failed: File not found at '{path}'.";

        string familyName = Path.GetFileNameWithoutExtension(path);

        try
        {
            using Transaction tx = new(doc, $"AIGRAV - Load Family {familyName}");
            tx.Start();

            bool loaded = doc.LoadFamily(path, out Family loadedFamily);

            tx.Commit();

            if (loaded && loadedFamily != null)
                return $"Successfully loaded Family '{loadedFamily.Name}' from '{path}'.";
            else
                return $"Family '{familyName}' was already loaded or partially merged.";
        }
        catch (Exception ex)
        {
            return $"LoadLibraryFamily failed for '{familyName}': {ex.Message}";
        }
    }

    private static string CreateTypes(Document doc, SteelTypeRequest request)
    {
        SteelMasterDatabase database = LoadDatabase();
        List<SteelRecord> records = SelectRecords(database.Records, request).ToList();

        if (records.Count == 0)
            return "No steel database records matched the request.";

        List<FamilySymbol> symbols = GetStructuralFramingSymbols(doc);
        List<CreatePlanItem> plan = new();

        foreach (SteelRecord record in records)
        {
            FamilyResolution family = ResolveFamily(symbols, record.Family, request.RevitFamily);
            if (!family.Success)
            {
                plan.Add(CreatePlanItem.Failed(record, family.Message));
                continue;
            }

            FamilySymbol? baseSymbol = ResolveBaseSymbol(family.Symbols, request.BaseType);
            if (baseSymbol == null)
            {
                plan.Add(CreatePlanItem.Failed(record,
                    string.IsNullOrWhiteSpace(request.BaseType)
                        ? "No base Family Type available."
                        : $"Base Type '{request.BaseType}' not found."));
                continue;
            }

            FamilySymbol? existing = family.Symbols.FirstOrDefault(x =>
                string.Equals(x.Name, record.TypeName, StringComparison.OrdinalIgnoreCase));

            plan.Add(new CreatePlanItem(record, family.FamilyName, baseSymbol, existing));
        }

        if (request.DryRun)
            return RenderPlan(plan, dryRun: true);

        int created = 0;
        int updated = 0;
        int failed = 0;

        using Transaction tx = new(doc, "AIGRAV - Create Steel Types");
        tx.Start();

        foreach (CreatePlanItem item in plan)
        {
            if (!item.Success)
            {
                failed++;
                continue;
            }

            try
            {
                FamilySymbol target = item.Existing ?? (FamilySymbol)item.BaseSymbol!.Duplicate(item.Record.TypeName);
                if (item.Existing == null)
                    created++;
                else
                    updated++;

                ApplyRecordToType(target, item.Record);
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
                failed++;
            }
        }

        if (failed > 0 && request.CommitPartial)
            tx.Commit();
        else if (failed > 0)
        {
            tx.RollBack();
            return RenderPlan(plan, dryRun: false, rolledBack: true);
        }
        else
            tx.Commit();

        return RenderPlan(plan, dryRun: false, created, updated, failed);
    }

    private static void ApplyRecordToType(FamilySymbol symbol, SteelRecord record)
    {
        foreach (KeyValuePair<string, double> dimension in record.DimensionsMm)
        {
            SetLengthParameter(symbol, dimension.Key, dimension.Value);
        }

        SetTextParameter(symbol, "Profile", record.Profile);
        SetTextParameter(symbol, "Type Mark", record.Profile);
        SetTextParameter(symbol, "Comments", $"AIGRAV Steel DB | {record.Profile}");
        SetTextParameter(symbol, "AIGRAV_Profile", record.Profile);
        SetTextParameter(symbol, "AIGRAV_ProfileKey", record.ProfileKey);
        SetTextParameter(symbol, "AIGRAV_Family", record.Family);
        SetDoubleParameter(symbol, "AIGRAV_WeightPerMeterKg", record.WeightPerMeterKg);
        SetDoubleParameter(symbol, "Weight Per Meter", record.WeightPerMeterKg);
        SetDoubleParameter(symbol, "Mass per Unit Length", record.WeightPerMeterKg);
        SetDoubleParameter(symbol, "AIGRAV_StandardLengthM", record.StandardLengthM);
    }

    private static void SetLengthParameter(FamilySymbol symbol, string dimension, double millimeters)
    {
        string[] aliases = dimension switch
        {
            "H" => ["H", "h", "Height", "Depth", "d"],
            "B" => ["B", "b", "Width", "bf", "Flange Width", "FlangeWidth"],
            "T" => ["T", "t", "Thickness"],
            "Tw" => ["Tw", "tw", "Web Thickness", "WebThickness", "t_w"],
            "Tf" => ["Tf", "tf", "Flange Thickness", "FlangeThickness", "t_f"],
            "R" => ["R", "r", "Fillet Radius", "Root Radius"],
            _ => [dimension]
        };

        Parameter? parameter = FindParameter(symbol, aliases);
        if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
            return;

        parameter.Set(UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters));
    }

    private static void SetDoubleParameter(FamilySymbol symbol, string name, double value)
    {
        Parameter? parameter = FindParameter(symbol, [name]);
        if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
            return;

        // These are custom/user parameters. Their display unit may be project-specific.
        // Only set them when the parameter's definition explicitly matches the requested name.
        parameter.Set(value);
    }

    private static void SetTextParameter(FamilySymbol symbol, string name, string value)
    {
        Parameter? parameter = FindParameter(symbol, [name]);
        if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
            return;

        parameter.Set(value);
    }

    private static Parameter? FindParameter(FamilySymbol symbol, IEnumerable<string> aliases)
    {
        Dictionary<string, string> normalized = aliases
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToDictionary(NormalizeName, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (Parameter parameter in symbol.Parameters)
        {
            string name = parameter.Definition?.Name ?? string.Empty;
            if (normalized.ContainsKey(NormalizeName(name)))
                return parameter;
        }

        return null;
    }

    private static string NormalizeName(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static List<FamilySymbol> GetStructuralFramingSymbols(Document doc) =>
        new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .ToList();

    private static FamilyResolution ResolveFamily(
        IReadOnlyList<FamilySymbol> symbols,
        string databaseFamily,
        string? requestedFamilyName)
    {
        if (!string.IsNullOrWhiteSpace(requestedFamilyName))
        {
            List<FamilySymbol> exact = symbols
                .Where(x => string.Equals(x.FamilyName, requestedFamilyName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return exact.Count > 0
                ? new FamilyResolution(true, requestedFamilyName, exact, "")
                : new FamilyResolution(false, requestedFamilyName, [], $"Revit family '{requestedFamilyName}' not found.");
        }

        if (!FamilyAliases.TryGetValue(databaseFamily, out string[]? aliases))
            return new FamilyResolution(false, "", [], $"No family alias map exists for '{databaseFamily}'.");

        List<FamilySymbol> candidates = symbols
            .Where(x => aliases.Any(alias => ContainsNormalized(x.FamilyName, alias)))
            .ToList();

        List<string> familyNames = candidates
            .Select(x => x.FamilyName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (familyNames.Count == 1)
            return new FamilyResolution(true, familyNames[0], candidates, "");

        if (familyNames.Count == 0)
            return new FamilyResolution(false, "", [], $"No loaded Structural Framing family matched '{databaseFamily}'.");

        return new FamilyResolution(false, "", candidates,
            $"Ambiguous Revit family for '{databaseFamily}': {string.Join(", ", familyNames)}. Supply 'revitFamily'.");
    }

    private static bool ContainsNormalized(string value, string token)
    {
        string a = NormalizeName(value);
        string b = NormalizeName(token);
        return a.Contains(b, StringComparison.OrdinalIgnoreCase);
    }

    private static FamilySymbol? ResolveBaseSymbol(IReadOnlyList<FamilySymbol> symbols, string? requestedType)
    {
        if (!string.IsNullOrWhiteSpace(requestedType))
        {
            return symbols.FirstOrDefault(x =>
                string.Equals(x.Name, requestedType, StringComparison.OrdinalIgnoreCase));
        }

        return symbols.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    private static IEnumerable<SteelRecord> SelectRecords(
        IEnumerable<SteelRecord> records,
        SteelTypeRequest request)
    {
        IEnumerable<SteelRecord> query = records;

        if (!string.IsNullOrWhiteSpace(request.Family))
            query = query.Where(x => string.Equals(x.Family, request.Family, StringComparison.OrdinalIgnoreCase));

        if (!request.All && !string.IsNullOrWhiteSpace(request.Profile))
        {
            string needle = NormalizeProfile(request.Profile);
            query = query.Where(x =>
                NormalizeProfile(x.Profile) == needle ||
                NormalizeProfile(x.ProfileKey) == needle ||
                NormalizeProfile(x.TypeName) == needle);
        }
        else if (!request.All && string.IsNullOrWhiteSpace(request.Profile))
        {
            query = query.Take(1);
        }

        return query.OrderBy(x => x.Family).ThenBy(x => x.TypeName);
    }

    private static string NormalizeProfile(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static SteelMasterDatabase LoadDatabase()
    {
        lock (DatabaseSync)
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(SteelTypeGeneratorSkill).Assembly.Location) ?? string.Empty;

            Assembly? hostAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "AIGRAV.RevitBridge", StringComparison.OrdinalIgnoreCase));
            string hostDirectory = hostAssembly?.Location != null ? Path.GetDirectoryName(hostAssembly.Location) ?? string.Empty : string.Empty;

            List<string> candidates = new();
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                candidates.Add(Path.Combine(assemblyDirectory, "SteelDatabase", "AIGRAV_SteelMasterDatabase_v2.json"));
                candidates.Add(Path.Combine(assemblyDirectory, "SteelDatabase", "AIGRAV.SteelDatabase.json"));
            }
            if (!string.IsNullOrWhiteSpace(hostDirectory))
            {
                candidates.Add(Path.Combine(hostDirectory, "Skills", "SteelDatabase", "AIGRAV_SteelMasterDatabase_v2.json"));
                candidates.Add(Path.Combine(hostDirectory, "Skills", "SteelDatabase", "AIGRAV.SteelDatabase.json"));
            }
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Skills", "SteelDatabase", "AIGRAV_SteelMasterDatabase_v2.json"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Skills", "SteelDatabase", "AIGRAV.SteelDatabase.json"));

            string path = candidates.FirstOrDefault(File.Exists) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
                throw new FileNotFoundException("Steel Master Database JSON not found.", candidates.Count > 0 ? candidates[0] : "unknown");

            if (_database != null && string.Equals(_databasePath, path, StringComparison.OrdinalIgnoreCase))
                return _database;

            string json = File.ReadAllText(path);
            SteelMasterDatabase? database = JsonSerializer.Deserialize<SteelMasterDatabase>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (database?.Records == null || database.Records.Count == 0)
                throw new InvalidDataException("Steel Master Database contains no records.");

            _database = database;
            _databasePath = path;
            return database;
        }
    }

    private static string RenderPlan(
        IReadOnlyList<CreatePlanItem> plan,
        bool dryRun,
        int created = 0,
        int updated = 0,
        int failed = 0,
        bool rolledBack = false)
    {
        StringBuilder sb = new();
        sb.AppendLine("AIGRAV STEEL TYPE GENERATOR");
        sb.AppendLine($"Mode: {(dryRun ? "DRY RUN" : "COMMIT")}");
        if (!string.IsNullOrWhiteSpace(_databasePath))
            sb.AppendLine($"Database: {_databasePath}");
        sb.AppendLine();

        foreach (CreatePlanItem item in plan)
        {
            if (!item.Success)
            {
                sb.AppendLine($"✗ {item.Record.Profile} | {item.Record.Family} | {item.Error}");
                continue;
            }

            string action = item.Existing != null ? "UPDATE" : "CREATE";
            sb.AppendLine($"✓ {action} | {item.Record.TypeName} | Revit Family: {item.FamilyName} | " +
                          $"{FormatDimensions(item.Record.DimensionsMm)} | {item.Record.WeightPerMeterKg:0.######} kg/m");
        }

        sb.AppendLine();
        sb.AppendLine($"Planned: {plan.Count}");
        if (!dryRun)
        {
            sb.AppendLine($"Created: {created}");
            sb.AppendLine($"Updated: {updated}");
            sb.AppendLine($"Failed: {failed}");
            if (rolledBack)
                sb.AppendLine("Transaction: ROLLED BACK because at least one item failed and commitPartial=false.");
        }
        else
        {
            sb.AppendLine("No Revit changes were committed.");
            sb.AppendLine("Set dryRun=false to create/update types.");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatDimensions(IReadOnlyDictionary<string, double> dimensions) =>
        string.Join(" × ", dimensions.Select(x =>
            $"{x.Key}={x.Value.ToString("0.###", CultureInfo.InvariantCulture)} mm"));

    private sealed record FamilyResolution(
        bool Success,
        string FamilyName,
        IReadOnlyList<FamilySymbol> Symbols,
        string Message);

    private sealed class CreatePlanItem
    {
        public CreatePlanItem(SteelRecord record, string familyName, FamilySymbol baseSymbol, FamilySymbol? existing)
        {
            Record = record;
            FamilyName = familyName;
            BaseSymbol = baseSymbol;
            Existing = existing;
        }

        public CreatePlanItem(SteelRecord record, string error)
        {
            Record = record;
            Error = error;
        }

        public SteelRecord Record { get; }
        public string FamilyName { get; } = string.Empty;
        public FamilySymbol? BaseSymbol { get; }
        public FamilySymbol? Existing { get; }
        public string Error { get; set; } = string.Empty;
        public bool Success => BaseSymbol != null && string.IsNullOrWhiteSpace(Error);

        public static CreatePlanItem Failed(SteelRecord record, string error) => new(record, error);
    }

    private sealed class SteelTypeRequest
    {
        public string Operation { get; set; } = "scan";
        public string? Family { get; set; }
        public string? Profile { get; set; }
        public bool All { get; set; }
        public bool DryRun { get; set; } = true;
        public bool CommitPartial { get; set; }
        public string? RevitFamily { get; set; }
        public string? BaseType { get; set; }
        public string? FamilyPath { get; set; }
    }

    private sealed class SteelMasterDatabase
    {
        public string SchemaVersion { get; set; } = "2.0";
        public string Source { get; set; } = string.Empty;
        public List<SteelRecord> Records { get; set; } = [];
    }

    private sealed class SteelRecord
    {
        public string Family { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public string ProfileKey { get; set; } = string.Empty;
        public Dictionary<string, double> DimensionsMm { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public double WeightPerMeterKg { get; set; }
        public double StandardLengthM { get; set; }
        public string SourceSheet { get; set; } = string.Empty;
        public int SourceRow { get; set; }
        public string TypeName { get; set; } = string.Empty;
    }
}

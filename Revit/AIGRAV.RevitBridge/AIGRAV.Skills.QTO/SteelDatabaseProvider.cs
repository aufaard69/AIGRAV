using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIGRAV.Skills.QTO;

public sealed record SteelProfileMatch(
    string Family,
    string Profile,
    string GradeOrSchedule,
    double WeightPerMeterKg,
    double SurfaceAreaM2PerM,
    string MatchStatus // EXACT | ALIAS | AMBIGUOUS | NOT_FOUND
);

public sealed class SteelProfileRecord
{
    public string Family { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string ProfileKey { get; set; } = string.Empty;
    public string GradeOrSchedule { get; set; } = string.Empty;
    public double WeightPerMeterKg { get; set; }
    public double StandardLengthM { get; set; }
    public double SurfaceAreaM2PerM { get; set; }
    public List<string> Aliases { get; set; } = new();
}

public sealed class SteelDatabasePayload
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SourceOfTruth { get; set; } = string.Empty;
    public double DensityKgPerM3 { get; set; } = 7850.0;
    public int RecordCount { get; set; }
    public List<SteelProfileRecord> Records { get; set; } = new();
}

public sealed class SteelDatabaseProvider
{
    private static readonly object Sync = new();
    private static SteelDatabaseProvider? _cached;
    private static DateTime _cachedMtimeUtc;
    private static string _cachedPath = string.Empty;

    public string LoadedPath { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string LoadMessage { get; private set; } = string.Empty;
    public List<SteelProfileRecord> Records { get; private set; } = new();

    private readonly Dictionary<string, List<SteelProfileRecord>> _dimIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SteelProfileRecord>> _aliasIndex = new(StringComparer.OrdinalIgnoreCase);

    public static SteelDatabaseProvider LoadDefault()
    {
        lock (Sync)
        {
            string path = ResolveDatabasePath();
            DateTime mtime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (_cached != null &&
                string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) &&
                _cachedMtimeUtc == mtime)
            {
                return _cached;
            }

            var provider = new SteelDatabaseProvider();
            provider.LoadFromPath(path);
            _cached = provider;
            _cachedPath = path;
            _cachedMtimeUtc = mtime;
            return provider;
        }
    }

    public static string ResolveDatabasePath()
    {
        string? envPath = Environment.GetEnvironmentVariable("AIGRAV_STEEL_DATABASE");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return Path.GetFullPath(envPath);

        List<string> candidates = new();

        Assembly? hostAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
            string.Equals(a.GetName().Name, "AIGRAV.RevitBridge", StringComparison.OrdinalIgnoreCase));
        string? hostDir = !string.IsNullOrWhiteSpace(hostAssembly?.Location)
            ? Path.GetDirectoryName(hostAssembly.Location)
            : null;
        if (!string.IsNullOrWhiteSpace(hostDir))
        {
            candidates.Add(Path.Combine(hostDir, "Skills", "SteelDatabase", "steel_profiles_sni_jis.json"));
            candidates.Add(Path.Combine(hostDir, "Skills", "SteelDatabase", "AIGRAV.SteelDatabase.json"));
        }

        candidates.Add(@"C:\AIGRAV\Databases\QTO\steel_profiles_sni_jis.json");
        candidates.Add(@"C:\AIGRAV\Revit\AIGRAV.SteelDatabase.csv");
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "Skills", "SteelDatabase", "steel_profiles_sni_jis.json"));

        foreach (string c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        return candidates[0];
    }

    private void LoadFromPath(string path)
    {
        LoadedPath = path;
        Records = new List<SteelProfileRecord>();
        _dimIndex.Clear();
        _aliasIndex.Clear();

        if (!File.Exists(path))
        {
            LoadMessage = $"Database file not found at: {path}";
            return;
        }

        try
        {
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string json = File.ReadAllText(path);
                var payload = JsonSerializer.Deserialize<SteelDatabasePayload>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (payload != null)
                {
                    Version = payload.Version;
                    Records = payload.Records ?? new List<SteelProfileRecord>();
                }
            }
            else if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                Version = "csv-fallback";
                string[] lines = File.ReadAllLines(path);
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    string[] cols = SplitCsvLine(lines[i]);
                    if (cols.Length < 5) continue;
                    if (!double.TryParse(cols[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double kgm)) continue;
                    double.TryParse(cols[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double stdLen);
                    Records.Add(new SteelProfileRecord
                    {
                        Family = cols[0].Trim(),
                        Profile = cols[1].Trim(),
                        ProfileKey = cols[2].Trim(),
                        WeightPerMeterKg = kgm,
                        StandardLengthM = stdLen > 0 ? stdLen : 12.0
                    });
                }
            }

            BuildIndexes();
            LoadMessage = $"Loaded {Records.Count} clean steel records (v{Version}).";
        }
        catch (Exception ex)
        {
            LoadMessage = $"Error loading steel database: {ex.Message}";
        }
    }

    private static string[] SplitCsvLine(string line)
    {
        List<string> result = new();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (char ch in line)
        {
            if (ch == '"') inQuotes = !inQuotes;
            else if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(ch);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private void BuildIndexes()
    {
        foreach (SteelProfileRecord rec in Records)
        {
            string dimKey = NormalizeDimensions(rec.Profile);
            if (!string.IsNullOrWhiteSpace(dimKey))
            {
                if (!_dimIndex.TryGetValue(dimKey, out var list))
                {
                    list = new List<SteelProfileRecord>();
                    _dimIndex[dimKey] = list;
                }
                list.Add(rec);
            }

            AddAliasKey($"{rec.Family} {rec.Profile}", rec);
            AddAliasKey(rec.Profile, rec);
            foreach (string al in rec.Aliases ?? Enumerable.Empty<string>())
            {
                AddAliasKey(al, rec);
            }
        }
    }

    private void AddAliasKey(string? raw, SteelProfileRecord rec)
    {
        string norm = NormalizeAliasText(raw);
        if (string.IsNullOrWhiteSpace(norm)) return;
        if (!_aliasIndex.TryGetValue(norm, out var list))
        {
            list = new List<SteelProfileRecord>();
            _aliasIndex[norm] = list;
        }
        if (!list.Contains(rec)) list.Add(rec);
    }

    public SteelProfileMatch TryMatch(string? familyName, string? typeName, string? profileParam = null)
    {
        string combined = $"{familyName} {typeName} {profileParam}".Trim();
        string detectedFam = NormalizeFamily(combined);

        // 1. Direct alias match (typeName or profileParam or combined)
        foreach (string probe in new[] { profileParam ?? "", typeName ?? "", combined })
        {
            string norm = NormalizeAliasText(probe);
            if (!string.IsNullOrWhiteSpace(norm) && _aliasIndex.TryGetValue(norm, out var exactList))
            {
                var filtered = FilterByDetectedFamily(exactList, detectedFam);
                if (filtered.Count == 1)
                {
                    var r = filtered[0];
                    return new SteelProfileMatch(r.Family, r.Profile, r.GradeOrSchedule, r.WeightPerMeterKg, r.SurfaceAreaM2PerM, "ALIAS");
                }
            }
        }

        // 2. Numeric dimension match + explicit family check (never silent FirstOrDefault across different families)
        string dims = NormalizeDimensions(profileParam);
        if (string.IsNullOrWhiteSpace(dims)) dims = NormalizeDimensions(typeName);
        if (string.IsNullOrWhiteSpace(dims)) dims = NormalizeDimensions(familyName);

        if (!string.IsNullOrWhiteSpace(dims) && _dimIndex.TryGetValue(dims, out var candidates))
        {
            var famCandidates = FilterByDetectedFamily(candidates, detectedFam);
            if (famCandidates.Count == 1)
            {
                var r = famCandidates[0];
                return new SteelProfileMatch(r.Family, r.Profile, r.GradeOrSchedule, r.WeightPerMeterKg, r.SurfaceAreaM2PerM, "EXACT");
            }

            if (famCandidates.Count > 1)
            {
                // Disambiguate PIPA grade/schedule if present in combined text
                string upper = combined.ToUpperInvariant();
                var schedMatches = famCandidates.Where(c =>
                    !string.IsNullOrWhiteSpace(c.GradeOrSchedule) &&
                    upper.Contains(c.GradeOrSchedule.ToUpperInvariant())).ToList();
                if (schedMatches.Count == 1)
                {
                    var r = schedMatches[0];
                    return new SteelProfileMatch(r.Family, r.Profile, r.GradeOrSchedule, r.WeightPerMeterKg, r.SurfaceAreaM2PerM, "EXACT");
                }
                return new SteelProfileMatch(detectedFam, combined, string.Empty, 0, 0, "AMBIGUOUS");
            }

            if (string.IsNullOrWhiteSpace(detectedFam) && candidates.Count == 1)
            {
                var r = candidates[0];
                return new SteelProfileMatch(r.Family, r.Profile, r.GradeOrSchedule, r.WeightPerMeterKg, r.SurfaceAreaM2PerM, "EXACT");
            }

            return new SteelProfileMatch(detectedFam, combined, string.Empty, 0, 0, "AMBIGUOUS");
        }

        // 3. Single nominal number fallback (e.g. "WF 350", "H 300", "C 150", "RCWF.450") when family is known
        if (!string.IsNullOrWhiteSpace(detectedFam))
        {
            var singleNum = Regex.Match(typeName ?? profileParam ?? combined, @"\d+(?:\.\d+)?");
            if (singleNum.Success)
            {
                string head = singleNum.Value;
                var headMatches = Records.Where(r =>
                    string.Equals(r.Family, detectedFam, StringComparison.OrdinalIgnoreCase) &&
                    r.Profile.StartsWith(head + "x", StringComparison.OrdinalIgnoreCase)).ToList();
                if (headMatches.Count == 1)
                {
                    var r = headMatches[0];
                    return new SteelProfileMatch(r.Family, r.Profile, r.GradeOrSchedule, r.WeightPerMeterKg, r.SurfaceAreaM2PerM, "ALIAS");
                }
                if (headMatches.Count > 1)
                {
                    // Pick standard/primary section ONLY if exact height alias matched in aliases list
                    string probeNorm = NormalizeAliasText(typeName ?? profileParam ?? "");
                    var aliasMatched = headMatches.Where(r =>
                        (r.Aliases ?? new List<string>()).Any(a => NormalizeAliasText(a) == probeNorm)).ToList();
                    if (aliasMatched.Count == 1)
                    {
                        var r = aliasMatched[0];
                        return new SteelProfileMatch(r.Family, r.Profile, r.GradeOrSchedule, r.WeightPerMeterKg, r.SurfaceAreaM2PerM, "ALIAS");
                    }
                    return new SteelProfileMatch(detectedFam, combined, string.Empty, 0, 0, "AMBIGUOUS");
                }
            }
        }

        return new SteelProfileMatch(detectedFam, combined, string.Empty, 0, 0, "NOT_FOUND");
    }

    private static List<SteelProfileRecord> FilterByDetectedFamily(List<SteelProfileRecord> candidates, string detectedFam)
    {
        if (string.IsNullOrWhiteSpace(detectedFam)) return candidates;
        return candidates.Where(c =>
            c.Family.Equals(detectedFam, StringComparison.OrdinalIgnoreCase) ||
            (detectedFam == "PIPA" && c.Family.StartsWith("PIPA", StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public static string NormalizeFamily(string? text)
    {
        string value = Regex.Replace((text ?? string.Empty).ToUpperInvariant()
            .Replace("×", "X").Replace("*", "X"), @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // Order matters: more specific composite / hollow tokens BEFORE single-letter tokens!
        if (value.Contains("HOLLOW") || value.Contains("SHS") || value.Contains("RHS") || value.Contains("BOX")) return "HOLLOW";
        if (value.Contains("CASTELLATED") || value.Contains("RCWF")) return "RCWF";
        if (value.Contains("2CNP") || value.Contains("2X C") || value.Contains("2XC") || value.Contains("DOUBLE CNP")) return "2CNP";
        if (value.Contains("2L") || value.Contains("DOUBLE ANGLE") || value.Contains("LL-")) return "2L";
        if (value.Contains("H-BEAM") || value.Contains("H BEAM") || value.Contains("HBEAM") ||
            value.Contains("BEARING PILE") || Regex.IsMatch(value, @"\bH[\s\-]?\d{2,3}\b")) return "HBEAM";
        if (value.Contains("WIDE FLANGE") || Regex.IsMatch(value, @"\bWF\b|\bWF[\.\-]?\d")) return "WF";
        if (value.Contains("CNP") || value.Contains("CANAL C") || Regex.IsMatch(value, @"\bC\s*\d{2,3}\b")) return "CNP";
        if (value.Contains("UNP")) return "UNP";
        if (value.Contains("INP")) return "INP";
        if (value.Contains("SIKU") || value.Contains("L-ANGLE") || Regex.IsMatch(value, @"\bL[\s\-]?\d{2,3}X\d{2,3}")) return "SIKU";
        if (value.Contains("TIE ROD") || value.Contains("WIND BRAC") || value.Contains("REGEL BAR") || value.Contains("ROUND BAR")) return "ROD";
        if (value.Contains("SEAMLESS")) return "SEAMLESS";
        if (value.Contains("STAAL-BUIS")) return "PIPA STAAL-BUIS";
        if (value.Contains("STK41")) return "PIPA HITAM STK41";
        if (value.Contains("PIPA CARBON")) return "PIPA CARBON";
        if (value.Contains("PIPA HITAM")) return "PIPA HITAM";
        if (value.Contains("PIPA")) return "PIPA";
        return string.Empty;
    }

    public static string NormalizeDimensions(string? text)
    {
        string value = (text ?? string.Empty).ToUpperInvariant()
            .Replace("×", "X").Replace("*", "X").Replace(",", ".");
        MatchCollection matches = Regex.Matches(value, @"\d+(?:\.\d+)?");
        if (matches.Count < 2) return string.Empty;

        List<string> numbers = new();
        foreach (Match m in matches)
        {
            if (double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                numbers.Add(num.ToString("0.######", CultureInfo.InvariantCulture));
        }
        return string.Join("X", numbers);
    }

    private static string NormalizeAliasText(string? text)
    {
        string v = (text ?? string.Empty).ToUpperInvariant()
            .Replace("×", "X").Replace("*", "X").Replace(".", " ").Replace("-", " ");
        return Regex.Replace(v, @"\s+", " ").Trim();
    }
}

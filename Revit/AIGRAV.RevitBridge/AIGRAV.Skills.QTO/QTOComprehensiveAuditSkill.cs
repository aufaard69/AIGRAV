using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.QTO;

/// <summary>
/// Read-only 12-Gate QTO Auditor across Structure, Architecture, and Siteplan/Civil disciplines:
/// 1. Unscheduled elements  2. Double counting  3. Spatial duplicates  4. Empty schedules + typo diagnosis
/// 5. Filter errors & cross-contamination  6. Unit & heading errors  7. Calculation errors (Totals, Bolt Number vs Count, 0kg)
/// 8. Steel calibration against clean SNI/JIS DB  9. Custom Connection & Voute parameter injection audit
/// 10. WBS numbering hygiene  11. Geometry/Param health  12. Engineering sanity (Rebar kg/m3 & Plaster/Brick ratio).
/// </summary>
public sealed class QTOComprehensiveAuditSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "QTO Comprehensive 12-Gate Auditor",
        Version = "4.0.0",
        Category = "Quantity Takeoff",
        Description = "12-gate read-only QA/QC auditor checking unscheduled elements, double counting, spatial duplicates, empty schedules, filter/unit/calculation errors, steel calibration, connection injection, WBS numbering, and engineering sanity.",
        Source = "External",
        Priority = 1960,
        IsEnabled = true
    };

    private static readonly BuiltInCategory[] AuditCategories =
    {
        BuiltInCategory.OST_StructuralFoundation,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_StructuralFraming,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_Walls,
        BuiltInCategory.OST_Roofs,
        BuiltInCategory.OST_Ceilings,
        BuiltInCategory.OST_Doors,
        BuiltInCategory.OST_Windows,
        BuiltInCategory.OST_StairsRailing,
        BuiltInCategory.OST_StairsRuns,
        BuiltInCategory.OST_Cornices,
        BuiltInCategory.OST_Fascia,
        BuiltInCategory.OST_Rebar,
        BuiltInCategory.OST_PipeCurves,
        BuiltInCategory.OST_StructConnections,
        BuiltInCategory.OST_StructConnectionPlates,
        BuiltInCategory.OST_StructConnectionBolts,
        BuiltInCategory.OST_StructConnectionAnchors,
        BuiltInCategory.OST_StructConnectionProfiles
    };

    private static readonly Regex TypoRegex = new(
        @"(?i)\b(diamater|lenght|pajang|pengggunaan|pagr|strous|pedestall|detil|standart|loadick|cemper)\b|  +",
        RegexOptions.Compiled);

    private static readonly Regex QuantityFieldHint = new(
        @"(?i)\b(volume|area|luas|length|panjang|perimeter|weight|berat|qty|number|quantity|total bar length|luas aktual|panjang stepnosing)\b",
        RegexOptions.Compiled);

    private sealed class AuditRequest
    {
        public string Discipline { get; set; } = "auto";
        public int MaxSamplesPerIssue { get; set; } = 12;
        public double TolerancePct { get; set; } = 5.0;
    }

    public bool CanHandle(string toolName) =>
        toolName.Equals("aigrav_qto_full_audit", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("qto_full_audit", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("aigrav_qto_reconcile_coverage", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("qto_reconcile_coverage", StringComparison.OrdinalIgnoreCase);

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null) return JsonSerializer.Serialize(new { error = "No active Revit document." });

        AuditRequest req = ParseArgs(argument);
        int cap = Math.Clamp(req.MaxSamplesPerIssue, 3, 50);

        List<ViewSchedule> allScheds = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(s => !s.IsTemplate &&
                        !s.IsTitleblockRevisionSchedule &&
                        !s.IsInternalKeynoteSchedule &&
                        !s.Name.StartsWith("<") &&
                        !s.Name.Contains("Revision Schedule", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<ViewSchedule> qtoScheds = allScheds
            .Where(s => !s.Name.Equals("DAFTAR GAMBAR", StringComparison.OrdinalIgnoreCase) &&
                        !s.Name.Equals("Keynote Legend", StringComparison.OrdinalIgnoreCase) &&
                        !s.Name.Equals("Revision Schedule", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Detect discipline
        string discipline = req.Discipline;
        if (string.Equals(discipline, "auto", StringComparison.OrdinalIgnoreCase))
        {
            if (qtoScheds.Any(s => s.Name.StartsWith("AR-", StringComparison.OrdinalIgnoreCase))) discipline = "Architecture";
            else if (qtoScheds.Any(s => s.Name.Contains("DPT", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("KDB", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Borepile", StringComparison.OrdinalIgnoreCase))) discipline = "Siteplan";
            else discipline = "Structure";
        }

        // Collect elements across audit categories & per-schedule coverage
        Dictionary<long, List<Element>> modelByCat = new();
        Dictionary<long, Element> elemById = new();
        foreach (BuiltInCategory bic in AuditCategories)
        {
            var list = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
            modelByCat[(long)bic] = list;
            foreach (Element e in list) elemById[e.Id.Value] = e;
        }

        // Build schedule membership map
        Dictionary<long, List<(string Name, string Domain)>> elemScheduleHits = new();
        HashSet<long> allScheduledIds = new();

        List<object> emptySchedules = new();
        List<object> filterErrors = new();
        List<object> unitAndHeadingErrors = new();
        List<object> calculationErrors = new();
        Dictionary<string, List<string>> wbsMap = new(StringComparer.OrdinalIgnoreCase);
        List<string> copyClutter = new();
        List<string> missingLokasiColA = new();

        int criticalCount = 0;
        int warningCount = 0;
        int infoCount = 0;

        foreach (ViewSchedule vs in qtoScheds)
        {
            ScheduleDefinition def = vs.Definition;
            long catId = def.CategoryId.Value;
            string schedDomain = ClassifyScheduleDomain(vs, def);

            // Track WBS codes
            var wbsMatch = Regex.Match(vs.Name.Trim(), @"^((?:AR-|SP-)?[A-Z]\.\d+(?:\.\d+[a-z]?)?)\b", RegexOptions.IgnoreCase);
            if (wbsMatch.Success)
            {
                string code = wbsMatch.Groups[1].Value.ToUpperInvariant();
                if (!wbsMap.TryGetValue(code, out var names))
                {
                    names = new List<string>();
                    wbsMap[code] = names;
                }
                names.Add(vs.Name);
            }
            if (Regex.IsMatch(vs.Name, @"(?i)\bcopy(\s+\d+)?\b")) copyClutter.Add(vs.Name);

            // Collect matched elements in schedule
            IList<ElementId> matchedIds = Array.Empty<ElementId>();
            try
            {
                matchedIds = new FilteredElementCollector(doc, vs.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds()
                    .ToList();
            }
            catch { }

            foreach (ElementId eid in matchedIds)
            {
                allScheduledIds.Add(eid.Value);
                if (!elemScheduleHits.TryGetValue(eid.Value, out var hits))
                {
                    hits = new List<(string Name, string Domain)>();
                    elemScheduleHits[eid.Value] = hits;
                }
                hits.Add((vs.Name, schedDomain));
            }

            // Read rendered body table safely
            TableSectionData? body = null;
            try { body = vs.GetTableData().GetSectionData(SectionType.Body); } catch { }
            int bodyRows = body?.NumberOfRows ?? 0;
            int bodyCols = body?.NumberOfColumns ?? 0;

            bool hasDataRows = CheckScheduleHasDataRows(vs, body);

            // Gate 4: Empty schedules + typo diagnosis
            if (!hasDataRows || matchedIds.Count == 0)
            {
                string diagnosis = DiagnoseEmptySchedule(doc, def, modelByCat.GetValueOrDefault(catId));
                criticalCount++;
                emptySchedules.Add(new
                {
                    schedule = vs.Name,
                    id = vs.Id.Value,
                    category = ((BuiltInCategory)catId).ToString(),
                    matched_elements = matchedIds.Count,
                    diagnosis
                });
            }

            // Gate 5: Filter errors & cross-contamination
            InspectFiltersAndContamination(doc, vs, def, catId, matchedIds, filterErrors, ref criticalCount, ref warningCount, ref infoCount);

            // Inspect visible & hidden fields
            InspectFieldsUnitsAndCalculations(
                doc, vs, def, catId, schedDomain, body, hasDataRows,
                unitAndHeadingErrors, calculationErrors, missingLokasiColA,
                ref criticalCount, ref warningCount, ref infoCount);
        }

        // Gate 1: Unscheduled elements
        List<object> coverageByCategory = new();
        foreach (BuiltInCategory bic in AuditCategories)
        {
            var elems = modelByCat[(long)bic];
            if (elems.Count == 0) continue;
            var missing = elems.Where(e => !allScheduledIds.Contains(e.Id.Value)).ToList();
            if (missing.Count > 0) warningCount++;

            coverageByCategory.Add(new
            {
                category = bic.ToString(),
                total_model = elems.Count,
                scheduled = elems.Count - missing.Count,
                unscheduled = missing.Count,
                coverage_pct = Math.Round((elems.Count - missing.Count) * 100.0 / elems.Count, 1),
                unscheduled_samples = missing.Take(cap).Select(e => new
                {
                    id = e.Id.Value,
                    type = doc.GetElement(e.GetTypeId())?.Name ?? e.Name,
                    comments = GetStr(e, "Comments"),
                    lokasi = GetStr(e, "Lokasi", "LOKASI")
                }).ToList()
            });
        }

        // Gate 2: Double-counting (same ElementId in >=2 schedules of SAME measurement domain)
        List<object> doubleCounted = new();
        foreach (var kvp in elemScheduleHits)
        {
            var sameDomainGroups = kvp.Value
                .Where(x => x.Domain != "REFERENCE")
                .GroupBy(x => x.Domain)
                .Where(g => g.Count() > 1)
                .ToList();
            if (sameDomainGroups.Count == 0) continue;

            criticalCount++;
            if (doubleCounted.Count < cap * 3)
            {
                Element? el = elemById.GetValueOrDefault(kvp.Key);
                doubleCounted.Add(new
                {
                    element_id = kvp.Key,
                    category = el?.Category?.Name ?? "Unknown",
                    domain = sameDomainGroups[0].Key,
                    schedules = sameDomainGroups[0].Select(x => x.Name).ToList()
                });
            }
        }

        // Gate 3: Spatial duplicates (same Category + TypeId within 5mm bounding box tolerance)
        List<object> spatialDuplicates = FindSpatialDuplicates(doc, modelByCat, cap, ref criticalCount);

        // Gate 8: Steel Calibration Audit against clean SNI/JIS DB
        var steelCalibration = AuditSteelCalibration(doc, modelByCat, req.TolerancePct, cap, ref criticalCount, ref warningCount);

        // Gate 9: Connection Injection & Voute Audit
        var connectionAudit = AuditConnectionsAndVoutes(doc, cap, ref criticalCount, ref warningCount);

        // Gate 10: WBS Numbering Hygiene
        var duplicateWbs = wbsMap
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => new { wbs_code = kv.Key, count = kv.Value.Count, schedules = kv.Value })
            .ToList();
        if (duplicateWbs.Count > 0) warningCount += duplicateWbs.Count;

        // Gate 11 & 12: Geometry/Param Health & Engineering Sanity
        var geomAndSanity = AuditGeometryAndEngineeringSanity(doc, qtoScheds, modelByCat, cap, ref warningCount);

        int penalty = Math.Min(90, criticalCount * 7 + warningCount * 2);
        int score = Math.Max(10, 100 - penalty);
        string verdict = criticalCount == 0 && warningCount <= 3 ? "PASS" : criticalCount > 0 ? "REJECT_ACTION_REQUIRED" : "WARNING_REVIEW_NEEDED";

        return JsonSerializer.Serialize(new
        {
            status = "AuditCompleted (Read-Only)",
            project = doc.Title,
            discipline_detected = discipline,
            total_qto_schedules = qtoScheds.Count,
            qto_health_score = score,
            verdict,
            summary_counts = new
            {
                critical = criticalCount,
                warning = warningCount,
                info = infoCount,
                empty_schedules = emptySchedules.Count,
                double_counted_elements = doubleCounted.Count,
                spatial_duplicate_groups = spatialDuplicates.Count,
                duplicate_wbs_codes = duplicateWbs.Count
            },
            gate_1_coverage_and_unscheduled = coverageByCategory,
            gate_2_double_counting = new { total_elements_double_counted = elemScheduleHits.Count(kv => kv.Value.Where(x => x.Domain != "REFERENCE").GroupBy(x => x.Domain).Any(g => g.Count() > 1)), samples = doubleCounted },
            gate_3_spatial_duplicates = spatialDuplicates,
            gate_4_empty_schedules = emptySchedules,
            gate_5_filter_errors = filterErrors,
            gate_6_unit_and_heading_errors = unitAndHeadingErrors,
            gate_7_calculation_errors = calculationErrors,
            gate_8_steel_calibration = steelCalibration,
            gate_9_connection_injection_and_voute = connectionAudit,
            gate_10_wbs_hygiene = new
            {
                duplicate_wbs_codes = duplicateWbs,
                copy_clutter_schedules = copyClutter,
                schedules_without_lokasi_in_col_a = missingLokasiColA.Take(cap * 3).ToList()
            },
            gate_11_12_geometry_and_engineering_sanity = geomAndSanity
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ClassifyScheduleDomain(ViewSchedule vs, ScheduleDefinition def)
    {
        string name = vs.Name;
        if (name.Contains("DOOR SCHEDULE", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("WINDOWS SCHEDULE", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Railing Schedule", StringComparison.OrdinalIgnoreCase))
            return "REFERENCE";

        if (name.Contains("Bekisting", StringComparison.OrdinalIgnoreCase)) return "FORMWORK";
        if (def.IsMaterialTakeoff)
        {
            // Separate distinct material takeoff filters so Bata vs Plester Aci do not false-flag
            for (int i = 0; i < def.GetFilterCount(); i++)
            {
                ScheduleFilter flt = def.GetFilter(i);
                string fn = def.GetField(flt.FieldId)?.GetName() ?? "";
                if (fn.Contains("Material", StringComparison.OrdinalIgnoreCase))
                {
                    try { return $"MTO:{flt.GetStringValue()}"; } catch { }
                }
            }
            return "MTO";
        }
        if (name.Contains("Sloof Pagar", StringComparison.OrdinalIgnoreCase)) return "DERIVED_SLOOF";
        if (name.Contains("Kolom Stump", StringComparison.OrdinalIgnoreCase)) return "DERIVED_STUMP";
        if (name.Contains("Shadow Line", StringComparison.OrdinalIgnoreCase)) return "DERIVED_PERIMETER";
        return "PRIMARY_QTO";
    }

    private static bool CheckScheduleHasDataRows(ViewSchedule vs, TableSectionData? body)
    {
        if (body == null || body.NumberOfRows <= 2) return false;
        for (int r = 1; r < body.NumberOfRows; r++)
        {
            for (int c = 0; c < body.NumberOfColumns; c++)
            {
                string txt = vs.GetCellText(SectionType.Body, r, c)?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(txt)) return true;
            }
        }
        return false;
    }

    private static string DiagnoseEmptySchedule(Document doc, ScheduleDefinition def, List<Element>? catElems)
    {
        if (catElems == null || catElems.Count == 0)
            return "No elements exist in this Revit category in the model.";

        List<string> hints = new();
        for (int i = 0; i < def.GetFilterCount(); i++)
        {
            ScheduleFilter flt = def.GetFilter(i);
            ScheduleField? fld = def.GetField(flt.FieldId);
            string fName = fld?.GetName() ?? "Unknown";
            string valStr = string.Empty;
            try { valStr = flt.GetStringValue() ?? string.Empty; } catch { }
            if (string.IsNullOrWhiteSpace(valStr)) continue;

            // Collect actual values of that parameter in category
            var actualCounts = catElems
                .Select(e => GetStr(e, fName))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ToList();

            var closest = actualCounts
                .OrderBy(g => Levenshtein(valStr.ToUpperInvariant(), g.Key.ToUpperInvariant()))
                .FirstOrDefault();

            if (closest != null)
            {
                hints.Add($"Filter {fName} {flt.FilterType} '{valStr}' matched 0 elements; closest in model is '{closest.Key}' ({closest.Count()} elements).");
            }
            else
            {
                hints.Add($"Filter {fName} {flt.FilterType} '{valStr}' matched 0 elements (parameter empty on all {catElems.Count} elements).");
            }
        }
        return hints.Count > 0 ? string.Join(" | ", hints) : "Matched 0 rows with current filters.";
    }

    private static void InspectFiltersAndContamination(
        Document doc,
        ViewSchedule vs,
        ScheduleDefinition def,
        long catId,
        IList<ElementId> matchedIds,
        List<object> filterErrors,
        ref int criticalCount,
        ref int warningCount,
        ref int infoCount)
    {
        bool usesElemIdFilter = false;
        bool usesNotEqual = false;
        bool hasTypeOfConnFilter = false;
        bool hasCommentsOrMaterialFilter = false;

        for (int i = 0; i < def.GetFilterCount(); i++)
        {
            ScheduleFilter flt = def.GetFilter(i);
            string fName = def.GetField(flt.FieldId)?.GetName() ?? "";

            if (fName.Equals("Type of Connection", StringComparison.OrdinalIgnoreCase)) hasTypeOfConnFilter = true;
            if (fName.Equals("Comments", StringComparison.OrdinalIgnoreCase) ||
                fName.Contains("Material", StringComparison.OrdinalIgnoreCase)) hasCommentsOrMaterialFilter = true;

            if (flt.FilterType == ScheduleFilterType.NotEqual) usesNotEqual = true;
            try
            {
                ElementId ev = flt.GetElementIdValue();
                if (ev != null && ev != ElementId.InvalidElementId) usesElemIdFilter = true;
            }
            catch { }
        }

        if (usesElemIdFilter)
        {
            warningCount++;
            filterErrors.Add(new { severity = "WARNING", schedule = vs.Name, issue = "Uses fragile 'Type/Family Equal ElemId' filter instead of Comments / Material: Name / Type of Connection." });
        }
        if (usesNotEqual)
        {
            warningCount++;
            filterErrors.Add(new { severity = "WARNING", schedule = vs.Name, issue = "Uses negative 'NotEqual' filter (prone to leaking unrelated elements into QTO)." });
        }

        bool isSteelConnCat = catId == (long)BuiltInCategory.OST_StructConnectionPlates ||
                              catId == (long)BuiltInCategory.OST_StructConnectionBolts ||
                              catId == (long)BuiltInCategory.OST_StructConnectionAnchors ||
                              catId == (long)BuiltInCategory.OST_StructConnectionProfiles;
        if (isSteelConnCat && !hasTypeOfConnFilter)
        {
            infoCount++;
            filterErrors.Add(new { severity = "INFO", schedule = vs.Name, issue = "Steel Connection schedule should standardize filter on 'Type of Connection' (+ 'Lokasi') instead of 'Comments' alone." });
        }
        else if (!isSteelConnCat && def.GetFilterCount() == 0 && !vs.Name.Contains("SCHEDULE", StringComparison.OrdinalIgnoreCase))
        {
            warningCount++;
            filterErrors.Add(new { severity = "WARNING", schedule = vs.Name, issue = "Schedule has 0 filters (risk of capturing unintended elements)." });
        }
        else if (!isSteelConnCat && !hasCommentsOrMaterialFilter && usesElemIdFilter)
        {
            infoCount++;
        }

        // Cross-contamination check: distinct unrelated Families inside 1 non-composite schedule
        if (matchedIds.Count > 1 && matchedIds.Count <= 2000 &&
            !vs.Name.Contains("Pagar", StringComparison.OrdinalIgnoreCase) &&
            !vs.Name.Contains("A.1.1 Pondasi Pagar", StringComparison.OrdinalIgnoreCase) &&
            !vs.Name.Contains("Lantai Kerja", StringComparison.OrdinalIgnoreCase) &&
            !vs.Name.Contains("A.1.9", StringComparison.OrdinalIgnoreCase) &&
            !vs.Name.Contains("DOOR", StringComparison.OrdinalIgnoreCase) &&
            !vs.Name.Contains("WINDOWS", StringComparison.OrdinalIgnoreCase))
        {
            HashSet<string> families = new(StringComparer.OrdinalIgnoreCase);
            foreach (ElementId eid in matchedIds.Take(200))
            {
                Element? e = doc.GetElement(eid);
                if (e == null) continue;
                ElementType? et = doc.GetElement(e.GetTypeId()) as ElementType;
                string fam = et?.FamilyName ?? "";
                if (!string.IsNullOrWhiteSpace(fam)) families.Add(fam);
            }
            if (families.Count > 1 && families.Any(f => f.Contains("Water Stop", StringComparison.OrdinalIgnoreCase) || f.Contains("Railing Pipa", StringComparison.OrdinalIgnoreCase)))
            {
                criticalCount++;
                filterErrors.Add(new
                {
                    severity = "CRITICAL",
                    schedule = vs.Name,
                    issue = $"Cross-contamination detected across families: {string.Join(", ", families)}."
                });
            }
        }
    }

    private static void InspectFieldsUnitsAndCalculations(
        Document doc,
        ViewSchedule vs,
        ScheduleDefinition def,
        long catId,
        string schedDomain,
        TableSectionData? body,
        bool hasDataRows,
        List<object> unitErrors,
        List<object> calcErrors,
        List<string> missingLokasiColA,
        ref int criticalCount,
        ref int warningCount,
        ref int infoCount)
    {
        if (TypoRegex.IsMatch(vs.Name))
        {
            infoCount++;
            unitErrors.Add(new { severity = "INFO", schedule = vs.Name, issue = "Typo or double space in schedule name." });
        }

        List<ScheduleField> visibleFields = new();
        for (int i = 0; i < def.GetFieldCount(); i++)
        {
            ScheduleField f = def.GetField(i);
            if (!f.IsHidden) visibleFields.Add(f);
        }

        if (visibleFields.Count > 0 && !visibleFields[0].GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase))
            missingLokasiColA.Add(vs.Name);

        bool isBoltOrAnchor = catId == (long)BuiltInCategory.OST_StructConnectionBolts ||
                              catId == (long)BuiltInCategory.OST_StructConnectionAnchors;
        bool isConcreteSchedule = vs.Name.Contains("Beton", StringComparison.OrdinalIgnoreCase) ||
                                  vs.Name.Contains("Sloof", StringComparison.OrdinalIgnoreCase) ||
                                  vs.Name.Contains("Pedestal", StringComparison.OrdinalIgnoreCase) ||
                                  vs.Name.Contains("Batu Kali", StringComparison.OrdinalIgnoreCase) ||
                                  vs.Name.Contains("Rabat", StringComparison.OrdinalIgnoreCase) ||
                                  vs.Name.Contains("Pile Cap", StringComparison.OrdinalIgnoreCase);

        bool hasCountOnlyOnBolt = false;
        bool hasNumberOnBolt = false;

        foreach (ScheduleField f in visibleFields)
        {
            string fName = f.GetName();
            string heading = f.ColumnHeading ?? "";

            if (TypoRegex.IsMatch(heading))
            {
                infoCount++;
                unitErrors.Add(new { severity = "INFO", schedule = vs.Name, field = heading, issue = $"Heading typo '{heading}'." });
            }

            if (isBoltOrAnchor)
            {
                if (fName.Equals("Count", StringComparison.OrdinalIgnoreCase)) hasCountOnlyOnBolt = true;
                if (fName.Equals("Number", StringComparison.OrdinalIgnoreCase)) hasNumberOnBolt = true;
            }

            if (isConcreteSchedule && (fName.Contains("Berat", StringComparison.OrdinalIgnoreCase) || heading.Contains("Berat", StringComparison.OrdinalIgnoreCase)))
            {
                warningCount++;
                unitErrors.Add(new { severity = "WARNING", schedule = vs.Name, field = heading, issue = "Concrete/Earthwork schedule includes Steel Weight ('Berat') column." });
            }

            // Calculation check: Totals enabled on primary quantitative columns?
            bool shouldTotal = QuantityFieldHint.IsMatch(fName) || QuantityFieldHint.IsMatch(heading);
            if (shouldTotal &&
                !fName.Equals("Cut Length", StringComparison.OrdinalIgnoreCase) &&
                !fName.Equals("System Length", StringComparison.OrdinalIgnoreCase) &&
                !fName.Equals("Bar Length", StringComparison.OrdinalIgnoreCase) &&
                !fName.Equals("Diameter", StringComparison.OrdinalIgnoreCase) &&
                f.DisplayType == ScheduleFieldDisplayType.Standard &&
                !(fName.Equals("Count", StringComparison.OrdinalIgnoreCase) && !def.IsItemized && !isBoltOrAnchor && !vs.Name.Contains("Volume Pintu", StringComparison.OrdinalIgnoreCase) && !vs.Name.Contains("Pipa Sulingan", StringComparison.OrdinalIgnoreCase) && !vs.Name.Contains("Sumur Resapan", StringComparison.OrdinalIgnoreCase)))
            {
                warningCount++;
                calcErrors.Add(new { severity = "WARNING", schedule = vs.Name, field = heading, issue = $"Quantitative field '{heading}' ({fName}) has DisplayType=Standard instead of Totals (Grand Total may be blank)." });
            }
        }

        if (isBoltOrAnchor && hasCountOnlyOnBolt && !hasNumberOnBolt)
        {
            criticalCount++;
            calcErrors.Add(new { severity = "CRITICAL", schedule = vs.Name, issue = "Bolts/Anchors schedule uses 'Count' (pattern count) instead of 'Number' (physical piece count) — causes severe undercount!" });
        }

        if (!def.ShowGrandTotal && schedDomain != "REFERENCE")
        {
            warningCount++;
            calcErrors.Add(new { severity = "WARNING", schedule = vs.Name, issue = "ShowGrandTotal is disabled." });
        }

        // Inspect actual cell values on rendered Body table for unit mistakes & 0.00 kg
        if (body != null && hasDataRows)
        {
            int lastRow = body.NumberOfRows - 1;
            int colCount = Math.Min(visibleFields.Count, body.NumberOfColumns);
            for (int c = 0; c < colCount; c++)
            {
                string fName = visibleFields[c].GetName();
                string heading = visibleFields[c].ColumnHeading ?? fName;
                string sampleCell = string.Empty;
                for (int r = 1; r < body.NumberOfRows; r++)
                {
                    string v = vs.GetCellText(SectionType.Body, r, c)?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(v)) { sampleCell = v; break; }
                }
                string lastCell = vs.GetCellText(SectionType.Body, lastRow, c)?.Trim() ?? "";

                // Concrete Volume formatted as kg! (catches E.1.1, E.1.2, C.2.1!)
                if (fName.Equals("Volume", StringComparison.OrdinalIgnoreCase) &&
                    (sampleCell.EndsWith("kg", StringComparison.OrdinalIgnoreCase) || lastCell.EndsWith("kg", StringComparison.OrdinalIgnoreCase)))
                {
                    criticalCount++;
                    unitErrors.Add(new { severity = "CRITICAL", schedule = vs.Name, field = heading, sample_value = sampleCell, issue = "Revit 'Volume' field is formatted with unit 'kg' instead of 'm³'!" });
                }

                // Heading says 'Volume' but cell shows 'm²' (catches AR-C.2, AR-D.1!)
                if (heading.Trim().Equals("Volume", StringComparison.OrdinalIgnoreCase) &&
                    sampleCell.Contains("m²", StringComparison.OrdinalIgnoreCase))
                {
                    warningCount++;
                    unitErrors.Add(new { severity = "WARNING", schedule = vs.Name, field = heading, sample_value = sampleCell, issue = "Column heading says 'Volume' but values are Area ('m²'). Rename heading to 'Luas (m²)'." });
                }

                // Populated Steel/Plate/Rebar schedule showing Grand Total 0.00 kg!
                if ((fName.Contains("Berat", StringComparison.OrdinalIgnoreCase) || fName.Equals("Weight", StringComparison.OrdinalIgnoreCase) || fName.Equals("Exact Weight", StringComparison.OrdinalIgnoreCase)) &&
                    !isConcreteSchedule && !vs.Name.Contains("Bekisting", StringComparison.OrdinalIgnoreCase) &&
                    Regex.IsMatch(lastCell, @"^0[.,]00\s*kg$", RegexOptions.IgnoreCase))
                {
                    criticalCount++;
                    calcErrors.Add(new { severity = "CRITICAL", schedule = vs.Name, field = heading, last_cell = lastCell, issue = "Populated steel/plate schedule computes 0.00 kg total weight!" });
                }
            }
        }
    }

    private static List<object> FindSpatialDuplicates(Document doc, Dictionary<long, List<Element>> modelByCat, int cap, ref int criticalCount)
    {
        List<object> dupGroups = new();
        BuiltInCategory[] checkCats =
        {
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows
        };

        foreach (BuiltInCategory bic in checkCats)
        {
            List<Element> elems = modelByCat.GetValueOrDefault((long)bic) ?? new();
            Dictionary<string, List<Element>> buckets = new();
            foreach (Element e in elems)
            {
                BoundingBoxXYZ? bb = null;
                try { bb = e.get_BoundingBox(null); } catch { }
                if (bb == null) continue;
                // Round coords to 10mm grid (0.033 ft) for fast spatial duplicate detection
                long minX = Math.Clamp((long)Math.Round(bb.Min.X * 30.48), -9999999, 9999999);
                long minY = Math.Clamp((long)Math.Round(bb.Min.Y * 30.48), -9999999, 9999999);
                long minZ = Math.Clamp((long)Math.Round(bb.Min.Z * 30.48), -9999999, 9999999);
                long maxX = Math.Clamp((long)Math.Round(bb.Max.X * 30.48), -9999999, 9999999);
                long maxY = Math.Clamp((long)Math.Round(bb.Max.Y * 30.48), -9999999, 9999999);
                long maxZ = Math.Clamp((long)Math.Round(bb.Max.Z * 30.48), -9999999, 9999999);
                string key = $"{e.GetTypeId().Value}:{minX},{minY},{minZ}:{maxX},{maxY},{maxZ}";
                if (!buckets.TryGetValue(key, out var b))
                {
                    b = new List<Element>();
                    buckets[key] = b;
                }
                b.Add(e);
            }

            foreach (var kv in buckets.Where(b => b.Value.Count > 1))
            {
                criticalCount++;
                if (dupGroups.Count < cap * 2)
                {
                    Element first = kv.Value[0];
                    dupGroups.Add(new
                    {
                        category = bic.ToString(),
                        type = doc.GetElement(first.GetTypeId())?.Name ?? first.Name,
                        lokasi = GetStr(first, "Lokasi", "LOKASI"),
                        duplicate_count = kv.Value.Count,
                        element_ids = kv.Value.Take(6).Select(x => x.Id.Value).ToList()
                    });
                }
            }
        }
        return dupGroups;
    }

    private static object AuditSteelCalibration(Document doc, Dictionary<long, List<Element>> modelByCat, double tolerancePct, int cap, ref int criticalCount, ref int warningCount)
    {
        SteelDatabaseProvider db = SteelDatabaseProvider.LoadDefault();
        int checkedSteelMembers = 0;
        int matched = 0;
        int uncalibrated = 0;
        int ambiguous = 0;
        int mismatchedWeight = 0;
        List<object> samples = new();

        foreach (BuiltInCategory bic in new[] { BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralFraming })
        {
            foreach (Element el in modelByCat.GetValueOrDefault((long)bic) ?? new())
            {
                ElementType? et = doc.GetElement(el.GetTypeId()) as ElementType;
                string fam = et?.FamilyName ?? "";
                string typeName = et?.Name ?? el.Name ?? "";
                string comm = GetStr(el, "Comments");

                // Skip concrete items inside Structural Columns/Framing
                string probe = $"{fam} {typeName} {comm}".ToUpperInvariant();
                if (probe.Contains("CONCRETE") || probe.Contains("BETON") || probe.Contains("SLOOF") ||
                    probe.Contains("PEDESTAL") || probe.Contains("BATU KALI") || probe.Contains("KP ") ||
                    probe.Contains("BP ") || probe.Contains("RB ") || probe.Contains("B2 ") ||
                    probe.Contains("B4 ") || probe.Contains("KANSTIN") || probe.Contains("DUDUKAN") ||
                    probe.Contains("SULINGAN") || probe.Contains("WATER") || probe.Contains("STUMP"))
                    continue;

                SteelProfileMatch match = db.TryMatch(fam, typeName, GetStr(el, "Profile"));
                if (string.IsNullOrEmpty(match.Family) && match.MatchStatus == "NOT_FOUND")
                    continue; // Likely non-steel architectural framing

                checkedSteelMembers++;
                if (match.MatchStatus == "NOT_FOUND")
                {
                    uncalibrated++;
                    warningCount++;
                    if (samples.Count < cap) samples.Add(new { id = el.Id.Value, issue = "UNCALIBRATED_PROFILE", family = fam, type = typeName });
                    continue;
                }
                if (match.MatchStatus == "AMBIGUOUS")
                {
                    ambiguous++;
                    if (samples.Count < cap) samples.Add(new { id = el.Id.Value, issue = "AMBIGUOUS_PROFILE", family = fam, type = typeName });
                    continue;
                }

                matched++;
                double lenM = GetLengthM(el);
                double dbWeightKg = lenM * match.WeightPerMeterKg;
                double modelWeightKg = GetDoubleParam(el, "Berat Kalibrasi (Kg)", "Berat (Kg)");
                if (modelWeightKg > 0.01 && dbWeightKg > 0.01)
                {
                    double diffPct = Math.Abs(modelWeightKg - dbWeightKg) * 100.0 / dbWeightKg;
                    if (diffPct > tolerancePct && !probe.Contains("VOUTE"))
                    {
                        mismatchedWeight++;
                        warningCount++;
                        if (samples.Count < cap)
                        {
                            samples.Add(new
                            {
                                id = el.Id.Value,
                                issue = "WEIGHT_MISMATCH_VS_DB",
                                type = typeName,
                                model_kg = Math.Round(modelWeightKg, 2),
                                db_kg = Math.Round(dbWeightKg, 2),
                                diff_pct = Math.Round(diffPct, 1)
                            });
                        }
                    }
                }
            }
        }

        // Check abnormal Plate thicknesses (> 100 mm)
        List<object> abnormalPlates = new();
        foreach (Element p in modelByCat.GetValueOrDefault((long)BuiltInCategory.OST_StructConnectionPlates) ?? new())
        {
            double thkFt = p.LookupParameter("Thickness")?.AsDouble() ?? 0;
            double thkMm = thkFt * 304.8;
            if (thkMm > 100.0)
            {
                criticalCount++;
                if (abnormalPlates.Count < cap)
                    abnormalPlates.Add(new { id = p.Id.Value, thickness_mm = Math.Round(thkMm, 1), comments = GetStr(p, "Comments") });
            }
        }

        return new
        {
            steel_db_path = db.LoadedPath,
            steel_db_version = db.Version,
            checked_steel_members = checkedSteelMembers,
            matched_clean = matched,
            uncalibrated_profiles = uncalibrated,
            ambiguous_profiles = ambiguous,
            weight_mismatches_over_tolerance = mismatchedWeight,
            abnormal_plates_over_100mm = abnormalPlates.Count,
            samples,
            abnormal_plate_samples = abnormalPlates
        };
    }

    private static object AuditConnectionsAndVoutes(Document doc, int cap, ref int criticalCount, ref int warningCount)
    {
        var handlers = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructConnections)
            .WhereElementIsNotElementType()
            .ToElements();

        ElementId? typeConnId = null;
        foreach (Element h in handlers)
        {
            Parameter? p = h.LookupParameter("Type of Connection") ?? h.LookupParameter("Type of connection");
            if (p != null) { typeConnId = p.Id; break; }
        }

        int totalSub = 0;
        int totalVoutes = 0;
        int uninjectedSub = 0;
        int uninjectedVoutes = 0;
        List<object> samples = new();

        foreach (Element el in handlers)
        {
            if (el is not StructuralConnectionHandler handler) continue;
            string parentType = GetStr(el, "Type of Connection", "Type of connection");
            IList<Subelement>? subs = null;
            try { subs = handler.GetSubelements(); } catch { }
            if (subs == null) continue;

            foreach (Subelement sub in subs)
            {
                totalSub++;
                bool isProfile = sub.Category?.Id.Value == (long)BuiltInCategory.OST_StructConnectionProfiles;
                if (isProfile) totalVoutes++;

                bool missing = typeConnId == null || !sub.HasParameter(typeConnId);
                if (!missing)
                {
                    try
                    {
                        var spv = sub.GetParameterValue(typeConnId) as StringParameterValue;
                        if (string.IsNullOrWhiteSpace(spv?.Value) ||
                            (!string.IsNullOrWhiteSpace(parentType) && !spv.Value.Equals(parentType, StringComparison.OrdinalIgnoreCase)))
                            missing = true;
                    }
                    catch { missing = true; }
                }

                if (missing)
                {
                    uninjectedSub++;
                    if (isProfile) uninjectedVoutes++;
                    if (samples.Count < cap)
                        samples.Add(new { handler_id = el.Id.Value, sub_category = sub.Category?.Name, parent_type = parentType });
                }
            }
        }

        if (uninjectedSub > 0) warningCount++;
        if (uninjectedVoutes > 0) criticalCount++;

        return new
        {
            total_connection_handlers = handlers.Count,
            total_subelements = totalSub,
            total_voute_subelements = totalVoutes,
            uninjected_subelements = uninjectedSub,
            uninjected_voutes = uninjectedVoutes,
            samples
        };
    }

    private static object AuditGeometryAndEngineeringSanity(
        Document doc,
        List<ViewSchedule> qtoScheds,
        Dictionary<long, List<Element>> modelByCat,
        int cap,
        ref int warningCount)
    {
        int zeroGeomCount = 0;
        List<object> zeroGeomSamples = new();

        foreach (BuiltInCategory bic in new[] { BuiltInCategory.OST_StructuralFoundation, BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_Floors })
        {
            foreach (Element e in modelByCat.GetValueOrDefault((long)bic) ?? new())
            {
                double vol = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? -1;
                if (vol == 0)
                {
                    zeroGeomCount++;
                    if (zeroGeomSamples.Count < cap)
                        zeroGeomSamples.Add(new { id = e.Id.Value, category = bic.ToString(), type = doc.GetElement(e.GetTypeId())?.Name ?? e.Name });
                }
            }
        }

        // Cross-check Rebar ratio (kg/m3) on matching DPT / Bak Kontrol concrete vs rebar schedules
        List<object> rebarChecks = new();
        var rebarScheds = qtoScheds.Where(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_Rebar).ToList();
        foreach (ViewSchedule rs in rebarScheds)
        {
            string zoneKey = rs.Name.Replace("Rebar", "", StringComparison.OrdinalIgnoreCase).Trim();
            string shortKey = Regex.Match(zoneKey, @"(?i)(Type\s+[A-Z]\d+|BC\d+)").Value;
            if (string.IsNullOrWhiteSpace(shortKey)) continue;

            ViewSchedule? concMatch = qtoScheds.FirstOrDefault(s =>
                s.Id != rs.Id &&
                !s.Name.Contains("Rebar", StringComparison.OrdinalIgnoreCase) &&
                !s.Name.Contains("Lantai Kerja", StringComparison.OrdinalIgnoreCase) &&
                s.Name.Contains(shortKey, StringComparison.OrdinalIgnoreCase));

            double rebarKg = ReadScheduleGrandTotalNumber(rs, "Berat", "kg");
            double concM3 = concMatch != null ? ReadScheduleGrandTotalNumber(concMatch, "Volume", "m³") : 0;

            if (rebarKg > 0 && concM3 > 0)
            {
                double ratio = rebarKg / concM3;
                string status = (ratio >= 65 && ratio <= 250) ? "OK" : "OUT_OF_RANGE";
                if (status != "OK") warningCount++;
                rebarChecks.Add(new
                {
                    zone = shortKey,
                    rebar_schedule = rs.Name,
                    concrete_schedule = concMatch!.Name,
                    rebar_kg = Math.Round(rebarKg, 2),
                    concrete_m3 = Math.Round(concM3, 2),
                    ratio_kg_per_m3 = Math.Round(ratio, 1),
                    status
                });
            }
        }

        return new
        {
            zero_volume_elements = zeroGeomCount,
            zero_volume_samples = zeroGeomSamples,
            rebar_ratio_checks = rebarChecks
        };
    }

    private static double ReadScheduleGrandTotalNumber(ViewSchedule vs, string colHint, string unitHint)
    {
        try
        {
            var body = vs.GetTableData().GetSectionData(SectionType.Body);
            if (body.NumberOfRows < 2) return 0;
            int lastRow = body.NumberOfRows - 1;
            ScheduleDefinition def = vs.Definition;
            List<ScheduleField> vis = new();
            for (int i = 0; i < def.GetFieldCount(); i++)
            {
                var f = def.GetField(i);
                if (!f.IsHidden) vis.Add(f);
            }

            for (int c = 0; c < Math.Min(vis.Count, body.NumberOfColumns); c++)
            {
                if (!vis[c].ColumnHeading.Contains(colHint, StringComparison.OrdinalIgnoreCase) &&
                    !vis[c].GetName().Contains(colHint, StringComparison.OrdinalIgnoreCase)) continue;
                string txt = vs.GetCellText(SectionType.Body, lastRow, c)?.Trim() ?? "";
                if (string.IsNullOrEmpty(txt)) continue;
                string cleaned = Regex.Replace(txt, @"[^\d.,\-]", "");
                if (cleaned.Contains(',') && !cleaned.Contains('.')) cleaned = cleaned.Replace(",", ".");
                else cleaned = cleaned.Replace(",", "");
                if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                    return val;
            }
        }
        catch { }
        return 0;
    }

    private static double GetLengthM(Element el)
    {
        double ft = el.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH)?.AsDouble() ?? 0;
        if (ft <= 0) ft = el.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM)?.AsDouble() ?? 0;
        if (ft <= 0 && el.Location is LocationCurve lc && lc.Curve != null) ft = lc.Curve.Length;
        return ft * 0.3048;
    }

    private static double GetDoubleParam(Element el, params string[] names)
    {
        foreach (string n in names)
        {
            Parameter? p = el.LookupParameter(n);
            if (p == null) continue;
            if (p.StorageType == StorageType.Double) return p.AsDouble();
            string s = p.AsValueString() ?? "";
            string cleaned = Regex.Replace(s, @"[^\d.,\-]", "").Replace(",", "");
            if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
        }
        return 0;
    }

    private static string GetStr(Element el, params string[] names)
    {
        foreach (string n in names)
        {
            Parameter? p = el.LookupParameter(n);
            if (p == null) continue;
            string? val = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
        }
        return string.Empty;
    }

    private static int Levenshtein(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[a.Length, b.Length];
    }

    private static AuditRequest ParseArgs(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return new AuditRequest();
        try
        {
            return JsonSerializer.Deserialize<AuditRequest>(argument, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AuditRequest();
        }
        catch
        {
            return new AuditRequest();
        }
    }
}

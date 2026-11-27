using System.Text.Json;
using System.Text.RegularExpressions;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.QTO;

/// <summary>
/// Universal Model-Driven QTO WBS Schedule Suite Generator (QTO v4.0):
/// - Mode "plan" (read-only): scans every model category & StructuralConnectionHandler subelements,
///   partitions disjointly using Dual-Key filters (Lokasi+Type of Connection for connections; Lokasi/Comments for others),
///   assigns unique non-colliding WBS numbers across Divisions A..N / AR / SP, and reports the full plan.
/// - Mode "execute": creates all planned ViewSchedules inside one atomic TransactionGroup with SilentWarningPreprocessor,
///   enforcing Column A = Lokasi, Number (Totals) for Bolts/Anchors, Exact Weight = Volume*7850 for Voutes, and ShowGrandTotal=True.
/// </summary>
public sealed class QTOAutoSuiteGeneratorSkill : IAIGRAVSkill
{
    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "QTO Auto-Suite WBS Generator",
        Version = "4.0.0",
        Category = "Quantity Takeoff",
        Description = "Universal model-driven QTO ViewSchedule suite generator with disjoint Dual-Key filtering, WBS A..N numbering, and Custom Connection subelement coverage.",
        Source = "External",
        Priority = 1945,
        IsEnabled = true
    };

    private sealed class SuiteRequest
    {
        public string Mode { get; set; } = "plan"; // "plan" | "execute"
        public bool ReplaceExisting { get; set; } = true;
        public bool DeleteExistingQtoSchedules { get; set; } = false;
        public bool IncludeZeroBlankBuckets { get; set; } = true;
    }

    private sealed class PlannedSchedule
    {
        public string WbsPrefix { get; set; } = "Z.1";
        public string ScheduleName { get; set; } = string.Empty;
        public string TitleSuffix { get; set; } = string.Empty;
        public BuiltInCategory Category { get; set; }
        public string SchemaKind { get; set; } = "General";
        public string? LokasiFilter { get; set; }
        public string? CommentsFilter { get; set; }
        public string? TypeOfConnectionFilter { get; set; }
        public string? TypeNameFilter { get; set; }
        public ElementId? TypeIdFilter { get; set; }
        public int ExpectedElementCount { get; set; }
        public int ZoneOrder { get; set; } = 0;
        public int ItemOrder { get; set; } = 0;
    }

    private sealed class SilentWarningPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (FailureMessageAccessor fma in failuresAccessor.GetFailureMessages())
            {
                if (fma.GetSeverity() == FailureSeverity.Warning)
                    failuresAccessor.DeleteWarning(fma);
            }
            return FailureProcessingResult.Continue;
        }
    }

    private static readonly Dictionary<string, int> ZoneWbsMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Waterline"] = 1,
        ["Power House"] = 2,
        ["Carton & Cups Storage"] = 3,
        ["Production Office"] = 4,
        ["Chiller Area 1"] = 5,
        ["Chiller Area 2"] = 6,
    };

    public bool CanHandle(string toolName) =>
        toolName.Equals("aigrav_qto_generate_all_schedules", StringComparison.OrdinalIgnoreCase) ||
        toolName.Equals("qto_generate_all_schedules", StringComparison.OrdinalIgnoreCase);

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null) return JsonSerializer.Serialize(new { error = "No active Revit document." });

        SuiteRequest req = ParseArgs(argument);
        bool isExecute = string.Equals(req.Mode, "execute", StringComparison.OrdinalIgnoreCase);

        try
        {
            List<PlannedSchedule> plans = BuildUniversalPlan(doc, req);
            AssignUniqueWbsNames(plans);

            if (!isExecute)
            {
                var byDiv = plans
                    .GroupBy(p => p.WbsPrefix)
                    .Select(g => new { division = g.Key, schedule_count = g.Count(), expected_elements = g.Sum(x => x.ExpectedElementCount) })
                    .OrderBy(x => x.division)
                    .ToList();

                return JsonSerializer.Serialize(new
                {
                    status = "PlanOnly (Read-Only)",
                    project = doc.Title,
                    total_planned_schedules = plans.Count,
                    divisions_summary = byDiv,
                    planned_schedules = plans.Select(p => new
                    {
                        schedule_name = p.ScheduleName,
                        category = p.Category.ToString(),
                        schema = p.SchemaKind,
                        lokasi_filter = p.LokasiFilter,
                        comments_filter = p.CommentsFilter,
                        type_of_connection_filter = p.TypeOfConnectionFilter,
                        type_filter = p.TypeNameFilter ?? (p.TypeIdFilter != null ? $"ElemId:{p.TypeIdFilter.Value}" : null),
                        expected_count = p.ExpectedElementCount
                    }).ToList()
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            return ExecutePlan(doc, req, plans);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                status = "Error",
                mode = req.Mode,
                error = ex.Message,
                stack = ex.StackTrace
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private static List<PlannedSchedule> BuildUniversalPlan(Document doc, SuiteRequest req)
    {
        List<PlannedSchedule> plans = new();
        int dynamicZoneCounter = 7;
        Dictionary<string, int> dynamicZones = new(ZoneWbsMap, StringComparer.OrdinalIgnoreCase);

        int GetZoneIdx(string lok)
        {
            if (dynamicZones.TryGetValue(lok, out int z)) return z;
            dynamicZones[lok] = dynamicZoneCounter;
            return dynamicZoneCounter++;
        }

        // ------------------------------------------------------------
        // 1. OST_StructuralFoundation (Divisi A.1 - Partitioned by Comments)
        // ------------------------------------------------------------
        var foundations = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFoundation)
            .WhereElementIsNotElementType()
            .ToElements();

        foreach (var g in foundations.GroupBy(e => GetStr(e, "Comments"), StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(g.Key) && !req.IncludeZeroBlankBuckets) continue;
            string comm = g.Key;
            bool isPile = comm.Contains("Strauss", StringComparison.OrdinalIgnoreCase) || comm.Contains("Pancang", StringComparison.OrdinalIgnoreCase);
            bool isFormworkPair = comm.Contains("Pilecap", StringComparison.OrdinalIgnoreCase);
            string title = string.IsNullOrWhiteSpace(comm) ? "Pondasi Lainnya (Blank Comments)" : comm;

            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "A.1",
                TitleSuffix = title,
                Category = BuiltInCategory.OST_StructuralFoundation,
                SchemaKind = isPile ? "FoundationPile" : "FoundationGeneral",
                CommentsFilter = comm,
                ExpectedElementCount = g.Count(),
                ItemOrder = isPile ? 1 : (comm.Contains("Pilecap", StringComparison.OrdinalIgnoreCase) ? 2 : 3)
            });

            if (isFormworkPair)
            {
                plans.Add(new PlannedSchedule
                {
                    WbsPrefix = "A.1",
                    TitleSuffix = "Bekisting Pondasi Pilecap",
                    Category = BuiltInCategory.OST_StructuralFoundation,
                    SchemaKind = "FormworkFoundation",
                    CommentsFilter = comm,
                    ExpectedElementCount = g.Count(),
                    ItemOrder = 2
                });
            }
        }

        // ------------------------------------------------------------
        // 2. OST_StructuralColumns (Concrete Pedestals/Hoist vs Steel Columns)
        // ------------------------------------------------------------
        var columns = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType()
            .ToElements();

        List<Element> concreteCols = new();
        List<Element> steelCols = new();
        foreach (Element c in columns)
        {
            string fam = GetFamilyName(doc, c);
            string typeName = GetTypeName(doc, c);
            string comm = GetStr(c, "Comments");
            if (IsConcreteElement(fam, typeName, comm)) concreteCols.Add(c);
            else steelCols.Add(c);
        }

        // Concrete columns grouped cleanly
        foreach (var g in concreteCols.GroupBy(e => GetStr(e, "Comments"), StringComparer.Ordinal))
        {
            string comm = g.Key;
            if (string.IsNullOrWhiteSpace(comm) && !req.IncludeZeroBlankBuckets) continue;
            bool isHoist = comm.Contains("HOIST", StringComparison.OrdinalIgnoreCase);
            var typeGroups = g.GroupBy(e => GetTypeName(doc, e)).ToList();
            bool needTypeSplit = isHoist || typeGroups.Count > 1 && comm.Contains("HOIST", StringComparison.OrdinalIgnoreCase);

            if (needTypeSplit)
            {
                foreach (var tg in typeGroups)
                {
                    var typeIds = tg.Select(x => x.GetTypeId()).Distinct().ToList();
                    plans.Add(new PlannedSchedule
                    {
                        WbsPrefix = "N.2",
                        TitleSuffix = $"Kolom Hoist Beton {tg.Key}",
                        Category = BuiltInCategory.OST_StructuralColumns,
                        SchemaKind = "ConcreteLinear",
                        CommentsFilter = comm,
                        TypeNameFilter = tg.Key,
                        TypeIdFilter = typeIds.Count == 1 ? typeIds[0] : null,
                        ExpectedElementCount = tg.Count(),
                        ItemOrder = 6
                    });
                }
            }
            else
            {
                plans.Add(new PlannedSchedule
                {
                    WbsPrefix = "A.1",
                    TitleSuffix = string.IsNullOrWhiteSpace(comm) ? "Kolom Beton Lainnya (Blank Comments)" : comm,
                    Category = BuiltInCategory.OST_StructuralColumns,
                    SchemaKind = "ConcreteLinear",
                    CommentsFilter = comm,
                    ExpectedElementCount = g.Count(),
                    ItemOrder = 7
                });
            }
        }

        // Steel columns grouped by (Lokasi, Comments, TypeName when >=2 types in bucket)
        foreach (var locComm in steelCols.GroupBy(e => (Lok: GetStr(e, "Lokasi", "LOKASI"), Comm: GetStr(e, "Comments"))))
        {
            string lok = locComm.Key.Lok;
            string comm = locComm.Key.Comm;
            if (string.IsNullOrWhiteSpace(comm) && !req.IncludeZeroBlankBuckets) continue;

            var byType = locComm.GroupBy(e => GetTypeName(doc, e), StringComparer.OrdinalIgnoreCase).ToList();
            bool isHoistGroup = comm.Contains("HOIST", StringComparison.OrdinalIgnoreCase);
            bool needTypeFilter = byType.Count >= 2 || isHoistGroup;

            foreach (var tg in byType)
            {
                bool isMain = IsMainBuildingLokasi(lok);
                string wbs = !isMain ? $"C.3.{GetZoneIdx(lok)}" : (isHoistGroup ? "N.2" : "B.1");
                string prefixTitle = !isMain ? $"{lok} - " : "";
                string roleLabel = comm.Contains("Gewel", StringComparison.OrdinalIgnoreCase) ? "Kolom Gewel" :
                                   isHoistGroup ? "Kolom Hoist" : "Kolom Baja";
                string suffix = $"{prefixTitle}{roleLabel} {tg.Key}".Trim();
                var typeIds = tg.Select(x => x.GetTypeId()).Distinct().ToList();

                plans.Add(new PlannedSchedule
                {
                    WbsPrefix = wbs,
                    TitleSuffix = suffix,
                    Category = BuiltInCategory.OST_StructuralColumns,
                    SchemaKind = "SteelProfile",
                    LokasiFilter = lok,
                    CommentsFilter = comm,
                    TypeNameFilter = needTypeFilter ? tg.Key : null,
                    TypeIdFilter = needTypeFilter && typeIds.Count == 1 ? typeIds[0] : null,
                    ExpectedElementCount = tg.Count(),
                    ItemOrder = 1
                });
            }
        }

        // ------------------------------------------------------------
        // 3. OST_StructuralFraming (Concrete Beams/Sloof/Batu Kali vs Steel Framing)
        // ------------------------------------------------------------
        var framing = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .ToElements();

        List<Element> concreteFraming = new();
        List<Element> steelFraming = new();
        foreach (Element f in framing)
        {
            string fam = GetFamilyName(doc, f);
            string typeName = GetTypeName(doc, f);
            string comm = GetStr(f, "Comments");
            if (IsConcreteElement(fam, typeName, comm)) concreteFraming.Add(f);
            else steelFraming.Add(f);
        }

        foreach (var g in concreteFraming.GroupBy(e => GetStr(e, "Comments"), StringComparer.Ordinal))
        {
            string comm = g.Key;
            if (string.IsNullOrWhiteSpace(comm))
            {
                if (!req.IncludeZeroBlankBuckets) continue;
                foreach (var tg in g.GroupBy(e => (Lok: GetStr(e, "Lokasi", "LOKASI"), Tn: GetTypeName(doc, e))))
                {
                    var typeIds = tg.Select(x => x.GetTypeId()).Distinct().ToList();
                    plans.Add(new PlannedSchedule
                    {
                        WbsPrefix = "A.1",
                        TitleSuffix = $"Balok Beton {tg.Key.Tn} (Blank Comments)",
                        Category = BuiltInCategory.OST_StructuralFraming,
                        SchemaKind = "ConcreteLinear",
                        LokasiFilter = tg.Key.Lok,
                        CommentsFilter = comm,
                        TypeNameFilter = tg.Key.Tn,
                        TypeIdFilter = typeIds.Count == 1 ? typeIds[0] : null,
                        ExpectedElementCount = tg.Count(),
                        ItemOrder = 5
                    });
                }
                continue;
            }
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "A.1",
                TitleSuffix = comm,
                Category = BuiltInCategory.OST_StructuralFraming,
                SchemaKind = "ConcreteLinear",
                CommentsFilter = comm,
                ExpectedElementCount = g.Count(),
                ItemOrder = comm.Contains("Batu Kali", StringComparison.OrdinalIgnoreCase) ? 4 : 5
            });

            if (comm.Contains("Sloof", StringComparison.OrdinalIgnoreCase))
            {
                plans.Add(new PlannedSchedule
                {
                    WbsPrefix = "A.1",
                    TitleSuffix = "Bekisting Sloof",
                    Category = BuiltInCategory.OST_StructuralFraming,
                    SchemaKind = "FormworkFraming",
                    CommentsFilter = comm,
                    ExpectedElementCount = g.Count(),
                    ItemOrder = 6
                });
            }
        }

        foreach (var locComm in steelFraming.GroupBy(e => (Lok: GetStr(e, "Lokasi", "LOKASI"), Comm: GetStr(e, "Comments"))))
        {
            string lok = locComm.Key.Lok;
            string comm = locComm.Key.Comm;
            if (string.IsNullOrWhiteSpace(comm) && !req.IncludeZeroBlankBuckets) continue;

            var byType = locComm.GroupBy(e => GetTypeName(doc, e), StringComparer.OrdinalIgnoreCase).ToList();
            bool splitByType = byType.Count >= 2;
            bool isMain = IsMainBuildingLokasi(lok);

            if (!splitByType)
            {
                var tg = byType[0];
                string wbs = isMain ? RouteMainFramingWbs(comm) : $"C.3.{GetZoneIdx(lok)}";
                string title = !isMain
                    ? $"{lok} - Balok Baja {tg.Key}"
                    : FormatMainFramingTitle(comm, tg.Key, singleTypeBucket: true);

                plans.Add(new PlannedSchedule
                {
                    WbsPrefix = wbs,
                    TitleSuffix = title,
                    Category = BuiltInCategory.OST_StructuralFraming,
                    SchemaKind = "SteelProfile",
                    LokasiFilter = lok,
                    CommentsFilter = comm,
                    ExpectedElementCount = tg.Count(),
                    ItemOrder = 2
                });
            }
            else
            {
                foreach (var tg in byType)
                {
                    string wbs = isMain ? RouteMainFramingWbs(comm) : $"C.3.{GetZoneIdx(lok)}";
                    string title = !isMain
                        ? $"{lok} - Balok Baja {tg.Key}"
                        : FormatMainFramingTitle(comm, tg.Key, singleTypeBucket: false);
                    var typeIds = tg.Select(x => x.GetTypeId()).Distinct().ToList();

                    plans.Add(new PlannedSchedule
                    {
                        WbsPrefix = wbs,
                        TitleSuffix = title,
                        Category = BuiltInCategory.OST_StructuralFraming,
                        SchemaKind = "SteelProfile",
                        LokasiFilter = lok,
                        CommentsFilter = comm,
                        TypeNameFilter = tg.Key,
                        TypeIdFilter = typeIds.Count == 1 ? typeIds[0] : null,
                        ExpectedElementCount = tg.Count(),
                        ItemOrder = 2
                    });
                }
            }
        }

        // ------------------------------------------------------------
        // 4. OST_Floors (Divisi A.1 - Partitioned cleanly by Comments)
        // ------------------------------------------------------------
        var floors = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .ToElements();

        foreach (var g in floors.GroupBy(e => GetStr(e, "Comments"), StringComparer.Ordinal))
        {
            string comm = g.Key;
            if (string.IsNullOrWhiteSpace(comm) && !req.IncludeZeroBlankBuckets) continue;
            string title = comm.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase)
                ? "Plat Lantai Lt. Dasar"
                : (string.IsNullOrWhiteSpace(comm) ? "Plat Lantai Lainnya (Blank Comments)" : comm);

            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "A.1",
                TitleSuffix = title,
                Category = BuiltInCategory.OST_Floors,
                SchemaKind = "Slab",
                CommentsFilter = comm,
                ExpectedElementCount = g.Count(),
                ItemOrder = comm.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase) ? 8 :
                            comm.Contains("Mezzanine", StringComparison.OrdinalIgnoreCase) ? 17 : 10
            });
        }

        // ------------------------------------------------------------
        // 5. Steel Connections: Unified Manual/Exploded + Custom Advance Steel Subelements
        //    (Plates, Bolts, Anchors, Voutes/Profiles) - ALL Dual-Key (Lokasi + Type of Connection)
        // ------------------------------------------------------------
        Dictionary<(BuiltInCategory Cat, string Lok, string Toc), int> connBuckets = new();

        void AddConnHit(BuiltInCategory bic, string lok, string toc)
        {
            var k = (bic, lok, toc);
            connBuckets[k] = connBuckets.GetValueOrDefault(k) + 1;
        }

        // Track parent-handled child IDs vs standalone manual elements: simply scan both!
        // Note: Custom connection subelements are NOT returned as separate elements in OfCategory(OST_StructConnectionPlates/Profiles),
        // while manual/exploded elements ARE returned in OfCategory(OST_StructConnectionPlates/Bolts/Anchors).
        foreach (BuiltInCategory bic in new[]
        {
            BuiltInCategory.OST_StructConnectionPlates,
            BuiltInCategory.OST_StructConnectionBolts,
            BuiltInCategory.OST_StructConnectionAnchors,
            BuiltInCategory.OST_StructConnectionProfiles
        })
        {
            var elems = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToElements();
            foreach (Element e in elems)
            {
                string lok = GetStr(e, "Lokasi", "LOKASI");
                string toc = GetStr(e, "Type of Connection", "Type of connection");
                if (string.IsNullOrWhiteSpace(toc)) toc = GetStr(e, "Comments");
                AddConnHit(bic, lok, toc);
            }
        }

        var handlers = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructConnections)
            .WhereElementIsNotElementType()
            .ToElements();

        foreach (Element h in handlers)
        {
            if (h is not StructuralConnectionHandler handler) continue;
            string lok = GetStr(h, "Lokasi", "LOKASI");
            string toc = GetStr(h, "Type of Connection", "Type of connection");
            if (string.IsNullOrWhiteSpace(toc))
            {
                ElementType? et = doc.GetElement(h.GetTypeId()) as ElementType;
                toc = !string.IsNullOrWhiteSpace(et?.FamilyName) ? et.FamilyName : (et?.Name ?? "Custom Connection");
            }

            IList<Subelement>? subs = null;
            try { subs = handler.GetSubelements(); } catch { }
            if (subs == null) continue;

            foreach (Subelement sub in subs)
            {
                long scId = sub.Category?.Id.Value ?? 0;
                if (scId == (long)BuiltInCategory.OST_StructConnectionPlates) AddConnHit(BuiltInCategory.OST_StructConnectionPlates, lok, toc);
                else if (scId == (long)BuiltInCategory.OST_StructConnectionBolts) AddConnHit(BuiltInCategory.OST_StructConnectionBolts, lok, toc);
                else if (scId == (long)BuiltInCategory.OST_StructConnectionAnchors) AddConnHit(BuiltInCategory.OST_StructConnectionAnchors, lok, toc);
                else if (scId == (long)BuiltInCategory.OST_StructConnectionProfiles) AddConnHit(BuiltInCategory.OST_StructConnectionProfiles, lok, toc);
            }
        }

        foreach (var kv in connBuckets.OrderBy(x => x.Key.Lok).ThenBy(x => x.Key.Cat).ThenBy(x => x.Key.Toc))
        {
            BuiltInCategory bic = kv.Key.Cat;
            string lok = kv.Key.Lok;
            string toc = kv.Key.Toc;
            if (string.IsNullOrWhiteSpace(toc) && !req.IncludeZeroBlankBuckets) continue;

            bool isMain = IsMainBuildingLokasi(lok);
            bool isBaseplate = toc.Contains("Baseplate", StringComparison.OrdinalIgnoreCase) || toc.Contains("Base Plate", StringComparison.OrdinalIgnoreCase);

            string wbs = !isMain ? $"C.3.{GetZoneIdx(lok)}" : RouteMainConnectionWbs(bic, toc, isBaseplate);
            string schema = bic switch
            {
                BuiltInCategory.OST_StructConnectionPlates => isBaseplate ? "Baseplate" : "ConnectionPlate",
                BuiltInCategory.OST_StructConnectionBolts => "ConnectionBolt",
                BuiltInCategory.OST_StructConnectionAnchors => "ConnectionAnchor",
                BuiltInCategory.OST_StructConnectionProfiles => "VouteProfile",
                _ => "General"
            };

            int itemOrd = bic switch
            {
                BuiltInCategory.OST_StructConnectionPlates => isBaseplate ? 5 : 3,
                BuiltInCategory.OST_StructConnectionBolts => 4,
                BuiltInCategory.OST_StructConnectionAnchors => 6,
                BuiltInCategory.OST_StructConnectionProfiles => 7,
                _ => 9
            };

            string catLabel = bic switch
            {
                BuiltInCategory.OST_StructConnectionPlates => isBaseplate ? "Baseplate" : "Pelat Sambungan",
                BuiltInCategory.OST_StructConnectionBolts => "Baut Sambungan",
                BuiltInCategory.OST_StructConnectionAnchors => "Angkur Kolom",
                BuiltInCategory.OST_StructConnectionProfiles => "Voute & CT",
                _ => bic.ToString()
            };

            string displayToc = string.IsNullOrWhiteSpace(toc) ? "Blank Connection" : toc;
            string title = !isMain
                ? $"{lok} - {catLabel} {displayToc}"
                : $"{catLabel} {displayToc}";

            plans.Add(new PlannedSchedule
            {
                WbsPrefix = wbs,
                TitleSuffix = title,
                Category = bic,
                SchemaKind = schema,
                LokasiFilter = lok,
                TypeOfConnectionFilter = toc,
                ExpectedElementCount = kv.Value,
                ItemOrder = itemOrd
            });
        }

        // ------------------------------------------------------------
        // 6. Roofs, Walls, Fascia, Cornices, Windows, Railings, Plumbing, Generic, Rebar
        // ------------------------------------------------------------
        var walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToElements();
        foreach (var g in walls.GroupBy(e => GetStr(e, "Comments"), StringComparer.Ordinal))
        {
            string comm = g.Key;
            bool isClad = comm.Contains("Clad", StringComparison.OrdinalIgnoreCase);
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = isClad ? "M.1" : "A.1",
                TitleSuffix = string.IsNullOrWhiteSpace(comm) ? "Dinding Lainnya" : comm,
                Category = BuiltInCategory.OST_Walls,
                SchemaKind = "WallAreaVol",
                CommentsFilter = comm,
                ExpectedElementCount = g.Count(),
                ItemOrder = 16
            });
        }

        var roofs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Roofs).WhereElementIsNotElementType().ToElements();
        foreach (var g in roofs.GroupBy(e => GetStr(e, "Comments"), StringComparer.Ordinal))
        {
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "L.1",
                TitleSuffix = string.IsNullOrWhiteSpace(g.Key) ? "Atap Lainnya" : g.Key,
                Category = BuiltInCategory.OST_Roofs,
                SchemaKind = "AreaSummary",
                CommentsFilter = g.Key,
                ExpectedElementCount = g.Count(),
                ItemOrder = g.Key.Contains("Gutter", StringComparison.OrdinalIgnoreCase) ? 2 : 1
            });
        }

        var fascia = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Fascia).WhereElementIsNotElementType().ToElements();
        if (fascia.Count > 0)
        {
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "L.1",
                TitleSuffix = "Flashing Atap",
                Category = BuiltInCategory.OST_Fascia,
                SchemaKind = "LinearLengthQty",
                ExpectedElementCount = fascia.Count,
                ItemOrder = 3
            });
        }

        var cornices = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Cornices).WhereElementIsNotElementType().ToElements();
        if (cornices.Count > 0)
        {
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "M.1",
                TitleSuffix = "Flashing Dinding Cladding",
                Category = BuiltInCategory.OST_Cornices,
                SchemaKind = "LinearLengthQty",
                ExpectedElementCount = cornices.Count,
                ItemOrder = 3
            });
        }

        var windows = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().ToElements();
        foreach (var g in windows.GroupBy(e => GetStr(e, "Comments"), StringComparer.Ordinal))
        {
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "M.1",
                TitleSuffix = string.IsNullOrWhiteSpace(g.Key) ? "Jendela / Skylight Bangunan Utama" : g.Key,
                Category = BuiltInCategory.OST_Windows,
                SchemaKind = "OpeningArea",
                CommentsFilter = g.Key,
                ExpectedElementCount = g.Count(),
                ItemOrder = 4
            });
        }

        var generic = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsNotElementType().ToElements();
        if (generic.Count > 0)
        {
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "M.1",
                TitleSuffix = "Roof Drain, Embedded Plate & Tali Air",
                Category = BuiltInCategory.OST_GenericModel,
                SchemaKind = "GenericSummary",
                ExpectedElementCount = generic.Count,
                ItemOrder = 6
            });
        }

        var plumbing = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PlumbingFixtures).WhereElementIsNotElementType().ToElements();
        if (plumbing.Count > 0)
        {
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "M.1",
                TitleSuffix = "Floor Drain & Sanitair",
                Category = BuiltInCategory.OST_PlumbingFixtures,
                SchemaKind = "CountTypeSummary",
                ExpectedElementCount = plumbing.Count,
                ItemOrder = 7
            });
        }

        var railings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing).WhereElementIsNotElementType().ToElements();
        if (railings.Count > 0)
        {
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "N.1",
                TitleSuffix = "Railing Tangga",
                Category = BuiltInCategory.OST_StairsRailing,
                SchemaKind = "LinearLengthQty",
                ExpectedElementCount = railings.Count,
                ItemOrder = 5
            });
        }

        var rebar = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rebar).WhereElementIsNotElementType().ToElements();
        foreach (var g in rebar.GroupBy(e => GetStr(e, "Comments"), StringComparer.Ordinal))
        {
            plans.Add(new PlannedSchedule
            {
                WbsPrefix = "N.3",
                TitleSuffix = string.IsNullOrWhiteSpace(g.Key) ? "Rebar Umum (Blank Comments)" : $"Rebar {g.Key}",
                Category = BuiltInCategory.OST_Rebar,
                SchemaKind = "Rebar",
                CommentsFilter = g.Key,
                ExpectedElementCount = g.Count(),
                ItemOrder = 1
            });
        }

        return plans;
    }

    private static void AssignUniqueWbsNames(List<PlannedSchedule> plans)
    {
        var sorted = plans
            .OrderBy(p => SortPrefixKey(p.WbsPrefix))
            .ThenBy(p => p.ItemOrder)
            .ThenBy(p => p.TitleSuffix, StringComparer.OrdinalIgnoreCase)
            .ToList();

        plans.Clear();
        plans.AddRange(sorted);

        Dictionary<string, int> seqByPrefix = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (PlannedSchedule p in plans)
        {
            int next = seqByPrefix.GetValueOrDefault(p.WbsPrefix) + 1;
            seqByPrefix[p.WbsPrefix] = next;

            string cleanTitle = SanitizeScheduleName(p.TitleSuffix);
            string fullName = $"{p.WbsPrefix}.{next} - {cleanTitle}";
            while (!usedNames.Add(fullName))
            {
                next = seqByPrefix[p.WbsPrefix] + 1;
                seqByPrefix[p.WbsPrefix] = next;
                fullName = $"{p.WbsPrefix}.{next} - {cleanTitle}";
            }
            p.ScheduleName = fullName;
        }
    }

    private static string ExecutePlan(Document doc, SuiteRequest req, List<PlannedSchedule> plans)
    {
        List<object> created = new();
        List<string> failed = new();

        using var tg = new TransactionGroup(doc, "AIGRAV QTO v4.0 - Generate Full WBS Schedule Suite");
        tg.Start();

        Dictionary<string, ViewSchedule> existingByName;
        using (var txDel = new Transaction(doc, "AIGRAV - Cleanup Existing QTO Schedules"))
        {
            FailureHandlingOptions fhoDel = txDel.GetFailureHandlingOptions();
            fhoDel.SetFailuresPreprocessor(new SilentWarningPreprocessor());
            fhoDel.SetClearAfterRollback(true);
            txDel.SetFailureHandlingOptions(fhoDel);
            txDel.Start();

            existingByName = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule && !s.IsInternalKeynoteSchedule)
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            HashSet<string> targetNames = new(plans.Select(p => p.ScheduleName), StringComparer.OrdinalIgnoreCase);
            foreach (var kv in existingByName.ToList())
            {
                string n = kv.Key;
                bool isProtected = n.Equals("DAFTAR GAMBAR", StringComparison.OrdinalIgnoreCase) ||
                                   n.Equals("Keynote Legend", StringComparison.OrdinalIgnoreCase) ||
                                   n.Equals("Revision Schedule", StringComparison.OrdinalIgnoreCase) ||
                                   n.StartsWith("<");
                if (isProtected) continue;

                bool shouldDelete = req.DeleteExistingQtoSchedules || (req.ReplaceExisting && targetNames.Contains(n));
                if (!shouldDelete) continue;

                try
                {
                    kv.Value.Name = "_DEL_" + kv.Value.Id.Value + "_" + Guid.NewGuid().ToString("N")[..6];
                    doc.Delete(kv.Value.Id);
                    existingByName.Remove(kv.Key);
                }
                catch (Exception ex)
                {
                    failed.Add($"Cleanup '{n}' (Id {kv.Value.Id.Value}) skipped: {ex.Message}");
                }
            }

            doc.Regenerate();
            txDel.Commit();
        }

        using (var tx = new Transaction(doc, "AIGRAV - Create Universal QTO Schedules"))
        {
            FailureHandlingOptions fho = tx.GetFailureHandlingOptions();
            fho.SetFailuresPreprocessor(new SilentWarningPreprocessor());
            fho.SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(fho);
            tx.Start();

            foreach (PlannedSchedule plan in plans)
            {
                try
                {
                    if (existingByName.ContainsKey(plan.ScheduleName))
                    {
                        failed.Add($"Skipped '{plan.ScheduleName}': still exists after cleanup.");
                        continue;
                    }

                    ViewSchedule vs = CreateScheduleFromPlan(doc, plan);
                    created.Add(new
                    {
                        schedule_name = vs.Name,
                        schedule_id = vs.Id.Value,
                        category = plan.Category.ToString(),
                        schema = plan.SchemaKind,
                        expected_count = plan.ExpectedElementCount
                    });
                }
                catch (Exception ex)
                {
                    failed.Add($"Failed '{plan.ScheduleName}' ({plan.Category}): {ex.Message}");
                }
            }

            doc.Regenerate();
            tx.Commit();
        }

        tg.Assimilate();

        return JsonSerializer.Serialize(new
        {
            status = failed.Count == 0 ? "Success" : "CompletedWithErrors",
            mode = "execute",
            project = doc.Title,
            total_planned = plans.Count,
            total_created = created.Count,
            total_failed = failed.Count,
            failed_items = failed,
            created_schedules_sample = created.Take(40).ToList(),
            all_created_names = created
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ViewSchedule CreateScheduleFromPlan(Document doc, PlannedSchedule plan)
    {
        ElementId catId = new((int)plan.Category);
        ViewSchedule vs = ViewSchedule.CreateSchedule(doc, catId);
        vs.Name = plan.ScheduleName;

        ScheduleDefinition def = vs.Definition;
        Dictionary<string, SchedulableField> fmap = new(StringComparer.OrdinalIgnoreCase);
        SchedulableField? sfTypeNameParam = null;
        foreach (SchedulableField sf in def.GetSchedulableFields())
        {
            string n = sf.GetName(doc);
            if (!fmap.ContainsKey(n)) fmap[n] = sf;
            if (sf.ParameterId.Value == (long)BuiltInParameter.SYMBOL_NAME_PARAM)
                sfTypeNameParam = sf;
        }

        // Column A is always Lokasi across ALL disciplines (fallback Comments only if Lokasi not bound on category)
        ScheduleField? fLok = TryAdd(def, fmap, "Lokasi", "Lokasi");
        ScheduleField? fTypeConn = null;
        ScheduleField? fComm = null;
        ScheduleField? fTypeForFilter = null;

        switch (plan.SchemaKind)
        {
            case "FoundationPile":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                TryAdd(def, fmap, "Depth", "Depth (mm)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fVol = TryAdd(def, fmap, "Volume", "Volume (m³)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                fTypeForFilter = fType;
                break;
            }
            case "FoundationGeneral":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                TryAdd(def, fmap, "Mark", "Jumlah Pile");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fVol = TryAdd(def, fmap, "Volume", "Volume (m³)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                fTypeForFilter = fType;
                break;
            }
            case "FormworkFoundation":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                TryAdd(def, fmap, "Length", "Length (mm)");
                TryAdd(def, fmap, "Width", "Width (mm)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fLuas = TryAdd(def, fmap, "Luas (m2)", "Luas (m2)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fLuas != null) fLuas.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "ConcreteLinear":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                var fLen = TryAdd(def, fmap, "System Length", "Length (mm)")
                           ?? TryAdd(def, fmap, "Cut Length", "Length (mm)")
                           ?? TryAdd(def, fmap, "Length", "Length (mm)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fVol = TryAdd(def, fmap, "Volume", "Volume (m³)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                if (fLen != null) def.AddSortGroupField(new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Ascending));
                fTypeForFilter = fType;
                break;
            }
            case "FormworkFraming":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                var fLen = TryAdd(def, fmap, "Cut Length", "Panjang (mm)") ?? TryAdd(def, fmap, "System Length", "Panjang (mm)");
                TryAdd(def, fmap, "Tinggi (mm)", "Tinggi (mm)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fLuas = TryAdd(def, fmap, "Luas (m2)", "Luas (m2)") ?? TryAdd(def, fmap, "Area", "Luas (m²)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fLuas != null) fLuas.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending) { ShowFooter = true });
                if (fLen != null) def.AddSortGroupField(new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "SteelProfile":
            {
                var fType = TryAdd(def, fmap, "Type", "Type") ?? TryAdd(def, fmap, "Family and Type", "Type");
                var fLen = TryAdd(def, fmap, "Cut Length", "Length (mm)")
                           ?? TryAdd(def, fmap, "System Length", "Length (mm)")
                           ?? TryAdd(def, fmap, "Length", "Length (mm)");
                fComm = TryAdd(def, fmap, "Comments", "Comments", hidden: true);
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fWt = TryAdd(def, fmap, "Berat Kalibrasi (Kg)", "Berat (Kg)")
                          ?? TryAdd(def, fmap, "Weight", "Berat (Kg)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fWt != null) fWt.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                if (fLen != null) def.AddSortGroupField(new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Ascending));
                fTypeForFilter = fType;
                break;
            }
            case "ConnectionPlate":
            {
                fTypeConn = TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
                var fThk = TryAdd(def, fmap, "Thickness", "Thickness (mm)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fWt = TryAdd(def, fmap, "Weight", "Berat (Kg)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fWt != null) fWt.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fTypeConn != null) def.AddSortGroupField(new ScheduleSortGroupField(fTypeConn.FieldId, ScheduleSortOrder.Ascending));
                if (fThk != null) def.AddSortGroupField(new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending) { ShowFooter = true });
                break;
            }
            case "Baseplate":
            {
                fTypeConn = TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
                var fThk = TryAdd(def, fmap, "Thickness", "Thickness (mm)");
                var fW = TryAdd(def, fmap, "Width", "Width (mm)");
                var fL = TryAdd(def, fmap, "Length", "Length (mm)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fWt = TryAdd(def, fmap, "Weight", "Berat (Kg)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fWt != null) fWt.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fThk != null) def.AddSortGroupField(new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending) { ShowFooter = true });
                if (fW != null) def.AddSortGroupField(new ScheduleSortGroupField(fW.FieldId, ScheduleSortOrder.Ascending));
                if (fL != null) def.AddSortGroupField(new ScheduleSortGroupField(fL.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "ConnectionBolt":
            {
                fTypeConn = TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
                var fDia = TryAdd(def, fmap, "Diameter", "Diameter (mm)");
                var fNum = TryAdd(def, fmap, "Number", "Qty (Pcs)");
                if (fNum != null) fNum.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fTypeConn != null) def.AddSortGroupField(new ScheduleSortGroupField(fTypeConn.FieldId, ScheduleSortOrder.Ascending));
                if (fDia != null) def.AddSortGroupField(new ScheduleSortGroupField(fDia.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "ConnectionAnchor":
            {
                fTypeConn = TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
                var fDia = TryAdd(def, fmap, "Diameter", "Diameter (mm)");
                var fLen = TryAdd(def, fmap, "Length", "Length (mm)");
                var fNum = TryAdd(def, fmap, "Number", "Qty (Pcs)");
                if (fNum != null) fNum.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fDia != null) def.AddSortGroupField(new ScheduleSortGroupField(fDia.FieldId, ScheduleSortOrder.Ascending));
                if (fLen != null) def.AddSortGroupField(new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "VouteProfile":
            {
                fTypeConn = TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
                var fProf = TryAdd(def, fmap, "Profile Type", "Type");
                var fCut = TryAdd(def, fmap, "Cut Length", "Length (mm)");
                var fVol = TryAdd(def, fmap, "Volume", "Volume (m³)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fWt = TryAdd(def, fmap, "Exact Weight", "Berat (Kg)");
                if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fWt != null) fWt.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fProf != null) def.AddSortGroupField(new ScheduleSortGroupField(fProf.FieldId, ScheduleSortOrder.Ascending));
                if (fCut != null) def.AddSortGroupField(new ScheduleSortGroupField(fCut.FieldId, ScheduleSortOrder.Ascending) { ShowFooter = true });
                break;
            }
            case "Slab":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Family and Type", "Family and Type");
                var fArea = TryAdd(def, fmap, "Area", "Area (m²)");
                TryAdd(def, fmap, "Default Thickness", "Tebal (mm)");
                var fVol = TryAdd(def, fmap, "Volume", "Volume (m³)");
                if (fArea != null) fArea.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "WallAreaVol":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fArea = TryAdd(def, fmap, "Area", "Luas (m²)");
                var fVol = TryAdd(def, fmap, "Volume", "Volume (m³)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fArea != null) fArea.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fVol != null) fVol.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "AreaSummary":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fArea = TryAdd(def, fmap, "Area", "Luas (m²)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fArea != null) fArea.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "LinearLengthQty":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                var fLen = TryAdd(def, fmap, "Length", "Length (mm)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                if (fLen != null) fLen.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "OpeningArea":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fType = TryAdd(def, fmap, "Type", "Type");
                TryAdd(def, fmap, "Width", "Width (mm)");
                TryAdd(def, fmap, "Height", "Height (mm)");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                var fArea = TryAdd(def, fmap, "Area", "Luas (m²)");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fArea != null) fArea.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "GenericSummary":
            case "CountTypeSummary":
            {
                fComm = TryAdd(def, fmap, "Comments", "Comments");
                var fFam = TryAdd(def, fmap, "Family", "Family");
                var fType = TryAdd(def, fmap, "Type", "Type");
                var fQty = TryAdd(def, fmap, "Count", "Qty");
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending) { ShowFooter = true });
                else if (fFam != null) def.AddSortGroupField(new ScheduleSortGroupField(fFam.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
            case "Rebar":
            {
                fComm = TryAdd(def, fmap, "Comments", "Keterangan");
                var fDia = TryAdd(def, fmap, "Bar Diameter", "Diameter");
                TryAdd(def, fmap, "Shape", "Shape");
                TryAdd(def, fmap, "Bar Length", "Bar Length (mm)");
                var fTotLen = TryAdd(def, fmap, "Total Bar Length", "Total Length (mm)");
                var fQty = TryAdd(def, fmap, "Quantity", "Qty");
                if (fTotLen != null) fTotLen.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fQty != null) fQty.DisplayType = ScheduleFieldDisplayType.Totals;
                if (fDia != null) def.AddSortGroupField(new ScheduleSortGroupField(fDia.FieldId, ScheduleSortOrder.Ascending));
                break;
            }
        }

        // Apply Disjoint Filters cleanly
        if (plan.LokasiFilter != null && fLok != null)
        {
            TryAddEqualFilter(def, fLok.FieldId, plan.LokasiFilter);
        }
        if (plan.TypeOfConnectionFilter != null)
        {
            fTypeConn ??= TryAdd(def, fmap, "Type of Connection", "Tipe Sambungan");
            if (fTypeConn != null) TryAddEqualFilter(def, fTypeConn.FieldId, plan.TypeOfConnectionFilter);
        }
        if (plan.CommentsFilter != null)
        {
            fComm ??= TryAdd(def, fmap, "Comments", "Comments", hidden: true);
            if (fComm != null) TryAddEqualFilter(def, fComm.FieldId, plan.CommentsFilter);
        }
        if (!string.IsNullOrWhiteSpace(plan.TypeNameFilter))
        {
            bool filteredByTypeId = false;
            if (plan.TypeIdFilter != null && fTypeForFilter != null)
            {
                try
                {
                    def.AddFilter(new ScheduleFilter(fTypeForFilter.FieldId, ScheduleFilterType.Equal, plan.TypeIdFilter));
                    filteredByTypeId = true;
                }
                catch { }
            }
            if (!filteredByTypeId && sfTypeNameParam != null)
            {
                ScheduleField? fTn = FindOrAddHiddenByParam(def, sfTypeNameParam);
                if (fTn != null)
                    TryAddEqualFilter(def, fTn.FieldId, plan.TypeNameFilter);
            }
        }

        def.IsItemized = false;
        def.ShowGrandTotal = true;
        def.ShowGrandTotalTitle = true;
        def.ShowGrandTotalCount = true;
        def.GrandTotalTitle = "Total";

        return vs;
    }

    private static bool TryAddEqualFilter(ScheduleDefinition def, ScheduleFieldId fid, string val)
    {
        try
        {
            ScheduleFilter f = new(fid, ScheduleFilterType.Equal, val);
            def.AddFilter(f);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ScheduleField? FindOrAddHiddenByParam(ScheduleDefinition def, SchedulableField sf)
    {
        for (int i = 0; i < def.GetFieldCount(); i++)
        {
            if (def.GetField(i).ParameterId == sf.ParameterId) return def.GetField(i);
        }
        try
        {
            ScheduleField f = def.AddField(sf);
            f.IsHidden = true;
            return f;
        }
        catch { return null; }
    }

    private static ScheduleField? TryAdd(ScheduleDefinition def, Dictionary<string, SchedulableField> fmap, string name, string heading, bool hidden = false)
    {
        if (!fmap.TryGetValue(name, out SchedulableField? sf)) return null;
        for (int i = 0; i < def.GetFieldCount(); i++)
        {
            if (def.GetField(i).GetName().Equals(name, StringComparison.OrdinalIgnoreCase))
                return def.GetField(i);
        }
        ScheduleField f = def.AddField(sf);
        if (!string.IsNullOrWhiteSpace(heading)) f.ColumnHeading = heading;
        f.IsHidden = hidden;
        return f;
    }

    private static bool IsConcreteElement(string fam, string typeName, string comm)
    {
        string probe = $"{fam} {typeName} {comm}".ToUpperInvariant();
        if (probe.Contains("PEDESTAL") || probe.Contains("SLOOF") || probe.Contains("BATU KALI") ||
            probe.Contains("CONCRETE") || probe.Contains("BETON"))
            return true;
        if (Regex.IsMatch(typeName.Trim(), @"^K\s*\d{3}\s*[X×]\s*\d{3}", RegexOptions.IgnoreCase))
            return true;
        return false;
    }

    private static bool IsMainBuildingLokasi(string lok) =>
        string.IsNullOrWhiteSpace(lok) || lok.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase);

    private static string RouteMainFramingWbs(string comm)
    {
        string c = comm.ToUpperInvariant();
        if (c.Contains("RAFTER") || c.Contains("GIRDER")) return "C.1";
        if (c.Contains("CREMONA")) return "D.1";
        if (c.Contains("TIE BEAM")) return "E.1";
        if (c.Contains("GORDING") || c.Contains("TIE ROD")) return "F.1";
        if (c.Contains("CLAD")) return "G.1";
        if (c.Contains("REGEL")) return "H.1";
        if (c.Contains("WALL BRAC")) return "I.1";
        if (c.Contains("WIND BRAC")) return "J.1";
        if (c.Contains("GUTTER") || c.Contains("TALANG")) return "K.1";
        if (c.Contains("TANGGA")) return "N.1";
        if (c.Contains("LIFT") || c.Contains("HOIST")) return "N.2";
        return "K.2";
    }

    private static string RouteMainConnectionWbs(BuiltInCategory bic, string toc, bool isBaseplate)
    {
        string t = toc.ToUpperInvariant();
        if (t.Contains("HOIST")) return "N.2";
        if (t.Contains("TANGGA") || t.Contains("CHECKER")) return "N.1";
        if (bic == BuiltInCategory.OST_StructConnectionAnchors) return "B.1";
        if (isBaseplate || t.Contains("KOLOM") || t.Contains("GEWEL")) return "B.1";
        if (t.Contains("RAFTER") || t.Contains("GIRDER") || t.Contains("CASTELA") || t.Contains("GUSSET") || t.Contains("CWF588") || t.Contains("MEZZANINE")) return "C.1";
        if (t.Contains("CREMONA")) return "D.1";
        if (t.Contains("TIE BEAM")) return "E.1";
        if (t.Contains("GORDING")) return "F.1";
        if (t.Contains("CLAD")) return "G.1";
        if (t.Contains("REGEL")) return "H.1";
        if (t.Contains("WALL BRAC")) return "I.1";
        if (t.Contains("WIND BRAC")) return "J.1";
        if (t.Contains("GUTTER") || t.Contains("TALANG")) return "K.1";
        return "C.1";
    }

    private static string FormatMainFramingTitle(string comm, string typeName, bool singleTypeBucket)
    {
        if (string.IsNullOrWhiteSpace(comm)) return $"Balok Baja {typeName} (Blank Comments)";
        if (singleTypeBucket) return comm;
        return $"{comm} - {typeName}";
    }

    private static (string Major, int Minor, int Sub) SortPrefixKey(string p)
    {
        var m = Regex.Match(p, @"^([A-Z]+)\.(\d+)(?:\.(\d+))?$");
        if (!m.Success) return ("ZZ", 99, 99);
        return (m.Groups[1].Value, int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
    }

    private static string SanitizeScheduleName(string raw)
    {
        string s = Regex.Replace(raw ?? "Schedule", @"[:\\{}\[\]|;<>?`~]", " ");
        s = Regex.Replace(s, @"\s{2,}", " ").Trim();
        return string.IsNullOrWhiteSpace(s) ? "Schedule" : s;
    }

    private static string GetFamilyName(Document doc, Element el)
    {
        return (doc.GetElement(el.GetTypeId()) as ElementType)?.FamilyName ?? string.Empty;
    }

    private static string GetTypeName(Document doc, Element el)
    {
        return doc.GetElement(el.GetTypeId())?.Name ?? el.Name ?? "Unknown";
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

    private static SuiteRequest ParseArgs(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return new SuiteRequest();
        try
        {
            return JsonSerializer.Deserialize<SuiteRequest>(argument, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new SuiteRequest();
        }
        catch { return new SuiteRequest(); }
    }
}

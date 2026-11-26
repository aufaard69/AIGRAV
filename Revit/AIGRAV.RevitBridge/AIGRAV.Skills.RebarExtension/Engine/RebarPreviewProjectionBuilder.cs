using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Contracts;
using AIGRAV.Skills.RebarExtension.Builders;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Engine;

/// <summary>
/// Builds BOTH live 3D/2D RebarPreviewPackage (from Revit Selection without mutating the model)
/// AND executes atomic TransactionGroup rebar generation on strictly the selected elements.
/// Guarantee: 100% deterministic, zero AI/LLM/cloud dependency.
/// </summary>
public static class RebarPreviewProjectionBuilder
{
    public static RebarPreviewPackage BuildPreview(Document doc, RebarGenerationRequest request)
    {
        var pkg = new RebarPreviewPackage();
        if (doc == null || !doc.IsValidObject) return pkg;

        var selectedElements = request.SelectedHostElementIds
            .Distinct()
            .Select(id => doc.GetElement(new ElementId(id)))
            .Where(el => el != null && HostGeometryExtractor.CanHostRebar(el))
            .Cast<Element>()
            .ToList();

        if (selectedElements.Count == 0)
            return pkg;

        // Gather raw plans using identical deterministic builders as Revit Commit!
        var (allPlanned, columnSegs, chains, summaries, rawMeshes, warnings) = CollectPlansAndMeshes(doc, selectedElements, request);

        pkg.Hosts.AddRange(summaries);
        pkg.Warnings.AddRange(warnings);

        // Compute scene centroid across all extracted concrete vertices so Viewport3D centers smoothly at (0,0,0)!
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var m in rawMeshes)
        {
            if (m.VerticesMm.Count == 0) continue;
            minX = Math.Min(minX, m.MinMm.X); minY = Math.Min(minY, m.MinMm.Y); minZ = Math.Min(minZ, m.MinMm.Z);
            maxX = Math.Max(maxX, m.MaxMm.X); maxY = Math.Max(maxY, m.MaxMm.Y); maxZ = Math.Max(maxZ, m.MaxMm.Z);
        }

        if (minX > maxX)
        {
            minX = minY = minZ = -1000;
            maxX = maxY = maxZ = 1000;
        }

        var center = new Point3DData((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5);
        pkg.SceneCenterMm = center;
        pkg.SceneRadiusMm = Math.Max(1500.0, Math.Sqrt(
            Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2) + Math.Pow(maxZ - minZ, 2)) * 0.65);

        // Offset ConcreteMeshes to scene centroid
        foreach (var m in rawMeshes)
        {
            m.VerticesMm = m.VerticesMm.Select(v => new Point3DData(v.X - center.X, v.Y - center.Y, v.Z - center.Z)).ToList();
            m.MinMm = new Point3DData(m.MinMm.X - center.X, m.MinMm.Y - center.Y, m.MinMm.Z - center.Z);
            m.MaxMm = new Point3DData(m.MaxMm.X - center.X, m.MaxMm.Y - center.Y, m.MaxMm.Z - center.Z);
            pkg.ConcreteMeshes.Add(m);
        }

        pkg.TotalConcreteVolumeM3 = Math.Round(summaries.Sum(s => s.ConcreteVolumeM3), 4);

        // Expand PlannedBars into 3D Polylines & Preview BBS Rows
        int markCounter = 1;
        var bbsGroups = new Dictionary<string, BbsPreviewRowDto>(StringComparer.Ordinal);
        double maxBarMm = 0;
        int overlapCount = 0;
        int totalPieces = 0;

        foreach (var plan in allPlanned)
        {
            var (_, _, _, colorHex) = RebarResourceResolver.GetColorForDiameter(plan.DiameterMm);
            var barDto = new RebarBarPreviewDto
            {
                HostElementId = plan.HostElementId,
                GroupTag = plan.Notes,
                Role = plan.Role,
                ShapeCode = plan.ShapeCode,
                DiameterMm = plan.DiameterMm,
                ColorHex = colorHex,
                IsStirrup = plan.Style == RebarStyle.StirrupTie,
                IsSpliceOverlap = plan.Notes.Contains("Splice", StringComparison.OrdinalIgnoreCase) || plan.Notes.Contains("Overlap", StringComparison.OrdinalIgnoreCase)
            };

            if (barDto.IsSpliceOverlap) overlapCount += Math.Max(1, plan.Quantity);

            // Expand array along aligned PreferredNormal identically to Revit CreateRevitRebars!
            int displayArrayCopies = Math.Clamp(plan.LayoutFixedCount, 1, 80);
            XYZ arrayStep = XYZ.Zero;
            if (plan.LayoutFixedCount > 1 && plan.ArrayLengthFeet > 1e-4 && plan.PolylineCurvesFeet.Count > 0)
            {
                XYZ pNorm = BeamRebarBuilder.ComputeSafePlaneNormal(plan.PolylineCurvesFeet[0], plan.PreferredNormal);
                if (plan.PreferredNormal.GetLength() > 1e-5 && pNorm.DotProduct(plan.PreferredNormal) < 0)
                    pNorm = -pNorm;
                arrayStep = (plan.BarsOnNormalSide ? pNorm.Normalize() : -pNorm.Normalize())
                    * (plan.ArrayLengthFeet / Math.Max(1, displayArrayCopies - 1));
            }

            foreach (var poly in plan.PolylineCurvesFeet)
            {
                for (int copy = 0; copy < displayArrayCopies; copy++)
                {
                    XYZ shift = arrayStep * copy;
                    var ptsMm = poly.Select(pt => new Point3DData(
                        RebarResourceResolver.FeetToMm(pt.X + shift.X) - center.X,
                        RebarResourceResolver.FeetToMm(pt.Y + shift.Y) - center.Y,
                        RebarResourceResolver.FeetToMm(pt.Z + shift.Z) - center.Z)).ToList();
                    barDto.PolylinesMm.Add(ptsMm);
                }
            }
            pkg.RebarBars.Add(barDto);

            maxBarMm = Math.Max(maxBarMm, plan.CutLengthMm);
            int actualQty = Math.Max(1, plan.Quantity);
            totalPieces += actualQty;

            string key = $"{plan.Role}|{plan.ShapeCode}|{plan.DiameterMm}|{(int)Math.Round(plan.CutLengthMm)}";
            if (bbsGroups.TryGetValue(key, out var existingRow))
            {
                existingRow.Qty += actualQty;
            }
            else
            {
                string prefix = plan.Style == RebarStyle.StirrupTie ? "S" : "B";
                bbsGroups[key] = new BbsPreviewRowDto
                {
                    Mark = $"{prefix}{markCounter++:D2}-D{plan.DiameterMm}",
                    HostTag = $"#{plan.HostElementId}",
                    HostCategory = summaries.FirstOrDefault(s => s.ElementId == plan.HostElementId)?.Category.ToString() ?? "Host",
                    Lokasi = request.Global.DefaultLokasi,
                    Role = plan.Role,
                    ShapeCode = plan.ShapeCode,
                    DiameterMm = plan.DiameterMm,
                    CutLengthMm = Math.Round(plan.CutLengthMm, 0),
                    Qty = actualQty,
                    Notes = plan.Notes
                };
            }
        }

        pkg.BbsRows = bbsGroups.Values.OrderBy(r => r.DiameterMm).ThenBy(r => r.Mark).ToList();
        pkg.TotalSteelWeightKg = Math.Round(pkg.BbsRows.Sum(r => r.TotalWeightKg), 2);
        pkg.MaxSingleBarLengthMm = Math.Round(maxBarMm, 0);
        pkg.TotalOverlapCount = overlapCount;
        pkg.TotalBarPieces = totalPieces;

        return pkg;
    }

    public static (
        List<RebarPlannedBar> AllPlanned,
        List<ColumnSegmentModel> ColumnSegs,
        List<ColumnChainSolution> ColumnChains,
        List<SelectedHostSummary> Summaries,
        List<ConcreteMeshPreviewDto> Meshes,
        List<string> Warnings)
    CollectPlansAndMeshes(
        Document doc,
        IReadOnlyList<Element> selectedElements,
        RebarGenerationRequest request)
    {
        var allPlanned = new List<RebarPlannedBar>();
        var colSegModels = new List<ColumnSegmentModel>();
        var colChainSolutions = new List<ColumnChainSolution>();
        var summaries = new List<SelectedHostSummary>();
        var meshes = new List<ConcreteMeshPreviewDto>();
        var warnings = new List<string>();

        var beams = new List<Element>();
        var cols = new List<Element>();
        var founds = new List<Element>();
        var floors = new List<Element>();

        foreach (var el in selectedElements)
        {
            switch (HostGeometryExtractor.ClassifyElement(el))
            {
                case StructuralHostCategory.Beam: beams.Add(el); break;
                case StructuralHostCategory.Column: cols.Add(el); break;
                case StructuralHostCategory.Foundation: founds.Add(el); break;
                case StructuralHostCategory.Floor: floors.Add(el); break;
            }
        }

        // 1. Beams
        foreach (var b in beams)
        {
            var info = HostGeometryExtractor.ExtractBeam(doc, b, request.Beam);
            var plan = BeamRebarBuilder.BuildPlan(info, request.Beam, request.Global, warnings);
            allPlanned.AddRange(plan);
            meshes.Add(HostGeometryExtractor.ExtractConcreteMeshForViewport(b, StructuralHostCategory.Beam, $"Beam #{b.Id.Value}"));
            summaries.Add(CreateSummary(doc, b, StructuralHostCategory.Beam, info.TotalLengthMm, info.WidthMm, info.DepthMm));
        }

        // 2. Column Vertical Chains (with Foundation base embedment + 90 deg bend!)
        foreach (var c in cols)
        {
            var seg = HostGeometryExtractor.ExtractColumnSegment(doc, c, request.Column);
            colSegModels.Add(seg);
            meshes.Add(HostGeometryExtractor.ExtractConcreteMeshForViewport(c, StructuralHostCategory.Column, $"Column #{c.Id.Value}"));
            summaries.Add(CreateSummary(doc, c, StructuralHostCategory.Column, seg.TopZMm - seg.BaseZMm, seg.WidthXMm, seg.DepthYMm));
        }
        var groupedChains = ColumnChainAnalyzer.GroupVerticalChains(colSegModels);
        foreach (var chain in groupedChains)
        {
            var footing = HostGeometryExtractor.FindSupportingFoundation(doc, chain[0]);
            var sol = ColumnChainAnalyzer.SolveChain(chain, footing, request.Column, request.Global);
            warnings.AddRange(sol.Warnings);
            colChainSolutions.Add(sol);
            allPlanned.AddRange(ColumnRebarBuilder.BuildPlanForChain(sol, request.Column, request.Global));
        }

        // 3. Foundations (pure Footplate X/Y Mesh without column dowels!)
        foreach (var f in founds)
        {
            var info = HostGeometryExtractor.ExtractFoundation(doc, f, request.Foundation);
            allPlanned.AddRange(FoundationRebarBuilder.BuildPlan(info, request.Foundation, request.Global));
            meshes.Add(HostGeometryExtractor.ExtractConcreteMeshForViewport(f, StructuralHostCategory.Foundation, $"Foundation #{f.Id.Value}"));
            summaries.Add(CreateSummary(doc, f, StructuralHostCategory.Foundation, info.LengthXMm, info.WidthYMm, info.ThicknessMm));
        }

        // 4. Floors (Two-way mesh + void opening trimming)
        foreach (var fl in floors)
        {
            var info = HostGeometryExtractor.ExtractFloor(doc, fl);
            allPlanned.AddRange(FloorRebarBuilder.BuildPlan(info, request.Floor, request.Global));
            meshes.Add(HostGeometryExtractor.ExtractConcreteMeshForViewport(fl, StructuralHostCategory.Floor, $"Floor #{fl.Id.Value}"));

            BoundingBoxXYZ? bb = fl.get_BoundingBox(null);
            double lx = bb != null ? RebarResourceResolver.FeetToMm(bb.Max.X - bb.Min.X) : 4000;
            double ly = bb != null ? RebarResourceResolver.FeetToMm(bb.Max.Y - bb.Min.Y) : 4000;
            summaries.Add(CreateSummary(doc, fl, StructuralHostCategory.Floor, lx, ly, info.ThicknessMm));
        }

        return (allPlanned, colSegModels, colChainSolutions, summaries, meshes, warnings);
    }

    private static SelectedHostSummary CreateSummary(Document doc, Element el, StructuralHostCategory cat, double spanOrHeightMm, double wMm, double dOrTMm)
    {
        string typeName = doc.GetElement(el.GetTypeId())?.Name ?? el.Name ?? cat.ToString();
        string hash = RebarIdempotencyManager.ComputeHostGeometryHash(el);
        var existing = RebarIdempotencyManager.FindExistingAigravRebarsForHost(doc, el.Id.Value);

        bool modified = false;
        string existingTag = string.Empty;
        if (existing.Count > 0)
        {
            existingTag = RebarIdempotencyManager.BuildHostTag(el.Id.Value);
            string? oldHash = RebarIdempotencyManager.TryGetStringParam(existing[0], RebarIdempotencyManager.ParamHostHash);
            if (!string.IsNullOrEmpty(oldHash) && !string.Equals(oldHash, hash, StringComparison.Ordinal))
                modified = true;
        }

        return new SelectedHostSummary
        {
            ElementId = el.Id.Value,
            Category = cat,
            CategoryDisplay = cat.ToString(),
            FamilyAndType = typeName,
            HostMark = el.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? $"#{el.Id.Value}",
            LevelName = HostGeometryExtractor.GetLevelName(doc, el),
            IsIncluded = true,
            SpanOrHeightMm = Math.Round(spanOrHeightMm, 0),
            WidthMm = Math.Round(wMm, 0),
            DepthOrThicknessMm = Math.Round(dOrTMm, 0),
            ConcreteVolumeM3 = HostGeometryExtractor.GetConcreteVolumeM3(el),
            ExistingRebarCount = existing.Count,
            ExistingAigravTag = existingTag,
            HostGeometryHash = hash,
            IsModifiedSinceGenerate = modified
        };
    }
}

/// <summary>
/// Atomic offline Revit generation orchestrator using TransactionGroup + SuppressWarnings
/// strictly operating on user-selected hosts, plus Reinforcement Browser project scanner.
/// </summary>
public static class RebarGenerationOrchestrator
{
    public static RebarExecutionResult GenerateSelected(Document doc, RebarGenerationRequest request)
    {
        var result = new RebarExecutionResult();
        if (doc == null || !doc.IsValidObject)
        {
            result.Message = "Tidak ada dokumen Revit yang aktif.";
            return result;
        }

        var selectedElements = request.SelectedHostElementIds
            .Distinct()
            .Select(id => doc.GetElement(new ElementId(id)))
            .Where(el => el != null && HostGeometryExtractor.CanHostRebar(el))
            .Cast<Element>()
            .ToList();

        if (selectedElements.Count == 0)
        {
            result.Message = "Pilih minimal satu elemen Beam, Column, Foundation, atau Floor di Revit.";
            return result;
        }

        var (allPlanned, _, _, _, _, planWarnings) = RebarPreviewProjectionBuilder.CollectPlansAndMeshes(doc, selectedElements, request);
        result.Warnings.AddRange(planWarnings);

        var resolver = new RebarResourceResolver();
        RebarSplitEngine.ApplyRebarConstraintsPreference(request.Global.UseRebarConstraints);

        using var tg = new TransactionGroup(doc, $"AIGRAV Rebar Generate ({selectedElements.Count} host)");
        tg.Start();

        using var tx = new Transaction(doc, "Generate AIGRAV Structural Reinforcement");
        TransactionFailureHandling.SuppressWarnings(tx);
        tx.Start();

        try
        {
            // 1. Idempotent clean previous AIGRAV-tagged rebars for each selected host ONLY
            foreach (var el in selectedElements)
            {
                var deleted = RebarIdempotencyManager.DeleteExistingAigravRebarsForHost(doc, el.Id.Value);
                result.DeletedRebarIds.AddRange(deleted);
            }

            // 2. Apply cover type per host category
            var byHost = allPlanned.GroupBy(p => p.HostElementId);
            foreach (var grp in byHost)
            {
                Element? host = doc.GetElement(new ElementId(grp.Key));
                if (host == null) continue;

                var cat = HostGeometryExtractor.ClassifyElement(host);
                double covMm = cat switch
                {
                    StructuralHostCategory.Beam => request.Beam.CoverMm,
                    StructuralHostCategory.Column => request.Column.CoverMm,
                    StructuralHostCategory.Foundation => request.Foundation.CoverMm,
                    StructuralHostCategory.Floor => request.Floor.CoverMm,
                    _ => request.Global.DefaultCoverMm
                };
                var coverType = resolver.ResolveOrCreateCoverType(doc, covMm, cat.ToString(), result.Warnings);
                resolver.AssignHostCoverParameters(host, coverType, result.Warnings);

                string hostHash = RebarIdempotencyManager.ComputeHostGeometryHash(host);
                string lokasi = !string.IsNullOrWhiteSpace(request.Global.DefaultLokasi)
                    ? request.Global.DefaultLokasi
                    : HostGeometryExtractor.GetLevelName(doc, host);

                var created = BeamRebarBuilder.CreateRevitRebars(
                    doc, host, grp.ToList(), resolver, lokasi, hostHash, result.Warnings, result.FailedItems);

                result.CreatedRebarIds.AddRange(created);
            }

            // 3. Assign deterministic AIGRAV_Mark & Schedule Mark
            if (result.CreatedRebarIds.Count > 0)
            {
                MarkNumberingEngine.AssignMarksInDocument(doc, request.Global,
                    request.Global.MarkScope == MarkScope.PerHost ? result.CreatedRebarIds : null);
            }

            // 4. Visual 3D Unobscured + Fine Detail + Diameter Color
            if (result.CreatedRebarIds.Count > 0 && doc.ActiveView != null)
            {
                RebarVisualAndBbsService.Apply3DSolidAndUnobscured(
                    doc, doc.ActiveView, result.CreatedRebarIds,
                    request.Global.SetSolidInFine3D, request.Global.ApplyColorByDiameter, result.Warnings);
            }

            RebarIdempotencyManager.EnsureSharedOrProjectParametersNotice(doc, result.Warnings);

            tx.Commit();
            tg.Assimilate();

            var bbs = MarkNumberingEngine.ExtractBbsRowsFromModel(doc, selectedElements.Select(e => e.Id.Value).ToList());
            result.TotalCreatedSets = result.CreatedRebarIds.Count;
            result.TotalBarPieces = bbs.Sum(r => r.Qty);
            result.TotalWeightKg = Math.Round(bbs.Sum(r => r.TotalWeightKg), 2);
            result.Success = result.CreatedRebarIds.Count > 0 || result.FailedItems.Count == 0;
            result.Message = $"Berhasil generate {result.TotalCreatedSets} set rebar ({result.TotalBarPieces} batang, {result.TotalWeightKg:N2} kg) pada {selectedElements.Count} host terpilih.";
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            if (tg.HasStarted()) tg.RollBack();
            result.Success = false;
            result.Message = $"Gagal generate rebar: {ex.Message}";
            result.FailedItems.Add(ex.ToString());
        }

        return result;
    }

    public static List<RebarBrowserNodeDto> ScanReinforcedHosts(Document? doc)
    {
        var nodes = new List<RebarBrowserNodeDto>();
        if (doc == null || !doc.IsValidObject) return nodes;

        var rebars = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>().ToList();
        var byHost = rebars.GroupBy(r => r.GetHostId());

        foreach (var grp in byHost)
        {
            if (grp.Key == ElementId.InvalidElementId) continue;
            Element? host = doc.GetElement(grp.Key);
            if (host == null) continue;

            var cat = HostGeometryExtractor.ClassifyElement(host);
            if (cat == StructuralHostCategory.Unknown) continue;

            string curHash = RebarIdempotencyManager.ComputeHostGeometryHash(host);
            bool modified = grp.Any(r =>
            {
                string? h = RebarIdempotencyManager.TryGetStringParam(r, RebarIdempotencyManager.ParamHostHash);
                return !string.IsNullOrEmpty(h) && !string.Equals(h, curHash, StringComparison.Ordinal);
            });

            int pieces = grp.Sum(r => Math.Max(1, r.Quantity));
            double wt = 0;
            var marks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in grp)
            {
                string? m = RebarIdempotencyManager.TryGetStringParam(r, RebarIdempotencyManager.ParamMark)
                    ?? r.get_Parameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK)?.AsString();
                if (!string.IsNullOrEmpty(m)) marks.Add(m);

                RebarBarType? bt = doc.GetElement(r.GetTypeId()) as RebarBarType;
                int dia = bt != null ? Math.Max(8, (int)Math.Round(RebarResourceResolver.FeetToMm(bt.BarNominalDiameter))) : 16;
                IList<Curve> cs = r.GetCenterlineCurves(false, false, true, MultiplanarOption.IncludeAllMultiplanarCurves, 0);
                double mLen = cs.Sum(c => RebarResourceResolver.FeetToMm(c.Length)) / 1000.0;
                wt += mLen * Math.Max(1, r.Quantity) * (dia * dia * 0.0061654);
            }

            nodes.Add(new RebarBrowserNodeDto
            {
                LevelName = HostGeometryExtractor.GetLevelName(doc, host),
                HostElementId = host.Id.Value,
                HostName = $"{host.Name} (#{host.Id.Value})",
                Category = cat,
                RebarTag = RebarIdempotencyManager.BuildHostTag(host.Id.Value),
                MarkSummary = marks.Count > 0 ? string.Join(", ", marks.OrderBy(x => x).Take(4)) : $"{grp.Count()} sets",
                BarCount = pieces,
                WeightKg = Math.Round(wt, 2),
                IsHostModified = modified
            });
        }

        return nodes.OrderBy(n => n.LevelName).ThenBy(n => n.Category).ThenBy(n => n.HostElementId).ToList();
    }
}

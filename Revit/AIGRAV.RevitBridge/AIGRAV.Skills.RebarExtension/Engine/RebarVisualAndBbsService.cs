using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AIGRAV.Skills.RebarExtension.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace AIGRAV.Skills.RebarExtension.Engine;

/// <summary>
/// Handles 3D Solid/Unobscured View visibility (via Fine detail level + SetUnobscuredInView in Revit 2026),
/// diameter color ParameterFilterElements, Auto-Tagging, Native Revit BBS ViewSchedule creation,
/// and CSV / BVBS ASCII P21 export.
/// </summary>
public static class RebarVisualAndBbsService
{
    public static void Apply3DSolidAndUnobscured(
        Document doc,
        View activeView,
        IReadOnlyCollection<long> rebarIds,
        bool setFine3D,
        bool applyColorFilters,
        List<string> warnings)
    {
        if (activeView == null || activeView.IsTemplate) return;

        try
        {
            if (setFine3D && activeView.DetailLevel != ViewDetailLevel.Fine && activeView.ViewTemplateId == ElementId.InvalidElementId)
            {
                activeView.DetailLevel = ViewDetailLevel.Fine;
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not switch active view detail level to Fine: {ex.Message}");
        }

        foreach (long id in rebarIds)
        {
            if (doc.GetElement(new ElementId(id)) is not Rebar rebar) continue;
            try
            {
                rebar.SetUnobscuredInView(activeView, true);
            }
            catch (Exception ex)
            {
                warnings.Add($"Unobscured view warning on rebar #{id}: {ex.Message}");
            }
        }

        if (applyColorFilters)
        {
            try
            {
                EnsureDiameterColorOverrides(doc, activeView, rebarIds);
            }
            catch (Exception ex)
            {
                warnings.Add($"Color-by-diameter override warning: {ex.Message}");
            }
        }
    }

    private static void EnsureDiameterColorOverrides(Document doc, View view, IReadOnlyCollection<long> rebarIds)
    {
        // Apply per-element projection & cut color cleanly on active view (instant & template-independent!)
        foreach (long id in rebarIds)
        {
            if (doc.GetElement(new ElementId(id)) is not Rebar r) continue;
            RebarBarType? bt = doc.GetElement(r.GetTypeId()) as RebarBarType;
            int diaMm = bt != null ? Math.Max(8, (int)Math.Round(RebarResourceResolver.FeetToMm(bt.BarNominalDiameter))) : 16;
            var (red, green, blue, _) = RebarResourceResolver.GetColorForDiameter(diaMm);

            Color col = new(red, green, blue);
            var ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(col);
            ogs.SetCutLineColor(col);
            ogs.SetSurfaceForegroundPatternColor(col);
            view.SetElementOverrides(r.Id, ogs);
        }
    }

    public static int AutoTagRebarsInView(Document doc, View view, IReadOnlyCollection<long> rebarIds, List<string> warnings)
    {
        if (view is View3D v3d && !v3d.IsLocked)
        {
            warnings.Add("Auto-Tag dilewati: View 3D tidak di-lock (Orientasi 3D harus di-lock atau aktifkan Plan/Section view terlebih dulu).");
            return 0;
        }

        FamilySymbol? tagType = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_RebarTags)
            .WhereElementIsElementType()
            .Cast<FamilySymbol>()
            .FirstOrDefault();

        if (tagType == null)
        {
            warnings.Add("Tidak ditemukan Structural Rebar Tag Family di proyek; muat Structural Rebar Tag untuk Auto-Tag.");
            return 0;
        }
        if (!tagType.IsActive) tagType.Activate();

        int tagged = 0;
        foreach (long id in rebarIds)
        {
            if (doc.GetElement(new ElementId(id)) is not Rebar r) continue;
            try
            {
                IList<Curve> curves = r.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
                if (curves.Count == 0) continue;
                XYZ mid = curves[curves.Count / 2].Evaluate(0.5, true);
                IndependentTag.Create(doc, tagType.Id, view.Id, new Reference(r), addLeader: true, TagOrientation.Horizontal, mid);
                tagged++;
            }
            catch
            {
                // Rebar not visible in current cut plane view; skip without throwing
            }
        }
        return tagged;
    }

    public static ElementId? CreateOrUpdateNativeBbsSchedule(Document doc, string scheduleName = "AIGRAV - Rebar Bending Schedule (BBS)", List<string>? warnings = null)
    {
        try
        {
            ViewSchedule? existing = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(s => s.Name.Equals(scheduleName, StringComparison.OrdinalIgnoreCase));

            if (existing != null) return existing.Id;

            ViewSchedule vs = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Rebar));
            vs.Name = scheduleName;

            ScheduleDefinition def = vs.Definition;
            def.IsItemized = false;
            def.ShowGrandTotal = true;
            def.ShowGrandTotalTitle = true;
            def.GrandTotalTitle = "TOTAL TULANGAN";
            def.ShowGrandTotalCount = true;

            // Match SOFiSTiK + AIGRAV QTO rule: put Lokasi first if present in project parameters
            var available = def.GetSchedulableFields();
            string[] desiredOrder = {
                "Lokasi",
                "AIGRAV_Mark",
                "Schedule Mark",
                "Type",
                "Bar Diameter",
                "Shape",
                "Quantity",
                "Bar Length",
                "Total Bar Length",
                "Comments"
            };

            foreach (string colName in desiredOrder)
            {
                SchedulableField? sf = available.FirstOrDefault(f => f.GetName(doc).Equals(colName, StringComparison.OrdinalIgnoreCase));
                if (sf != null)
                {
                    ScheduleField added = def.AddField(sf);
                    if (colName.Equals("Quantity", StringComparison.OrdinalIgnoreCase) ||
                        colName.Equals("Total Bar Length", StringComparison.OrdinalIgnoreCase))
                    {
                        added.DisplayType = ScheduleFieldDisplayType.Totals;
                    }
                    if (colName.Equals("Schedule Mark", StringComparison.OrdinalIgnoreCase) ||
                        colName.Equals("AIGRAV_Mark", StringComparison.OrdinalIgnoreCase))
                    {
                        def.AddSortGroupField(new ScheduleSortGroupField(added.FieldId, ScheduleSortOrder.Ascending));
                    }
                }
            }

            return vs.Id;
        }
        catch (Exception ex)
        {
            warnings?.Add($"CreateOrUpdateNativeBbsSchedule warning: {ex.Message}");
            return null;
        }
    }

    public static string ExportBbsToCsv(IReadOnlyList<BbsPreviewRowDto> rows, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var sb = new StringBuilder();
        sb.AppendLine("Lokasi,Mark,Host,Category,Role,ShapeCode,Diameter_mm,CutLength_mm,Qty,TotalLength_m,UnitWeight_kg_m,TotalWeight_kg,Notes");
        foreach (var r in rows)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",{6},{7:F0},{8},{9:F2},{10:F3},{11:F2},\"{12}\"{13}",
                r.Lokasi, r.Mark, r.HostTag, r.HostCategory, r.Role, r.ShapeCode,
                r.DiameterMm, r.CutLengthMm, r.Qty, r.TotalLengthM, r.UnitWeightKgPerM, r.TotalWeightKg,
                r.Notes.Replace("\"", "'"), Environment.NewLine);
        }
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    /// <summary>
    /// Exports shop bending ASCII P21 / BVBS-compatible machine cutting list (@... format).
    /// </summary>
    public static string ExportBvbsAscii(IReadOnlyList<BbsPreviewRowDto> rows, string steelGrade, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var sb = new StringBuilder();
        int line = 1;
        foreach (var r in rows)
        {
            // Standard BF2D BVBS record header
            double wt = Math.Round(r.TotalWeightKg, 2);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "BF2D@Hj{0:D4}@p{1}@l{2:F0}@n{3}@e{4:F2}@d{5}@g{6}@s{7}@Gl{2:F0}@w0@C0@{8}",
                line++, r.Mark, r.CutLengthMm, r.Qty, wt, r.DiameterMm, steelGrade.Replace("@", ""), r.ShapeCode, Environment.NewLine);
        }
        File.WriteAllText(filePath, sb.ToString(), Encoding.ASCII);
        return filePath;
    }
}

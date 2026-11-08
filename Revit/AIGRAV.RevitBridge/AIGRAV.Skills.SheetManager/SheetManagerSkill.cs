using System.Globalization;
using System.Text;
using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.SheetManager;

public sealed class SheetManagerSkill : IAIGRAVSkill
{
    private const double FtToMmFactor = 304.8;

    public AIGRAVSkillInfo Info { get; } = new()
    {
        Name = "Sheet Manager",
        Version = "2.0",
        Category = "Documentation",
        Description = "DED 2D Paper Space Intelligence: Manages sheets, titleblock bounds, collision-free viewport layout, and relative sheet anchoring.",
        Source = "External",
        Priority = 1600
    };

    public static double MmToFt(double mm) => mm / FtToMmFactor;
    public static double FtToMm(double ft) => Math.Round(ft * FtToMmFactor, 2);

    public bool CanHandle(string toolName)
    {
        return toolName.Equals("aigrav_inspect_views", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_place_viewport", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_setup_sheets", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_inspect_viewports", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_adjust_viewports", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_get_revision_clouds", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_delete_revision_clouds", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("delete_revision_clouds", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("get_sheet_paper_space", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_get_sheet_paper_space", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("place_view_on_sheet", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_place_view_on_sheet", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("place_next_to_viewport_on_sheet", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("aigrav_place_next_to_viewport_on_sheet", StringComparison.OrdinalIgnoreCase);
    }

    public string Execute(string toolName, UIApplication app, string? argument = null)
    {
        Document? doc = app?.ActiveUIDocument?.Document;
        if (doc == null) return "Sheet Manager failed: No active Revit document.";

        try
        {
            if (toolName.Equals("get_sheet_paper_space", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("aigrav_get_sheet_paper_space", StringComparison.OrdinalIgnoreCase))
            {
                return GetSheetPaperSpace(doc, argument);
            }

            if (toolName.Equals("place_view_on_sheet", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("aigrav_place_view_on_sheet", StringComparison.OrdinalIgnoreCase))
            {
                return PlaceViewOnSheetSmart(doc, argument);
            }

            if (toolName.Equals("place_next_to_viewport_on_sheet", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("aigrav_place_next_to_viewport_on_sheet", StringComparison.OrdinalIgnoreCase))
            {
                return PlaceNextToViewportOnSheet(doc, argument);
            }

            if (toolName.Equals("aigrav_inspect_views", StringComparison.OrdinalIgnoreCase))
            {
                return InspectViews(doc);
            }

            if (toolName.Equals("aigrav_place_viewport", StringComparison.OrdinalIgnoreCase))
            {
                return PlaceViewport(doc, argument);
            }

            if (toolName.Equals("aigrav_setup_sheets", StringComparison.OrdinalIgnoreCase))
            {
                return SetupSheets(doc, argument);
            }

            if (toolName.Equals("aigrav_inspect_viewports", StringComparison.OrdinalIgnoreCase))
            {
                return InspectViewports(doc, argument);
            }

            if (toolName.Equals("aigrav_adjust_viewports", StringComparison.OrdinalIgnoreCase))
            {
                return AdjustViewports(doc, argument);
            }

            if (toolName.Equals("aigrav_get_revision_clouds", StringComparison.OrdinalIgnoreCase))
            {
                return GetRevisionClouds(doc);
            }

            if (toolName.Equals("aigrav_delete_revision_clouds", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("delete_revision_clouds", StringComparison.OrdinalIgnoreCase))
            {
                return DeleteRevisionClouds(doc);
            }

            return $"Unknown tool: {toolName}";
        }
        catch (Exception ex)
        {
            return $"Sheet Manager execution error: {ex}";
        }
    }

    // =========================================================================
    // PILLAR 4: 2D PAPER SPACE INTELLIGENCE & COLLISION-FREE VIEWPORT ENGINE
    // =========================================================================

    private sealed class PaperRectMm
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double Width => Math.Round(MaxX - MinX, 2);
        public double Height => Math.Round(MaxY - MinY, 2);
        public double CenterX => Math.Round((MinX + MaxX) * 0.5, 2);
        public double CenterY => Math.Round((MinY + MaxY) * 0.5, 2);

        public bool Intersects(PaperRectMm other, double paddingMm = 5.0)
        {
            return !(MaxX + paddingMm <= other.MinX ||
                     MinX - paddingMm >= other.MaxX ||
                     MaxY + paddingMm <= other.MinY ||
                     MinY - paddingMm >= other.MaxY);
        }

        public bool Contains(PaperRectMm inner)
        {
            return inner.MinX >= MinX && inner.MaxX <= MaxX &&
                   inner.MinY >= MinY && inner.MaxY <= MaxY;
        }
    }

    private static ViewSheet? ResolveSheet(Document doc, JsonElement root, string? rawArg)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("sheet_id", out JsonElement sid) && sid.ValueKind == JsonValueKind.Number)
                return doc.GetElement(new ElementId(sid.GetInt64())) as ViewSheet;
            if (root.TryGetProperty("sheetId", out JsonElement sid2) && sid2.ValueKind == JsonValueKind.Number)
                return doc.GetElement(new ElementId(sid2.GetInt64())) as ViewSheet;
            if (root.TryGetProperty("sheet_number", out JsonElement snum) && snum.ValueKind == JsonValueKind.String)
            {
                string num = snum.GetString() ?? "";
                return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                    .FirstOrDefault(s => s.SheetNumber.Equals(num, StringComparison.OrdinalIgnoreCase));
            }
            if (root.TryGetProperty("sheetNumber", out JsonElement snum2) && snum2.ValueKind == JsonValueKind.String)
            {
                string num = snum2.GetString() ?? "";
                return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                    .FirstOrDefault(s => s.SheetNumber.Equals(num, StringComparison.OrdinalIgnoreCase));
            }
        }
        else if (!string.IsNullOrWhiteSpace(rawArg))
        {
            if (long.TryParse(rawArg.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long idVal))
                return doc.GetElement(new ElementId(idVal)) as ViewSheet;

            var byNum = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .FirstOrDefault(s => s.SheetNumber.Equals(rawArg.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byNum != null) return byNum;
        }

        if (doc.ActiveView is ViewSheet activeSheet)
            return activeSheet;

        return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().FirstOrDefault();
    }

    private static View? ResolveView(Document doc, JsonElement root, string propId = "view_id", string propName = "view_name")
    {
        if (root.ValueKind != JsonValueKind.Object) return null;

        if (root.TryGetProperty(propId, out JsonElement vid) && vid.ValueKind == JsonValueKind.Number)
            return doc.GetElement(new ElementId(vid.GetInt64())) as View;
        if (root.TryGetProperty("viewId", out JsonElement vid2) && vid2.ValueKind == JsonValueKind.Number)
            return doc.GetElement(new ElementId(vid2.GetInt64())) as View;

        string? vName = null;
        if (root.TryGetProperty(propName, out JsonElement vn) && vn.ValueKind == JsonValueKind.String)
            vName = vn.GetString();
        else if (root.TryGetProperty("viewName", out JsonElement vn2) && vn2.ValueKind == JsonValueKind.String)
            vName = vn2.GetString();

        if (!string.IsNullOrWhiteSpace(vName))
        {
            return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name.Equals(vName, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static (PaperRectMm tbRect, PaperRectMm drawableRect, string paperSizeName, string tbName, long tbId) ExtractTitleBlockGeometry(Document doc, ViewSheet sheet)
    {
        Element? tb = new FilteredElementCollector(doc, sheet.Id)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsNotElementType()
            .FirstOrDefault();

        PaperRectMm tbRect;
        string tbName = "Default A1 Titleblock";
        long tbId = -1;

        if (tb != null)
        {
            tbId = tb.Id.Value;
            Element? tbType = doc.GetElement(tb.GetTypeId());
            tbName = tbType != null ? $"{tb.Name} ({tbType.Name})" : tb.Name;

            BoundingBoxXYZ? bb = tb.get_BoundingBox(sheet) ?? tb.get_BoundingBox(null);
            if (bb != null)
            {
                tbRect = new PaperRectMm
                {
                    MinX = FtToMm(bb.Min.X),
                    MinY = FtToMm(bb.Min.Y),
                    MaxX = FtToMm(bb.Max.X),
                    MaxY = FtToMm(bb.Max.Y)
                };
            }
            else
            {
                tbRect = new PaperRectMm { MinX = 0, MinY = 0, MaxX = 841, MaxY = 594 };
            }
        }
        else
        {
            tbRect = new PaperRectMm { MinX = 0, MinY = 0, MaxX = 841, MaxY = 594 };
        }

        double w = tbRect.Width;
        double h = tbRect.Height;
        string paperSize = ClassifyIsoPaperSize(w, h);

        // Reserve border margin (15mm left/top/bottom) and right-hand Titleblock legend strip (~14% of width)
        double leftMargin = Math.Max(15.0, Math.Round(w * 0.025, 1));
        double topMargin = Math.Max(12.0, Math.Round(h * 0.025, 1));
        double bottomMargin = Math.Max(12.0, Math.Round(h * 0.025, 1));
        double rightStampStrip = Math.Max(65.0, Math.Round(w * 0.14, 1));

        PaperRectMm drawable = new()
        {
            MinX = Math.Round(tbRect.MinX + leftMargin, 2),
            MinY = Math.Round(tbRect.MinY + bottomMargin, 2),
            MaxX = Math.Round(tbRect.MaxX - rightStampStrip, 2),
            MaxY = Math.Round(tbRect.MaxY - topMargin, 2)
        };

        return (tbRect, drawable, paperSize, tbName, tbId);
    }

    private static string ClassifyIsoPaperSize(double widthMm, double heightMm)
    {
        double maxDim = Math.Max(widthMm, heightMm);
        double minDim = Math.Min(widthMm, heightMm);

        if (Math.Abs(maxDim - 1189) < 40 && Math.Abs(minDim - 841) < 40) return "A0 (1189 x 841 mm)";
        if (Math.Abs(maxDim - 841) < 40 && Math.Abs(minDim - 594) < 40) return "A1 (841 x 594 mm)";
        if (Math.Abs(maxDim - 594) < 35 && Math.Abs(minDim - 420) < 35) return "A2 (594 x 420 mm)";
        if (Math.Abs(maxDim - 420) < 30 && Math.Abs(minDim - 297) < 30) return "A3 (420 x 297 mm)";
        if (Math.Abs(maxDim - 297) < 25 && Math.Abs(minDim - 210) < 25) return "A4 (297 x 210 mm)";
        return $"Custom ({widthMm:F0} x {heightMm:F0} mm)";
    }

    private static string BuildSheetAsciiMap(PaperRectMm tbRect, PaperRectMm drawableRect, List<(long vpId, string viewName, PaperRectMm box)> viewports, long highlightVpId = -1)
    {
        const int cols = 48;
        const int rows = 20;
        char[,] canvas = new char[rows, cols];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                canvas[r, c] = ' ';

        // Draw outer titleblock border
        for (int c = 0; c < cols; c++) { canvas[0, c] = '-'; canvas[rows - 1, c] = '-'; }
        for (int r = 0; r < rows; r++) { canvas[r, 0] = '|'; canvas[r, cols - 1] = '|'; }

        double spanX = Math.Max(1.0, tbRect.Width);
        double spanY = Math.Max(1.0, tbRect.Height);

        int MapX(double xMm) => Math.Clamp((int)Math.Round(((xMm - tbRect.MinX) / spanX) * (cols - 2)) + 1, 1, cols - 2);
        int MapY(double yMm) => Math.Clamp(rows - 2 - (int)Math.Round(((yMm - tbRect.MinY) / spanY) * (rows - 2)), 1, rows - 2);

        // Draw right-side titleblock stamp separator
        int stampX = MapX(drawableRect.MaxX);
        for (int r = 1; r < rows - 1; r++)
        {
            if (stampX > 1 && stampX < cols - 1)
                canvas[r, stampX] = ':';
        }

        // Draw each viewport box
        int idx = 1;
        foreach (var (vpId, _, box) in viewports)
        {
            int c1 = MapX(box.MinX);
            int c2 = MapX(box.MaxX);
            int r1 = MapY(box.MaxY);
            int r2 = MapY(box.MinY);

            char fill = vpId == highlightVpId ? '#' : (char)('0' + (idx % 10));
            for (int r = Math.Min(r1, r2); r <= Math.Max(r1, r2); r++)
            {
                for (int c = Math.Min(c1, c2); c <= Math.Max(c1, c2); c++)
                {
                    canvas[r, c] = fill;
                }
            }
            idx++;
        }

        StringBuilder sb = new();
        sb.AppendLine($"Paper Space Map [{tbRect.Width:F0}x{tbRect.Height:F0} mm] (':' = Titleblock Stamp Strip, '#' = Active/New Viewport):");
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++) sb.Append(canvas[r, c]);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string GetSheetPaperSpace(Document doc, string? argument)
    {
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(argument) && argument.TrimStart().StartsWith("{"))
        {
            using JsonDocument jd = JsonDocument.Parse(argument);
            root = jd.RootElement.Clone();
        }

        ViewSheet? sheet = ResolveSheet(doc, root, argument);
        if (sheet == null)
            return JsonSerializer.Serialize(new { error = "Target ViewSheet not found in document." });

        var (tbRect, drawableRect, paperSize, tbName, tbId) = ExtractTitleBlockGeometry(doc, sheet);

        ICollection<ElementId> vpIds = sheet.GetAllViewports();
        List<object> vpTelemetry = new();
        List<(long vpId, string viewName, PaperRectMm box)> vpBoxes = new();

        foreach (ElementId vpId in vpIds)
        {
            if (doc.GetElement(vpId) is not Viewport vp) continue;
            View? v = doc.GetElement(vp.ViewId) as View;
            Outline box = vp.GetBoxOutline();
            Outline lbl = vp.GetLabelOutline();
            XYZ center = vp.GetBoxCenter();

            PaperRectMm rectMm = new()
            {
                MinX = FtToMm(box.MinimumPoint.X),
                MinY = FtToMm(box.MinimumPoint.Y),
                MaxX = FtToMm(box.MaximumPoint.X),
                MaxY = FtToMm(box.MaximumPoint.Y)
            };
            vpBoxes.Add((vp.Id.Value, v?.Name ?? "View", rectMm));

            vpTelemetry.Add(new
            {
                viewport_id = vp.Id.Value,
                view_id = vp.ViewId.Value,
                view_name = v?.Name ?? "Unknown",
                view_type = v?.ViewType.ToString() ?? "Unknown",
                scale = v?.Scale ?? 1,
                center_mm = new { x = FtToMm(center.X), y = FtToMm(center.Y) },
                box_outline_mm = new
                {
                    min = new { x = rectMm.MinX, y = rectMm.MinY },
                    max = new { x = rectMm.MaxX, y = rectMm.MaxY },
                    width_mm = rectMm.Width,
                    height_mm = rectMm.Height
                },
                label_outline_mm = new
                {
                    min = new { x = FtToMm(lbl.MinimumPoint.X), y = FtToMm(lbl.MinimumPoint.Y) },
                    max = new { x = FtToMm(lbl.MaximumPoint.X), y = FtToMm(lbl.MaximumPoint.Y) }
                }
            });
        }

        // Check if any existing viewports overlap each other
        List<string> overlaps = new();
        for (int i = 0; i < vpBoxes.Count; i++)
        {
            for (int j = i + 1; j < vpBoxes.Count; j++)
            {
                if (vpBoxes[i].box.Intersects(vpBoxes[j].box, 0.0))
                {
                    overlaps.Add($"COLLISION: Viewport {vpBoxes[i].vpId} ('{vpBoxes[i].viewName}') overlaps Viewport {vpBoxes[j].vpId} ('{vpBoxes[j].viewName}')");
                }
            }
        }

        var payload = new
        {
            sheet_id = sheet.Id.Value,
            sheet_number = sheet.SheetNumber,
            sheet_name = sheet.Name,
            titleblock = new
            {
                element_id = tbId,
                name = tbName,
                paper_size_iso = paperSize,
                origin_anchor_mm = new { x = tbRect.MinX, y = tbRect.MinY },
                bounds_mm = new
                {
                    min = new { x = tbRect.MinX, y = tbRect.MinY },
                    max = new { x = tbRect.MaxX, y = tbRect.MaxY },
                    width_mm = tbRect.Width,
                    height_mm = tbRect.Height
                },
                drawable_canvas_mm = new
                {
                    min = new { x = drawableRect.MinX, y = drawableRect.MinY },
                    max = new { x = drawableRect.MaxX, y = drawableRect.MaxY },
                    width_mm = drawableRect.Width,
                    height_mm = drawableRect.Height,
                    center = new { x = drawableRect.CenterX, y = drawableRect.CenterY }
                }
            },
            existing_viewports_count = vpTelemetry.Count,
            existing_viewports = vpTelemetry,
            paper_space_collisions = overlaps,
            ascii_paper_map = BuildSheetAsciiMap(tbRect, drawableRect, vpBoxes)
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string PlaceViewOnSheetSmart(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { error = "Argument JSON required: { sheet_id, view_id, layout_rule }" });

        using JsonDocument jd = JsonDocument.Parse(argument);
        JsonElement root = jd.RootElement;

        ViewSheet? sheet = ResolveSheet(doc, root, null);
        if (sheet == null)
            return JsonSerializer.Serialize(new { error = "Sheet not found." });

        View? view = ResolveView(doc, root);
        if (view == null)
            return JsonSerializer.Serialize(new { error = "View not found." });

        string layoutRule = "Auto";
        if (root.TryGetProperty("layout_rule", out JsonElement lr) && lr.ValueKind == JsonValueKind.String)
            layoutRule = lr.GetString() ?? "Auto";
        else if (root.TryGetProperty("layoutRule", out JsonElement lr2) && lr2.ValueKind == JsonValueKind.String)
            layoutRule = lr2.GetString() ?? "Auto";

        double paddingMm = 12.0;
        if (root.TryGetProperty("padding_mm", out JsonElement pm) && pm.ValueKind == JsonValueKind.Number)
            paddingMm = pm.GetDouble();

        var (tbRect, drawableRect, paperSize, _, _) = ExtractTitleBlockGeometry(doc, sheet);

        // Gather existing viewports on this sheet (excluding target view if already on this sheet)
        List<(long vpId, string viewName, PaperRectMm box)> existingBoxes = new();
        Viewport? existingVpForThisView = null;

        foreach (ElementId id in sheet.GetAllViewports())
        {
            if (doc.GetElement(id) is not Viewport vpItem) continue;
            if (vpItem.ViewId == view.Id)
            {
                existingVpForThisView = vpItem;
                continue;
            }

            View? vItem = doc.GetElement(vpItem.ViewId) as View;
            Outline o = vpItem.GetBoxOutline();
            existingBoxes.Add((vpItem.Id.Value, vItem?.Name ?? "View", new PaperRectMm
            {
                MinX = FtToMm(o.MinimumPoint.X),
                MinY = FtToMm(o.MinimumPoint.Y),
                MaxX = FtToMm(o.MaximumPoint.X),
                MaxY = FtToMm(o.MaximumPoint.Y)
            }));
        }

        using Transaction tx = new(doc, "AIGRAV - Smart Place View on Sheet");
        tx.Start();

        Viewport targetVp;
        bool wasCreatedNew = false;

        if (existingVpForThisView != null)
        {
            targetVp = existingVpForThisView;
        }
        else
        {
            if (!Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
            {
                tx.RollBack();
                return JsonSerializer.Serialize(new
                {
                    error = $"View '{view.Name}' (Id: {view.Id.Value}) cannot be added to Sheet '{sheet.SheetNumber}'. It may already be placed on another sheet or is a schedule/template."
                });
            }

            XYZ initialCenter = new(MmToFt(drawableRect.CenterX), MmToFt(drawableRect.CenterY), 0);
            targetVp = Viewport.Create(doc, sheet.Id, view.Id, initialCenter);
            wasCreatedNew = true;
            doc.Regenerate();
        }

        // Measure the real 2D BoxOutline of the viewport on paper space
        Outline vpOutline = targetVp.GetBoxOutline();
        XYZ currentCenter = targetVp.GetBoxCenter();
        double vpWidthMm = FtToMm(vpOutline.MaximumPoint.X - vpOutline.MinimumPoint.X);
        double vpHeightMm = FtToMm(vpOutline.MaximumPoint.Y - vpOutline.MinimumPoint.Y);

        // Compute offset between BoxCenter and Geometric Center of BoxOutline
        double outlineCenterXmm = FtToMm((vpOutline.MinimumPoint.X + vpOutline.MaximumPoint.X) * 0.5);
        double outlineCenterYmm = FtToMm((vpOutline.MinimumPoint.Y + vpOutline.MaximumPoint.Y) * 0.5);
        double centerDeltaXmm = FtToMm(currentCenter.X) - outlineCenterXmm;
        double centerDeltaYmm = FtToMm(currentCenter.Y) - outlineCenterYmm;

        // Determine candidate geometric centers (in mm) based on layout_rule
        List<(double cx, double cy, string slotName)> candidates = GenerateLayoutCandidates(
            drawableRect, vpWidthMm, vpHeightMm, paddingMm, layoutRule, root, existingBoxes);

        double chosenCx = drawableRect.CenterX;
        double chosenCy = drawableRect.CenterY;
        string chosenSlot = "Center (Fallback)";
        bool collisionFree = false;

        foreach (var (cx, cy, slotName) in candidates)
        {
            PaperRectMm candidateRect = new()
            {
                MinX = cx - vpWidthMm * 0.5,
                MaxX = cx + vpWidthMm * 0.5,
                MinY = cy - vpHeightMm * 0.5,
                MaxY = cy + vpHeightMm * 0.5
            };

            bool collides = existingBoxes.Any(eb => candidateRect.Intersects(eb.box, paddingMm));
            if (!collides)
            {
                chosenCx = cx;
                chosenCy = cy;
                chosenSlot = slotName;
                collisionFree = true;
                break;
            }
        }

        // Apply final center position
        double finalSetCenterXmm = chosenCx + centerDeltaXmm;
        double finalSetCenterYmm = chosenCy + centerDeltaYmm;
        targetVp.SetBoxCenter(new XYZ(MmToFt(finalSetCenterXmm), MmToFt(finalSetCenterYmm), 0));
        doc.Regenerate();

        Outline finalOutline = targetVp.GetBoxOutline();
        PaperRectMm finalRectMm = new()
        {
            MinX = FtToMm(finalOutline.MinimumPoint.X),
            MinY = FtToMm(finalOutline.MinimumPoint.Y),
            MaxX = FtToMm(finalOutline.MaximumPoint.X),
            MaxY = FtToMm(finalOutline.MaximumPoint.Y)
        };

        tx.Commit();

        existingBoxes.Add((targetVp.Id.Value, view.Name, finalRectMm));

        var resultPayload = new
        {
            status = "Success",
            action = wasCreatedNew ? "CreatedAndPositioned" : "RepositionedExisting",
            sheet = new
            {
                sheet_id = sheet.Id.Value,
                sheet_number = sheet.SheetNumber,
                sheet_name = sheet.Name,
                paper_size_iso = paperSize
            },
            placed_viewport = new
            {
                viewport_id = targetVp.Id.Value,
                view_id = view.Id.Value,
                view_name = view.Name,
                scale = view.Scale,
                layout_rule_applied = chosenSlot,
                center_mm = new { x = Math.Round(finalSetCenterXmm, 2), y = Math.Round(finalSetCenterYmm, 2) },
                box_outline_mm = new
                {
                    min = new { x = finalRectMm.MinX, y = finalRectMm.MinY },
                    max = new { x = finalRectMm.MaxX, y = finalRectMm.MaxY },
                    width_mm = finalRectMm.Width,
                    height_mm = finalRectMm.Height
                }
            },
            spatial_paper_check = new
            {
                collision_free = collisionFree,
                fits_inside_drawable_canvas = drawableRect.Contains(finalRectMm),
                drawable_canvas_mm = new
                {
                    min = new { x = drawableRect.MinX, y = drawableRect.MinY },
                    max = new { x = drawableRect.MaxX, y = drawableRect.MaxY },
                    width_mm = drawableRect.Width,
                    height_mm = drawableRect.Height
                }
            },
            ascii_paper_map = BuildSheetAsciiMap(tbRect, drawableRect, existingBoxes, targetVp.Id.Value)
        };

        return JsonSerializer.Serialize(resultPayload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<(double cx, double cy, string slotName)> GenerateLayoutCandidates(
        PaperRectMm drawable,
        double vpW,
        double vpH,
        double pad,
        string rule,
        JsonElement root,
        List<(long vpId, string viewName, PaperRectMm box)> existing)
    {
        List<(double cx, double cy, string slotName)> list = new();

        double leftX = drawable.MinX + vpW * 0.5 + pad;
        double rightX = drawable.MaxX - vpW * 0.5 - pad;
        double bottomY = drawable.MinY + vpH * 0.5 + pad;
        double topY = drawable.MaxY - vpH * 0.5 - pad;
        double midX = drawable.CenterX;
        double midY = drawable.CenterY;

        // Clamp if viewport is larger than half margin
        if (leftX > rightX) leftX = rightX = midX;
        if (bottomY > topY) bottomY = topY = midY;

        string r = rule.Trim().ToLowerInvariant();

        if (r == "custommm" || (root.TryGetProperty("x_mm", out _) && root.TryGetProperty("y_mm", out _)))
        {
            double xMm = root.TryGetProperty("x_mm", out JsonElement xm) ? xm.GetDouble() : midX;
            double yMm = root.TryGetProperty("y_mm", out JsonElement ym) ? ym.GetDouble() : midY;
            list.Add((xMm, yMm, "CustomMM"));
        }
        else if (r == "topleft" || r == "top_left")
        {
            list.Add((leftX, topY, "TopLeft"));
        }
        else if (r == "topright" || r == "top_right")
        {
            list.Add((rightX, topY, "TopRight"));
        }
        else if (r == "bottomleft" || r == "bottom_left")
        {
            list.Add((leftX, bottomY, "BottomLeft"));
        }
        else if (r == "bottomright" || r == "bottom_right")
        {
            list.Add((rightX, bottomY, "BottomRight"));
        }
        else if (r == "center")
        {
            list.Add((midX, midY, "Center"));
        }
        else if (r == "lefthalf" || r == "left_half")
        {
            list.Add(((drawable.MinX + midX) * 0.5, midY, "LeftHalf"));
        }
        else if (r == "righthalf" || r == "right_half")
        {
            list.Add(((midX + drawable.MaxX) * 0.5, midY, "RightHalf"));
        }

        // Standard DED Grid search order: TopLeft -> BottomLeft -> TopRight -> BottomRight -> Center -> 2D Grid Scan
        list.Add((leftX, topY, "Auto:TopLeft"));
        list.Add((leftX, bottomY, "Auto:BottomLeft"));
        list.Add((rightX, topY, "Auto:TopRight"));
        list.Add((rightX, bottomY, "Auto:BottomRight"));
        list.Add((midX, midY, "Auto:Center"));

        // Relative to existing viewports (Right of / Below existing viewports)
        foreach (var eb in existing)
        {
            double rightOfExisting = eb.box.MaxX + pad + vpW * 0.5;
            if (rightOfExisting + vpW * 0.5 <= drawable.MaxX)
            {
                list.Add((rightOfExisting, eb.box.CenterY, $"Auto:RightOf_VP_{eb.vpId}"));
            }

            double belowExisting = eb.box.MinY - pad - vpH * 0.5;
            if (belowExisting - vpH * 0.5 >= drawable.MinY)
            {
                list.Add((eb.box.CenterX, belowExisting, $"Auto:Below_VP_{eb.vpId}"));
            }
        }

        // Fine 2D scan across the drawable canvas (top-to-bottom, left-to-right)
        const int stepsX = 6;
        const int stepsY = 5;
        for (int iy = stepsY; iy >= 0; iy--)
        {
            double ty = bottomY + (topY - bottomY) * (iy / (double)stepsY);
            for (int ix = 0; ix <= stepsX; ix++)
            {
                double tx = leftX + (rightX - leftX) * (ix / (double)stepsX);
                list.Add((tx, ty, $"GridScan({ix},{iy})"));
            }
        }

        return list;
    }

    private static string PlaceNextToViewportOnSheet(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return JsonSerializer.Serialize(new { error = "Argument JSON required: { sheet_id, view_id, reference_viewport_id, direction, gap_mm }" });

        using JsonDocument jd = JsonDocument.Parse(argument);
        JsonElement root = jd.RootElement;

        ViewSheet? sheet = ResolveSheet(doc, root, null);
        if (sheet == null)
            return JsonSerializer.Serialize(new { error = "Sheet not found." });

        View? view = ResolveView(doc, root);
        if (view == null)
            return JsonSerializer.Serialize(new { error = "Target View not found." });

        long refVpId = -1;
        if (root.TryGetProperty("reference_viewport_id", out JsonElement rv1) && rv1.ValueKind == JsonValueKind.Number)
            refVpId = rv1.GetInt64();
        else if (root.TryGetProperty("referenceViewportId", out JsonElement rv2) && rv2.ValueKind == JsonValueKind.Number)
            refVpId = rv2.GetInt64();

        Viewport? refVp = doc.GetElement(new ElementId(refVpId)) as Viewport;
        if (refVp == null)
        {
            // Fallback: pick the last placed viewport on the sheet
            ElementId? lastId = sheet.GetAllViewports().LastOrDefault(id => id != ElementId.InvalidElementId);
            if (lastId != null) refVp = doc.GetElement(lastId) as Viewport;
        }

        if (refVp == null)
        {
            // No reference viewport exists yet; delegate to PlaceViewOnSheetSmart with TopLeft
            return PlaceViewOnSheetSmart(doc, argument);
        }

        string direction = "right";
        if (root.TryGetProperty("direction", out JsonElement dirEl) && dirEl.ValueKind == JsonValueKind.String)
            direction = (dirEl.GetString() ?? "right").Trim().ToLowerInvariant();

        double gapMm = 15.0;
        if (root.TryGetProperty("gap_mm", out JsonElement gapEl) && gapEl.ValueKind == JsonValueKind.Number)
            gapMm = gapEl.GetDouble();

        Outline refOutline = refVp.GetBoxOutline();
        PaperRectMm refRect = new()
        {
            MinX = FtToMm(refOutline.MinimumPoint.X),
            MinY = FtToMm(refOutline.MinimumPoint.Y),
            MaxX = FtToMm(refOutline.MaximumPoint.X),
            MaxY = FtToMm(refOutline.MaximumPoint.Y)
        };

        var (tbRect, drawableRect, paperSize, _, _) = ExtractTitleBlockGeometry(doc, sheet);

        // Find or create viewport for the new view
        Viewport? targetVp = sheet.GetAllViewports()
            .Select(id => doc.GetElement(id) as Viewport)
            .FirstOrDefault(vp => vp != null && vp.ViewId == view.Id);

        using Transaction tx = new(doc, "AIGRAV - Place Next To Viewport");
        tx.Start();

        if (targetVp == null)
        {
            if (!Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
            {
                tx.RollBack();
                return JsonSerializer.Serialize(new { error = $"View '{view.Name}' cannot be added to sheet '{sheet.SheetNumber}'." });
            }
            targetVp = Viewport.Create(doc, sheet.Id, view.Id, new XYZ(MmToFt(drawableRect.CenterX), MmToFt(drawableRect.CenterY), 0));
            doc.Regenerate();
        }

        Outline newOutline = targetVp.GetBoxOutline();
        XYZ curCenter = targetVp.GetBoxCenter();
        double vpW = FtToMm(newOutline.MaximumPoint.X - newOutline.MinimumPoint.X);
        double vpH = FtToMm(newOutline.MaximumPoint.Y - newOutline.MinimumPoint.Y);

        double outlineCx = FtToMm((newOutline.MinimumPoint.X + newOutline.MaximumPoint.X) * 0.5);
        double outlineCy = FtToMm((newOutline.MinimumPoint.Y + newOutline.MaximumPoint.Y) * 0.5);
        double deltaX = FtToMm(curCenter.X) - outlineCx;
        double deltaY = FtToMm(curCenter.Y) - outlineCy;

        double targetGeomCx = refRect.CenterX;
        double targetGeomCy = refRect.CenterY;

        switch (direction)
        {
            case "left":
                targetGeomCx = refRect.MinX - gapMm - vpW * 0.5;
                targetGeomCy = refRect.CenterY;
                break;
            case "below":
            case "bottom":
            case "down":
                targetGeomCx = refRect.CenterX;
                targetGeomCy = refRect.MinY - gapMm - vpH * 0.5;
                break;
            case "above":
            case "top":
            case "up":
                targetGeomCx = refRect.CenterX;
                targetGeomCy = refRect.MaxY + gapMm + vpH * 0.5;
                break;
            default: // "right"
                targetGeomCx = refRect.MaxX + gapMm + vpW * 0.5;
                targetGeomCy = refRect.CenterY;
                break;
        }

        double setX = targetGeomCx + deltaX;
        double setY = targetGeomCy + deltaY;
        targetVp.SetBoxCenter(new XYZ(MmToFt(setX), MmToFt(setY), 0));
        doc.Regenerate();

        Outline finalBox = targetVp.GetBoxOutline();
        PaperRectMm finalRect = new()
        {
            MinX = FtToMm(finalBox.MinimumPoint.X),
            MinY = FtToMm(finalBox.MinimumPoint.Y),
            MaxX = FtToMm(finalBox.MaximumPoint.X),
            MaxY = FtToMm(finalBox.MaximumPoint.Y)
        };

        tx.Commit();

        List<(long vpId, string viewName, PaperRectMm box)> allVps = new();
        foreach (ElementId id in sheet.GetAllViewports())
        {
            if (doc.GetElement(id) is not Viewport vpi) continue;
            Outline bo = vpi.GetBoxOutline();
            View? vv = doc.GetElement(vpi.ViewId) as View;
            allVps.Add((vpi.Id.Value, vv?.Name ?? "View", new PaperRectMm
            {
                MinX = FtToMm(bo.MinimumPoint.X),
                MinY = FtToMm(bo.MinimumPoint.Y),
                MaxX = FtToMm(bo.MaximumPoint.X),
                MaxY = FtToMm(bo.MaximumPoint.Y)
            }));
        }

        var response = new
        {
            status = "Success",
            relative_anchor = new
            {
                reference_viewport_id = refVp.Id.Value,
                direction,
                gap_mm = gapMm
            },
            placed_viewport = new
            {
                viewport_id = targetVp.Id.Value,
                view_id = view.Id.Value,
                view_name = view.Name,
                center_mm = new { x = Math.Round(setX, 2), y = Math.Round(setY, 2) },
                box_outline_mm = new
                {
                    min = new { x = finalRect.MinX, y = finalRect.MinY },
                    max = new { x = finalRect.MaxX, y = finalRect.MaxY },
                    width_mm = finalRect.Width,
                    height_mm = finalRect.Height
                }
            },
            fits_inside_drawable_canvas = drawableRect.Contains(finalRect),
            paper_size_iso = paperSize,
            ascii_paper_map = BuildSheetAsciiMap(tbRect, drawableRect, allVps, targetVp.Id.Value)
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    // =========================================================================
    // LEGACY TOOLS (100% BACKWARD COMPATIBLE)
    // =========================================================================

    private static string InspectViews(Document doc)
    {
        List<View> views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .OrderBy(v => v.ViewType.ToString())
            .ThenBy(v => v.Name)
            .ToList();

        var result = views.Select(v => new
        {
            Id = v.Id.Value,
            Name = v.Name,
            Type = v.ViewType.ToString(),
            Scale = v.Scale
        });

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string PlaceViewport(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return "PlaceViewport failed: Argument required.";

        using JsonDocument jdoc = JsonDocument.Parse(argument);
        JsonElement root = jdoc.RootElement;

        long sheetIdVal = root.TryGetProperty("sheetId", out JsonElement s1) ? s1.GetInt64() : root.GetProperty("sheet_id").GetInt64();
        long viewIdVal = root.TryGetProperty("viewId", out JsonElement v1) ? v1.GetInt64() : root.GetProperty("view_id").GetInt64();

        // Support both legacy feet (x, y) and millimeters (x_mm, y_mm)
        double xFt = root.TryGetProperty("x_mm", out JsonElement xmm) ? MmToFt(xmm.GetDouble()) : root.GetProperty("x").GetDouble();
        double yFt = root.TryGetProperty("y_mm", out JsonElement ymm) ? MmToFt(ymm.GetDouble()) : root.GetProperty("y").GetDouble();

        ElementId sheetId = new(sheetIdVal);
        ElementId viewId = new(viewIdVal);

        using Transaction tx = new(doc, "AIGRAV - Place Viewport");
        tx.Start();

        if (!Viewport.CanAddViewToSheet(doc, sheetId, viewId))
        {
            tx.RollBack();
            return $"Cannot add view {viewIdVal} to sheet {sheetIdVal}. (View might already be placed on a sheet)";
        }

        XYZ center = new(xFt, yFt, 0);
        Viewport vp = Viewport.Create(doc, sheetId, viewId, center);
        doc.Regenerate();
        Outline box = vp.GetBoxOutline();

        tx.Commit();

        return JsonSerializer.Serialize(new
        {
            status = "Success",
            viewport_id = vp.Id.Value,
            sheet_id = sheetIdVal,
            view_id = viewIdVal,
            center_mm = new { x = FtToMm(xFt), y = FtToMm(yFt) },
            box_outline_mm = new
            {
                min = new { x = FtToMm(box.MinimumPoint.X), y = FtToMm(box.MinimumPoint.Y) },
                max = new { x = FtToMm(box.MaximumPoint.X), y = FtToMm(box.MaximumPoint.Y) },
                width_mm = FtToMm(box.MaximumPoint.X - box.MinimumPoint.X),
                height_mm = FtToMm(box.MaximumPoint.Y - box.MinimumPoint.Y)
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string SetupSheets(Document doc, string? argument)
    {
        Element? tbType = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .FirstOrDefault(e => e.Name.Contains("KOP GAMBAR A3", StringComparison.OrdinalIgnoreCase));

        ElementId tbId = tbType != null ? tbType.Id : ElementId.InvalidElementId;

        var targets = new (string Number, string Name)[]
        {
            ("PN-BG-001", "BARIER GATE MOTOR"),
            ("PN-BG-002", "DETAIL ATAP KANOPI BARIER GATE MOTOR"),
            ("PN-BG-003", "DETAIL PONDASI KANOPI BARIER GATE"),
            ("PN-BG-004", "DETAIL TIANG RF-ID & CCTV"),
            ("PN-BG-005", "DETAIL BARIER GATE & PAGAR BRC")
        };

        var existingSheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .ToList();

        List<string> logs = new();

        using Transaction tx = new(doc, "AIGRAV - Setup Sheets");
        tx.Start();

        foreach (var (number, name) in targets)
        {
            ViewSheet? s = existingSheets.FirstOrDefault(x => string.Equals(x.SheetNumber, number, StringComparison.OrdinalIgnoreCase));
            if (s != null)
            {
                s.Name = name;
                logs.Add($"Existing Sheet updated: {number} - {name} (Id: {s.Id.Value})");
            }
            else
            {
                ViewSheet newSheet = ViewSheet.Create(doc, tbId);
                newSheet.SheetNumber = number;
                newSheet.Name = name;
                logs.Add($"Created Sheet: {number} - {name} (Id: {newSheet.Id.Value})");
            }
        }

        tx.Commit();
        return string.Join("\n", logs);
    }

    private static string InspectViewports(Document doc, string? argument)
    {
        var vps = new FilteredElementCollector(doc)
            .OfClass(typeof(Viewport))
            .Cast<Viewport>()
            .ToList();

        var details = vps.Select(vp =>
        {
            ViewSheet? sheet = doc.GetElement(vp.SheetId) as ViewSheet;
            View? view = doc.GetElement(vp.ViewId) as View;
            XYZ center = vp.GetBoxCenter();
            Outline outline = vp.GetBoxOutline();

            return new
            {
                ViewportId = vp.Id.Value,
                SheetId = vp.SheetId.Value,
                SheetNumber = sheet?.SheetNumber ?? "",
                SheetName = sheet?.Name ?? "",
                ViewId = vp.ViewId.Value,
                ViewName = view?.Name ?? "",
                CenterX_Ft = center.X,
                CenterY_Ft = center.Y,
                CenterX_Mm = FtToMm(center.X),
                CenterY_Mm = FtToMm(center.Y),
                OutlineMinX_Mm = FtToMm(outline.MinimumPoint.X),
                OutlineMinY_Mm = FtToMm(outline.MinimumPoint.Y),
                OutlineMaxX_Mm = FtToMm(outline.MaximumPoint.X),
                OutlineMaxY_Mm = FtToMm(outline.MaximumPoint.Y),
                Width_Mm = FtToMm(outline.MaximumPoint.X - outline.MinimumPoint.X),
                Height_Mm = FtToMm(outline.MaximumPoint.Y - outline.MinimumPoint.Y)
            };
        });

        return JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string AdjustViewports(Document doc, string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return "AdjustViewports failed: argument required.";

        using JsonDocument jdoc = JsonDocument.Parse(argument);
        JsonElement root = jdoc.RootElement;

        List<string> logs = new();

        using Transaction tx = new(doc, "AIGRAV - Adjust Viewports");
        tx.Start();

        foreach (JsonElement item in root.EnumerateArray())
        {
            long vpIdVal = item.GetProperty("viewportId").GetInt64();
            Viewport? vp = doc.GetElement(new ElementId(vpIdVal)) as Viewport;
            if (vp == null) continue;

            if (item.TryGetProperty("x_mm", out JsonElement xmm) && item.TryGetProperty("y_mm", out JsonElement ymm))
            {
                double x = MmToFt(xmm.GetDouble());
                double y = MmToFt(ymm.GetDouble());
                vp.SetBoxCenter(new XYZ(x, y, 0));
                logs.Add($"Viewport {vpIdVal} center set to ({xmm.GetDouble():F1} mm, {ymm.GetDouble():F1} mm)");
            }
            else if (item.TryGetProperty("x", out JsonElement xElem) && item.TryGetProperty("y", out JsonElement yElem))
            {
                double x = xElem.GetDouble();
                double y = yElem.GetDouble();
                vp.SetBoxCenter(new XYZ(x, y, 0));
                logs.Add($"Viewport {vpIdVal} center set to ({x:F3} ft, {y:F3} ft)");
            }

            if (item.TryGetProperty("labelLineLength", out JsonElement lenElem))
            {
                double len = lenElem.GetDouble();
                try
                {
                    vp.LabelLineLength = len;
                    logs.Add($"Viewport {vpIdVal} LabelLineLength set to {len:F3}");
                }
                catch (Exception ex)
                {
                    logs.Add($"Viewport {vpIdVal} LabelLineLength error: {ex.Message}");
                }
            }

            if (item.TryGetProperty("labelOffset", out JsonElement offElem))
            {
                double ox = offElem.GetProperty("x").GetDouble();
                double oy = offElem.GetProperty("y").GetDouble();
                try
                {
                    vp.LabelOffset = new XYZ(ox, oy, 0);
                    logs.Add($"Viewport {vpIdVal} LabelOffset set to ({ox:F3}, {oy:F3})");
                }
                catch (Exception ex)
                {
                    logs.Add($"Viewport {vpIdVal} LabelOffset error: {ex.Message}");
                }
            }
        }

        tx.Commit();
        return string.Join("\n", logs);
    }

    private static string GetRevisionClouds(Document doc)
    {
        var clouds = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_RevisionClouds)
            .WhereElementIsNotElementType()
            .ToList();

        if (clouds.Count == 0) return "No revision clouds found in the project.";

        var list = clouds.Select(c => new
        {
            Id = c.Id.Value,
            OwnerViewId = c.OwnerViewId.Value,
            ViewName = doc.GetElement(c.OwnerViewId)?.Name ?? "Unknown"
        });

        return JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string DeleteRevisionClouds(Document doc)
    {
        var cloudIds = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_RevisionClouds)
            .WhereElementIsNotElementType()
            .ToElementIds();

        if (cloudIds.Count == 0)
        {
            return "No revision clouds found in the active project.";
        }

        int count = cloudIds.Count;
        using (Transaction t = new Transaction(doc, "AIGRAV - Delete All Revision Clouds"))
        {
            t.Start();
            var deleted = doc.Delete(cloudIds);
            t.Commit();
            return $"Successfully deleted {count} revision cloud(s) from project (Total affected elements: {deleted.Count}).";
        }
    }
}

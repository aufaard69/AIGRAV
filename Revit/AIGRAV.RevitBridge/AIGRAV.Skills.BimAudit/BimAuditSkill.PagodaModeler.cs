using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class BimAuditSkill
{
    private static double PagodaMToFt(double meters)
    {
        return UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);
    }

    private static XYZ PagodaPt(double xM, double yM, double zM)
    {
        return new XYZ(PagodaMToFt(xM), PagodaMToFt(yM), PagodaMToFt(zM));
    }

    private static CurveLoop PagodaCreateRectLoop(double halfW, double halfH, double zM, bool ccw = true)
    {
        CurveLoop loop = new();
        XYZ p1 = PagodaPt(-halfW, -halfH, zM);
        XYZ p2 = PagodaPt(halfW, -halfH, zM);
        XYZ p3 = PagodaPt(halfW, halfH, zM);
        XYZ p4 = PagodaPt(-halfW, halfH, zM);

        if (ccw)
        {
            loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3));
            loop.Append(Line.CreateBound(p3, p4));
            loop.Append(Line.CreateBound(p4, p1));
        }
        else
        {
            loop.Append(Line.CreateBound(p1, p4));
            loop.Append(Line.CreateBound(p4, p3));
            loop.Append(Line.CreateBound(p3, p2));
            loop.Append(Line.CreateBound(p2, p1));
        }
        return loop;
    }

    private static Level PagodaGetOrCreateLevel(Document doc, string name, double elevM)
    {
        Level? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        Level newLvl = Level.Create(doc, PagodaMToFt(elevM));
        newLvl.Name = name;
        return newLvl;
    }

    private static Material PagodaGetOrCreateMaterial(Document doc, string name, byte r, byte g, byte b, int transparency = 0)
    {
        Material? mat = new FilteredElementCollector(doc)
            .OfClass(typeof(Material))
            .Cast<Material>()
            .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (mat == null)
        {
            ElementId id = Material.Create(doc, name);
            mat = doc.GetElement(id) as Material;
        }

        if (mat != null)
        {
            mat.Color = new Color(r, g, b);
            mat.Transparency = transparency;
        }
        return mat!;
    }

    private static FloorType PagodaCreateOrGetFloorType(Document doc, FloorType baseType, string newName, Material mat, double thickM)
    {
        FloorType? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault(ft => ft.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            CompoundStructure ecs = existing.GetCompoundStructure();
            if (ecs != null)
            {
                var layers = ecs.GetLayers();
                foreach (var l in layers) if (mat != null) l.MaterialId = mat.Id;
                ecs.SetLayers(layers);
                existing.SetCompoundStructure(ecs);
            }
            return existing;
        }

        FloorType newType = (FloorType)baseType.Duplicate(newName);
        CompoundStructure cs = newType.GetCompoundStructure();
        if (cs != null)
        {
            IList<CompoundStructureLayer> layers = cs.GetLayers();
            int core = cs.GetFirstCoreLayerIndex();
            for (int i = 0; i < layers.Count; i++)
            {
                if (mat != null) layers[i].MaterialId = mat.Id;
                if (i == core) layers[i].Width = PagodaMToFt(thickM);
            }
            cs.SetLayers(layers);
            newType.SetCompoundStructure(cs);
        }
        return newType;
    }

    private static WallType PagodaCreateOrGetWallType(Document doc, WallType baseType, string newName, Material mat, double thickM)
    {
        WallType? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            CompoundStructure ecs = existing.GetCompoundStructure();
            if (ecs != null)
            {
                var layers = ecs.GetLayers();
                foreach (var l in layers) if (mat != null) l.MaterialId = mat.Id;
                ecs.SetLayers(layers);
                existing.SetCompoundStructure(ecs);
            }
            return existing;
        }

        WallType newType = (WallType)baseType.Duplicate(newName);
        CompoundStructure cs = newType.GetCompoundStructure();
        if (cs != null)
        {
            IList<CompoundStructureLayer> layers = cs.GetLayers();
            int core = cs.GetFirstCoreLayerIndex();
            for (int i = 0; i < layers.Count; i++)
            {
                if (mat != null) layers[i].MaterialId = mat.Id;
                if (i == core) layers[i].Width = PagodaMToFt(thickM);
            }
            cs.SetLayers(layers);
            newType.SetCompoundStructure(cs);
        }
        return newType;
    }

    public static string ExecutePagodaMilestone(UIApplication app, Document doc, string? argument)
    {
        string milestone = "all";
        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                if (jd.RootElement.TryGetProperty("milestone", out JsonElement mEl))
                    milestone = mEl.GetString()?.ToLowerInvariant() ?? "all";
            }
            catch {}
        }

        StringBuilder log = new();
        log.AppendLine($"=== AIGRAV PAGODA MODELING ENGINE: EXECUTING [{milestone.ToUpper()}] ===");

        // Fetch Base Types
        FloorType baseFloor = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault(ft => ft.Name.Contains("Generic") || ft.Name.Contains("Conc"))
            ?? new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().First();

        WallType baseWall = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Kind == WallKind.Basic && (wt.Name.Contains("Generic") || wt.Name.Contains("Concrete")))
            ?? new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().First();

        Material matVermilion, matPlaster, matRoof, matGold, matStone, matHinoki;
        FloorType floorStone, floorWood, floorRoofEaves;
        WallType wallVermilion, wallPlaster, wallRailing, wallGold;
        Level l0, l1, l2, l3, l4, l5, l6, l7;

        // Transaction 0: Setup Types, Materials, and Levels
        using (Transaction txInit = new(doc, "AIGRAV Pagoda - Setup Types & Materials"))
        {
            txInit.Start();

            matVermilion = PagodaGetOrCreateMaterial(doc, "Pagoda - Vermilion Lacquer", 232, 58, 26);
            matPlaster = PagodaGetOrCreateMaterial(doc, "Pagoda - White Shikkui Plaster", 248, 248, 244);
            matRoof = PagodaGetOrCreateMaterial(doc, "Pagoda - Dark Bronze Roof", 52, 44, 40);
            matGold = PagodaGetOrCreateMaterial(doc, "Pagoda - Gold Sorin", 225, 175, 45);
            matStone = PagodaGetOrCreateMaterial(doc, "Pagoda - Stone Podium", 155, 150, 142);
            matHinoki = PagodaGetOrCreateMaterial(doc, "Pagoda - Hinoki Wood Decking", 195, 145, 95);

            floorStone = PagodaCreateOrGetFloorType(doc, baseFloor, "Pagoda - Podium Batu", matStone, 0.40);
            floorWood = PagodaCreateOrGetFloorType(doc, baseFloor, "Pagoda - Lantai Kayu Hinoki", matHinoki, 0.15);
            floorRoofEaves = PagodaCreateOrGetFloorType(doc, baseFloor, "Pagoda - Atap Tembaga Sori", matRoof, 0.20);

            wallVermilion = PagodaCreateOrGetWallType(doc, baseWall, "Pagoda - Dinding Vermilion", matVermilion, 0.20);
            wallPlaster = PagodaCreateOrGetWallType(doc, baseWall, "Pagoda - Panel Plester Putih", matPlaster, 0.15);
            wallRailing = PagodaCreateOrGetWallType(doc, baseWall, "Pagoda - Balustrade Vermilion", matVermilion, 0.08);
            wallGold = PagodaCreateOrGetWallType(doc, baseWall, "Pagoda - Logam Emas Sorin", matGold, 0.12);

            l0 = PagodaGetOrCreateLevel(doc, "L0 - Podium", 0.00);
            l1 = PagodaGetOrCreateLevel(doc, "L1 - Tingkat 1", 1.00);
            l2 = PagodaGetOrCreateLevel(doc, "L2 - Tingkat 2", 5.20);
            l3 = PagodaGetOrCreateLevel(doc, "L3 - Tingkat 3", 8.80);
            l4 = PagodaGetOrCreateLevel(doc, "L4 - Tingkat 4", 12.20);
            l5 = PagodaGetOrCreateLevel(doc, "L5 - Tingkat 5", 15.40);
            l6 = PagodaGetOrCreateLevel(doc, "L6 - Puncak Atap", 18.80);
            l7 = PagodaGetOrCreateLevel(doc, "L7 - Mahkota Sorin", 28.50);

            doc.Regenerate();
            txInit.Commit();
        }

        List<ElementId> createdIds = new();

        // -------------------------------------------------------------
        // RESET FUNCTION: If milestone == "reset" or "all" or "m1" (on clean start)
        // -------------------------------------------------------------
        if (milestone == "reset" || milestone == "m1" || milestone == "all")
        {
            using Transaction txReset = new(doc, "AIGRAV Pagoda - Clean Reset");
            txReset.Start();
            var oldElements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e => (e.Name != null && e.Name.StartsWith("Pagoda -")) ||
                            (e is Wall w && w.WallType != null && w.WallType.Name.StartsWith("Pagoda -")) ||
                            (e is Floor f && f.FloorType != null && f.FloorType.Name.StartsWith("Pagoda -")))
                .Select(e => e.Id)
                .ToList();
            if (oldElements.Count > 0)
            {
                doc.Delete(oldElements);
                log.AppendLine($"Reset: Cleaned {oldElements.Count} previous Pagoda elements.");
            }
            txReset.Commit();
            if (milestone == "reset") return log.ToString();
        }

        // =============================================================
        // MILESTONE 1: DATUM, SUBSTRUCTURE & SUPERSTRUCTURE SKELETON
        // =============================================================
        if (milestone == "m1" || milestone == "all")
        {
            using TransactionGroup tgM1 = new(doc, "AIGRAV M1 - Datum & Structure");
            tgM1.Start();
            using (Transaction tx = new(doc, "AIGRAV M1 - Build Skeleton"))
            {
                FailureHandlingOptions fOpts = tx.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                tx.SetFailureHandlingOptions(fOpts);
                tx.Start();

                // 1. Podium Substructure: 14.0m x 14.0m outer stone platform
                CurveLoop podOuter = PagodaCreateRectLoop(7.0, 7.0, 0.0, true);
                Floor podium = Floor.Create(doc, new List<CurveLoop> { podOuter }, floorStone.Id, l0.Id);
                podium.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(PagodaMToFt(0.0));
                createdIds.Add(podium.Id);

                // 2. Central Shinbashira (Pilar Utama Gempa)
                double colR = 0.35; // 700mm diameter
                int shinSides = 8;
                for (int i = 0; i < shinSides; i++)
                {
                    double a1 = (2.0 * Math.PI / shinSides) * i;
                    double a2 = (2.0 * Math.PI / shinSides) * ((i + 1) % shinSides);
                    XYZ pStart = PagodaPt(colR * Math.Cos(a1), colR * Math.Sin(a1), 0.0);
                    XYZ pEnd = PagodaPt(colR * Math.Cos(a2), colR * Math.Sin(a2), 0.0);
                    Wall sw = Wall.Create(doc, Line.CreateBound(pStart, pEnd), wallVermilion.Id, l0.Id, PagodaMToFt(18.80), 0.0, false, false);
                    createdIds.Add(sw.Id);
                }

                // 3. Shitenbashira (4 Pilar Penyangga Dalam) at X, Y = +-1.2m from L1 to L5
                double[] inOffsets = { -1.2, 1.2 };
                foreach (double ix in inOffsets)
                {
                    foreach (double iy in inOffsets)
                    {
                        XYZ pCol1 = PagodaPt(ix - 0.2, iy - 0.2, 1.0);
                        XYZ pCol2 = PagodaPt(ix + 0.2, iy - 0.2, 1.0);
                        XYZ pCol3 = PagodaPt(ix + 0.2, iy + 0.2, 1.0);
                        XYZ pCol4 = PagodaPt(ix - 0.2, iy + 0.2, 1.0);

                        Wall w1 = Wall.Create(doc, Line.CreateBound(pCol1, pCol2), wallVermilion.Id, l1.Id, PagodaMToFt(14.40), 0.0, false, false);
                        Wall w2 = Wall.Create(doc, Line.CreateBound(pCol2, pCol3), wallVermilion.Id, l1.Id, PagodaMToFt(14.40), 0.0, false, false);
                        Wall w3 = Wall.Create(doc, Line.CreateBound(pCol3, pCol4), wallVermilion.Id, l1.Id, PagodaMToFt(14.40), 0.0, false, false);
                        Wall w4 = Wall.Create(doc, Line.CreateBound(pCol4, pCol1), wallVermilion.Id, l1.Id, PagodaMToFt(14.40), 0.0, false, false);
                        createdIds.AddRange(new[] { w1.Id, w2.Id, w3.Id, w4.Id });
                    }
                }

                // 4. Perimeter Sangensha Pillars (12 Kolom Luar per tingkat)
                (Level lvl, double halfSpan, double hLvl)[] tierData = new[]
                {
                    (l1, 2.70, 4.20),
                    (l2, 2.50, 3.60),
                    (l3, 2.30, 3.40),
                    (l4, 2.10, 3.20),
                    (l5, 1.90, 3.40)
                };

                foreach (var td in tierData)
                {
                    double s = td.halfSpan;
                    double[] gridCoords = { -s, -s / 3.0, s / 3.0, s };
                    for (int side = 0; side < 4; side++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            double gx = 0, gy = 0;
                            if (side == 0) { gx = gridCoords[k]; gy = -s; }
                            else if (side == 1) { gx = s; gy = gridCoords[k]; }
                            else if (side == 2) { gx = -gridCoords[k]; gy = s; }
                            else if (side == 3) { gx = -s; gy = -gridCoords[k]; }

                            XYZ pt1 = PagodaPt(gx - 0.12, gy - 0.12, 0);
                            XYZ pt2 = PagodaPt(gx + 0.12, gy - 0.12, 0);
                            Wall pw = Wall.Create(doc, Line.CreateBound(pt1, pt2), wallVermilion.Id, td.lvl.Id, PagodaMToFt(td.hLvl), 0.0, false, false);
                            createdIds.Add(pw.Id);
                        }
                    }
                }

                doc.Regenerate();
                tx.Commit();
            }
            tgM1.Assimilate();
            log.AppendLine("Milestone 1 Completed: Levels, Stone Podium, Central Shinbashira, 4 Shitenbashira, and 60 Sangensha Columns.");
        }

        // =============================================================
        // MILESTONE 2: BUILDING ENVELOPE, SLABS & 5 ROOF TIERS
        // =============================================================
        if (milestone == "m2" || milestone == "all")
        {
            using TransactionGroup tgM2 = new(doc, "AIGRAV M2 - Envelope & Slabs");
            tgM2.Start();
            using (Transaction tx = new(doc, "AIGRAV M2 - Build Envelope & Slabs"))
            {
                FailureHandlingOptions fOpts = tx.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                tx.SetFailureHandlingOptions(fOpts);
                tx.Start();

                (Level lvl, double coreHalfW, double deckHalfW, double roofHalfW, double roofElevM)[] tierSpecs = new[]
                {
                    (l1, 2.70, 3.80, 6.00, 4.60),
                    (l2, 2.50, 3.50, 5.40, 8.20),
                    (l3, 2.30, 3.20, 4.90, 11.60),
                    (l4, 2.10, 2.90, 4.40, 14.80),
                    (l5, 1.90, 2.60, 3.90, 18.20)
                };

                foreach (var ts in tierSpecs)
                {
                    CurveLoop outerDeck = PagodaCreateRectLoop(ts.deckHalfW, ts.deckHalfW, 0.0, true);
                    CurveLoop innerShaft = PagodaCreateRectLoop(0.40, 0.40, 0.0, false);
                    Floor deck = Floor.Create(doc, new List<CurveLoop> { outerDeck, innerShaft }, floorWood.Id, ts.lvl.Id);
                    createdIds.Add(deck.Id);

                    double cw = ts.coreHalfW;
                    XYZ wp1 = PagodaPt(-cw, -cw, 0.0);
                    XYZ wp2 = PagodaPt(cw, -cw, 0.0);
                    XYZ wp3 = PagodaPt(cw, cw, 0.0);
                    XYZ wp4 = PagodaPt(-cw, cw, 0.0);

                    double wh = (ts.roofElevM - UnitUtils.ConvertFromInternalUnits(ts.lvl.Elevation, UnitTypeId.Meters)) - 0.30;
                    Wall wallS = Wall.Create(doc, Line.CreateBound(wp1, wp2), wallVermilion.Id, ts.lvl.Id, PagodaMToFt(wh), 0.0, false, false);
                    Wall wallE = Wall.Create(doc, Line.CreateBound(wp2, wp3), wallVermilion.Id, ts.lvl.Id, PagodaMToFt(wh), 0.0, false, false);
                    Wall wallN = Wall.Create(doc, Line.CreateBound(wp3, wp4), wallVermilion.Id, ts.lvl.Id, PagodaMToFt(wh), 0.0, false, false);
                    Wall wallW = Wall.Create(doc, Line.CreateBound(wp4, wp1), wallVermilion.Id, ts.lvl.Id, PagodaMToFt(wh), 0.0, false, false);
                    createdIds.AddRange(new[] { wallS.Id, wallE.Id, wallN.Id, wallW.Id });

                    double rw = ts.roofHalfW;
                    CurveLoop roofOuter = PagodaCreateRectLoop(rw, rw, 0.0, true);
                    CurveLoop roofCoreHole = PagodaCreateRectLoop(ts.coreHalfW * 0.7, ts.coreHalfW * 0.7, 0.0, false);
                    Floor eavesSlab = Floor.Create(doc, new List<CurveLoop> { roofOuter, roofCoreHole }, floorRoofEaves.Id, ts.lvl.Id);
                    double zOffset = ts.roofElevM - UnitUtils.ConvertFromInternalUnits(ts.lvl.Elevation, UnitTypeId.Meters);
                    eavesSlab.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(PagodaMToFt(zOffset));
                    createdIds.Add(eavesSlab.Id);

                    double capHalfW = rw * 0.85;
                    CurveLoop capOuter = PagodaCreateRectLoop(capHalfW, capHalfW, 0.0, true);
                    CurveLoop capHole = PagodaCreateRectLoop(ts.coreHalfW * 0.6, ts.coreHalfW * 0.6, 0.0, false);
                    Floor capSlab = Floor.Create(doc, new List<CurveLoop> { capOuter, capHole }, floorRoofEaves.Id, ts.lvl.Id);
                    capSlab.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(PagodaMToFt(zOffset + 0.35));
                    createdIds.Add(capSlab.Id);

                    if (ts.lvl == l5)
                    {
                        CurveLoop apexLoop = PagodaCreateRectLoop(2.2, 2.2, 0.0, true);
                        Floor apexCap = Floor.Create(doc, new List<CurveLoop> { apexLoop }, floorRoofEaves.Id, l6.Id);
                        apexCap.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(PagodaMToFt(0.20));
                        createdIds.Add(apexCap.Id);
                    }
                }

                doc.Regenerate();
                tx.Commit();
            }
            tgM2.Assimilate();
            log.AppendLine("Milestone 2 Completed: Zero-Overlap Deck Slabs, Core Walls, and 5-Tier Sori Overhanging Eaves Roofs.");
        }

        // =============================================================
        // MILESTONE 3: BALCONIES, VERMILION RAILINGS & PLASTER PANELS
        // =============================================================
        if (milestone == "m3" || milestone == "all")
        {
            using TransactionGroup tgM3 = new(doc, "AIGRAV M3 - Partitions & Openings");
            tgM3.Start();
            using (Transaction tx = new(doc, "AIGRAV M3 - Build Railings & Panels"))
            {
                FailureHandlingOptions fOpts = tx.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                tx.SetFailureHandlingOptions(fOpts);
                tx.Start();

                double[] balconyHalfWidths = { 3.75, 3.45, 3.15, 2.85, 2.55 };
                Level[] balconyLevels = { l1, l2, l3, l4, l5 };

                for (int t = 0; t < 5; t++)
                {
                    double bw = balconyHalfWidths[t];
                    Level blvl = balconyLevels[t];

                    XYZ rp1 = PagodaPt(-bw, -bw, 0.0);
                    XYZ rp2 = PagodaPt(bw, -bw, 0.0);
                    XYZ rp3 = PagodaPt(bw, bw, 0.0);
                    XYZ rp4 = PagodaPt(-bw, bw, 0.0);

                    double rh = 0.90;
                    Wall rS = Wall.Create(doc, Line.CreateBound(rp1, rp2), wallRailing.Id, blvl.Id, PagodaMToFt(rh), 0.0, false, false);
                    Wall rE = Wall.Create(doc, Line.CreateBound(rp2, rp3), wallRailing.Id, blvl.Id, PagodaMToFt(rh), 0.0, false, false);
                    Wall rN = Wall.Create(doc, Line.CreateBound(rp3, rp4), wallRailing.Id, blvl.Id, PagodaMToFt(rh), 0.0, false, false);
                    Wall rW = Wall.Create(doc, Line.CreateBound(rp4, rp1), wallRailing.Id, blvl.Id, PagodaMToFt(rh), 0.0, false, false);
                    createdIds.AddRange(new[] { rS.Id, rE.Id, rN.Id, rW.Id });
                }

                (Level lvl, double cw, double hPan)[] panelTiers = new[]
                {
                    (l1, 2.65, 3.20),
                    (l2, 2.45, 2.60),
                    (l3, 2.25, 2.40),
                    (l4, 2.05, 2.20),
                    (l5, 1.85, 2.20)
                };

                foreach (var pt in panelTiers)
                {
                    double w = pt.cw;
                    XYZ sp1 = PagodaPt(-w * 0.7, -w, 0.15);
                    XYZ sp2 = PagodaPt(w * 0.7, -w, 0.15);
                    Wall panS = Wall.Create(doc, Line.CreateBound(sp1, sp2), wallPlaster.Id, pt.lvl.Id, PagodaMToFt(pt.hPan), 0.0, false, false);

                    XYZ np1 = PagodaPt(-w * 0.7, w, 0.15);
                    XYZ np2 = PagodaPt(w * 0.7, w, 0.15);
                    Wall panN = Wall.Create(doc, Line.CreateBound(np1, np2), wallPlaster.Id, pt.lvl.Id, PagodaMToFt(pt.hPan), 0.0, false, false);

                    XYZ ep1 = PagodaPt(w, -w * 0.7, 0.15);
                    XYZ ep2 = PagodaPt(w, w * 0.7, 0.15);
                    Wall panE = Wall.Create(doc, Line.CreateBound(ep1, ep2), wallPlaster.Id, pt.lvl.Id, PagodaMToFt(pt.hPan), 0.0, false, false);

                    XYZ wp1 = PagodaPt(-w, -w * 0.7, 0.15);
                    XYZ wp2 = PagodaPt(-w, w * 0.7, 0.15);
                    Wall panW = Wall.Create(doc, Line.CreateBound(wp1, wp2), wallPlaster.Id, pt.lvl.Id, PagodaMToFt(pt.hPan), 0.0, false, false);

                    createdIds.AddRange(new[] { panS.Id, panN.Id, panE.Id, panW.Id });
                }

                double[] dirs = { 0, 90, 180, 270 };
                foreach (double d in dirs)
                {
                    double rad = d * Math.PI / 180.0;
                    double cx = Math.Cos(rad) * 4.40;
                    double cy = Math.Sin(rad) * 4.40;

                    CurveLoop stepLoop = PagodaCreateRectLoop(1.2, 0.6, 0.0, true);
                    Floor stepSlab = Floor.Create(doc, new List<CurveLoop> { stepLoop }, floorStone.Id, l0.Id);
                    stepSlab.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(PagodaMToFt(0.50));
                    ElementTransformUtils.MoveElement(doc, stepSlab.Id, PagodaPt(cx, cy, 0.0));
                    createdIds.Add(stepSlab.Id);
                }

                doc.Regenerate();
                tx.Commit();
            }
            tgM3.Assimilate();
            log.AppendLine("Milestone 3 Completed: Vermilion Balustrades, White Shikkui Infill Panels, and Podium Entrance Steps.");
        }

        // =============================================================
        // MILESTONE 4: SŌRIN SACRED SPIRE, MODEL HEALTH & VISUAL VIEWS
        // =============================================================
        if (milestone == "m4" || milestone == "all")
        {
            using TransactionGroup tgM4 = new(doc, "AIGRAV M4 - Rooms & QTO");
            tgM4.Start();
            using (Transaction tx = new(doc, "AIGRAV M4 - Build Sorin & Finalize"))
            {
                FailureHandlingOptions fOpts = tx.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                tx.SetFailureHandlingOptions(fOpts);
                tx.Start();

                // 1. Sōrin Finial (9.3m Sacred Spire crowning Tier 5)
                XYZ rbp1 = PagodaPt(-0.7, -0.7, 0.0);
                XYZ rbp2 = PagodaPt(0.7, -0.7, 0.0);
                XYZ rbp3 = PagodaPt(0.7, 0.7, 0.0);
                XYZ rbp4 = PagodaPt(-0.7, 0.7, 0.0);
                Wall rb1 = Wall.Create(doc, Line.CreateBound(rbp1, rbp2), wallGold.Id, l6.Id, PagodaMToFt(0.60), PagodaMToFt(0.20), false, false);
                Wall rb2 = Wall.Create(doc, Line.CreateBound(rbp2, rbp3), wallGold.Id, l6.Id, PagodaMToFt(0.60), PagodaMToFt(0.20), false, false);
                Wall rb3 = Wall.Create(doc, Line.CreateBound(rbp3, rbp4), wallGold.Id, l6.Id, PagodaMToFt(0.60), PagodaMToFt(0.20), false, false);
                Wall rb4 = Wall.Create(doc, Line.CreateBound(rbp4, rbp1), wallGold.Id, l6.Id, PagodaMToFt(0.60), PagodaMToFt(0.20), false, false);
                createdIds.AddRange(new[] { rb1.Id, rb2.Id, rb3.Id, rb4.Id });

                double mastR = 0.15;
                for (int i = 0; i < 8; i++)
                {
                    double a1 = (2.0 * Math.PI / 8) * i;
                    double a2 = (2.0 * Math.PI / 8) * ((i + 1) % 8);
                    XYZ mp1 = PagodaPt(mastR * Math.Cos(a1), mastR * Math.Sin(a1), 0.0);
                    XYZ mp2 = PagodaPt(mastR * Math.Cos(a2), mastR * Math.Sin(a2), 0.0);
                    Wall mw = Wall.Create(doc, Line.CreateBound(mp1, mp2), wallGold.Id, l6.Id, PagodaMToFt(8.50), PagodaMToFt(0.80), false, false);
                    createdIds.Add(mw.Id);
                }

                for (int r = 0; r < 9; r++)
                {
                    double ringZ = 2.20 + (r * 0.50);
                    double ringRadius = 0.55 - (r * 0.025);

                    CurveLoop ringLoop = new();
                    int sides = 8;
                    for (int s = 0; s < sides; s++)
                    {
                        double a1 = (2.0 * Math.PI / sides) * s;
                        double a2 = (2.0 * Math.PI / sides) * ((s + 1) % sides);
                        XYZ pA = PagodaPt(ringRadius * Math.Cos(a1), ringRadius * Math.Sin(a1), 0.0);
                        XYZ pB = PagodaPt(ringRadius * Math.Cos(a2), ringRadius * Math.Sin(a2), 0.0);
                        ringLoop.Append(Line.CreateBound(pA, pB));
                    }
                    CurveLoop innerHole = PagodaCreateRectLoop(0.16, 0.16, 0.0, false);
                    Floor ringFloor = Floor.Create(doc, new List<CurveLoop> { ringLoop, innerHole }, floorRoofEaves.Id, l6.Id);
                    ringFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(PagodaMToFt(ringZ));
                    createdIds.Add(ringFloor.Id);
                }

                for (int i = 0; i < 4; i++)
                {
                    double rad = (i * 90.0) * Math.PI / 180.0;
                    XYZ tipP1 = PagodaPt(0, 0, 0);
                    XYZ tipP2 = PagodaPt(0.40 * Math.Cos(rad), 0.40 * Math.Sin(rad), 0);
                    Wall flame = Wall.Create(doc, Line.CreateBound(tipP1, tipP2), wallGold.Id, l6.Id, PagodaMToFt(1.80), PagodaMToFt(6.80), false, false);
                    createdIds.Add(flame.Id);
                }

                Wall tipNeedle = Wall.Create(doc, Line.CreateBound(PagodaPt(-0.04, 0, 0), PagodaPt(0.04, 0, 0)), wallGold.Id, l6.Id, PagodaMToFt(1.20), PagodaMToFt(8.50), false, false);
                createdIds.Add(tipNeedle.Id);

                // 2. Set Up 3D Perspective View Orientation
                View3D? view3d = doc.ActiveView as View3D
                    ?? new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(v => !v.IsTemplate);

                if (view3d != null)
                {
                    view3d.DisplayStyle = DisplayStyle.ShadingWithEdges;
                    view3d.DetailLevel = ViewDetailLevel.Fine;

                    XYZ eye = PagodaPt(22.0, -22.0, 15.0);
                    XYZ target = PagodaPt(0.0, 0.0, 13.0);
                    XYZ forward = (target - eye).Normalize();
                    XYZ right = forward.CrossProduct(XYZ.BasisZ).Normalize();
                    XYZ up = right.CrossProduct(forward).Normalize();

                    ViewOrientation3D orient = new(eye, up, forward);
                    view3d.SetOrientation(orient);
                }

                doc.Regenerate();
                tx.Commit();
            }
            tgM4.Assimilate();
            log.AppendLine("Milestone 4 Completed: Sōrin Sacred Spire (Roban, Mast, 9 Kururin Rings, Suien Flames, Hōju Tip), Shading & Camera Setup.");
        }

        log.AppendLine($"=== EXECUTION SUMMARY: Total New Elements Created = {createdIds.Count} ===");
        return JsonSerializer.Serialize(new
        {
            status = "Success",
            milestone,
            total_elements_created = createdIds.Count,
            elements = createdIds.Select(id => id.Value).ToList(),
            log = log.ToString()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string InspectPagoda(Document doc)
    {
        StringBuilder sb = new();
        sb.AppendLine("=== AUDIT KELAYAKAN TEKNIK SIPIL & ARSITEKTUR PAGODA 5 TINGKAT ===");

        var pagodaWalls = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .Where(w => w.WallType != null && w.WallType.Name.StartsWith("Pagoda -"))
            .ToList();

        var pagodaFloors = new FilteredElementCollector(doc)
            .OfClass(typeof(Floor))
            .Cast<Floor>()
            .Where(f => {
                FloorType? ft = doc.GetElement(f.GetTypeId()) as FloorType;
                return ft != null && ft.Name.StartsWith("Pagoda -");
            })
            .ToList();

        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();

        sb.AppendLine($"Total Levels               : {levels.Count}");
        sb.AppendLine($"Total Dinding & Rangka     : {pagodaWalls.Count}");
        sb.AppendLine($"Total Pelat & Eaves Atap   : {pagodaFloors.Count}");

        sb.AppendLine("\n--- Hirarki Level & Datum ---");
        foreach (var l in levels)
        {
            double elevM = UnitUtils.ConvertFromInternalUnits(l.Elevation, UnitTypeId.Meters);
            sb.AppendLine($"Level: '{l.Name}' | Elevasi: {elevM:F2} m");
        }

        sb.AppendLine("\n--- Audit Zero-Overlap Pelat Lantai & Eaves ---");
        foreach (var f in pagodaFloors)
        {
            FloorType? ft = doc.GetElement(f.GetTypeId()) as FloorType;
            Level? lvl = doc.GetElement(f.LevelId) as Level;
            Parameter? pOffset = f.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            double offM = pOffset != null ? UnitUtils.ConvertFromInternalUnits(pOffset.AsDouble(), UnitTypeId.Meters) : 0;
            sb.AppendLine($"Floor Id: {f.Id} | Type: '{ft?.Name}' | Level: '{lvl?.Name}' | Offset: +{offM:F2} m");
        }

        return sb.ToString();
    }

    public static string ExportPagodaViews(Document doc, string? argument)
    {
        try
        {
            string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "AIGRAV_VisualEye");
            Directory.CreateDirectory(outDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outPath = Path.Combine(outDir, $"pagoda_3d_render_{timestamp}.png");

            View3D? view3d = doc.ActiveView as View3D
                ?? new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(v => !v.IsTemplate);

            if (view3d == null) return "Error: No 3D view found.";

            using (Transaction tx = new(doc, "Prepare Pagoda Viewport & Shading"))
            {
                FailureHandlingOptions fOpts = tx.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                tx.SetFailureHandlingOptions(fOpts);
                tx.Start();

                // 1. Ensure all materials have vivid authentic colors
                var allMats = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().ToList();
                Material? matVermilion = allMats.FirstOrDefault(m => m.Name.Equals("Pagoda - Vermilion Lacquer", StringComparison.OrdinalIgnoreCase));
                Material? matPlaster = allMats.FirstOrDefault(m => m.Name.Equals("Pagoda - White Shikkui Plaster", StringComparison.OrdinalIgnoreCase));
                Material? matRoof = allMats.FirstOrDefault(m => m.Name.Equals("Pagoda - Dark Bronze Roof", StringComparison.OrdinalIgnoreCase));
                Material? matGold = allMats.FirstOrDefault(m => m.Name.Equals("Pagoda - Gold Sorin", StringComparison.OrdinalIgnoreCase));
                Material? matStone = allMats.FirstOrDefault(m => m.Name.Equals("Pagoda - Stone Podium", StringComparison.OrdinalIgnoreCase));
                Material? matHinoki = allMats.FirstOrDefault(m => m.Name.Equals("Pagoda - Hinoki Wood Decking", StringComparison.OrdinalIgnoreCase));

                if (matVermilion != null) matVermilion.Color = new Color(232, 58, 26);
                if (matPlaster != null) matPlaster.Color = new Color(248, 248, 244);
                if (matRoof != null) matRoof.Color = new Color(52, 44, 40);
                if (matGold != null) matGold.Color = new Color(225, 175, 45);
                if (matStone != null) matStone.Color = new Color(155, 150, 142);
                if (matHinoki != null) matHinoki.Color = new Color(195, 145, 95);

                // 2. Configure View Graphics & Hide Datum Lines
                view3d.DisplayStyle = DisplayStyle.ShadingWithEdges;
                view3d.DetailLevel = ViewDetailLevel.Fine;
                view3d.CropBoxActive = false;
                view3d.CropBoxVisible = false;

                try
                {
                    Category? catLvl = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Levels);
                    if (catLvl != null) view3d.SetCategoryHidden(catLvl.Id, true);
                    Category? catGrd = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Grids);
                    if (catGrd != null) view3d.SetCategoryHidden(catGrd.Id, true);
                }
                catch {}

                // Camera orientation: 3D perspective framed at Pagoda center
                XYZ eye = PagodaPt(22.0, -22.0, 15.0);
                XYZ target = PagodaPt(0.0, 0.0, 13.0);
                XYZ forward = (target - eye).Normalize();
                XYZ right = forward.CrossProduct(XYZ.BasisZ).Normalize();
                XYZ up = right.CrossProduct(forward).Normalize();

                view3d.SetOrientation(new ViewOrientation3D(eye, up, forward));

                // 3. Apply direct Graphic Overrides for vivid colors in Shading mode
                Color cVermilion = new Color(232, 58, 26);
                Color cPlaster = new Color(250, 250, 248);
                Color cRoof = new Color(55, 45, 40);
                Color cGold = new Color(225, 175, 45);
                Color cStone = new Color(155, 150, 142);
                Color cWood = new Color(195, 145, 95);

                FillPatternElement? solidFill = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                var pagodaWalls = new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>().Where(w => w.WallType != null && w.WallType.Name.StartsWith("Pagoda -")).ToList();
                foreach (var w in pagodaWalls)
                {
                    OverrideGraphicSettings og = new();
                    Color col = w.WallType.Name.Contains("Plaster") ? cPlaster : (w.WallType.Name.Contains("Gold") || w.WallType.Name.Contains("Sorin") ? cGold : cVermilion);
                    if (solidFill != null)
                    {
                        og.SetSurfaceForegroundPatternId(solidFill.Id);
                        og.SetSurfaceForegroundPatternColor(col);
                    }
                    view3d.SetElementOverrides(w.Id, og);
                }

                var pagodaFloors = new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>().Where(f => {
                    FloorType? ft = doc.GetElement(f.GetTypeId()) as FloorType;
                    return ft != null && ft.Name.StartsWith("Pagoda -");
                }).ToList();
                foreach (var f in pagodaFloors)
                {
                    FloorType? ft = doc.GetElement(f.GetTypeId()) as FloorType;
                    OverrideGraphicSettings og = new();
                    Color col = ft.Name.Contains("Batu") ? cStone : (ft.Name.Contains("Atap") ? cRoof : cWood);
                    if (solidFill != null)
                    {
                        og.SetSurfaceForegroundPatternId(solidFill.Id);
                        og.SetSurfaceForegroundPatternColor(col);
                    }
                    view3d.SetElementOverrides(f.Id, og);
                }

                doc.Regenerate();
                tx.Commit();
            }

            ImageExportOptions opts = new()
            {
                ExportRange = ExportRange.VisibleRegionOfCurrentView,
                FilePath = outPath,
                FitDirection = FitDirectionType.Horizontal,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ImageResolution = ImageResolution.DPI_150,
                ShadowViewsFileType = ImageFileType.PNG,
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = 1920
            };

            doc.ExportImage(opts);

            string actualFile = Directory.GetFiles(outDir, $"pagoda_3d_render_{timestamp}*.png").FirstOrDefault() ?? outPath;
            return JsonSerializer.Serialize(new
            {
                status = "Success",
                exported_image = actualFile
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { status = "Error", message = ex.Message });
        }
    }
}

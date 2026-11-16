using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public static class EiffelTowerModeler
{
    private const double ToFt = 1.0 / 0.3048;
    private static double MToFt(double meters) => meters * ToFt;

    private static XYZ Pt(double xM, double yM, double zM)
    {
        return new XYZ(xM * ToFt, yM * ToFt, zM * ToFt);
    }

    public static string BuildEiffelTower(Document doc, JsonElement op)
    {
        // 0. Clean old elements if requested
        bool clearOld = !op.TryGetProperty("clear_existing", out JsonElement cEl) || cEl.GetBoolean();
        if (clearOld)
        {
            using Transaction txClean = new(doc, "AIGRAV - Clear Canvas for Eiffel Tower");
            TransactionFailureHandling.SuppressWarnings(txClean);
            txClean.Start();

            var toDelete = new List<ElementId>();
            toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Floor)).WhereElementIsNotElementType().ToElementIds());
            toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Wall)).WhereElementIsNotElementType().ToElementIds());
            toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(DirectShape)).WhereElementIsNotElementType().ToElementIds());

            var structIds = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType()
                .Where(fi => fi.Category != null && (
                    fi.Category.Id.Value == (long)BuiltInCategory.OST_Columns ||
                    fi.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns ||
                    fi.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming))
                .Select(e => e.Id);
            toDelete.AddRange(structIds);

            if (toDelete.Count > 0)
            {
                doc.Delete(toDelete);
            }
            txClean.Commit();
        }

        // 1. Resolve Authentic Materials (Puddle Iron from Pompey Forges, 1887)
        Material matPuddleIron;
        Material matGrass;
        Material matPaving;
        Material matStone;
        Material matGlass;
        Material matGold;

        using (Transaction txMat = new(doc, "AIGRAV - Resolve Eiffel Materials"))
        {
            TransactionFailureHandling.SuppressWarnings(txMat);
            txMat.Start();

            matPuddleIron = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Besi Tempa Puddle - Fer Puddlé Tour Eiffel (Pompey)",
                new[] { "fer", "puddlé", "puddle", "wrought", "eiffel", "brun", "iron", "baja" }, 138, 114, 92, 0, 90, 80);
            matGrass = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Lansekap - Rumput Hijau Champ de Mars",
                new[] { "grass", "rumput", "lawn" }, 55, 150, 45, 0, 15, 20);
            matPaving = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Perkerasan - Paving Esplanade Eiffel",
                new[] { "paving", "stone", "andesit" }, 190, 185, 175, 0, 30, 40);
            matStone = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Struktur - Batu Andesit Socle",
                new[] { "socle", "stone", "andesit", "beton" }, 125, 128, 130, 0, 35, 45);
            matGlass = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Kaca - Paviliun & Balustrade Eiffel",
                new[] { "glass", "kaca", "glazing" }, 200, 230, 245, 75, 120, 100);
            matGold = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Puncak - Kubah Emas & Phare Eiffel",
                new[] { "gold", "emas", "yellow" }, 235, 205, 110, 0, 120, 100);

            txMat.Commit();
        }

        // 2. Resolve Family & Wall / Floor Types
        FloorType ftGrass;
        FloorType ftPaving;
        FloorType ftStoneSocle;
        FloorType ftL1Deck;
        FloorType ftL2Deck;
        FloorType ftSommetDeck;

        WallType wtStoneSocle;
        WallType wtBalustrade;
        WallType wtGlassPavilion;
        WallType wtShaft;

        using (Transaction txTypes = new(doc, "AIGRAV - Setup Eiffel Types"))
        {
            TransactionFailureHandling.SuppressWarnings(txTypes);
            txTypes.Start();

            ftGrass = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Eiffel - Rumput Champ de Mars 150mm", matGrass, 150.0);
            ftPaving = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Eiffel - Paving Esplanade 200mm", matPaving, 200.0);
            ftStoneSocle = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Eiffel - Socle Fondasi Batu 1000mm", matStone, 1000.0);
            ftL1Deck = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Eiffel - Pelat Dek Lantai 1 300mm", matPuddleIron, 300.0);
            ftL2Deck = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Eiffel - Pelat Dek Lantai 2 300mm", matPuddleIron, 300.0);
            ftSommetDeck = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Eiffel - Pelat Dek Sommet 200mm", matPuddleIron, 200.0);

            wtStoneSocle = SpatialAgenticSandboxSkill.CreateOrGetWallType(doc, "Eiffel - Dinding Socle Fondasi 1000mm", matStone, 1000.0);
            wtBalustrade = SpatialAgenticSandboxSkill.CreateOrGetWallType(doc, "Eiffel - Balustrade Baja 150mm", matPuddleIron, 150.0);
            wtGlassPavilion = SpatialAgenticSandboxSkill.CreateOrGetWallType(doc, "Eiffel - Fasad Paviliun Kaca 100mm", matGlass, 100.0);
            wtShaft = SpatialAgenticSandboxSkill.CreateOrGetWallType(doc, "Eiffel - Dinding Inti Shaft 300mm", matPuddleIron, 300.0);

            txTypes.Commit();
        }

        // 3. Resolve Authentic Historic Puddle Iron Profiles (Cornières L-Angles, Profils en U, Fers Plats)
        FamilySymbol symAngleMain;
        FamilySymbol symAngleBrace;
        FamilySymbol symChannelBelt;
        FamilySymbol symArchChord;

        using (Transaction txFraming = new(doc, "AIGRAV - Load Authentic Eiffel Profiles"))
        {
            TransactionFailureHandling.SuppressWarnings(txFraming);
            txFraming.Start();

            string angleRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Structural Framing\Steel\M_L-Angle.rfa";
            string channelRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Structural Framing\Steel\M_C-Channel.rfa";

            FamilySymbol? baseAngle = LoadOrGetFramingSymbol(doc, angleRfa, "M_L-Angle", "");
            FamilySymbol? baseChannel = LoadOrGetFramingSymbol(doc, channelRfa, "M_C-Channel", "");

            // Fallback if either is missing
            FamilySymbol validBase = baseAngle ?? baseChannel
                ?? new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming).Cast<FamilySymbol>().First();

            symAngleMain = CreateOrGetDuplicateSymbol(doc, baseAngle ?? validBase, "Eiffel - Cornière L-Angle 200x200x20mm");
            symAngleBrace = CreateOrGetDuplicateSymbol(doc, baseAngle ?? validBase, "Eiffel - Cornière L-Angle 120x120x12mm");
            symChannelBelt = CreateOrGetDuplicateSymbol(doc, baseChannel ?? validBase, "Eiffel - Profil Kanal U 300x100mm");
            symArchChord = CreateOrGetDuplicateSymbol(doc, baseChannel ?? validBase, "Eiffel - Double Kanal U 250x80mm");

            if (!symAngleMain.IsActive) symAngleMain.Activate();
            if (!symAngleBrace.IsActive) symAngleBrace.Activate();
            if (!symChannelBelt.IsActive) symChannelBelt.Activate();
            if (!symArchChord.IsActive) symArchChord.Activate();

            doc.Regenerate();
            txFraming.Commit();
        }

        // 4. Official Datum Levels (10 Levels from 0.00m to +312.00m)
        double[] levelElevs = new[]
        {
            0.0,    // 01 - Esplanade Champ de Mars
            3.5,    // 02 - Puncak Socle Fondasi
            39.0,   // 03 - Puncak Busur Monumental
            57.6,   // 04 - Lantai 1 Premier Étage
            115.7,  // 05 - Lantai 2 Deuxième Étage
            195.0,  // 06 - Belt Truss Pertengahan
            276.1,  // 07 - Lantai 3 Sommet Observasi
            281.0,  // 08 - Dek Terbuka Puncak
            288.0,  // 09 - Kubah Lentera & Phare
            312.0   // 10 - Ujung Spire Antena
        };

        string[] levelNames = new[]
        {
            "01 - Esplanade Champ de Mars (0.00m)",
            "02 - Puncak Socle Fondasi (+3.50m)",
            "03 - Puncak Busur Monumental (+39.00m)",
            "04 - Lantai 1 Premier Étage (+57.60m)",
            "05 - Lantai 2 Deuxième Étage (+115.70m)",
            "06 - Belt Truss Pertengahan (+195.00m)",
            "07 - Lantai 3 Sommet Observasi (+276.10m)",
            "08 - Dek Terbuka Puncak (+281.00m)",
            "09 - Kubah Lentera & Phare (+288.00m)",
            "10 - Ujung Spire Antena (+312.00m)"
        };

        List<Level> levels = new();
        using (Transaction txLvl = new(doc, "AIGRAV - Create Eiffel Levels"))
        {
            TransactionFailureHandling.SuppressWarnings(txLvl);
            txLvl.Start();

            var existingLvls = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            for (int i = 0; i < levelElevs.Length; i++)
            {
                double targetElevFt = MToFt(levelElevs[i]);
                Level? matched = existingLvls.FirstOrDefault(l => Math.Abs(l.Elevation - targetElevFt) < 0.2);
                if (matched == null)
                {
                    matched = Level.Create(doc, targetElevFt);
                    matched.Name = levelNames[i];
                }
                levels.Add(matched);
            }
            txLvl.Commit();
        }

        int totalFloors = 0;
        int totalWalls = 0;
        int totalFraming = 0;

        using (Transaction txBuild = new(doc, "AIGRAV - Build Eiffel Tower (Authentic Lattice)"))
        {
            TransactionFailureHandling.SuppressWarnings(txBuild);
            txBuild.Start();

            Level lGround = levels[0];
            Level lSocle = levels[1];
            Level lArch = levels[2];
            Level lL1 = levels[3];
            Level lL2 = levels[4];
            Level lMid = levels[5];
            Level lSommet = levels[6];
            Level lTopDeck = levels[7];
            Level lDome = levels[8];
            Level lSpire = levels[9];

            // ========================================================
            // 5.1 Lanskap Champ de Mars & Esplanade (Zero-Overlap Donut Slabs)
            // ========================================================
            // Outer lawn: 240m x 240m with central void 140m x 140m
            double lawnHalf = 120.0;
            double esplanadeHalf = 70.0;
            CurveLoop grassOuter = CreateRectLoop(lawnHalf, lawnHalf, 0, 0, 0, true);
            CurveLoop grassInner = CreateRectLoop(esplanadeHalf, esplanadeHalf, 0, 0, 0, false);
            Floor.Create(doc, new List<CurveLoop> { grassOuter, grassInner }, ftGrass.Id, lGround.Id);
            totalFloors++;

            // Esplanade Paving: 140m x 140m outer with 4 corner void openings for the 4 plinths
            double plinthCenter = 43.0;
            double plinthHalf = 11.0; // 22m x 22m each plinth
            CurveLoop espOuter = CreateRectLoop(esplanadeHalf, esplanadeHalf, 0, 0, 0, true);
            List<CurveLoop> espLoops = new() { espOuter };

            double[] signs = new[] { 1.0, -1.0 };
            foreach (double sx in signs)
            {
                foreach (double sy in signs)
                {
                    CurveLoop plinthVoid = CreateRectLoop(plinthHalf, plinthHalf, sx * plinthCenter, sy * plinthCenter, 0, false);
                    espLoops.Add(plinthVoid);
                }
            }
            Floor.Create(doc, espLoops, ftPaving.Id, lGround.Id);
            totalFloors++;

            // 4 Massive Foundation Plinths (Masonry Pedestals at corners)
            foreach (double sx in signs)
            {
                foreach (double sy in signs)
                {
                    double px = sx * plinthCenter;
                    double py = sy * plinthCenter;
                    CurveLoop pLoop = CreateRectLoop(plinthHalf, plinthHalf, px, py, 0, true);
                    Floor.Create(doc, new List<CurveLoop> { pLoop }, ftStoneSocle.Id, lGround.Id);
                    totalFloors++;

                    // Surrounding socle walls (H = 3.5m)
                    XYZ[] swPts = new[]
                    {
                        Pt(px - plinthHalf, py - plinthHalf, 0),
                        Pt(px + plinthHalf, py - plinthHalf, 0),
                        Pt(px + plinthHalf, py + plinthHalf, 0),
                        Pt(px - plinthHalf, py + plinthHalf, 0)
                    };
                    for (int si = 0; si < 4; si++)
                    {
                        Line wLine = Line.CreateBound(swPts[si], swPts[(si + 1) % 4]);
                        Wall.Create(doc, wLine, wtStoneSocle.Id, lGround.Id, MToFt(3.5), 0, false, false);
                        totalWalls++;
                    }
                }
            }

            // ========================================================
            // 5.2 Helper function for creating 3D framing lines with profile symbol
            // ========================================================
            void AddBeam(XYZ p1, XYZ p2, Level lvl, FamilySymbol sym)
            {
                try
                {
                    if (p1.DistanceTo(p2) > 0.1)
                    {
                        Line l = Line.CreateBound(p1, p2);
                        doc.Create.NewFamilyInstance(l, sym, lvl, StructuralType.Beam);
                        totalFraming++;
                    }
                }
                catch { }
            }

            // ========================================================
            // 5.3 Ground to Level 1: 4 Massive Inclined Built-up Lattice Pillars
            // ========================================================
            // Each pillar is a 4-corner space truss built from Cornières L-Angles & Treillis
            double[] zBaysL1 = new[] { 0.0, 19.2, 38.4, 57.6 };
            Level[] lvlBaysL1 = new[] { lGround, lGround, lSocle, lL1 };

            double Interpolate(double v0, double v1, double t) => v0 + (v1 - v0) * t;

            foreach (double sx in signs)
            {
                foreach (double sy in signs)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        double z0 = zBaysL1[b];
                        double z1 = zBaysL1[b + 1];
                        double t0 = z0 / 57.6;
                        double t1 = z1 / 57.6;

                        double cX0 = sx * Interpolate(43.0, 24.5, t0);
                        double cY0 = sy * Interpolate(43.0, 24.5, t0);
                        double wHalf0 = Interpolate(4.5, 3.0, t0);

                        double cX1 = sx * Interpolate(43.0, 24.5, t1);
                        double cY1 = sy * Interpolate(43.0, 24.5, t1);
                        double wHalf1 = Interpolate(4.5, 3.0, t1);

                        // 4 corner points at bottom of bay
                        XYZ[] pts0 = new[]
                        {
                            Pt(cX0 - wHalf0, cY0 - wHalf0, z0),
                            Pt(cX0 + wHalf0, cY0 - wHalf0, z0),
                            Pt(cX0 + wHalf0, cY0 + wHalf0, z0),
                            Pt(cX0 - wHalf0, cY0 + wHalf0, z0)
                        };

                        // 4 corner points at top of bay
                        XYZ[] pts1 = new[]
                        {
                            Pt(cX1 - wHalf1, cY1 - wHalf1, z1),
                            Pt(cX1 + wHalf1, cY1 - wHalf1, z1),
                            Pt(cX1 + wHalf1, cY1 + wHalf1, z1),
                            Pt(cX1 - wHalf1, cY1 + wHalf1, z1)
                        };

                        // 4 main corner legs: Cornière L-Angle 200x200x20mm
                        for (int k = 0; k < 4; k++)
                        {
                            AddBeam(pts0[k], pts1[k], lvlBaysL1[b], symAngleMain);
                        }

                        // Horizontal perimeter tie-beams: Profil Kanal U 300x100mm
                        for (int k = 0; k < 4; k++)
                        {
                            AddBeam(pts1[k], pts1[(k + 1) % 4], lvlBaysL1[b + 1], symChannelBelt);
                        }

                        // Diagonal X-bracings on the 4 faces: Cornière L-Angle 120x120x12mm
                        for (int k = 0; k < 4; k++)
                        {
                            int nextK = (k + 1) % 4;
                            AddBeam(pts0[k], pts1[nextK], lvlBaysL1[b], symAngleBrace);
                            AddBeam(pts0[nextK], pts1[k], lvlBaysL1[b], symAngleBrace);
                        }
                    }
                }
            }

            // ========================================================
            // 5.4 The 4 Monumental Parabolic Arches (Under Level 1)
            // ========================================================
            // Double-chord lattice arches: Double Kanal U 250x80mm chords with Cornière L-Angle web
            const int archSegs = 14;
            double spanHalf = 38.0;
            double hApex = 39.0;
            double hSpring = 3.5;

            double ArchZ(double u)
            {
                double norm = u / spanHalf;
                return hApex - (hApex - hSpring) * (norm * norm);
            }

            double archYCenter = 33.75;
            for (int s = 0; s < 4; s++)
            {
                List<XYZ> lowerPts = new();
                List<XYZ> upperPts = new();

                for (int i = 0; i <= archSegs; i++)
                {
                    double u = -spanHalf + (2.0 * spanHalf * i / archSegs);
                    double zIntrados = ArchZ(u);
                    double zExtrados = zIntrados + 2.5;

                    XYZ pInt, pExt;
                    if (s == 0) // North
                    {
                        pInt = Pt(u, archYCenter, zIntrados);
                        pExt = Pt(u, archYCenter, zExtrados);
                    }
                    else if (s == 1) // South
                    {
                        pInt = Pt(u, -archYCenter, zIntrados);
                        pExt = Pt(u, -archYCenter, zExtrados);
                    }
                    else if (s == 2) // East
                    {
                        pInt = Pt(archYCenter, u, zIntrados);
                        pExt = Pt(archYCenter, u, zExtrados);
                    }
                    else // West
                    {
                        pInt = Pt(-archYCenter, u, zIntrados);
                        pExt = Pt(-archYCenter, u, zExtrados);
                    }

                    lowerPts.Add(pInt);
                    upperPts.Add(pExt);
                }

                // Chords using Double Kanal U
                for (int i = 0; i < archSegs; i++)
                {
                    AddBeam(lowerPts[i], lowerPts[i + 1], lArch, symArchChord);
                    AddBeam(upperPts[i], upperPts[i + 1], lArch, symArchChord);
                    AddBeam(lowerPts[i], upperPts[i], lArch, symAngleBrace);
                    AddBeam(lowerPts[i], upperPts[i + 1], lArch, symAngleBrace);
                }
                AddBeam(lowerPts[archSegs], upperPts[archSegs], lArch, symAngleBrace);
            }

            // ========================================================
            // 5.5 Level 1 Platform (Premier Étage at Z = 57.6m)
            // ========================================================
            double l1Half = 33.0;
            double l1VoidHalf = 18.0;
            CurveLoop l1Outer = CreateRectLoop(l1Half, l1Half, 0, 0, 57.6, true);
            CurveLoop l1Inner = CreateRectLoop(l1VoidHalf, l1VoidHalf, 0, 0, 57.6, false);
            Floor.Create(doc, new List<CurveLoop> { l1Outer, l1Inner }, ftL1Deck.Id, lL1.Id);
            totalFloors++;

            // Platform Perimeter Girders (Channel Belt)
            XYZ[] l1OutPts = new[]
            {
                Pt(-l1Half, -l1Half, 57.6),
                Pt(l1Half, -l1Half, 57.6),
                Pt(l1Half, l1Half, 57.6),
                Pt(-l1Half, l1Half, 57.6)
            };
            for (int k = 0; k < 4; k++)
            {
                AddBeam(l1OutPts[k], l1OutPts[(k + 1) % 4], lL1, symChannelBelt);
                Wall.Create(doc, Line.CreateBound(l1OutPts[k], l1OutPts[(k + 1) % 4]), wtBalustrade.Id, lL1.Id, MToFt(1.2), 0, false, false);
                totalWalls++;
            }

            // Inner void glass railing (H = 1.2m)
            XYZ[] l1InPts = new[]
            {
                Pt(-l1VoidHalf, -l1VoidHalf, 57.6),
                Pt(l1VoidHalf, -l1VoidHalf, 57.6),
                Pt(l1VoidHalf, l1VoidHalf, 57.6),
                Pt(-l1VoidHalf, l1VoidHalf, 57.6)
            };
            for (int k = 0; k < 4; k++)
            {
                AddBeam(l1InPts[k], l1InPts[(k + 1) % 4], lL1, symChannelBelt);
                Wall.Create(doc, Line.CreateBound(l1InPts[k], l1InPts[(k + 1) % 4]), wtGlassPavilion.Id, lL1.Id, MToFt(1.2), 0, false, false);
                totalWalls++;
            }

            // Pavilions on Level 1 (Madame Brasserie & Ferrié Pavilions on East & West)
            double[] pavX = new[] { -25.5, 25.5 };
            foreach (double px in pavX)
            {
                double pHalfW = 4.5;
                double pHalfL = 12.0;
                XYZ[] pvPts = new[]
                {
                    Pt(px - pHalfW, -pHalfL, 57.6),
                    Pt(px + pHalfW, -pHalfL, 57.6),
                    Pt(px + pHalfW, pHalfL, 57.6),
                    Pt(px - pHalfW, pHalfL, 57.6)
                };
                for (int k = 0; k < 4; k++)
                {
                    Wall.Create(doc, Line.CreateBound(pvPts[k], pvPts[(k + 1) % 4]), wtGlassPavilion.Id, lL1.Id, MToFt(4.5), 0, false, false);
                    totalWalls++;
                }
            }

            // ========================================================
            // 5.6 Level 1 to Level 2: 4 Pillars Converging (Z = 57.6 -> 115.7m)
            // ========================================================
            double[] zBaysL2 = new[] { 57.6, 86.65, 115.7 };
            foreach (double sx in signs)
            {
                foreach (double sy in signs)
                {
                    for (int b = 0; b < 2; b++)
                    {
                        double z0 = zBaysL2[b];
                        double z1 = zBaysL2[b + 1];
                        double t0 = (z0 - 57.6) / (115.7 - 57.6);
                        double t1 = (z1 - 57.6) / (115.7 - 57.6);

                        double cX0 = sx * Interpolate(24.5, 13.5, t0);
                        double cY0 = sy * Interpolate(24.5, 13.5, t0);
                        double wHalf0 = Interpolate(3.0, 2.0, t0);

                        double cX1 = sx * Interpolate(24.5, 13.5, t1);
                        double cY1 = sy * Interpolate(24.5, 13.5, t1);
                        double wHalf1 = Interpolate(3.0, 2.0, t1);

                        XYZ[] pts0 = new[]
                        {
                            Pt(cX0 - wHalf0, cY0 - wHalf0, z0),
                            Pt(cX0 + wHalf0, cY0 - wHalf0, z0),
                            Pt(cX0 + wHalf0, cY0 + wHalf0, z0),
                            Pt(cX0 - wHalf0, cY0 + wHalf0, z0)
                        };

                        XYZ[] pts1 = new[]
                        {
                            Pt(cX1 - wHalf1, cY1 - wHalf1, z1),
                            Pt(cX1 + wHalf1, cY1 - wHalf1, z1),
                            Pt(cX1 + wHalf1, cY1 + wHalf1, z1),
                            Pt(cX1 - wHalf1, cY1 + wHalf1, z1)
                        };

                        for (int k = 0; k < 4; k++)
                        {
                            AddBeam(pts0[k], pts1[k], lL2, symAngleMain);
                            AddBeam(pts1[k], pts1[(k + 1) % 4], lL2, symChannelBelt);
                            int nextK = (k + 1) % 4;
                            AddBeam(pts0[k], pts1[nextK], lL2, symAngleBrace);
                            AddBeam(pts0[nextK], pts1[k], lL2, symAngleBrace);
                        }
                    }
                }
            }

            // ========================================================
            // 5.7 Level 2 Platform (Deuxième Étage at Z = 115.7m)
            // ========================================================
            double l2Half = 20.0;
            double l2VoidHalf = 9.0;
            CurveLoop l2Outer = CreateRectLoop(l2Half, l2Half, 0, 0, 115.7, true);
            CurveLoop l2Inner = CreateRectLoop(l2VoidHalf, l2VoidHalf, 0, 0, 115.7, false);
            Floor.Create(doc, new List<CurveLoop> { l2Outer, l2Inner }, ftL2Deck.Id, lL2.Id);
            totalFloors++;

            XYZ[] l2OutPts = new[]
            {
                Pt(-l2Half, -l2Half, 115.7),
                Pt(l2Half, -l2Half, 115.7),
                Pt(l2Half, l2Half, 115.7),
                Pt(-l2Half, l2Half, 115.7)
            };
            for (int k = 0; k < 4; k++)
            {
                AddBeam(l2OutPts[k], l2OutPts[(k + 1) % 4], lL2, symChannelBelt);
                Wall.Create(doc, Line.CreateBound(l2OutPts[k], l2OutPts[(k + 1) % 4]), wtBalustrade.Id, lL2.Id, MToFt(1.2), 0, false, false);
                totalWalls++;
            }

            // Jules Verne Restaurant Pavilion on Level 2 (North side)
            XYZ[] jvPts = new[]
            {
                Pt(-7.0, 10.0, 115.7),
                Pt(7.0, 10.0, 115.7),
                Pt(7.0, 17.5, 115.7),
                Pt(-7.0, 17.5, 115.7)
            };
            for (int k = 0; k < 4; k++)
            {
                Wall.Create(doc, Line.CreateBound(jvPts[k], jvPts[(k + 1) % 4]), wtGlassPavilion.Id, lL2.Id, MToFt(4.0), 0, false, false);
                totalWalls++;
            }

            // ========================================================
            // 5.8 Tower Shaft / Body: Level 2 to Level 3 (Z = 115.7 -> 276.1m)
            // ========================================================
            double[] zShaft = new[] { 115.7, 160.0, 195.0, 235.0, 276.1 };
            double[] rShaft = new[] { 13.5, 8.5, 6.8, 5.5, 4.5 };

            for (int s = 0; s < 4; s++)
            {
                double z0 = zShaft[s];
                double z1 = zShaft[s + 1];
                double r0 = rShaft[s];
                double r1 = rShaft[s + 1];

                XYZ[] sPts0 = new[]
                {
                    Pt(-r0, -r0, z0),
                    Pt(r0, -r0, z0),
                    Pt(r0, r0, z0),
                    Pt(-r0, r0, z0)
                };

                XYZ[] sPts1 = new[]
                {
                    Pt(-r1, -r1, z1),
                    Pt(r1, -r1, z1),
                    Pt(r1, r1, z1),
                    Pt(-r1, r1, z1)
                };

                // 4 corner columns: Cornière L-Angle 200x200x20mm
                for (int k = 0; k < 4; k++)
                {
                    AddBeam(sPts0[k], sPts1[k], lMid, symAngleMain);
                }

                // Horizontal ring girder at top of stage: Profil Kanal U 300x100mm
                for (int k = 0; k < 4; k++)
                {
                    AddBeam(sPts1[k], sPts1[(k + 1) % 4], lMid, symChannelBelt);
                }

                // Cross bracings: Cornière L-Angle 120x120x12mm
                for (int k = 0; k < 4; k++)
                {
                    int nextK = (k + 1) % 4;
                    AddBeam(sPts0[k], sPts1[nextK], lMid, symAngleBrace);
                    AddBeam(sPts0[nextK], sPts1[k], lMid, symAngleBrace);
                }
            }

            // Central elevator core shaft structure (Z = 115.7 -> 276.1m)
            double coreHalf = 2.2;
            XYZ[] corePts0 = new[]
            {
                Pt(-coreHalf, -coreHalf, 115.7),
                Pt(coreHalf, -coreHalf, 115.7),
                Pt(coreHalf, coreHalf, 115.7),
                Pt(-coreHalf, coreHalf, 115.7)
            };
            XYZ[] corePts1 = new[]
            {
                Pt(-coreHalf, -coreHalf, 276.1),
                Pt(coreHalf, -coreHalf, 276.1),
                Pt(coreHalf, coreHalf, 276.1),
                Pt(-coreHalf, coreHalf, 276.1)
            };
            for (int k = 0; k < 4; k++)
            {
                AddBeam(corePts0[k], corePts1[k], lSommet, symChannelBelt);
            }

            // ========================================================
            // 5.9 Level 3 Platform (Sommet at Z = 276.1m -> 281.0m)
            // ========================================================
            double l3Half = 6.0;
            CurveLoop l3Loop = CreateRectLoop(l3Half, l3Half, 0, 0, 276.1, true);
            Floor.Create(doc, new List<CurveLoop> { l3Loop }, ftSommetDeck.Id, lSommet.Id);
            totalFloors++;

            XYZ[] l3Pts = new[]
            {
                Pt(-l3Half, -l3Half, 276.1),
                Pt(l3Half, -l3Half, 276.1),
                Pt(l3Half, l3Half, 276.1),
                Pt(-l3Half, l3Half, 276.1)
            };
            for (int k = 0; k < 4; k++)
            {
                AddBeam(l3Pts[k], l3Pts[(k + 1) % 4], lSommet, symChannelBelt);
                Wall.Create(doc, Line.CreateBound(l3Pts[k], l3Pts[(k + 1) % 4]), wtGlassPavilion.Id, lSommet.Id, MToFt(3.5), 0, false, false);
                totalWalls++;
            }

            // Upper open-air observation deck at Z = 281.0m (10m x 10m)
            double lTopHalf = 5.0;
            CurveLoop lTopLoop = CreateRectLoop(lTopHalf, lTopHalf, 0, 0, 281.0, true);
            Floor.Create(doc, new List<CurveLoop> { lTopLoop }, ftSommetDeck.Id, lTopDeck.Id);
            totalFloors++;

            XYZ[] lTopPts = new[]
            {
                Pt(-lTopHalf, -lTopHalf, 281.0),
                Pt(lTopHalf, -lTopHalf, 281.0),
                Pt(lTopHalf, lTopHalf, 281.0),
                Pt(-lTopHalf, lTopHalf, 281.0)
            };
            for (int k = 0; k < 4; k++)
            {
                AddBeam(lTopPts[k], lTopPts[(k + 1) % 4], lTopDeck, symChannelBelt);
                Wall.Create(doc, Line.CreateBound(lTopPts[k], lTopPts[(k + 1) % 4]), wtBalustrade.Id, lTopDeck.Id, MToFt(2.5), 0, false, false);
                totalWalls++;
            }

            // ========================================================
            // 5.10 Dome Cupola, Rotating Beacon & Antenna Spire (+281.0m to +312.0m)
            // ========================================================
            try
            {
                List<GeometryObject> spireGeoms = new();

                // 1. Cupola / Dome (Z = 281.0 -> 288.0m): Lofted segmented dome
                List<CurveLoop> domeLoops = new();
                double[] domeZs = new[] { 281.0, 283.5, 286.0, 288.0 };
                double[] domeRs = new[] { 3.6, 3.2, 2.4, 1.2 };
                for (int i = 0; i < domeZs.Length; i++)
                {
                    domeLoops.Add(CreatePolygonLoop(0, 0, domeRs[i], 16, domeZs[i], true));
                }
                SolidOptions sOpt = new(matGold.Id, ElementId.InvalidElementId);
                Solid domeSolid = GeometryCreationUtilities.CreateLoftGeometry(domeLoops, sOpt);
                if (domeSolid != null) spireGeoms.Add(domeSolid);

                // 2. Rotating Beacon Housing (Phare de la Tour Eiffel at Z = 288.0m -> 291.0m)
                List<CurveLoop> beaconLoops = new();
                double[] bZs = new[] { 288.0, 289.5, 291.0 };
                double[] bRs = new[] { 1.2, 1.5, 1.0 };
                for (int i = 0; i < bZs.Length; i++)
                {
                    beaconLoops.Add(CreatePolygonLoop(0, 0, bRs[i], 12, bZs[i], true));
                }
                Solid beaconSolid = GeometryCreationUtilities.CreateLoftGeometry(beaconLoops, sOpt);
                if (beaconSolid != null) spireGeoms.Add(beaconSolid);

                // 3. Tapered Antenna Spire (Z = 291.0m -> 312.0m, Height 21m)
                List<CurveLoop> mastLoops = new();
                double[] mZs = new[] { 291.0, 298.0, 305.0, 312.0 };
                double[] mRs = new[] { 0.85, 0.50, 0.25, 0.08 };
                for (int i = 0; i < mZs.Length; i++)
                {
                    mastLoops.Add(CreatePolygonLoop(0, 0, mRs[i], 8, mZs[i], true));
                }
                Solid mastSolid = GeometryCreationUtilities.CreateLoftGeometry(mastLoops, sOpt);
                if (mastSolid != null) spireGeoms.Add(mastSolid);

                if (spireGeoms.Count > 0)
                {
                    DirectShape dsSpire = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    dsSpire.SetShape(spireGeoms);
                    dsSpire.Name = "Kubah Phare & Spire Antena Menara Eiffel (+312.00m)";
                    Parameter? pMat = dsSpire.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                    if (pMat != null && !pMat.IsReadOnly) pMat.Set(matGold.Id);
                }
            }
            catch { }

            // ========================================================
            // 5.11 Set 3D Views & Camera Setup
            // ========================================================
            foreach (var v3d in new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>())
            {
                if (!v3d.IsTemplate)
                {
                    try
                    {
                        v3d.DisplayStyle = DisplayStyle.Shading;
                        v3d.DetailLevel = ViewDetailLevel.Fine;
                        v3d.CropBoxActive = false;
                        v3d.CropBoxVisible = false;
                        v3d.IsSectionBoxActive = false;
                    }
                    catch { }
                }
            }

            doc.Regenerate();
            txBuild.Commit();
        }

        return JsonSerializer.Serialize(new
        {
            status = "Success",
            model = "Tour Eiffel (Eiffel Tower, Paris) - Authentic Puddle Iron Lattice",
            material_specification = "Besi Tempa Puddle (Fer Puddlé des forges de Pompey, 1887)",
            total_height_m = 312.0,
            base_footprint_m = "125m x 125m (Champ de Mars 240m x 240m)",
            total_floors = totalFloors,
            total_walls = totalWalls,
            total_framing = totalFraming,
            authentic_profiles_used = new[]
            {
                "Cornière L-Angle 200x200x20mm (Kaki Utama Arbalétriers & Poros Menara)",
                "Cornière L-Angle 120x120x12mm (Pengaku Diagonal Kisi X-Bracing & Rangka Busur)",
                "Profil Kanal U 300x100mm (Balok Sabuk Pengikat Belt Truss & Perimeter Dek)",
                "Double Kanal U 250x80mm (Intrados & Extrados 4 Busur Monumental)"
            },
            materials = new[]
            {
                "Besi Tempa Puddle - Fer Puddlé Tour Eiffel (Pompey)",
                "Lansekap - Rumput Hijau Champ de Mars",
                "Perkerasan - Paving Esplanade Eiffel",
                "Struktur - Batu Andesit Socle",
                "Kaca - Paviliun & Balustrade Eiffel",
                "Puncak - Kubah Emas & Phare Eiffel"
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static FamilySymbol? LoadOrGetFramingSymbol(Document doc, string rfaPath, string familyName, string preferredTypeName)
    {
        FamilySymbol? sym = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .Cast<FamilySymbol>()
            .FirstOrDefault(s => s.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase)
                              && (string.IsNullOrEmpty(preferredTypeName) || s.Name.Equals(preferredTypeName, StringComparison.OrdinalIgnoreCase)));
        if (sym != null)
        {
            if (!sym.IsActive) sym.Activate();
            return sym;
        }

        if (File.Exists(rfaPath))
        {
            try
            {
                if (doc.LoadFamily(rfaPath, out Family fam))
                {
                    foreach (ElementId id in fam.GetFamilySymbolIds())
                    {
                        if (doc.GetElement(id) is FamilySymbol fs)
                        {
                            if (string.IsNullOrEmpty(preferredTypeName) || fs.Name.Equals(preferredTypeName, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!fs.IsActive) fs.Activate();
                                return fs;
                            }
                        }
                    }
                    var firstId = fam.GetFamilySymbolIds().FirstOrDefault();
                    if (firstId != null && doc.GetElement(firstId) is FamilySymbol firstFs)
                    {
                        if (!firstFs.IsActive) firstFs.Activate();
                        return firstFs;
                    }
                }
            }
            catch { }
        }

        return new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .Cast<FamilySymbol>()
            .FirstOrDefault();
    }

    private static FamilySymbol CreateOrGetDuplicateSymbol(Document doc, FamilySymbol baseSymbol, string newName)
    {
        FamilySymbol? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .Cast<FamilySymbol>()
            .FirstOrDefault(s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        return (FamilySymbol)baseSymbol.Duplicate(newName);
    }

    private static CurveLoop CreateRectLoop(double halfW, double halfH, double cxM, double cyM, double zM, bool ccw)
    {
        CurveLoop loop = new();
        XYZ p1 = Pt(cxM - halfW, cyM - halfH, zM);
        XYZ p2 = Pt(cxM + halfW, cyM - halfH, zM);
        XYZ p3 = Pt(cxM + halfW, cyM + halfH, zM);
        XYZ p4 = Pt(cxM - halfW, cyM + halfH, zM);

        List<XYZ> pts = new() { p1, p2, p3, p4 };
        if (!ccw)
        {
            pts.Reverse();
        }

        for (int i = 0; i < pts.Count; i++)
        {
            loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
        }
        return loop;
    }

    private static CurveLoop CreatePolygonLoop(double cxM, double cyM, double rM, int segments, double zM, bool ccw)
    {
        CurveLoop loop = new();
        List<XYZ> pts = new();
        for (int i = 0; i < segments; i++)
        {
            double theta = 2.0 * Math.PI * i / segments;
            pts.Add(Pt(cxM + rM * Math.Cos(theta), cyM + rM * Math.Sin(theta), zM));
        }

        if (!ccw)
        {
            pts.Reverse();
        }

        for (int i = 0; i < segments; i++)
        {
            loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % segments]));
        }
        return loop;
    }
}

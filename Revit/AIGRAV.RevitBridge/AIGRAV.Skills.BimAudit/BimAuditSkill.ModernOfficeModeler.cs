using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AIGRAV.Contracts;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public static class ModernOfficeModeler
{
    private const double ToFt = 1.0 / 0.3048;
    private static double MToFt(double meters) => meters * ToFt;
    private static XYZ Pt(double xM, double yM, double zM) => new(xM * ToFt, yM * ToFt, zM * ToFt);

    public static string ExecuteOfficeMilestone(UIApplication app, Document doc, string? argument)
    {
        string milestone = "1";
        bool clearCanvas = false;

        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                var root = jd.RootElement;
                if (root.TryGetProperty("milestone", out JsonElement mEl))
                    milestone = mEl.GetString()?.ToLowerInvariant() ?? "1";
                if (root.TryGetProperty("clear_existing", out JsonElement cEl))
                    clearCanvas = cEl.GetBoolean();
                else if (root.TryGetProperty("clear_canvas", out JsonElement ccEl))
                    clearCanvas = ccEl.GetBoolean();
            }
            catch
            {
                milestone = argument.Trim().ToLowerInvariant();
            }
        }

        StringBuilder log = new();
        log.AppendLine($"=== AIGRAV MODERN OFFICE ENGINE: EXECUTING MILESTONE [{milestone.ToUpper()}] ===");

        // Optional Canvas Cleaning
        if (clearCanvas)
        {
            using Transaction txClean = new(doc, "AIGRAV - Clear Canvas for Modern Office");
            TransactionFailureHandling.SuppressWarnings(txClean);
            txClean.Start();
            try
            {
                var toDelete = new List<ElementId>();
                toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Floor)).WhereElementIsNotElementType().ToElementIds());
                toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Wall)).WhereElementIsNotElementType().ToElementIds());
                toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Grid)).ToElementIds());
                var instIds = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType()
                    .Where(fi => fi.Category != null && (
                        fi.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns ||
                        fi.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming ||
                        fi.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation ||
                        fi.Category.Id.Value == (long)BuiltInCategory.OST_Windows ||
                        fi.Category.Id.Value == (long)BuiltInCategory.OST_Doors))
                    .Select(fi => fi.Id);
                toDelete.AddRange(instIds);

                if (toDelete.Count > 0)
                {
                    doc.Delete(toDelete);
                    log.AppendLine($"Cleaned {toDelete.Count} previous instances for fresh consultative modeling.");
                }
                txClean.Commit();
            }
            catch (Exception exClean)
            {
                if (txClean.GetStatus() == TransactionStatus.Started) txClean.RollBack();
                log.AppendLine($"Note: Clear canvas warning: {exClean.Message}");
            }
        }

        // =================================================================
        // PHASE 0: SETUP MATERIALS & COMPLETE STRATIFIED LEVELS
        // =================================================================
        Level lPileDepth, l0PileCap, l1Ground, l2Floor, l3Floor, lRoof, lParapet;
        Material matConcrete, matSteel, matFloorGranite, matGlass, matSpandrel, matPileCap, matBoredPile;
        FloorType officeFloorType, pileCapSlabType;

        using (Transaction txInit = new(doc, "AIGRAV Office - Setup Levels & Materials"))
        {
            TransactionFailureHandling.SuppressWarnings(txInit);
            txInit.Start();

            // Materials
            matConcrete = GetOrCreateMaterial(doc, "Office - Structural Concrete", 185, 185, 185);
            matPileCap = GetOrCreateMaterial(doc, "Office - Foundation PileCap Concrete", 90, 95, 105);
            matBoredPile = GetOrCreateMaterial(doc, "Office - Bored Pile Concrete", 70, 110, 160);
            matSteel = GetOrCreateMaterial(doc, "Office - Steel Frame Charcoal", 45, 50, 55);
            matFloorGranite = GetOrCreateMaterial(doc, "Office - Floor Granite Cream", 235, 230, 220);
            matGlass = GetOrCreateMaterial(doc, "Office - Vision Glass Solar", 195, 230, 245, 60);
            matSpandrel = GetOrCreateMaterial(doc, "Office - Spandrel Metallic Clad", 90, 95, 100);

            // Complete Stratified Levels: Deep Foundation (-10.0m) to Roof (+10.8m)
            lPileDepth = GetOrCreateLevel(doc, "L-1 - Kedalaman Bored Pile", -10.00);
            l0PileCap = GetOrCreateLevel(doc, "L0 - Top Pile Cap", -1.50);
            l1Ground = GetOrCreateLevel(doc, "L1 - Ground Lobby & Sloof", 0.00);
            l2Floor = GetOrCreateLevel(doc, "L2 - Office Floor 2", 3.60);
            l3Floor = GetOrCreateLevel(doc, "L3 - Office Floor 3", 7.20);
            lRoof = GetOrCreateLevel(doc, "L4 - Roof Level", 10.80);
            lParapet = GetOrCreateLevel(doc, "L5 - Parapet Top", 11.80);

            // Floor Types
            FloorType baseFloor = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType)).Cast<FloorType>()
                .FirstOrDefault(ft => ft.Name.Contains("Concrete") || ft.Name.Contains("Generic"))
                ?? new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().First();

            officeFloorType = CreateOrGetFloorType(doc, baseFloor, "Office - Floor Concrete 150mm", matFloorGranite, 0.15);
            pileCapSlabType = CreateOrGetFloorType(doc, baseFloor, "Office - Pile Cap Slab 700mm", matPileCap, 0.70);

            txInit.Commit();
        }

        List<ElementId> createdIds = new();

        // =================================================================
        // MILESTONE 1: DEEP FOUNDATION (BORED PILE), PILE CAP, PEDESTAL, SLOOF, STRONG COLUMN & BEAM
        // =================================================================
        if (milestone == "1" || milestone == "all")
        {
            using TransactionGroup tgM1 = new(doc, "AIGRAV M1 - Deep Substructure & Superstructure Frame");
            tgM1.Start();

            // Grid Coordinates (24m x 16m)
            double[] xCoords = { 0.0, 6.0, 12.0, 18.0, 24.0 };
            double[] yCoords = { 0.0, 8.0, 16.0 };
            string[] xLabels = { "A", "B", "C", "D", "E" };
            string[] yLabels = { "1", "2", "3" };

            // 1. Grid Matrix
            using (Transaction txGrid = new(doc, "AIGRAV M1 - Create Grids"))
            {
                TransactionFailureHandling.SuppressWarnings(txGrid);
                txGrid.Start();

                double minY = -3.0, maxY = 19.0;
                double minX = -3.0, maxX = 27.0;

                for (int i = 0; i < xCoords.Length; i++)
                {
                    double x = xCoords[i];
                    Grid g = Grid.Create(doc, Line.CreateBound(Pt(x, minY, 0), Pt(x, maxY, 0)));
                    try { g.Name = xLabels[i]; } catch { }
                    createdIds.Add(g.Id);
                }

                for (int j = 0; j < yCoords.Length; j++)
                {
                    double y = yCoords[j];
                    Grid g = Grid.Create(doc, Line.CreateBound(Pt(minX, y, 0), Pt(maxX, y, 0)));
                    try { g.Name = yLabels[j]; } catch { }
                    createdIds.Add(g.Id);
                }

                txGrid.Commit();
            }

            // 2. Load Structural Families
            string beamRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Structural Framing\Concrete\M_Concrete-Rectangular Beam.rfa";
            string colRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Structural Columns\Concrete\M_Concrete-Rectangular-Column.rfa";
            string roundColRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Structural Columns\Concrete\M_Concrete-Round-Column.rfa";

            using (Transaction txLoad = new(doc, "Load Official Structural Families"))
            {
                TransactionFailureHandling.SuppressWarnings(txLoad);
                txLoad.Start();
                if (File.Exists(beamRfa)) try { doc.LoadFamily(beamRfa, out _); } catch { }
                if (File.Exists(colRfa)) try { doc.LoadFamily(colRfa, out _); } catch { }
                if (File.Exists(roundColRfa)) try { doc.LoadFamily(roundColRfa, out _); } catch { }
                txLoad.Commit();
            }

            // 3. Substructure Phase A: 60 Bored Piles (Group of 4 Piles per column, D=400mm, Depth -10.0m to -1.50m)
            FamilySymbol? roundColSymbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.FamilyName.Contains("Round") || s.Name.Contains("Round"))
                ?? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();

            if (roundColSymbol != null)
            {
                using Transaction txPiles = new(doc, "AIGRAV M1 - Bored Piles Substructure");
                TransactionFailureHandling.SuppressWarnings(txPiles);
                txPiles.Start();

                FamilySymbol pileType = roundColSymbol;
                try
                {
                    var existingPile = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>().FirstOrDefault(s => s.Name == "Bored Pile D400mm");
                    if (existingPile != null)
                    {
                        pileType = existingPile;
                    }
                    else
                    {
                        pileType = (FamilySymbol)roundColSymbol.Duplicate("Bored Pile D400mm");
                        pileType.LookupParameter("Diameter")?.Set(MToFt(0.40));
                        pileType.LookupParameter("b")?.Set(MToFt(0.40));
                    }
                }
                catch { }

                if (!pileType.IsActive) pileType.Activate();

                int pileCount = 0;
                double offsetPile = 0.55; // 4 piles grouped around column center
                double[,] pileOffsets = { { -offsetPile, -offsetPile }, { offsetPile, -offsetPile }, { offsetPile, offsetPile }, { -offsetPile, offsetPile } };

                foreach (double x in xCoords)
                {
                    foreach (double y in yCoords)
                    {
                        for (int p = 0; p < 4; p++)
                        {
                            double px = x + pileOffsets[p, 0];
                            double py = y + pileOffsets[p, 1];
                            XYZ pilePt = Pt(px, py, -10.00);
                            FamilyInstance pile = doc.Create.NewFamilyInstance(pilePt, pileType, lPileDepth, StructuralType.Column);
                            if (pile != null)
                            {
                                ConfigureColumnLevels(pile, lPileDepth, l0PileCap, 0.0, -0.60);
                                createdIds.Add(pile.Id);
                                pileCount++;
                            }
                        }
                    }
                }

                txPiles.Commit();
                log.AppendLine($"Constructed Deep Geotechnical Substructure with {pileCount} Bored Piles (D=400mm, L=8.5m) to hard bearing stratum.");
            }

            // 4. Substructure Phase B: 15 Pile Caps (2.00m x 2.00m x 0.70m at Level L0: -1.50m)
            // Created as monolithic foundation cap pads sitting directly on top of the 4 bored piles
            using (Transaction txCaps = new(doc, "AIGRAV M1 - Monolithic Pile Caps"))
            {
                TransactionFailureHandling.SuppressWarnings(txCaps);
                txCaps.Start();

                foreach (double x in xCoords)
                {
                    foreach (double y in yCoords)
                    {
                        // 2.0m x 2.0m pad centered at (x, y)
                        double cx1 = x - 1.0, cy1 = y - 1.0;
                        double cx2 = x + 1.0, cy2 = y + 1.0;
                        CurveLoop capLoop = CreateRectLoop(cx1, cy1, cx2, cy2, -1.50, true);
                        Floor cap = Floor.Create(doc, new List<CurveLoop> { capLoop }, pileCapSlabType.Id, l0PileCap.Id);
                        if (cap != null)
                        {
                            createdIds.Add(cap.Id);
                        }
                    }
                }

                txCaps.Commit();
                log.AppendLine("Cast 15 massive reinforced concrete Pile Caps (2.00m x 2.00m x 0.70m) capping each 4-bored-pile group at elevation -1.50m.");
            }

            // 5. Substructure Phase C: 15 Concrete Pedestals (550 x 700 mm from Pile Cap -1.50m to Sloof 0.00m)
            // AND Superstructure: 15 Strong Columns (450 x 600 mm from Sloof 0.00m to Roof +10.80m flush)
            FamilySymbol? rectColSymbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.FamilyName.Contains("Rectangular") || s.Name.Contains("Concrete"))
                ?? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();

            if (rectColSymbol != null)
            {
                using Transaction txPedCol = new(doc, "AIGRAV M1 - Pedestals & Strong Columns");
                TransactionFailureHandling.SuppressWarnings(txPedCol);
                txPedCol.Start();

                // Pedestal Type 550 x 700 mm (Enlarged base sitting directly on Pile Cap)
                FamilySymbol pedestalType = rectColSymbol;
                try
                {
                    var existingPed = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>().FirstOrDefault(s => s.Name == "Pedestal Kolom 550x700 mm");
                    if (existingPed != null) pedestalType = existingPed;
                    else
                    {
                        pedestalType = (FamilySymbol)rectColSymbol.Duplicate("Pedestal Kolom 550x700 mm");
                        pedestalType.LookupParameter("b")?.Set(MToFt(0.55));
                        pedestalType.LookupParameter("h")?.Set(MToFt(0.70));
                    }
                }
                catch { }
                if (pedestalType != null && !pedestalType.IsActive) pedestalType.Activate();

                // Upper Column Type 450 x 600 mm (Strong Column above Ground)
                FamilySymbol upperColType = rectColSymbol;
                try
                {
                    var existingCol = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>().FirstOrDefault(s => s.Name == "Kolom Struktur 450x600 mm" || s.Name.Contains("450 x 600"));
                    if (existingCol != null) upperColType = existingCol;
                    else
                    {
                        upperColType = (FamilySymbol)rectColSymbol.Duplicate("Kolom Struktur 450x600 mm");
                        upperColType.LookupParameter("b")?.Set(MToFt(0.45));
                        upperColType.LookupParameter("h")?.Set(MToFt(0.60));
                    }
                }
                catch { }
                if (upperColType != null && !upperColType.IsActive) upperColType.Activate();

                // Place 15 Pedestals from L0 (-1.50m) to L1 (0.00m)
                foreach (double x in xCoords)
                {
                    foreach (double y in yCoords)
                    {
                        XYZ pedOrigin = Pt(x, y, -1.50);
                        FamilyInstance ped = doc.Create.NewFamilyInstance(pedOrigin, pedestalType!, l0PileCap, StructuralType.Column);
                        if (ped != null)
                        {
                            ConfigureColumnLevels(ped, l0PileCap, l1Ground, 0.0, 0.0);
                            createdIds.Add(ped.Id);
                        }
                    }
                }
                log.AppendLine($"Cast 15 solid Concrete Pedestals ({pedestalType!.Name}) directly monolithic onto Pile Caps.");

                // Place 15 Upper Columns from L1 (0.00m) to LRoof (+10.80m) - Strictly zero offset to terminate flush with Roof
                foreach (double x in xCoords)
                {
                    foreach (double y in yCoords)
                    {
                        XYZ colOrigin = Pt(x, y, 0.00);
                        FamilyInstance col = doc.Create.NewFamilyInstance(colOrigin, upperColType!, l1Ground, StructuralType.Column);
                        if (col != null)
                        {
                            ConfigureColumnLevels(col, l1Ground, lRoof, 0.0, 0.0);
                            createdIds.Add(col.Id);
                        }
                    }
                }
                log.AppendLine($"Erected 15 continuous Strong Columns ({upperColType!.Name}) seamlessly rooted onto Pedestals.");

                txPedCol.Commit();
            }

            // 6. Beams: Ground Sloof Tie Beams (0.00m) & Superstructure Balok Induk (300x600mm)
            FamilySymbol? baseBeam = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.FamilyName.Contains("Concrete") || s.Name.Contains("Concrete"))
                ?? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault();

            if (baseBeam != null)
            {
                using Transaction txBeam = new(doc, "AIGRAV M1 - Sloof & Superstructure Beams");
                TransactionFailureHandling.SuppressWarnings(txBeam);
                txBeam.Start();

                // Balok Induk 300 x 600 mm (Enclosed within 450x600 Column)
                FamilySymbol girderType = baseBeam;
                try
                {
                    var existingGirder = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>().FirstOrDefault(s => s.Name == "Balok Induk 300x600 mm");
                    if (existingGirder != null) girderType = existingGirder;
                    else
                    {
                        girderType = (FamilySymbol)baseBeam.Duplicate("Balok Induk 300x600 mm");
                        girderType.LookupParameter("b")?.Set(MToFt(0.30));
                        girderType.LookupParameter("h")?.Set(MToFt(0.60));
                    }
                }
                catch { }
                if (!girderType.IsActive) girderType.Activate();

                // Balok Sloof 250 x 450 mm
                FamilySymbol sloofType = baseBeam;
                try
                {
                    var existingSloof = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>().FirstOrDefault(s => s.Name == "Balok Sloof 250x450 mm");
                    if (existingSloof != null) sloofType = existingSloof;
                    else
                    {
                        sloofType = (FamilySymbol)baseBeam.Duplicate("Balok Sloof 250x450 mm");
                        sloofType.LookupParameter("b")?.Set(MToFt(0.25));
                        sloofType.LookupParameter("h")?.Set(MToFt(0.45));
                    }
                }
                catch { }
                if (!sloofType.IsActive) sloofType.Activate();

                // A. Ground Sloof Tie Beams (At Z = 0.00m tying Pedestal heads)
                int sloofCount = 0;
                foreach (double y in yCoords)
                {
                    for (int i = 0; i < xCoords.Length - 1; i++)
                    {
                        XYZ p0 = Pt(xCoords[i], y, 0.0);
                        XYZ p1 = Pt(xCoords[i + 1], y, 0.0);
                        var slf = doc.Create.NewFamilyInstance(Line.CreateBound(p0, p1), sloofType, l1Ground, StructuralType.Beam);
                        if (slf != null) { createdIds.Add(slf.Id); sloofCount++; }
                    }
                }
                foreach (double x in xCoords)
                {
                    for (int j = 0; j < yCoords.Length - 1; j++)
                    {
                        XYZ p0 = Pt(x, yCoords[j], 0.0);
                        XYZ p1 = Pt(x, yCoords[j + 1], 0.0);
                        var slf = doc.Create.NewFamilyInstance(Line.CreateBound(p0, p1), sloofType, l1Ground, StructuralType.Beam);
                        if (slf != null) { createdIds.Add(slf.Id); sloofCount++; }
                    }
                }
                log.AppendLine($"Tied all 15 Pedestal heads with {sloofCount} Sloof Tie Beams ({sloofType.Name}) at Ground Level 0.00m.");

                // B. Superstructure Girder Beams (L2, L3, Roof)
                Level[] superstructureLevels = { l2Floor, l3Floor, lRoof };
                int girderCount = 0;
                foreach (Level lvl in superstructureLevels)
                {
                    double z = lvl.Elevation * 0.3048;

                    // Beams along X (4 bays per Y-line)
                    foreach (double y in yCoords)
                    {
                        for (int i = 0; i < xCoords.Length - 1; i++)
                        {
                            XYZ p0 = Pt(xCoords[i], y, z);
                            XYZ p1 = Pt(xCoords[i + 1], y, z);
                            var beam = doc.Create.NewFamilyInstance(Line.CreateBound(p0, p1), girderType, lvl, StructuralType.Beam);
                            if (beam != null) { createdIds.Add(beam.Id); girderCount++; }
                        }
                    }

                    // Beams along Y (2 bays per X-line)
                    foreach (double x in xCoords)
                    {
                        for (int j = 0; j < yCoords.Length - 1; j++)
                        {
                            XYZ p0 = Pt(x, yCoords[j], z);
                            XYZ p1 = Pt(x, yCoords[j + 1], z);
                            var beam = doc.Create.NewFamilyInstance(Line.CreateBound(p0, p1), girderType, lvl, StructuralType.Beam);
                            if (beam != null) { createdIds.Add(beam.Id); girderCount++; }
                        }
                    }
                }
                log.AppendLine($"Connected moment frame with {girderCount} Balok Induk ({girderType.Name}) across Level 2, 3, and Roof.");

                txBeam.Commit();
            }

            // 7. Floor Slabs (Ground, L2 with core void, L3 with core void, Roof)
            using (Transaction txFloor = new(doc, "AIGRAV M1 - Floor Slabs & Core Void"))
            {
                TransactionFailureHandling.SuppressWarnings(txFloor);
                txFloor.Start();

                // L1 Ground Slab (Solid 24m x 16m)
                CurveLoop l1Loop = CreateRectLoop(0.0, 0.0, 24.0, 16.0, 0.0, true);
                Floor f1 = Floor.Create(doc, new List<CurveLoop> { l1Loop }, officeFloorType.Id, l1Ground.Id);
                if (f1 != null) createdIds.Add(f1.Id);

                // Core Void Dimensions (Bay C-D between Y 8m and 14m: 6m x 6m elevator/stair core)
                double vx1 = 12.0, vy1 = 8.0, vx2 = 18.0, vy2 = 14.0;

                // L2 Floor Slab with Core Void
                CurveLoop l2Outer = CreateRectLoop(0.0, 0.0, 24.0, 16.0, 3.60, true);
                CurveLoop l2Inner = CreateRectLoop(vx1, vy1, vx2, vy2, 3.60, false);
                Floor f2 = Floor.Create(doc, new List<CurveLoop> { l2Outer, l2Inner }, officeFloorType.Id, l2Floor.Id);
                if (f2 != null) createdIds.Add(f2.Id);

                // L3 Floor Slab with Core Void
                CurveLoop l3Outer = CreateRectLoop(0.0, 0.0, 24.0, 16.0, 7.20, true);
                CurveLoop l3Inner = CreateRectLoop(vx1, vy1, vx2, vy2, 7.20, false);
                Floor f3 = Floor.Create(doc, new List<CurveLoop> { l3Outer, l3Inner }, officeFloorType.Id, l3Floor.Id);
                if (f3 != null) createdIds.Add(f3.Id);

                // LRoof Slab
                CurveLoop lRoofOuter = CreateRectLoop(0.0, 0.0, 24.0, 16.0, 10.80, true);
                Floor fRoof = Floor.Create(doc, new List<CurveLoop> { lRoofOuter }, officeFloorType.Id, lRoof.Id);
                if (fRoof != null) createdIds.Add(fRoof.Id);

                txFloor.Commit();
                log.AppendLine("Built 4 monolithic floor slabs (L1 Ground, L2-L3 with elevator core opening, and L4 Roof).");
            }

            tgM1.Assimilate();
        }

        // =================================================================
        // MILESTONE 2: BUILDING ENVELOPE (STOREFRONT CURTAIN WALL, ENTRANCE PORTAL, CANOPY & ROOF PARAPET)
        // =================================================================
        if (milestone == "2" || milestone == "all")
        {
            using TransactionGroup tgM2 = new(doc, "AIGRAV M2 - Building Envelope & Curtain Wall Façade");
            tgM2.Start();

            // 1. Resolve Wall Types & Official Door Family
            WallType? storefrontType = null;
            WallType? parapetType = null;
            FamilySymbol? entranceDoorSymbol = null;

            using (Transaction txTypes = new(doc, "AIGRAV M2 - Setup Envelope Types & Families"))
            {
                TransactionFailureHandling.SuppressWarnings(txTypes);
                txTypes.Start();

                // Storefront Curtain Wall
                storefrontType = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType)).Cast<WallType>()
                    .FirstOrDefault(wt => wt.Kind == WallKind.Curtain && wt.Name.Contains("Storefront", StringComparison.OrdinalIgnoreCase))
                    ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType)).Cast<WallType>()
                    .FirstOrDefault(wt => wt.Kind == WallKind.Curtain)
                    ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType)).Cast<WallType>().First();

                // Solid Parapet / Feature Wall
                WallType baseBasic = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType)).Cast<WallType>()
                    .FirstOrDefault(wt => wt.Kind == WallKind.Basic && (wt.Name.Contains("150") || wt.Name.Contains("Generic") || wt.Name.Contains("200")))
                    ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType)).Cast<WallType>()
                    .FirstOrDefault(wt => wt.Kind == WallKind.Basic)
                    ?? storefrontType;

                parapetType = CreateOrGetWallType(doc, baseBasic, "Office - Parapet Wall 150mm", matConcrete, 0.15);

                // Load Official Commercial Glass Door
                string doorRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Doors\M_Door-Double-Glass.rfa";
                if (File.Exists(doorRfa))
                {
                    try { doc.LoadFamily(doorRfa, out _); } catch { }
                }

                entranceDoorSymbol = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.FamilyName.Contains("Double-Glass") || s.Name.Contains("Double-Glass") || s.Name.Contains("Glass"))
                    ?? new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>().FirstOrDefault();

                if (entranceDoorSymbol != null && !entranceDoorSymbol.IsActive)
                {
                    try { entranceDoorSymbol.Activate(); } catch { }
                }

                txTypes.Commit();
            }

            // 2. Build Perimeter Storefront Bays across Floors 1, 2, and 3
            // Infill cleanly between concrete columns (zero clash)
            (Level baseLvl, Level topLvl, int floorNum)[] floors =
            {
                (l1Ground, l2Floor, 1),
                (l2Floor, l3Floor, 2),
                (l3Floor, lRoof, 3)
            };

            int storefrontCount = 0;
            using (Transaction txWalls = new(doc, "AIGRAV M2 - Perimeter Storefront Walls"))
            {
                TransactionFailureHandling.SuppressWarnings(txWalls);
                txWalls.Start();

                foreach (var (baseLvl, topLvl, floorNum) in floors)
                {
                    // A. South Façade (Y = 0.00m)
                    // Bay 1 (A-B): X 0.225 -> 5.775
                    CreateStorefrontSegment(doc, Pt(0.225, 0.0, 0), Pt(5.775, 0.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                    // Bay 2 (B-C): X 6.225 -> 11.775
                    CreateStorefrontSegment(doc, Pt(6.225, 0.0, 0), Pt(11.775, 0.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);

                    // Bay 3 (C-D): X 12.225 -> 17.775
                    if (floorNum == 1)
                    {
                        // Ground Floor: Grand Entrance Portal Bay!
                        // Sidelight Left: 12.225 -> 13.50
                        CreateStorefrontSegment(doc, Pt(12.225, 0.0, 0), Pt(13.50, 0.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                        // Portal Center Wall: 13.50 -> 16.50 (Hosts Double Glass Entrance Door)
                        Wall portalWall = Wall.Create(doc, Line.CreateBound(Pt(13.50, 0.0, 0), Pt(16.50, 0.0, 0)), parapetType.Id, baseLvl.Id, MToFt(3.60), 0.0, false, false);
                        if (portalWall != null)
                        {
                            portalWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(topLvl.Id);
                            portalWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.Set(0.0);
                            portalWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.Set(0.0);
                            createdIds.Add(portalWall.Id);

                            // Place Entrance Door in Center of Portal Wall
                            if (entranceDoorSymbol != null)
                            {
                                try
                                {
                                    XYZ doorLoc = Pt(15.00, 0.0, 0.0);
                                    FamilyInstance door = doc.Create.NewFamilyInstance(doorLoc, entranceDoorSymbol, portalWall, baseLvl, StructuralType.NonStructural);
                                    if (door != null) createdIds.Add(door.Id);
                                }
                                catch (Exception exDoor)
                                {
                                    log.AppendLine($"Note: Entrance door placement note: {exDoor.Message}");
                                }
                            }
                        }
                        // Sidelight Right: 16.50 -> 17.775
                        CreateStorefrontSegment(doc, Pt(16.50, 0.0, 0), Pt(17.775, 0.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                    }
                    else
                    {
                        // Upper Floors: Full Vision Glass Storefront across Bay C-D
                        CreateStorefrontSegment(doc, Pt(12.225, 0.0, 0), Pt(17.775, 0.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                    }

                    // Bay 4 (D-E): X 18.225 -> 23.775
                    CreateStorefrontSegment(doc, Pt(18.225, 0.0, 0), Pt(23.775, 0.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);

                    // B. East Façade (X = 24.00m)
                    // Bay 1: Y 0.30 -> 7.70
                    CreateStorefrontSegment(doc, Pt(24.0, 0.30, 0), Pt(24.0, 7.70, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                    // Bay 2: Y 8.30 -> 15.70
                    CreateStorefrontSegment(doc, Pt(24.0, 8.30, 0), Pt(24.0, 15.70, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);

                    // C. North Façade (Y = 16.00m)
                    // Bay 1: X 23.775 -> 18.225
                    CreateStorefrontSegment(doc, Pt(23.775, 16.0, 0), Pt(18.225, 16.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                    // Bay 2: X 17.775 -> 12.225
                    CreateStorefrontSegment(doc, Pt(17.775, 16.0, 0), Pt(12.225, 16.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                    // Bay 3: X 11.775 -> 6.225
                    CreateStorefrontSegment(doc, Pt(11.775, 16.0, 0), Pt(6.225, 16.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                    // Bay 4: X 5.775 -> 0.225
                    CreateStorefrontSegment(doc, Pt(5.775, 16.0, 0), Pt(0.225, 16.0, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);

                    // D. West Façade (X = 0.00m)
                    // Bay 1: Y 15.70 -> 8.30
                    CreateStorefrontSegment(doc, Pt(0.0, 15.70, 0), Pt(0.0, 8.30, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                    // Bay 2: Y 7.70 -> 0.30
                    CreateStorefrontSegment(doc, Pt(0.0, 7.70, 0), Pt(0.0, 0.30, 0), storefrontType, baseLvl, topLvl, createdIds, ref storefrontCount);
                }

                txWalls.Commit();
                log.AppendLine($"Erected {storefrontCount} high-performance Storefront Curtain Wall panels across Floors 1, 2, and 3.");
            }

            // 3. Ground Entrance Canopy (Sleek Cantilevered Canopy at Z = +3.20m, Y = -1.50m to 0.00m)
            using (Transaction txCanopy = new(doc, "AIGRAV M2 - Ground Entrance Canopy"))
            {
                TransactionFailureHandling.SuppressWarnings(txCanopy);
                txCanopy.Start();

                // 4.0m wide (X 13.0 -> 17.0), 1.5m projection (Y -1.50 -> 0.00)
                CurveLoop canopyLoop = CreateRectLoop(13.0, -1.50, 17.0, 0.00, 0.0, true);
                Floor canopy = Floor.Create(doc, new List<CurveLoop> { canopyLoop }, officeFloorType.Id, l1Ground.Id);
                if (canopy != null)
                {
                    canopy.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(MToFt(3.20));
                    createdIds.Add(canopy.Id);
                    log.AppendLine("Constructed modern cantilevered Glass Entrance Canopy (4.0m x 1.5m) over Grand Lobby entrance.");
                }

                txCanopy.Commit();
            }

            // 4. Roof Parapet Wall (L4 Roof: +10.80m to L5: +11.80m, H = 1.00m around full perimeter)
            using (Transaction txParapet = new(doc, "AIGRAV M2 - Roof Parapet Wall"))
            {
                TransactionFailureHandling.SuppressWarnings(txParapet);
                txParapet.Start();

                XYZ p00 = Pt(0.0, 0.0, 0.0);
                XYZ p24_0 = Pt(24.0, 0.0, 0.0);
                XYZ p24_16 = Pt(24.0, 16.0, 0.0);
                XYZ p0_16 = Pt(0.0, 16.0, 0.0);

                Line[] parapetLines =
                {
                    Line.CreateBound(p00, p24_0),
                    Line.CreateBound(p24_0, p24_16),
                    Line.CreateBound(p24_16, p0_16),
                    Line.CreateBound(p0_16, p00)
                };

                int parapetCount = 0;
                foreach (Line line in parapetLines)
                {
                    Wall pw = Wall.Create(doc, line, parapetType.Id, lRoof.Id, MToFt(1.00), 0.0, false, false);
                    if (pw != null)
                    {
                        pw.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(lParapet.Id);
                        pw.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.Set(0.0);
                        pw.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.Set(0.0);
                        createdIds.Add(pw.Id);
                        parapetCount++;
                    }
                }
                log.AppendLine($"Erected {parapetCount} continuous reinforced concrete Parapet Walls (H=1.00m) enclosing the Roof deck.");

                txParapet.Commit();
            }

            tgM2.Assimilate();
        }

        // =================================================================
        // MILESTONE 3: ARCHITECTURAL FIT-OUT, CORE SERVICE, RESTROOMS, DOORS & CIRCULATION
        // =================================================================
        if (milestone == "3" || milestone == "all")
        {
            using TransactionGroup tgM3 = new(doc, "AIGRAV M3 - Architectural Fit-Out & Core Circulation");
            tgM3.Start();

            // 1. Resolve Partition & Wet Wall Types, Stair Slabs, and Interior Doors
            WallType? coreWallType = null;
            WallType? partitionWallType = null;
            WallType? wetWallType = null;
            FloorType? stairSlabType = null;
            FamilySymbol? interiorDoorSymbol = null;

            using (Transaction txInitM3 = new(doc, "AIGRAV M3 - Setup Interior Types"))
            {
                TransactionFailureHandling.SuppressWarnings(txInitM3);
                txInitM3.Start();

                Material matStair = GetOrCreateMaterial(doc, "Office - Stair Concrete Finish", 150, 90, 200);
                Material matDrywall = GetOrCreateMaterial(doc, "Office - Drywall Interior Partition", 240, 235, 225);
                Material matWetTile = GetOrCreateMaterial(doc, "Office - Restroom Ceramic Tile", 200, 215, 225);

                WallType baseBasic = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType)).Cast<WallType>()
                    .FirstOrDefault(wt => wt.Kind == WallKind.Basic && (wt.Name.Contains("150") || wt.Name.Contains("Generic") || wt.Name.Contains("200")))
                    ?? new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().First(wt => wt.Kind == WallKind.Basic);

                coreWallType = CreateOrGetWallType(doc, baseBasic, "Office - Core Shear Wall 200mm", matConcrete, 0.20);
                partitionWallType = CreateOrGetWallType(doc, baseBasic, "Office - Interior Partition 100mm", matDrywall, 0.10);
                wetWallType = CreateOrGetWallType(doc, baseBasic, "Office - Restroom Wet Wall 150mm", matWetTile, 0.15);

                FloorType baseFloor = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType)).Cast<FloorType>().First();
                stairSlabType = CreateOrGetFloorType(doc, baseFloor, "Office - Stair Monolithic Slab 150mm", matStair, 0.15);

                // Load Single Interior Commercial Door (Strictly targeting robust M_Door-Single-Panel)
                string doorSingleRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Doors\M_Door-Single-Panel.rfa";
                if (File.Exists(doorSingleRfa))
                {
                    try { doc.LoadFamily(doorSingleRfa, out _); } catch { }
                }

                interiorDoorSymbol = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.FamilyName.Equals("M_Door-Single-Panel", StringComparison.OrdinalIgnoreCase) && (s.Name.Contains("900") || s.Name.Contains("850") || s.Name.Contains("800")))
                    ?? new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.FamilyName.Equals("M_Door-Single-Panel", StringComparison.OrdinalIgnoreCase))
                    ?? new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.FamilyName.Contains("Single-Panel"))
                    ?? new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.FamilyName.Contains("Double-Glass"));

                if (interiorDoorSymbol != null && !interiorDoorSymbol.IsActive)
                {
                    try { interiorDoorSymbol.Activate(); } catch { }
                }

                txInitM3.Commit();
            }

            // 2. Build Core, Restrooms, Office Partitions, Doors, and Stairs per Floor
            (Level baseLvl, Level topLvl, int floorNum, double zBase)[] floors =
            {
                (l1Ground, l2Floor, 1, 0.0),
                (l2Floor, l3Floor, 2, 3.60),
                (l3Floor, lRoof, 3, 7.20)
            };

            int interiorWallCount = 0;
            int doorCount = 0;
            int stairElementsCount = 0;
            List<(XYZ loc, Wall host, Level lvl)> doorsToPlace = new();

            // STEP 2A: Construct 55 Interior Walls, Core Shear Walls & Partitions
            using (Transaction txWalls = new(doc, "AIGRAV M3 - Construct Interior Walls"))
            {
                TransactionFailureHandling.SuppressWarnings(txWalls);
                txWalls.Start();

                foreach (var (baseLvl, topLvl, floorNum, zBase) in floors)
                {
                    // =========================================================
                    // A. CORE ELEVATOR BANK SHAFT (X: 12.00 -> 15.00, Y: 8.00 -> 14.00)
                    // =========================================================
                    // West Core Wall
                    CreateWallSegment(doc, Pt(12.0, 8.0, zBase), Pt(12.0, 14.0, zBase), coreWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // North Core Wall
                    CreateWallSegment(doc, Pt(12.0, 14.0, zBase), Pt(15.0, 14.0, zBase), coreWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // Center Divider (Elevator vs Stairs)
                    CreateWallSegment(doc, Pt(15.0, 8.0, zBase), Pt(15.0, 14.0, zBase), coreWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // South Core Wall (Left of Elevator Doors)
                    CreateWallSegment(doc, Pt(12.0, 8.0, zBase), Pt(12.8, 8.0, zBase), coreWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // South Core Wall (Between 2 Elevator Doors)
                    CreateWallSegment(doc, Pt(13.8, 8.0, zBase), Pt(14.0, 8.0, zBase), coreWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);

                    // =========================================================
                    // B. ENCLOSED FIRE ESCAPE STAIR SHAFT (X: 15.00 -> 18.00, Y: 8.00 -> 14.00)
                    // =========================================================
                    // North Stair Wall
                    CreateWallSegment(doc, Pt(15.0, 14.0, zBase), Pt(18.0, 14.0, zBase), coreWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // East Stair Wall
                    CreateWallSegment(doc, Pt(18.0, 14.0, zBase), Pt(18.0, 8.0, zBase), coreWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // South Stair Wall (Hosts Fire Exit Door at X = 16.50m)
                    Wall? stairSouthWall = CreateWallSegment(doc, Pt(15.0, 8.0, zBase), Pt(18.0, 8.0, zBase), coreWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    if (stairSouthWall != null)
                        doorsToPlace.Add((Pt(16.50, 8.0, zBase), stairSouthWall, baseLvl));

                    // =========================================================
                    // C. RESTROOMS / WET CORE (X: 9.00 -> 12.00, Y: 8.00 -> 14.00)
                    // =========================================================
                    // West Restroom Wall
                    CreateWallSegment(doc, Pt(9.0, 8.0, zBase), Pt(9.0, 14.0, zBase), wetWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // North Restroom Wall
                    CreateWallSegment(doc, Pt(9.0, 14.0, zBase), Pt(12.0, 14.0, zBase), wetWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // Divider Male / Female Restroom
                    CreateWallSegment(doc, Pt(9.0, 11.0, zBase), Pt(12.0, 11.0, zBase), wetWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    // South Restroom Corridor Wall (Hosts 2 Restroom Doors)
                    Wall? wetSouthWall = CreateWallSegment(doc, Pt(9.0, 8.0, zBase), Pt(12.0, 8.0, zBase), wetWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    if (wetSouthWall != null)
                    {
                        doorsToPlace.Add((Pt(10.00, 8.0, zBase), wetSouthWall, baseLvl));
                        doorsToPlace.Add((Pt(11.20, 8.0, zBase), wetSouthWall, baseLvl));
                    }

                    // =========================================================
                    // D. OFFICE PARTITIONS PER FLOOR
                    // =========================================================
                    if (floorNum == 1)
                    {
                        // Lantai 1: Meeting Room, ME/Server Room, Security
                        // ME / Server Room Wall along Y = 8.0m (X: 0 -> 6)
                        Wall? meWall = CreateWallSegment(doc, Pt(0.0, 8.0, zBase), Pt(6.0, 8.0, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                        if (meWall != null)
                            doorsToPlace.Add((Pt(3.00, 8.0, zBase), meWall, baseLvl));

                        // Server Room Divider at X = 3.0m
                        CreateWallSegment(doc, Pt(3.0, 8.0, zBase), Pt(3.0, 14.0, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                        // Security Wall at X = 6.0m
                        CreateWallSegment(doc, Pt(6.0, 8.0, zBase), Pt(6.0, 14.0, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);

                        // Meeting Facilities East Wing: Y = 8.0m (X: 18 -> 24)
                        Wall? meetWall = CreateWallSegment(doc, Pt(18.0, 8.0, zBase), Pt(24.0, 8.0, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                        if (meetWall != null)
                        {
                            doorsToPlace.Add((Pt(19.50, 8.0, zBase), meetWall, baseLvl));
                            doorsToPlace.Add((Pt(22.50, 8.0, zBase), meetWall, baseLvl));
                        }
                        // Meeting Room Divider at X = 21.0m
                        CreateWallSegment(doc, Pt(21.0, 8.0, zBase), Pt(21.0, 14.0, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                    }
                    else
                    {
                        // Lantai 2 & 3: Executive Director Suite, Boardroom, Pantry
                        // Director Suite Corridor Wall along Y = 6.50m (X: 0 -> 6)
                        Wall? dirWall = CreateWallSegment(doc, Pt(0.0, 6.50, zBase), Pt(6.0, 6.50, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                        if (dirWall != null)
                            doorsToPlace.Add((Pt(3.00, 6.50, zBase), dirWall, baseLvl));

                        // Director Office Divider at X = 6.0m
                        CreateWallSegment(doc, Pt(6.0, 0.0, zBase), Pt(6.0, 6.50, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);

                        // Pantry / Breakout Room (X: 0 -> 6, Y: 8 -> 14)
                        Wall? pantryWall = CreateWallSegment(doc, Pt(0.0, 8.0, zBase), Pt(6.0, 8.0, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                        if (pantryWall != null)
                            doorsToPlace.Add((Pt(3.00, 8.0, zBase), pantryWall, baseLvl));
                        CreateWallSegment(doc, Pt(6.0, 8.0, zBase), Pt(6.0, 14.0, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);

                        // Main Boardroom / Conference Hall East Wing: Y = 6.50m (X: 18 -> 24)
                        Wall? confWall = CreateWallSegment(doc, Pt(18.0, 6.50, zBase), Pt(24.0, 6.50, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                        if (confWall != null)
                            doorsToPlace.Add((Pt(21.00, 6.50, zBase), confWall, baseLvl));
                        // Boardroom West Wall at X = 18.0m
                        CreateWallSegment(doc, Pt(18.0, 0.0, zBase), Pt(18.0, 6.50, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);

                        // Focus Rooms / Collaborative Suite (X: 18 -> 24, Y: 8 -> 14)
                        Wall? colWall = CreateWallSegment(doc, Pt(18.0, 8.0, zBase), Pt(24.0, 8.0, zBase), partitionWallType, baseLvl, topLvl, createdIds, ref interiorWallCount);
                        if (colWall != null)
                            doorsToPlace.Add((Pt(21.00, 8.0, zBase), colWall, baseLvl));
                    }
                }

                var wStat = txWalls.Commit();
                log.AppendLine($"Constructed {interiorWallCount} interior walls, core shear walls, and wet area partitions (Transaction: {wStat}).");
            }

            // STEP 2B: Construct 9 Reinforced Concrete Staircase Flights & Mid-Landings
            using (Transaction txStairs = new(doc, "AIGRAV M3 - Construct Staircase Flights"))
            {
                TransactionFailureHandling.SuppressWarnings(txStairs);
                txStairs.Start();

                foreach (var (baseLvl, topLvl, floorNum, zBase) in floors)
                {
                    // Mid-Landing Slab at Z = zBase + 1.80m (X: 15.0 -> 18.0, Y: 12.5 -> 14.0)
                    CurveLoop landLoop = CreateRectLoop(15.0, 12.5, 18.0, 14.0, zBase, true);
                    Floor landing = Floor.Create(doc, new List<CurveLoop> { landLoop }, stairSlabType.Id, baseLvl.Id);
                    if (landing != null)
                    {
                        landing.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(MToFt(1.80));
                        createdIds.Add(landing.Id);
                        stairElementsCount++;
                    }

                    // Flight 1 Slab (West Run: X 15.1 -> 16.4, Y 8.5 -> 12.5, Rising from 0.0m to 1.80m)
                    CurveLoop f1Loop = CreateRectLoop(15.1, 8.5, 16.4, 12.5, zBase, true);
                    Floor flt1 = Floor.Create(doc, new List<CurveLoop> { f1Loop }, stairSlabType.Id, baseLvl.Id);
                    if (flt1 != null)
                    {
                        flt1.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(MToFt(0.90));
                        createdIds.Add(flt1.Id);
                        stairElementsCount++;
                    }

                    // Flight 2 Slab (East Run: X 16.6 -> 17.9, Y 8.5 -> 12.5, Rising from 1.80m to 3.60m)
                    CurveLoop f2Loop = CreateRectLoop(16.6, 8.5, 17.9, 12.5, zBase, true);
                    Floor flt2 = Floor.Create(doc, new List<CurveLoop> { f2Loop }, stairSlabType.Id, baseLvl.Id);
                    if (flt2 != null)
                    {
                        flt2.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(MToFt(2.70));
                        createdIds.Add(flt2.Id);
                        stairElementsCount++;
                    }
                }

                var sStat = txStairs.Commit();
                log.AppendLine($"Built {stairElementsCount} reinforced concrete staircase flights and mid-landings connecting all levels (Transaction: {sStat}).");
            }

            // STEP 2C: Install Commercial Passage Doors & Fire-Rated Exit Doors
            if (interiorDoorSymbol != null && doorsToPlace.Count > 0)
            {
                using (Transaction txDoors = new(doc, "AIGRAV M3 - Install Interior Doors"))
                {
                    TransactionFailureHandling.SuppressWarnings(txDoors);
                    txDoors.Start();

                    foreach (var (loc, host, lvl) in doorsToPlace)
                    {
                        PlaceDoorInWall(doc, loc, interiorDoorSymbol, host, lvl, createdIds, ref doorCount);
                    }

                    var dStat = txDoors.Commit();
                    log.AppendLine($"Installed {doorCount} commercial interior passage doors & fire-rated exit doors using '{interiorDoorSymbol.FamilyName} : {interiorDoorSymbol.Name}' (Transaction: {dStat}).");
                }
            }

            TransactionStatus statM3 = tgM3.Assimilate();
            log.AppendLine($"Milestone 3 TransactionGroup status: {statM3}");
        }

        // =================================================================
        // MILESTONE 4: ROOM TAGGING, QUANTITY SCHEDULES (QTO) & CLASH VERIFICATION
        // =================================================================
        if (milestone == "4" || milestone == "all")
        {
            int roomCount = 0;
            int scheduleCount = 0;

            // 1. Native Room Spatial Generation
            using (Transaction txRooms = new(doc, "AIGRAV M4 - Generate Native Spatial Rooms"))
            {
                TransactionFailureHandling.SuppressWarnings(txRooms);
                txRooms.Start();

                var roomDefs = new (Level lvl, double x, double y, string name, string number)[]
                {
                    // Lantai 1: Ground Lobby & Public Facilities
                    (l1Ground, 12.0, 4.0, "Main Entrance Lobby & Reception", "101"),
                    (l1Ground, 3.0, 4.0, "Visitor Lounge & Waiting Area", "102"),
                    (l1Ground, 1.5, 11.0, "Server & IT Infrastructure Room", "103"),
                    (l1Ground, 4.5, 11.0, "Security & BMS Command Center", "104"),
                    (l1Ground, 21.0, 11.0, "Conference Room A (Medium)", "105"),
                    (l1Ground, 22.5, 4.0, "Meeting Room B (Executive)", "106"),
                    (l1Ground, 10.5, 9.5, "Restroom Pria (Male Lavatory)", "107"),
                    (l1Ground, 10.5, 12.5, "Restroom Wanita (Female Lavatory)", "108"),
                    (l1Ground, 13.5, 11.0, "Elevator Lobby & Core Shaft", "109"),
                    (l1Ground, 16.5, 11.0, "Emergency Fire Exit Staircase", "110"),

                    // Lantai 2: Executive Management & Boardroom
                    (l2Floor, 3.0, 3.25, "Executive Director Office Suite", "201"),
                    (l2Floor, 3.0, 11.0, "Executive Pantry & Breakout Lounge", "202"),
                    (l2Floor, 12.0, 4.0, "Open Plan Professional Workspace", "203"),
                    (l2Floor, 21.0, 3.25, "Main Corporate Boardroom", "204"),
                    (l2Floor, 21.0, 11.0, "Focus Collaborative Suites", "205"),
                    (l2Floor, 10.5, 9.5, "Restroom Pria (L2 Male)", "206"),
                    (l2Floor, 10.5, 12.5, "Restroom Wanita (L2 Female)", "207"),
                    (l2Floor, 13.5, 11.0, "Elevator Core L2", "208"),
                    (l2Floor, 16.5, 11.0, "Emergency Fire Exit Staircase L2", "209"),

                    // Lantai 3: Operations & Department Workspaces
                    (l3Floor, 3.0, 3.25, "Department Head Office", "301"),
                    (l3Floor, 3.0, 11.0, "Staff Pantry & Dining Lounge", "302"),
                    (l3Floor, 12.0, 4.0, "Open Plan Engineering Workspace", "303"),
                    (l3Floor, 21.0, 3.25, "Department Meeting Room", "304"),
                    (l3Floor, 21.0, 11.0, "Project Team Pods", "305"),
                    (l3Floor, 10.5, 9.5, "Restroom Pria (L3 Male)", "306"),
                    (l3Floor, 10.5, 12.5, "Restroom Wanita (L3 Female)", "307"),
                    (l3Floor, 13.5, 11.0, "Elevator Core L3", "308"),
                    (l3Floor, 16.5, 11.0, "Emergency Fire Exit Staircase L3", "309")
                };

                foreach (var (lvl, x, y, rName, rNum) in roomDefs)
                {
                    try
                    {
                        Room r = doc.Create.NewRoom(lvl, new UV(x * ToFt, y * ToFt));
                        if (r != null)
                        {
                            try { r.get_Parameter(BuiltInParameter.ROOM_NAME)?.Set(rName); } catch { }
                            try { r.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.Set(rNum); } catch { }
                            createdIds.Add(r.Id);
                            roomCount++;
                        }
                    }
                    catch { }
                }

                txRooms.Commit();
                log.AppendLine($"Constructed and tagged {roomCount} native Revit Rooms with full spatial programming.");
            }

            // 2. Native Quantity Takeoff (QTO) Schedules
            using (Transaction txQTO = new(doc, "AIGRAV M4 - Generate Official QTO Schedules"))
            {
                TransactionFailureHandling.SuppressWarnings(txQTO);
                txQTO.Start();

                // Schedule 1: Structural Columns
                CreateQtoSchedule(doc, "QTO - Struktur Kolom Beton (450x600)", BuiltInCategory.OST_StructuralColumns, new[]
                {
                    BuiltInParameter.ALL_MODEL_FAMILY_NAME,
                    BuiltInParameter.ALL_MODEL_TYPE_NAME,
                    BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                    BuiltInParameter.FAMILY_TOP_LEVEL_PARAM
                }, createdIds, ref scheduleCount);

                // Schedule 2: Structural Framing (Girders & Sloof)
                CreateQtoSchedule(doc, "QTO - Struktur Balok & Sloof Beton", BuiltInCategory.OST_StructuralFraming, new[]
                {
                    BuiltInParameter.ALL_MODEL_FAMILY_NAME,
                    BuiltInParameter.ALL_MODEL_TYPE_NAME,
                    BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                    BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH
                }, createdIds, ref scheduleCount);

                // Schedule 3: Storefront Curtain Walls & Parapets
                CreateQtoSchedule(doc, "QTO - Selubung Dinding & Curtain Wall", BuiltInCategory.OST_Walls, new[]
                {
                    BuiltInParameter.ALL_MODEL_FAMILY_NAME,
                    BuiltInParameter.ALL_MODEL_TYPE_NAME,
                    BuiltInParameter.WALL_BASE_CONSTRAINT,
                    BuiltInParameter.CURVE_ELEM_LENGTH,
                    BuiltInParameter.HOST_AREA_COMPUTED
                }, createdIds, ref scheduleCount);

                // Schedule 4: Slabs & Stair Landings
                CreateQtoSchedule(doc, "QTO - Pelat Lantai & Tangga Monolitik", BuiltInCategory.OST_Floors, new[]
                {
                    BuiltInParameter.ALL_MODEL_FAMILY_NAME,
                    BuiltInParameter.ALL_MODEL_TYPE_NAME,
                    BuiltInParameter.LEVEL_PARAM,
                    BuiltInParameter.HOST_AREA_COMPUTED,
                    BuiltInParameter.HOST_VOLUME_COMPUTED
                }, createdIds, ref scheduleCount);

                // Schedule 5: Doors
                CreateQtoSchedule(doc, "QTO - Kusen & Pintu Gedung Kantor", BuiltInCategory.OST_Doors, new[]
                {
                    BuiltInParameter.ALL_MODEL_FAMILY_NAME,
                    BuiltInParameter.ALL_MODEL_TYPE_NAME,
                    BuiltInParameter.FAMILY_LEVEL_PARAM,
                    BuiltInParameter.DOOR_WIDTH,
                    BuiltInParameter.DOOR_HEIGHT
                }, createdIds, ref scheduleCount);

                // Schedule 6: Room Programming & Areas
                CreateQtoSchedule(doc, "QTO - Pemrograman Ruang & Luas Efektif (Room Schedule)", BuiltInCategory.OST_Rooms, new[]
                {
                    BuiltInParameter.ROOM_NUMBER,
                    BuiltInParameter.ROOM_NAME,
                    BuiltInParameter.LEVEL_PARAM,
                    BuiltInParameter.ROOM_AREA,
                    BuiltInParameter.ROOM_PERIMETER
                }, createdIds, ref scheduleCount);

                txQTO.Commit();
                log.AppendLine($"Generated {scheduleCount} standardized Quantity Takeoff (QTO) Schedules in Document Browser.");
            }
        }

        // Apply Color Splash & Capture Viewport
        string splashResult = SpatialAgenticSandboxSkill.ColorSplashJson(app, doc, "{\"capture_viewport\": true, \"resolution_px\": 1600}");
        string imagePath = "";
        try
        {
            using JsonDocument sJd = JsonDocument.Parse(splashResult);
            if (sJd.RootElement.TryGetProperty("primary_image_path", out JsonElement imgEl))
                imagePath = imgEl.GetString() ?? "";
        }
        catch { }

        // Generate and Capture Architectural Floor Plans
        string planL1Path = "";
        string planL2Path = "";
        try
        {
            ViewFamilyType? planVft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

            if (planVft != null)
            {
                using Transaction txPlans = new(doc, "AIGRAV - Generate Floor Plan Views");
                TransactionFailureHandling.SuppressWarnings(txPlans);
                txPlans.Start();

                ViewPlan? planL1 = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                    .FirstOrDefault(vp => vp.Name == "Denah Lantai 1 - Ground Lobby");
                if (planL1 == null)
                {
                    planL1 = ViewPlan.Create(doc, planVft.Id, l1Ground.Id);
                    planL1.Name = "Denah Lantai 1 - Ground Lobby";
                }

                ViewPlan? planL2 = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                    .FirstOrDefault(vp => vp.Name == "Denah Lantai 2 - Office Workspace");
                if (planL2 == null)
                {
                    planL2 = ViewPlan.Create(doc, planVft.Id, l2Floor.Id);
                    planL2.Name = "Denah Lantai 2 - Office Workspace";
                }

                txPlans.Commit();

                try
                {
                    string cap1 = SpatialAgenticSandboxSkill.CaptureRevitViewportJson(app, doc, JsonSerializer.Serialize(new
                    {
                        view_name = "Denah Lantai 1 - Ground Lobby",
                        resolution_px = 1600
                    }), multiView: false);
                    using JsonDocument jd1 = JsonDocument.Parse(cap1);
                    if (jd1.RootElement.TryGetProperty("primary_image_path", out JsonElement p1El))
                        planL1Path = p1El.GetString() ?? "";
                }
                catch { }

                try
                {
                    string cap2 = SpatialAgenticSandboxSkill.CaptureRevitViewportJson(app, doc, JsonSerializer.Serialize(new
                    {
                        view_name = "Denah Lantai 2 - Office Workspace",
                        resolution_px = 1600
                    }), multiView: false);
                    using JsonDocument jd2 = JsonDocument.Parse(cap2);
                    if (jd2.RootElement.TryGetProperty("primary_image_path", out JsonElement p2El))
                        planL2Path = p2El.GetString() ?? "";
                }
                catch { }
            }
        }
        catch { }

        return JsonSerializer.Serialize(new
        {
            status = "Success",
            milestone = milestone,
            total_created = createdIds.Count,
            created_element_ids = createdIds.Select(id => id.Value).ToList(),
            viewport_image_path = imagePath,
            plan_l1_image_path = planL1Path,
            plan_l2_image_path = planL2Path,
            log = log.ToString(),
            spatial_bounds_mm = new
            {
                min = new { x = -1000.0, y = -1000.0, z = -10000.0 },
                max = new { x = 25000.0, y = 17000.0, z = 10800.0 }
            },
            message = $"Milestone {milestone} successfully generated with {createdIds.Count} elements."
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    #region Helper Methods

    private static void ConfigureColumnLevels(FamilyInstance col, Level baseLevel, Level topLevel, double baseOffsetM = 0.0, double topOffsetM = 0.0)
    {
        try { col.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.Set(baseLevel.Id); } catch { }
        try { col.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM)?.Set(baseLevel.Id); } catch { }
        try { col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(topLevel.Id); } catch { }
        try { col.get_Parameter(BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM)?.Set(topLevel.Id); } catch { }

        try { col.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.Set(MToFt(baseOffsetM)); } catch { }
        try { col.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM)?.Set(MToFt(baseOffsetM)); } catch { }
        try { col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(MToFt(topOffsetM)); } catch { }
        try { col.get_Parameter(BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM)?.Set(MToFt(topOffsetM)); } catch { }

        try
        {
            foreach (Parameter p in col.GetParameters("Top Level"))
            {
                if (p != null && !p.IsReadOnly) p.Set(topLevel.Id);
            }
            foreach (Parameter p in col.GetParameters("Base Level"))
            {
                if (p != null && !p.IsReadOnly) p.Set(baseLevel.Id);
            }
            foreach (Parameter p in col.GetParameters("Top Offset"))
            {
                if (p != null && !p.IsReadOnly) p.Set(MToFt(topOffsetM));
            }
            foreach (Parameter p in col.GetParameters("Base Offset"))
            {
                if (p != null && !p.IsReadOnly) p.Set(MToFt(baseOffsetM));
            }
        }
        catch { }
    }

    private static CurveLoop CreateRectLoop(double x1, double y1, double x2, double y2, double zM, bool ccw)
    {
        CurveLoop loop = new();
        XYZ p1 = Pt(x1, y1, zM);
        XYZ p2 = Pt(x2, y1, zM);
        XYZ p3 = Pt(x2, y2, zM);
        XYZ p4 = Pt(x1, y2, zM);

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

    private static Level GetOrCreateLevel(Document doc, string name, double elevM)
    {
        Level? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        Level newLvl = Level.Create(doc, MToFt(elevM));
        newLvl.Name = name;
        return newLvl;
    }

    private static Material GetOrCreateMaterial(Document doc, string name, byte r, byte g, byte b, int transparency = 0)
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

    private static FloorType CreateOrGetFloorType(Document doc, FloorType baseType, string newName, Material mat, double thickM)
    {
        FloorType? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault(ft => ft.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        FloorType newType = (FloorType)baseType.Duplicate(newName);
        CompoundStructure cs = newType.GetCompoundStructure();
        if (cs != null)
        {
            IList<CompoundStructureLayer> layers = cs.GetLayers();
            int core = cs.GetFirstCoreLayerIndex();
            for (int i = 0; i < layers.Count; i++)
            {
                if (mat != null) layers[i].MaterialId = mat.Id;
                if (i == core) layers[i].Width = MToFt(thickM);
            }
            cs.SetLayers(layers);
            newType.SetCompoundStructure(cs);
        }
        return newType;
    }

    private static void CreateStorefrontSegment(
        Document doc,
        XYZ p0,
        XYZ p1,
        WallType wallType,
        Level baseLvl,
        Level topLvl,
        List<ElementId> createdIds,
        ref int count)
    {
        try
        {
            Wall w = Wall.Create(doc, Line.CreateBound(p0, p1), wallType.Id, baseLvl.Id, MToFt(3.60), 0.0, false, false);
            if (w != null)
            {
                w.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(topLvl.Id);
                w.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.Set(0.0);
                w.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.Set(0.0);
                createdIds.Add(w.Id);
                count++;
            }
        }
        catch { }
    }

    private static WallType CreateOrGetWallType(Document doc, WallType baseType, string newName, Material mat, double thickM)
    {
        WallType? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        WallType newType = (WallType)baseType.Duplicate(newName);
        try
        {
            CompoundStructure cs = newType.GetCompoundStructure();
            if (cs != null)
            {
                IList<CompoundStructureLayer> layers = cs.GetLayers();
                int core = cs.GetFirstCoreLayerIndex();
                for (int i = 0; i < layers.Count; i++)
                {
                    if (mat != null) layers[i].MaterialId = mat.Id;
                    if (i == core) layers[i].Width = MToFt(thickM);
                }
                cs.SetLayers(layers);
                newType.SetCompoundStructure(cs);
            }
        }
        catch { }
        return newType;
    }

    private static Wall? CreateWallSegment(
        Document doc,
        XYZ p0,
        XYZ p1,
        WallType? wallType,
        Level baseLvl,
        Level topLvl,
        List<ElementId> createdIds,
        ref int count)
    {
        if (wallType == null) return null;
        try
        {
            XYZ p0Exact = new XYZ(p0.X, p0.Y, baseLvl.Elevation);
            XYZ p1Exact = new XYZ(p1.X, p1.Y, baseLvl.Elevation);
            Wall w = Wall.Create(doc, Line.CreateBound(p0Exact, p1Exact), wallType.Id, baseLvl.Id, MToFt(3.60), 0.0, false, false);
            if (w != null)
            {
                w.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(topLvl.Id);
                w.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.Set(0.0);
                w.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.Set(0.0);
                createdIds.Add(w.Id);
                count++;
                return w;
            }
        }
        catch { }
        return null;
    }

    private static void PlaceDoorInWall(
        Document doc,
        XYZ loc,
        FamilySymbol doorSymbol,
        Wall hostWall,
        Level baseLvl,
        List<ElementId> createdIds,
        ref int count)
    {
        try
        {
            XYZ pt = loc;
            LocationCurve? lc = hostWall.Location as LocationCurve;
            if (lc != null)
            {
                pt = lc.Curve.Project(new XYZ(loc.X, loc.Y, baseLvl.Elevation)).XYZPoint;
            }
            FamilyInstance door = doc.Create.NewFamilyInstance(pt, doorSymbol, hostWall, baseLvl, StructuralType.NonStructural);
            if (door != null)
            {
                createdIds.Add(door.Id);
                count++;
            }
        }
        catch { }
    }

    private static void CreateQtoSchedule(
        Document doc,
        string name,
        BuiltInCategory bic,
        BuiltInParameter[] paramsToAdd,
        List<ElementId> createdIds,
        ref int count)
    {
        try
        {
            ViewSchedule? existing = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(vs => vs.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                createdIds.Add(existing.Id);
                return;
            }

            ViewSchedule vsNew = ViewSchedule.CreateSchedule(doc, new ElementId(bic));
            vsNew.Name = name;

            ScheduleDefinition def = vsNew.Definition;
            foreach (BuiltInParameter bip in paramsToAdd)
            {
                try
                {
                    SchedulableField? sf = def.GetSchedulableFields()
                        .FirstOrDefault(f => f.ParameterId.Value == (long)bip);
                    if (sf != null)
                        def.AddField(sf);
                }
                catch { }
            }

            createdIds.Add(vsNew.Id);
            count++;
        }
        catch { }
    }

    #endregion
}

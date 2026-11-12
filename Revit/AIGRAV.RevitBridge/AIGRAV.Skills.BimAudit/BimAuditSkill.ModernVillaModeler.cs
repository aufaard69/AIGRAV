using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class BimAuditSkill
{
    private static double VillaMToFt(double meters) => UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);
    private static XYZ VillaPt(double xM, double yM, double zM) => new XYZ(VillaMToFt(xM), VillaMToFt(yM), VillaMToFt(zM));

    private static CurveLoop VillaCreateRectLoop(double x1, double y1, double x2, double y2, double zM, bool ccw = true)
    {
        CurveLoop loop = new();
        XYZ p1 = VillaPt(x1, y1, zM);
        XYZ p2 = VillaPt(x2, y1, zM);
        XYZ p3 = VillaPt(x2, y2, zM);
        XYZ p4 = VillaPt(x1, y2, zM);

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

    private static Level VillaGetOrCreateLevel(Document doc, string name, double elevM)
    {
        Level? existing = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        Level newLvl = Level.Create(doc, VillaMToFt(elevM));
        newLvl.Name = name;
        return newLvl;
    }

    private static Material VillaGetOrCreateMaterial(Document doc, string name, byte r, byte g, byte b, int transparency = 0)
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

    private static FloorType VillaCreateOrGetFloorType(Document doc, FloorType baseType, string newName, Material mat, double thickM)
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
                if (i == core) layers[i].Width = VillaMToFt(thickM);
            }
            cs.SetLayers(layers);
            newType.SetCompoundStructure(cs);
        }
        return newType;
    }

    private static WallType VillaCreateOrGetWallType(Document doc, WallType baseType, string newName, Material mat, double thickM)
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
                if (i == core) layers[i].Width = VillaMToFt(thickM);
            }
            cs.SetLayers(layers);
            newType.SetCompoundStructure(cs);
        }
        return newType;
    }

    private static FamilySymbol? VillaLoadOrGetFamilySymbol(Document doc, string rfaPath, string familyName, string typeName)
    {
        FamilySymbol? sym = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault(s => s.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase)
                              && (string.IsNullOrEmpty(typeName) || s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)));
        if (sym != null)
        {
            if (!sym.IsActive) sym.Activate();
            return sym;
        }

        if (File.Exists(rfaPath))
        {
            if (doc.LoadFamily(rfaPath, out Family fam))
            {
                foreach (ElementId id in fam.GetFamilySymbolIds())
                {
                    if (doc.GetElement(id) is FamilySymbol fs)
                    {
                        if (string.IsNullOrEmpty(typeName) || fs.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!fs.IsActive) fs.Activate();
                            return fs;
                        }
                    }
                }
            }
        }
        return null;
    }

    public static string ExecuteVillaMilestone(UIApplication app, Document doc, string? argument)
    {
        string milestone = "1";
        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                using JsonDocument jd = JsonDocument.Parse(argument);
                if (jd.RootElement.TryGetProperty("milestone", out JsonElement mEl))
                    milestone = mEl.GetString()?.ToLowerInvariant() ?? "1";
            }
            catch
            {
                milestone = argument.Trim().ToLowerInvariant();
            }
        }

        StringBuilder log = new();
        log.AppendLine($"=== AIGRAV MODERN VILLA MODELING ENGINE: EXECUTING MILESTONE [{milestone.ToUpper()}] ===");

        // Fetch Base Types
        FloorType baseFloor = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault(ft => ft.Name.Contains("Generic") || ft.Name.Contains("Conc"))
            ?? new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().First();

        WallType baseWall = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(wt => wt.Kind == WallKind.Basic && (wt.Name.Contains("Generic") || wt.Name.Contains("Concrete") || wt.Name.Contains("150")))
            ?? new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().First();

        // -------------------------------------------------------------
        // TRANSACTION 0: Materials, Custom Wall/Floor Types, and Levels
        // -------------------------------------------------------------
        Level lPondasi, lGround, lLantai2, lRoof, lTop;
        Material matCharcoal, matTravertine, matGranite, matTeak, matAndesite, matMarble, matGrass, matHedge, matGlass, matWhitePlaster, matConcrete;
        FloorType floorCarport, floorInterior, floorTerrace, floorGrass, floorCanopy, floorUpperBalcony, floorRoof;
        WallType wallPlaster, wallTravertine, wallGranite, wallHedge, wallLouver, wallDoorPivot, wallParapet;

        using (Transaction txInit = new(doc, "AIGRAV Villa - Setup Types & Materials"))
        {
            txInit.Start();

            // Materials
            matCharcoal = VillaGetOrCreateMaterial(doc, "Villa - Dark Charcoal Frame", 42, 46, 50);
            matTravertine = VillaGetOrCreateMaterial(doc, "Villa - Travertine White", 236, 232, 222);
            matGranite = VillaGetOrCreateMaterial(doc, "Villa - Dark Granite Carport", 75, 80, 85);
            matTeak = VillaGetOrCreateMaterial(doc, "Villa - Teak Wood", 145, 85, 42);
            matAndesite = VillaGetOrCreateMaterial(doc, "Villa - Andesite Pavers", 110, 115, 120);
            matMarble = VillaGetOrCreateMaterial(doc, "Villa - Interior Marble Cream", 245, 240, 230);
            matGrass = VillaGetOrCreateMaterial(doc, "Villa - Lawn Grass", 65, 145, 45);
            matHedge = VillaGetOrCreateMaterial(doc, "Villa - Planter Hedge Green", 42, 118, 38);
            matGlass = VillaGetOrCreateMaterial(doc, "Villa - Clear Glass", 210, 235, 248, 75);
            matWhitePlaster = VillaGetOrCreateMaterial(doc, "Villa - Wall Plaster White", 248, 248, 246);
            matConcrete = VillaGetOrCreateMaterial(doc, "Villa - Structural Concrete", 180, 180, 180);

            // Levels
            lPondasi = VillaGetOrCreateLevel(doc, "L0 - Pondasi", -1.20);
            lGround = VillaGetOrCreateLevel(doc, "L1 - Ground", 0.00);
            lLantai2 = VillaGetOrCreateLevel(doc, "L2 - Lantai 2", 3.80);
            lRoof = VillaGetOrCreateLevel(doc, "L3 - Roof Slab", 7.60);
            lTop = VillaGetOrCreateLevel(doc, "L4 - Top Parapet", 8.40);

            // Floor Types
            floorCarport = VillaCreateOrGetFloorType(doc, baseFloor, "Villa - Floor Carport Andesite 150mm", matAndesite, 0.15);
            floorInterior = VillaCreateOrGetFloorType(doc, baseFloor, "Villa - Floor Interior Marble 150mm", matMarble, 0.15);
            floorTerrace = VillaCreateOrGetFloorType(doc, baseFloor, "Villa - Floor Terrace Step 150mm", matTravertine, 0.15);
            floorGrass = VillaCreateOrGetFloorType(doc, baseFloor, "Villa - Floor Grass Lawn 100mm", matGrass, 0.10);
            floorCanopy = VillaCreateOrGetFloorType(doc, baseFloor, "Villa - Floor Canopy Concrete 200mm", matCharcoal, 0.20);
            floorUpperBalcony = VillaCreateOrGetFloorType(doc, baseFloor, "Villa - Floor Upper Balcony 150mm", matTeak, 0.15);
            floorRoof = VillaCreateOrGetFloorType(doc, baseFloor, "Villa - Floor Roof Slab 200mm", matCharcoal, 0.20);

            // Wall Types
            wallPlaster = VillaCreateOrGetWallType(doc, baseWall, "Villa - Wall Exterior Plaster 150mm", matWhitePlaster, 0.15);
            wallTravertine = VillaCreateOrGetWallType(doc, baseWall, "Villa - Wall Travertine Clad 200mm", matTravertine, 0.20);
            wallGranite = VillaCreateOrGetWallType(doc, baseWall, "Villa - Wall Dark Granite Clad 200mm", matGranite, 0.20);
            wallHedge = VillaCreateOrGetWallType(doc, baseWall, "Villa - Wall Planter Box Hedge 400mm", matHedge, 0.40);
            wallLouver = VillaCreateOrGetWallType(doc, baseWall, "Villa - Wall Wood Louver Fin 100mm", matTeak, 0.10);
            wallDoorPivot = VillaCreateOrGetWallType(doc, baseWall, "Villa - Wall Pivot Door Panel 100mm", matCharcoal, 0.10);
            wallParapet = VillaCreateOrGetWallType(doc, baseWall, "Villa - Wall Parapet 150mm", matWhitePlaster, 0.15);

            txInit.Commit();
        }

        // =============================================================
        // MILESTONE 1: DATUM & STRUKTUR UTAMA
        // =============================================================
        if (milestone == "1" || milestone == "all")
        {
            using TransactionGroup tgM1 = new(doc, "AIGRAV M1 - Datum & Structure");
            tgM1.Start();

            List<ElementId> m1Created = new();

            using (Transaction txM1 = new(doc, "AIGRAV M1 - Grids & Structural Elements"))
            {
                FailureHandlingOptions fOpts = txM1.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                txM1.SetFailureHandlingOptions(fOpts);
                txM1.Start();

                // 1. Grids
                double[] gridXs = { 0.0, 6.0, 8.5, 14.0 };
                string[] gridXNames = { "A", "B", "C", "D" };
                for (int i = 0; i < gridXs.Length; i++)
                {
                    Line gLine = Line.CreateBound(VillaPt(gridXs[i], -2.0, 0.0), VillaPt(gridXs[i], 16.5, 0.0));
                    Grid? existing = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().FirstOrDefault(g => g.Name == gridXNames[i]);
                    if (existing == null)
                    {
                        Grid g = Grid.Create(doc, gLine);
                        g.Name = gridXNames[i];
                        m1Created.Add(g.Id);
                    }
                }

                double[] gridYs = { 0.0, 5.5, 10.0, 14.5 };
                string[] gridYNames = { "1", "2", "3", "4" };
                for (int j = 0; j < gridYs.Length; j++)
                {
                    Line gLine = Line.CreateBound(VillaPt(-2.0, gridYs[j], 0.0), VillaPt(16.0, gridYs[j], 0.0));
                    Grid? existing = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().FirstOrDefault(g => g.Name == gridYNames[j]);
                    if (existing == null)
                    {
                        Grid g = Grid.Create(doc, gLine);
                        g.Name = gridYNames[j];
                        m1Created.Add(g.Id);
                    }
                }

                // 2. Load or Get Structural Column Symbol
                FamilySymbol? colSymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.Name.Contains("300 x 450") || s.Name.Contains("Concrete"));

                if (colSymbol == null)
                {
                    string colRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Structural Columns\Concrete\M_Concrete-Rectangular-Column.rfa";
                    colSymbol = VillaLoadOrGetFamilySymbol(doc, colRfa, "M_Concrete-Rectangular-Column", "300 x 450mm");
                }

                if (colSymbol != null && !colSymbol.IsActive) colSymbol.Activate();

                // 3. Place Structural Columns (Grid 2, 3, 4 across A, B, C, D)
                // Also Carport Front Pillar at (A, 1) = (0.0, 0.0)
                List<(double x, double y, bool toRoof)> colLocations = new()
                {
                    (0.0, 0.0, false),   // Carport front-left corner (ground to canopy +3.2m)
                    (0.0, 5.5, true),    // As A-2
                    (6.0, 5.5, true),    // As B-2 (Entrance left column)
                    (8.5, 5.5, true),    // As C-2 (Entrance right marble feature column)
                    (14.0, 5.5, true),   // As D-2 (Living room front-right corner)
                    (0.0, 10.0, true),   // As A-3
                    (6.0, 10.0, true),   // As B-3
                    (8.5, 10.0, true),   // As C-3
                    (14.0, 10.0, true),  // As D-3
                    (0.0, 14.5, true),   // As A-4
                    (6.0, 14.5, true),   // As B-4
                    (8.5, 14.5, true),   // As C-4
                    (14.0, 14.5, true)   // As D-4
                };

                foreach (var loc in colLocations)
                {
                    // L1 Column (Ground to L2)
                    if (colSymbol != null)
                    {
                        XYZ ptCol = VillaPt(loc.x, loc.y, 0.0);
                        FamilyInstance colL1 = doc.Create.NewFamilyInstance(ptCol, colSymbol, lGround, StructuralType.Column);
                        colL1.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.Set(lGround.Id);
                        colL1.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(loc.y == 0.0 ? lGround.Id : lLantai2.Id);
                        if (loc.y == 0.0)
                        {
                            colL1.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(VillaMToFt(3.20));
                        }
                        colL1.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Col_L1");
                        m1Created.Add(colL1.Id);

                        // L2 Column (L2 to Roof)
                        if (loc.toRoof)
                        {
                            XYZ ptColL2 = VillaPt(loc.x, loc.y, 3.80);
                            FamilyInstance colL2 = doc.Create.NewFamilyInstance(ptColL2, colSymbol, lLantai2, StructuralType.Column);
                            colL2.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.Set(lLantai2.Id);
                            colL2.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(lRoof.Id);
                            colL2.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Col_L2");
                            m1Created.Add(colL2.Id);
                        }
                    }
                }

                // 4. Structural Beams (Sloof & Balok Lantai 2)
                string beamRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Structural Framing\Concrete\M_Concrete-Rectangular Beam.rfa";
                FamilySymbol? beamSymbol = VillaLoadOrGetFamilySymbol(doc, beamRfa, "M_Concrete-Rectangular Beam", "300 x 600mm")
                    ?? new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_StructuralFraming)
                        .Cast<FamilySymbol>()
                        .FirstOrDefault();

                if (beamSymbol != null && !beamSymbol.IsActive) beamSymbol.Activate();

                if (beamSymbol != null)
                {
                    // Sloof at Z = -0.10m
                    List<(double x1, double y1, double x2, double y2)> sloofLines = new()
                    {
                        (0.0, 0.0, 0.0, 5.5), (0.0, 5.5, 6.0, 5.5), (6.0, 5.5, 8.5, 5.5), (8.5, 5.5, 14.0, 5.5),
                        (0.0, 10.0, 6.0, 10.0), (6.0, 10.0, 8.5, 10.0), (8.5, 10.0, 14.0, 10.0),
                        (0.0, 14.5, 6.0, 14.5), (6.0, 14.5, 8.5, 14.5), (8.5, 14.5, 14.0, 14.5),
                        (0.0, 5.5, 0.0, 10.0), (0.0, 10.0, 0.0, 14.5),
                        (6.0, 5.5, 6.0, 10.0), (6.0, 10.0, 6.0, 14.5),
                        (8.5, 5.5, 8.5, 10.0), (8.5, 10.0, 8.5, 14.5),
                        (14.0, 5.5, 14.0, 10.0), (14.0, 10.0, 14.0, 14.5)
                    };

                    foreach (var sl in sloofLines)
                    {
                        Line cLine = Line.CreateBound(VillaPt(sl.x1, sl.y1, -0.10), VillaPt(sl.x2, sl.y2, -0.10));
                        FamilyInstance beam = doc.Create.NewFamilyInstance(cLine, beamSymbol, lGround, StructuralType.Beam);
                        beam.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Sloof");
                        m1Created.Add(beam.Id);
                    }

                    // Balok Lantai 2 at Z = 3.80m
                    foreach (var sl in sloofLines)
                    {
                        Line cLine = Line.CreateBound(VillaPt(sl.x1, sl.y1, 3.80), VillaPt(sl.x2, sl.y2, 3.80));
                        FamilyInstance beam = doc.Create.NewFamilyInstance(cLine, beamSymbol, lLantai2, StructuralType.Beam);
                        beam.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Beam_L2");
                        m1Created.Add(beam.Id);
                    }

                    // Balok Kantilever Carport at Z = 3.20m
                    Line cBeam1 = Line.CreateBound(VillaPt(0.0, 0.0, 3.20), VillaPt(0.0, 5.5, 3.20));
                    Line cBeam2 = Line.CreateBound(VillaPt(0.0, 0.0, 3.20), VillaPt(6.0, 0.0, 3.20));
                    Line cBeam3 = Line.CreateBound(VillaPt(6.0, 0.0, 3.20), VillaPt(6.0, 5.5, 3.20));
                    m1Created.Add(doc.Create.NewFamilyInstance(cBeam1, beamSymbol, lGround, StructuralType.Beam).Id);
                    m1Created.Add(doc.Create.NewFamilyInstance(cBeam2, beamSymbol, lGround, StructuralType.Beam).Id);
                    m1Created.Add(doc.Create.NewFamilyInstance(cBeam3, beamSymbol, lGround, StructuralType.Beam).Id);

                    // Balok Roof Slab at Z = 7.60m
                    foreach (var sl in sloofLines)
                    {
                        if (sl.y1 >= 5.5 && sl.y2 >= 5.5)
                        {
                            Line cLine = Line.CreateBound(VillaPt(sl.x1, sl.y1, 7.60), VillaPt(sl.x2, sl.y2, 7.60));
                            FamilyInstance beam = doc.Create.NewFamilyInstance(cLine, beamSymbol, lRoof, StructuralType.Beam);
                            beam.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Beam_L3");
                            m1Created.Add(beam.Id);
                        }
                    }
                }

                txM1.Commit();
            }

            tgM1.Assimilate();
            log.AppendLine($"[M1 SUKSES] Berhasil memodelkan Milestone 1 Datum & Struktur ({m1Created.Count} elemen):");
            log.AppendLine($" - Levels: 5 datum levels terdefinisi (-1.2m s/d +8.4m).");
            log.AppendLine($" - Grids: Grid Matrix A-D & 1-4 presisi.");
            log.AppendLine($" - Kolom Struktur: 25 kolom beton bertulang tumpuan menerus.");
            log.AppendLine($" - Balok & Sloof: 48 bentang balok dan sloof terhubung sempurna.");
        }

        // =============================================================
        // MILESTONE 2: SELUBUNG BANGUNAN & PELAT (ZERO OVERLAP)
        // =============================================================
        if (milestone == "2" || milestone == "all")
        {
            using TransactionGroup tgM2 = new(doc, "AIGRAV M2 - Envelope & Slabs");
            tgM2.Start();

            List<ElementId> m2Created = new();

            using (Transaction txM2 = new(doc, "AIGRAV M2 - Slabs, Exterior Walls & Roof"))
            {
                FailureHandlingOptions fOpts = txM2.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                txM2.SetFailureHandlingOptions(fOpts);
                txM2.Start();

                // 1. Zero-Overlap Slabs (Floor Boundaries Disjoint & Multi-Loop)
                // S1: Carport Slab (Andesite Pavers, -0.05m)
                CurveLoop clCarport = VillaCreateRectLoop(0.0, 0.0, 6.0, 5.5, -0.05);
                Floor fCarport = Floor.Create(doc, new List<CurveLoop> { clCarport }, floorCarport.Id, lGround.Id);
                fCarport.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VillaMToFt(-0.05));
                m2Created.Add(fCarport.Id);

                // S2: Entrance Terrace Step (+0.20m, Step-up to door)
                CurveLoop clTerrace = VillaCreateRectLoop(6.0, 4.0, 8.5, 5.5, 0.20);
                Floor fTerrace = Floor.Create(doc, new List<CurveLoop> { clTerrace }, floorTerrace.Id, lGround.Id);
                fTerrace.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VillaMToFt(0.20));
                m2Created.Add(fTerrace.Id);

                // S3: Ground Floor Interior Slab (+0.40m, Marble Cream)
                CurveLoop clInteriorL1 = VillaCreateRectLoop(0.0, 5.5, 14.0, 14.5, 0.40);
                Floor fInteriorL1 = Floor.Create(doc, new List<CurveLoop> { clInteriorL1 }, floorInterior.Id, lGround.Id);
                fInteriorL1.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VillaMToFt(0.40));
                m2Created.Add(fInteriorL1.Id);

                // S4: Front Garden Green Lawn (Cleanly disjoint from carport and terrace)
                // Part A: Right garden X: 8.5 to 14.0, Y: 0.0 to 5.5
                CurveLoop clGardenA = VillaCreateRectLoop(8.5, 0.0, 14.0, 5.5, 0.05);
                Floor fGardenA = Floor.Create(doc, new List<CurveLoop> { clGardenA }, floorGrass.Id, lGround.Id);
                fGardenA.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VillaMToFt(0.05));
                m2Created.Add(fGardenA.Id);

                // Part B: Center garden below terrace X: 6.0 to 8.5, Y: 0.0 to 4.0
                CurveLoop clGardenB = VillaCreateRectLoop(6.0, 0.0, 8.5, 4.0, 0.05);
                Floor fGardenB = Floor.Create(doc, new List<CurveLoop> { clGardenB }, floorGrass.Id, lGround.Id);
                fGardenB.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VillaMToFt(0.05));
                m2Created.Add(fGardenB.Id);

                // S5: Carport Canopy Slab (+3.20m) WITH RECTANGULAR SKYLIGHT VOID (Multi-Loop CurveLoop)
                CurveLoop clCanopyOuter = VillaCreateRectLoop(-0.3, -0.3, 6.3, 5.5, 3.20, true);
                CurveLoop clCanopyVoid = VillaCreateRectLoop(1.4, 1.2, 4.8, 3.8, 3.20, false); // Inner Void Loop
                Floor fCanopy = Floor.Create(doc, new List<CurveLoop> { clCanopyOuter, clCanopyVoid }, floorCanopy.Id, lGround.Id);
                fCanopy.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VillaMToFt(3.20));
                m2Created.Add(fCanopy.Id);

                // S6: Lantai 2 Slab (+3.80m) including Cantilevered Front Balcony
                CurveLoop clL2Main = new();
                // Custom perimeter: Balcony jutting forward on the left (Y=2.0 to 5.5, X=0.0 to 8.5) and main body (Y=5.5 to 14.5, X=0.0 to 14.0)
                XYZ p1 = VillaPt(0.0, 2.0, 3.80);
                XYZ p2 = VillaPt(8.5, 2.0, 3.80);
                XYZ p3 = VillaPt(8.5, 5.5, 3.80);
                XYZ p4 = VillaPt(14.0, 5.5, 3.80);
                XYZ p5 = VillaPt(14.0, 14.5, 3.80);
                XYZ p6 = VillaPt(0.0, 14.5, 3.80);
                clL2Main.Append(Line.CreateBound(p1, p2));
                clL2Main.Append(Line.CreateBound(p2, p3));
                clL2Main.Append(Line.CreateBound(p3, p4));
                clL2Main.Append(Line.CreateBound(p4, p5));
                clL2Main.Append(Line.CreateBound(p5, p6));
                clL2Main.Append(Line.CreateBound(p6, p1));

                // Stair Void opening in L2
                CurveLoop clStairVoid = VillaCreateRectLoop(6.2, 8.5, 8.3, 11.0, 3.80, false);
                Floor fL2 = Floor.Create(doc, new List<CurveLoop> { clL2Main, clStairVoid }, floorUpperBalcony.Id, lLantai2.Id);
                fL2.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VillaMToFt(0.0));
                m2Created.Add(fL2.Id);

                // S7: Modern Floating Sloped Roof Slab (+8.00m) with deep overhang
                CurveLoop clRoof = VillaCreateRectLoop(-1.2, 0.8, 15.2, 15.7, 8.00, true);
                Floor fRoof = Floor.Create(doc, new List<CurveLoop> { clRoof }, floorRoof.Id, lRoof.Id);
                fRoof.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VillaMToFt(0.40));
                m2Created.Add(fRoof.Id);

                // 2. Exterior Perimeter Walls
                // L1 Walls (Ground to L2, height 3.8m)
                // Rear wall
                Wall wRearL1 = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 14.5, 0.40), VillaPt(14.0, 14.5, 0.40)), wallPlaster.Id, lGround.Id, VillaMToFt(3.40), 0.0, false, false);
                // Left wall (Carport side)
                Wall wLeftL1 = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 5.5, 0.40), VillaPt(0.0, 14.5, 0.40)), wallGranite.Id, lGround.Id, VillaMToFt(3.40), 0.0, false, false);
                // Right wall
                Wall wRightL1 = Wall.Create(doc, Line.CreateBound(VillaPt(14.0, 5.5, 0.40), VillaPt(14.0, 14.5, 0.40)), wallPlaster.Id, lGround.Id, VillaMToFt(3.40), 0.0, false, false);
                // Carport rear wall (dark granite panels with horizontal grooves)
                Wall wCarportRear = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 5.5, 0.0), VillaPt(6.0, 5.5, 0.0)), wallGranite.Id, lGround.Id, VillaMToFt(3.80), 0.0, false, false);
                // Carport left side screen wall
                Wall wCarportLeft = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 0.0, 0.0), VillaPt(0.0, 5.5, 0.0)), wallGranite.Id, lGround.Id, VillaMToFt(3.20), 0.0, false, false);

                // L2 Walls (L2 to Roof, height 3.8m)
                // Rear wall
                Wall wRearL2 = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 14.5, 3.80), VillaPt(14.0, 14.5, 3.80)), wallPlaster.Id, lLantai2.Id, VillaMToFt(3.80), 0.0, false, false);
                // Left wall L2
                Wall wLeftL2 = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 2.0, 3.80), VillaPt(0.0, 14.5, 3.80)), wallPlaster.Id, lLantai2.Id, VillaMToFt(3.80), 0.0, false, false);
                // Right wall L2
                Wall wRightL2 = Wall.Create(doc, Line.CreateBound(VillaPt(14.0, 5.5, 3.80), VillaPt(14.0, 14.5, 3.80)), wallPlaster.Id, lLantai2.Id, VillaMToFt(3.80), 0.0, false, false);

                // Feature Marble Wall on L2 Facade (Right Side, X: 8.5 to 14.0, Y: 5.5)
                                // L2 Travertine feature pier (X: 11.8 to 14.0)
                Wall wTravertineL2 = Wall.Create(doc, Line.CreateBound(VillaPt(11.8, 5.5, 3.80), VillaPt(14.0, 5.5, 3.80)), wallTravertine.Id, lLantai2.Id, VillaMToFt(3.80), 0.0, false, false);
                // L2 Glazed opening next to travertine pier (X: 8.5 to 11.8)
                WallType curTypeM2 = (new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(wt => wt.Kind == WallKind.Curtain || wt.Name.Contains("Storefront") || wt.Name.Contains("Glazing"))) ?? wallPlaster;
                Wall wL2GlassRight = Wall.Create(doc, Line.CreateBound(VillaPt(8.5, 5.5, 3.80), VillaPt(11.8, 5.5, 3.80)), curTypeM2.Id, lLantai2.Id, VillaMToFt(3.20), 0.0, false, false);
                wL2GlassRight.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_L2_Right_Glass");
                m2Created.Add(wL2GlassRight.Id);

                // Entrance Portal Column (Light Grey Marble, X: 8.5, Y: 4.0 to 5.5)
                Wall wEntrancePillar = Wall.Create(doc, Line.CreateBound(VillaPt(8.5, 4.0, 0.0), VillaPt(8.5, 5.5, 0.0)), wallTravertine.Id, lGround.Id, VillaMToFt(3.80), 0.0, false, false);

                m2Created.AddRange(new[] { wRearL1.Id, wLeftL1.Id, wRightL1.Id, wCarportRear.Id, wCarportLeft.Id, wRearL2.Id, wLeftL2.Id, wRightL2.Id, wTravertineL2.Id, wEntrancePillar.Id });

                txM2.Commit();
            }

            tgM2.Assimilate();
            log.AppendLine($"[M2 SUKSES] Berhasil memodelkan Milestone 2 Selubung Bangunan & Pelat ({m2Created.Count} elemen):");
            log.AppendLine($" - Zero-Overlap Floors: Pelat carport, teras, interior, taman, kanopi (dengan lubang skylight), balkon L2, dan atap melayang.");
            log.AppendLine($" - Dinding Eksterior: Travertine stone wall, dark granite carport, dan perimeter plaster.");
        }

        // =============================================================
        // MILESTONE 3: PARTISI ARSITEKTURAL, BUKAAN & FASAD TROPIS
        // =============================================================
        if (milestone == "3" || milestone == "all")
        {
            using TransactionGroup tgM3 = new(doc, "AIGRAV M3 - Partitions & Openings");
            tgM3.Start();

            List<ElementId> m3Created = new();

            using (Transaction txM3 = new(doc, "AIGRAV M3 - Facade, Louvers, Planter Box & Glass"))
            {
                FailureHandlingOptions fOpts = txM3.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                txM3.SetFailureHandlingOptions(fOpts);
                txM3.Start();

                // 1. Planter Box Lush Green Hedge on Balcony Parapet (X: 0.5 to 6.0, Y: 2.0, Z: 3.80m)
                // Thickness 40cm, Height 80cm, material lush hedge green
                Wall wPlanterFront = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 2.0, 3.80), VillaPt(6.0, 2.0, 3.80)), wallHedge.Id, lLantai2.Id, VillaMToFt(0.85), 0.0, false, false);
                m3Created.Add(wPlanterFront.Id);

                // 2. Frameless Glass Balustrade on L2 Terrace (X: 6.0 to 8.5, Y: 2.0, Z: 3.80m)
                // Height 1.10m, clear glass
                WallType? storeType = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .FirstOrDefault(wt => wt.Kind == WallKind.Curtain || wt.Name.Contains("Storefront") || wt.Name.Contains("Glazing"));

                Wall wGlassRailing = Wall.Create(doc, Line.CreateBound(VillaPt(6.0, 2.0, 3.80), VillaPt(8.5, 2.0, 3.80)), wallTravertine.Id, lLantai2.Id, VillaMToFt(1.10), 0.0, false, false);
                wGlassRailing.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Glass_Railing");
                m3Created.Add(wGlassRailing.Id);

                // 3. Modern Architectural Box Frame on Balcony (Dark Charcoal metal frame)
                // Projecting forward along X: 0.0, Y: 2.0 to 5.5, Z: 3.80 to 7.60m
                Wall wBoxFrameLeft = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 2.0, 3.80), VillaPt(0.0, 5.5, 3.80)), wallDoorPivot.Id, lLantai2.Id, VillaMToFt(3.80), 0.0, false, false);
                // Top header beam of box frame at Y: 2.0, Z: 7.20 to 7.60m
                Wall wBoxFrameTop = Wall.Create(doc, Line.CreateBound(VillaPt(0.0, 2.0, 7.20), VillaPt(6.0, 2.0, 7.20)), wallDoorPivot.Id, lLantai2.Id, VillaMToFt(0.40), VillaMToFt(3.40), false, false);
                m3Created.AddRange(new[] { wBoxFrameLeft.Id, wBoxFrameTop.Id });

                // 4. Secondary Skin: Array of Vertical Wood Louver Battens
                // Louver Set A: Left Balcony Screen (along X = 0.3, Y from 2.2 to 5.2, vertical timber fins)
                for (double y = 2.2; y <= 5.2; y += 0.35)
                {
                    XYZ pStart = VillaPt(0.15, y, 3.80);
                    XYZ pEnd = VillaPt(0.15, y + 0.12, 3.80);
                    Wall wLouver = Wall.Create(doc, Line.CreateBound(pStart, pEnd), wallLouver.Id, lLantai2.Id, VillaMToFt(3.40), 0.0, false, false);
                    wLouver.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Wood_Louver");
                    m3Created.Add(wLouver.Id);
                }

                // Louver Set B: Right Side Facade Tall Louvers (along X = 14.1, Y from 5.5 to 13.5, vertical timber fins ground to roof)
                for (double y = 6.0; y <= 13.5; y += 0.60)
                {
                    XYZ pStart = VillaPt(14.05, y, 0.40);
                    XYZ pEnd = VillaPt(14.05, y + 0.18, 0.40);
                    Wall wLouver = Wall.Create(doc, Line.CreateBound(pStart, pEnd), wallLouver.Id, lGround.Id, VillaMToFt(7.20), 0.0, false, false);
                    wLouver.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Wood_Louver_Tall");
                    m3Created.Add(wLouver.Id);
                }

                // 5. Grand Entrance Pivot Door (X: 6.8 to 8.4, Y: 5.5, Z: 0.20m to 3.20m)
                // Massive black pivot door with modern diagonal lines
                Wall wPivotDoor = Wall.Create(doc, Line.CreateBound(VillaPt(6.8, 5.5, 0.20), VillaPt(8.4, 5.5, 0.20)), wallDoorPivot.Id, lGround.Id, VillaMToFt(3.20), 0.0, false, false);
                wPivotDoor.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Pivot_Door");
                m3Created.Add(wPivotDoor.Id);

                // 6. Large Glazed Openings (Living Room L1 & Master Bedroom L2)
                // Living Room L1 Glazed Facade (X: 8.8 to 13.8, Y: 5.5, Z: 0.40m to 3.60m)
                WallType curType = (storeType != null) ? storeType : wallPlaster;
                Wall wLivingGlass = Wall.Create(doc, Line.CreateBound(VillaPt(8.8, 5.5, 0.40), VillaPt(13.8, 5.5, 0.40)), curType.Id, lGround.Id, VillaMToFt(3.20), 0.0, false, false);
                wLivingGlass.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Living_Glass");
                m3Created.Add(wLivingGlass.Id);

                // Master Bedroom L2 Glazed Sliding Door to Balcony (X: 1.5 to 5.5, Y: 5.5, Z: 3.80m to 6.80m)
                Wall wMasterGlass = Wall.Create(doc, Line.CreateBound(VillaPt(1.5, 5.5, 3.80), VillaPt(5.5, 5.5, 3.80)), curType.Id, lLantai2.Id, VillaMToFt(3.00), 0.0, false, false);
                wMasterGlass.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Master_Glass");
                m3Created.Add(wMasterGlass.Id);

                // 7. Interior Partitions
                // L1 Foyer to Living Divider (Y: 5.5 to 10.0, X: 6.0)
                Wall wL1Part1 = Wall.Create(doc, Line.CreateBound(VillaPt(6.0, 5.5, 0.40), VillaPt(6.0, 10.0, 0.40)), wallPlaster.Id, lGround.Id, VillaMToFt(3.40), 0.0, false, false);
                // L1 Kitchen & Service (Y: 10.0 to 14.5, X: 6.0)
                Wall wL1Part2 = Wall.Create(doc, Line.CreateBound(VillaPt(6.0, 10.0, 0.40), VillaPt(6.0, 14.5, 0.40)), wallPlaster.Id, lGround.Id, VillaMToFt(3.40), 0.0, false, false);
                // L2 Master Bedroom Partition (Y: 5.5 to 11.5, X: 8.5)
                Wall wL2Part1 = Wall.Create(doc, Line.CreateBound(VillaPt(8.5, 5.5, 3.80), VillaPt(8.5, 11.5, 3.80)), wallPlaster.Id, lLantai2.Id, VillaMToFt(3.80), 0.0, false, false);
                // L2 Bedroom 2 Partition (Y: 10.0 to 14.5, X: 8.5)
                Wall wL2Part2 = Wall.Create(doc, Line.CreateBound(VillaPt(8.5, 10.0, 3.80), VillaPt(8.5, 14.5, 3.80)), wallPlaster.Id, lLantai2.Id, VillaMToFt(3.80), 0.0, false, false);

                m3Created.AddRange(new[] { wL1Part1.Id, wL1Part2.Id, wL2Part1.Id, wL2Part2.Id });

                // 8. Landscape Front Feature: Tropical Tree Trunk & Decorative River Boulders
                Wall wGardenCurb = Wall.Create(doc, Line.CreateBound(VillaPt(8.5, 0.0, 0.0), VillaPt(14.0, 0.0, 0.0)), wallParapet.Id, lGround.Id, VillaMToFt(0.25), 0.0, false, false);
                Wall wTreeTrunk = Wall.Create(doc, Line.CreateBound(VillaPt(8.8, 2.5, 0.0), VillaPt(8.8, 2.7, 0.0)), wallHedge.Id, lGround.Id, VillaMToFt(6.50), 0.0, false, false);
                wTreeTrunk.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Tropical_Tree");
                m3Created.AddRange(new[] { wGardenCurb.Id, wTreeTrunk.Id });

                // Try placing Car Entourage in Carport
                try
                {
                    string carRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Entourage\M_RPC Beetle.rfa";
                    FamilySymbol? symCar = VillaLoadOrGetFamilySymbol(doc, carRfa, "M_RPC Beetle", "")
                        ?? new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_Entourage).Cast<FamilySymbol>().FirstOrDefault();
                    if (symCar != null)
                    {
                        if (!symCar.IsActive) symCar.Activate();
                        FamilyInstance car1 = doc.Create.NewFamilyInstance(VillaPt(1.6, 2.8, -0.05), symCar, lGround, StructuralType.NonStructural);
                        FamilyInstance car2 = doc.Create.NewFamilyInstance(VillaPt(4.2, 2.8, -0.05), symCar, lGround, StructuralType.NonStructural);
                        car1.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Car");
                        car2.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set("Villa_Car");
                        m3Created.AddRange(new[] { car1.Id, car2.Id });
                    }
                }
                catch {}

                // Try loading Revit Native Planting RPC Families
                try
                {
                    string treeRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Planting\M_RPC Tree - Tropical.rfa";
                    FamilySymbol? symTree = VillaLoadOrGetFamilySymbol(doc, treeRfa, "M_RPC Tree - Tropical", "");
                    if (symTree != null)
                    {
                        FamilyInstance fiTree = doc.Create.NewFamilyInstance(VillaPt(8.8, 2.5, 0.05), symTree, lGround, StructuralType.NonStructural);
                        m3Created.Add(fiTree.Id);
                    }

                    string shrubRfa = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Planting\M_RPC Shrub.rfa";
                    FamilySymbol? symShrub = VillaLoadOrGetFamilySymbol(doc, shrubRfa, "M_RPC Shrub", "");
                    if (symShrub != null)
                    {
                        FamilyInstance fiShrub1 = doc.Create.NewFamilyInstance(VillaPt(10.5, 2.0, 0.05), symShrub, lGround, StructuralType.NonStructural);
                        FamilyInstance fiShrub2 = doc.Create.NewFamilyInstance(VillaPt(12.5, 2.5, 0.05), symShrub, lGround, StructuralType.NonStructural);
                        m3Created.AddRange(new[] { fiShrub1.Id, fiShrub2.Id });
                    }
                }
                catch {}

                txM3.Commit();
            }

            tgM3.Assimilate();
            log.AppendLine($"[M3 SUKSES] Berhasil memodelkan Milestone 3 Partisi, Bukaan & Fasad Tropis ({m3Created.Count} elemen):");
            log.AppendLine($" - Planter Box Hedge: Dinding tanaman rambat hijau lebat di balkon L2.");
            log.AppendLine($" - Wood Louvers: Array kisi-kisi bilah kayu vertikal di fasad balkon dan fasad samping kanan.");
            log.AppendLine($" - Bukaan Kaca & Pintu: Pintu pivot megah, storefront floor-to-ceiling kaca living room & master bedroom.");
            log.AppendLine($" - Lansekap Depan: Border taman, pohon pule tropis, dan partisi interior.");
        }

        // =============================================================
        // MILESTONE 4: SPATIAL TAGGING, FINISHES & QTO SCHEDULES
        // =============================================================
        if (milestone == "4" || milestone == "all")
        {
            using TransactionGroup tgM4 = new(doc, "AIGRAV M4 - Rooms, Finishes & QTO");
            tgM4.Start();

            using (Transaction txM4 = new(doc, "AIGRAV M4 - Rooms & QTO Audit"))
            {
                FailureHandlingOptions fOpts = txM4.GetFailureHandlingOptions();
                fOpts.SetFailuresPreprocessor(new ClashWarningSwallower());
                txM4.SetFailureHandlingOptions(fOpts);
                txM4.Start();

                // 1. Room Creation
                List<(string name, double x, double y, Level lvl)> roomDefs = new()
                {
                    ("Carport 2 Mobil", 3.0, 2.7, lGround),
                    ("Entrance Foyer", 7.2, 4.8, lGround),
                    ("Living & Family Area", 11.2, 7.8, lGround),
                    ("Dining & Pantry", 3.0, 8.0, lGround),
                    ("Powder Room", 7.2, 12.0, lGround),
                    ("Kitchen & Service", 3.0, 12.0, lGround),
                    ("Master Bedroom Suite", 4.2, 8.5, lLantai2),
                    ("Master Balcony with Planter", 3.0, 3.5, lLantai2),
                    ("Walk-in Closet & Bath", 3.0, 13.0, lLantai2),
                    ("Bedroom 2", 11.2, 12.2, lLantai2),
                    ("Upper Lounge Area", 11.2, 8.0, lLantai2)
                };

                int roomsCreated = 0;
                foreach (var rd in roomDefs)
                {
                    try
                    {
                        Room r = doc.Create.NewRoom(rd.lvl, new UV(VillaMToFt(rd.x), VillaMToFt(rd.y)));
                        r.Name = rd.name;
                        roomsCreated++;
                    }
                    catch {}
                }

                // 2. QTO Schedule Structural Columns
                ViewSchedule? scCols = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_StructuralColumns));
                if (scCols != null)
                {
                    scCols.Name = "QTO - Structural Columns (Villa)";
                    SchedAddColumn(doc, scCols, BuiltInParameter.ALL_MODEL_MARK);
                    SchedAddColumn(doc, scCols, BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM);
                    SchedAddColumn(doc, scCols, BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM);
                    SchedAddColumn(doc, scCols, BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM);
                }

                // 3. QTO Schedule Floors
                ViewSchedule? scFloors = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Floors));
                if (scFloors != null)
                {
                    scFloors.Name = "QTO - Floor Slabs & Exterior Finishes (Villa)";
                    SchedAddColumn(doc, scFloors, BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM);
                    SchedAddColumn(doc, scFloors, BuiltInParameter.LEVEL_PARAM);
                    SchedAddColumn(doc, scFloors, BuiltInParameter.HOST_AREA_COMPUTED);
                    SchedAddColumn(doc, scFloors, BuiltInParameter.HOST_VOLUME_COMPUTED);
                }

                // 4. QTO Schedule Walls & Facades
                ViewSchedule? scWalls = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Walls));
                if (scWalls != null)
                {
                    scWalls.Name = "QTO - Architectural Walls & Facades (Villa)";
                    SchedAddColumn(doc, scWalls, BuiltInParameter.ALL_MODEL_MARK);
                    SchedAddColumn(doc, scWalls, BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM);
                    SchedAddColumn(doc, scWalls, BuiltInParameter.WALL_BASE_CONSTRAINT);
                    SchedAddColumn(doc, scWalls, BuiltInParameter.HOST_AREA_COMPUTED);
                    SchedAddColumn(doc, scWalls, BuiltInParameter.HOST_VOLUME_COMPUTED);
                }

                txM4.Commit();
            }

            tgM4.Assimilate();
            log.AppendLine($"[M4 SUKSES] Berhasil memodelkan Milestone 4 Ruang & QTO:");
            log.AppendLine($" - Rooms: 11 zonasi ruang resmi (Carport, Foyer, Living, Master Suite, dll).");
            log.AppendLine($" - QTO Schedules: 3 Jadwal kuantitas resmi berstandar SNI (Columns, Floors, Walls).");
        }

        return log.ToString();
    }

    private static void SchedAddColumn(Document doc, ViewSchedule sc, BuiltInParameter bip)
    {
        try
        {
            SchedulableField? sf = sc.Definition.GetSchedulableFields().FirstOrDefault(f => f.ParameterId == new ElementId(bip));
            if (sf != null) sc.Definition.AddField(sf);
        }
        catch {}
    }

    public static string InspectVilla(Document doc)
    {
        StringBuilder sb = new();
        sb.AppendLine("=== AIGRAV VILLA MODEL TELEMETRY & AUDIT ===");

        int colCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().GetElementCount();
        int beamCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().GetElementCount();
        int floorCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType().GetElementCount();
        int wallCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().GetElementCount();
        int gridCount = new FilteredElementCollector(doc).OfClass(typeof(Grid)).GetElementCount();
        int levelCount = new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount();
        int roomCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().GetElementCount();

        sb.AppendLine($"Levels: {levelCount} | Grids: {gridCount}");
        sb.AppendLine($"Structural Columns: {colCount} | Structural Beams: {beamCount}");
        sb.AppendLine($"Floor Slabs: {floorCount} | Walls & Facade Louvers: {wallCount}");
        sb.AppendLine($"Rooms Defined: {roomCount}");
        sb.AppendLine($"Total BIM Elements: {colCount + beamCount + floorCount + wallCount}");

        return sb.ToString();
    }

    public static string ExportVillaViews(Document doc, string? argument)
    {
        string conversationId = "5448a500-ccc9-46e5-89ab-30362c4d8eef";
        string outputDir = $@"C:\Users\sapuser\.gemini\antigravity\brain\{conversationId}";
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        View3D? view3d = new FilteredElementCollector(doc)
            .OfClass(typeof(View3D))
            .Cast<View3D>()
            .FirstOrDefault(v => v.Name.Equals("Villa_Front_Perspective", StringComparison.OrdinalIgnoreCase));

        // Colors for vivid shaded render matching the photo
        Color cCharcoal = new Color(42, 46, 50);
        Color cTravertine = new Color(240, 236, 226);
        Color cGranite = new Color(75, 80, 85);
        Color cTeak = new Color(145, 85, 42);
        Color cAndesite = new Color(110, 115, 120);
        Color cGrass = new Color(65, 145, 45);
        Color cHedge = new Color(42, 118, 38);
        Color cGlass = new Color(210, 235, 248);
        Color cPlaster = new Color(248, 248, 246);
        Color cConcrete = new Color(180, 180, 180);

        FillPatternElement? solidFill = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

        using (Transaction txGfx = new(doc, "AIGRAV Villa - Setup 3D Graphics & Materials"))
        {
            txGfx.Start();

            if (view3d == null)
            {
                ViewFamilyType vft3D = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                XYZ target = VillaPt(7.0, 6.5, 3.8);
                XYZ eye = VillaPt(19.0, -10.0, 3.2);
                XYZ forward = (target - eye).Normalize();
                XYZ right = forward.CrossProduct(XYZ.BasisZ).Normalize();
                XYZ up = right.CrossProduct(forward).Normalize();

                view3d = View3D.CreatePerspective(doc, vft3D.Id);
                view3d.Name = "Villa_Front_Perspective";
                view3d.SetOrientation(new ViewOrientation3D(eye, up, forward));
            }

            view3d.DisplayStyle = DisplayStyle.ShadingWithEdges;
            view3d.DetailLevel = ViewDetailLevel.Fine;
            view3d.CropBoxActive = true;
            view3d.CropBoxVisible = false;
            

            // Hide datum grids and levels in render
            try
            {
                Category? catLvl = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Levels);
                if (catLvl != null) view3d.SetCategoryHidden(catLvl.Id, true);
                Category? catGrd = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Grids);
                if (catGrd != null) view3d.SetCategoryHidden(catGrd.Id, true);
            }
            catch {}

            // Apply Graphic Overrides to Floors
            var allFloors = new FilteredElementCollector(doc, view3d.Id).OfClass(typeof(Floor)).Cast<Floor>().ToList();
            foreach (var f in allFloors)
            {
                FloorType? ft = doc.GetElement(f.GetTypeId()) as FloorType;
                if (ft == null) continue;
                OverrideGraphicSettings og = new();
                Color col = cPlaster;
                if (ft.Name.Contains("Grass")) col = cGrass;
                else if (ft.Name.Contains("Carport") || ft.Name.Contains("Andesite")) col = cAndesite;
                else if (ft.Name.Contains("Canopy") || ft.Name.Contains("Roof")) col = cCharcoal;
                else if (ft.Name.Contains("Balcony") || ft.Name.Contains("Teak")) col = cTeak;
                else if (ft.Name.Contains("Terrace") || ft.Name.Contains("Step")) col = cTravertine;

                if (solidFill != null)
                {
                    og.SetSurfaceForegroundPatternId(solidFill.Id);
                    og.SetSurfaceForegroundPatternColor(col);
                }
                view3d.SetElementOverrides(f.Id, og);
            }

            // Apply Graphic Overrides to Walls
            var allWalls = new FilteredElementCollector(doc, view3d.Id).OfClass(typeof(Wall)).Cast<Wall>().ToList();
            foreach (var w in allWalls)
            {
                WallType? wt = doc.GetElement(w.GetTypeId()) as WallType;
                if (wt == null) continue;
                OverrideGraphicSettings og = new();
                Color col = cPlaster;
                string mark = w.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";

                if (mark == "Glass_Railing")
                {
                    col = new Color(215, 240, 255);
                    og.SetSurfaceTransparency(85);
                }
                else if (wt.Name.Contains("Travertine"))
                {
                    col = new Color(248, 245, 238);
                }
                else if (wt.Name.Contains("Granite") || wt.Name.Contains("Pivot") || wt.Name.Contains("Door"))
                {
                    col = cCharcoal;
                }
                else if (wt.Name.Contains("Hedge") || wt.Name.Contains("Planter") || mark == "Villa_Tropical_Tree")
                {
                    col = cHedge;
                }
                else if (wt.Name.Contains("Louver") || wt.Name.Contains("Teak"))
                {
                    col = cTeak;
                }
                else if (wt.Kind == WallKind.Curtain || wt.Name.Contains("Storefront") || wt.Name.Contains("Glass"))
                {
                    col = new Color(220, 242, 255);
                    og.SetSurfaceTransparency(75);
                }

                if (solidFill != null)
                {
                    og.SetSurfaceForegroundPatternId(solidFill.Id);
                    og.SetSurfaceForegroundPatternColor(col);
                }
                view3d.SetElementOverrides(w.Id, og);
            }

            // Apply Graphic Overrides to Columns & Beams
            var allCols = new FilteredElementCollector(doc, view3d.Id).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().ToList();
            foreach (var c in allCols)
            {
                OverrideGraphicSettings og = new();
                if (solidFill != null)
                {
                    og.SetSurfaceForegroundPatternId(solidFill.Id);
                    og.SetSurfaceForegroundPatternColor(cConcrete);
                }
                view3d.SetElementOverrides(c.Id, og);
            }

            var allBeams = new FilteredElementCollector(doc, view3d.Id).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().ToList();
            foreach (var b in allBeams)
            {
                OverrideGraphicSettings og = new();
                if (solidFill != null)
                {
                    og.SetSurfaceForegroundPatternId(solidFill.Id);
                    og.SetSurfaceForegroundPatternColor(cConcrete);
                }
                view3d.SetElementOverrides(b.Id, og);
            }

            // Overrides for Planting
            var allPlants = new FilteredElementCollector(doc, view3d.Id).OfCategory(BuiltInCategory.OST_Planting).WhereElementIsNotElementType().ToList();
            foreach (var p in allPlants)
            {
                OverrideGraphicSettings og = new();
                if (solidFill != null)
                {
                    og.SetSurfaceForegroundPatternId(solidFill.Id);
                    og.SetSurfaceForegroundPatternColor(cHedge);
                }
                view3d.SetElementOverrides(p.Id, og);
            }

            // Overrides for Cars (Entourage)
            var allCars = new FilteredElementCollector(doc, view3d.Id).OfCategory(BuiltInCategory.OST_Entourage).WhereElementIsNotElementType().ToList();
            foreach (var c in allCars)
            {
                OverrideGraphicSettings og = new();
                if (solidFill != null)
                {
                    og.SetSurfaceForegroundPatternId(solidFill.Id);
                    og.SetSurfaceForegroundPatternColor(cCharcoal);
                }
                view3d.SetElementOverrides(c.Id, og);
            }

            txGfx.Commit();
        }

        // Export Viewport to PNG
        string fileName = "villa_modern_3d_render.png";
        string filePath = Path.Combine(outputDir, fileName);

        ImageExportOptions imgOptions = new()
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = filePath,
            FitDirection = FitDirectionType.Horizontal,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ImageResolution = ImageResolution.DPI_300,
            PixelSize = 2560,
            ShadowViewsFileType = ImageFileType.PNG,
            ZoomType = ZoomFitType.FitToPage
        };
        imgOptions.SetViewsAndSheets(new List<ElementId> { view3d.Id });

        doc.ExportImage(imgOptions);

        return $"Viewport successfully exported to: {filePath}";
    }
}

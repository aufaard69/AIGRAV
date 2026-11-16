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

public static class PisaTowerModeler
{
    private const double ToFt = 1.0 / 0.3048;

    private static double MToFt(double meters) => meters * ToFt;

    private static XYZ Pt(double xM, double yM, double zM)
    {
        return new XYZ(xM * ToFt, yM * ToFt, zM * ToFt);
    }

    public static string BuildPisaTower(Document doc, JsonElement op)
    {
        // 0. Clean old elements if requested
        bool clearOld = op.TryGetProperty("clear_existing", out JsonElement cEl) && cEl.GetBoolean();
        if (clearOld)
        {
            using Transaction txClean = new(doc, "AIGRAV - Clear Canvas for Pisa");
            TransactionFailureHandling.SuppressWarnings(txClean);
            txClean.Start();
            
            var toDelete = new List<ElementId>();
            toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Floor)).WhereElementIsNotElementType().ToElementIds());
            toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Wall)).WhereElementIsNotElementType().ToElementIds());
            toDelete.AddRange(new FilteredElementCollector(doc).OfClass(typeof(DirectShape)).WhereElementIsNotElementType().ToElementIds());
            
            var colIds = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType()
                .Where(fi => fi.Category != null && (
                    fi.Category.Id.Value == (long)BuiltInCategory.OST_Columns || 
                    fi.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns))
                .Select(e => e.Id);
            toDelete.AddRange(colIds);

            if (toDelete.Count > 0)
            {
                doc.Delete(toDelete);
            }
            txClean.Commit();
        }

        // 1. Resolve Materials
        Material matCarrara;
        Material matGrass;
        Material matPaving;
        Material matBronze;

        using (Transaction txMat = new(doc, "AIGRAV - Resolve Pisa Materials"))
        {
            TransactionFailureHandling.SuppressWarnings(txMat);
            txMat.Start();
            matCarrara = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Fasad - Marmer Putih Carrara (Pisa)",
                new[] { "carrara", "marmer", "marble" }, 245, 243, 238, 0, 85, 90);
            matGrass = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Lansekap - Rumput Hijau (Grass)",
                new[] { "grass", "rumput", "lawn" }, 55, 150, 45, 0, 15, 20);
            matPaving = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Perkerasan - Batu Plaza Miracoli",
                new[] { "paving", "stone", "andesit" }, 215, 212, 205, 0, 30, 40);
            matBronze = SpatialAgenticSandboxSkill.FindOrCreateMaterial(doc, "Logam - Perunggu Lonceng Pisa",
                new[] { "bronze", "perunggu", "metal" }, 185, 135, 65, 0, 110, 95);
            txMat.Commit();
        }

        // 2. Types Setup
        FloorType ftCarrara;
        FloorType ftGrass;
        FloorType ftPaving;
        WallType wtCarraraDrum;
        WallType wtCarraraShaft;
        WallType wtBalustrade;

        using (Transaction txTypes = new(doc, "AIGRAV - Setup Pisa Types"))
        {
            TransactionFailureHandling.SuppressWarnings(txTypes);
            txTypes.Start();
            ftCarrara = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Pisa - Pelat Marmer Carrara 300mm", matCarrara, 300.0);
            ftGrass = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Lansekap - Rumput Taman (Grass)", matGrass, 150.0);
            ftPaving = SpatialAgenticSandboxSkill.CreateOrGetFloorType(doc, "Pisa - Perkerasan Plaza 300mm", matPaving, 300.0);

            wtCarraraDrum = SpatialAgenticSandboxSkill.CreateOrGetWallType(doc, "Pisa - Dinding Basamento Marmer 800mm", matCarrara, 800.0);
            wtCarraraShaft = SpatialAgenticSandboxSkill.CreateOrGetWallType(doc, "Pisa - Dinding Inti Shaft 400mm", matCarrara, 400.0);
            wtBalustrade = SpatialAgenticSandboxSkill.CreateOrGetWallType(doc, "Pisa - Parapet Balustrade 200mm", matCarrara, 200.0);
            txTypes.Commit();
        }

        // 3. Load Column Family
        FamilySymbol? colSymbol = null;
        using (Transaction txColFam = new(doc, "AIGRAV - Load Round Column"))
        {
            TransactionFailureHandling.SuppressWarnings(txColFam);
            txColFam.Start();

            // Try find existing round column symbol
            colSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Category.Id.Value == (long)BuiltInCategory.OST_Columns ||
                                      fs.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns &&
                                      (fs.FamilyName.Contains("Round", StringComparison.OrdinalIgnoreCase) ||
                                       fs.Name.Contains("Round", StringComparison.OrdinalIgnoreCase)));

            if (colSymbol == null)
            {
                string rfaPath = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Columns\M_Round Column.rfa";
                if (!File.Exists(rfaPath))
                {
                    rfaPath = @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Structural Columns\Concrete\M_Concrete-Round-Column.rfa";
                }

                if (File.Exists(rfaPath) && doc.LoadFamily(rfaPath, out Family loadedFam))
                {
                    var symId = loadedFam.GetFamilySymbolIds().FirstOrDefault();
                    if (symId != null) colSymbol = doc.GetElement(symId) as FamilySymbol;
                }
            }

            if (colSymbol != null)
            {
                if (!colSymbol.IsActive) colSymbol.Activate();
                try
                {
                    FamilySymbol pisaColSym = (FamilySymbol)colSymbol.Duplicate("Pisa - Kolom Marmer 400mm");
                    // Try set diameter to 400mm (0.4m)
                    foreach (Parameter p in pisaColSym.Parameters)
                    {
                        if (p.Definition.Name.Equals("Diameter", StringComparison.OrdinalIgnoreCase) ||
                            p.Definition.Name.Equals("d", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!p.IsReadOnly) p.Set(MToFt(0.40));
                        }
                    }
                    colSymbol = pisaColSym;
                }
                catch { }
            }
            txColFam.Commit();
        }

        // 4. Geometry and Tier Definitions
        // Lean angle: 3.97 degrees toward +X
        const double leanAngleRad = 3.97 * Math.PI / 180.0;
        const double tanLean = 0.06938; // tan(3.97 deg)

        double LeanOffsetM(double zM) => zM * tanLean;

        // Tiers elevations (meters)
        double[] tierElevations = new[]
        {
            0.0,   // Level 1: Ground Plinth
            8.5,   // Level 2: Gallery 1
            14.3,  // Level 3: Gallery 2
            20.1,  // Level 4: Gallery 3
            25.9,  // Level 5: Gallery 4
            31.7,  // Level 6: Gallery 5
            37.5,  // Level 7: Gallery 6
            43.3,  // Level 8: Belfry Floor
            50.0,  // Level 9: Observation Platform
            51.2   // Puncak Balustrade
        };

        string[] tierNames = new[]
        {
            "01 - Piazza dei Miracoli (0.00m)",
            "02 - Galeri 1 Loggia (+8.50m)",
            "03 - Galeri 2 Loggia (+14.30m)",
            "04 - Galeri 3 Loggia (+20.10m)",
            "05 - Galeri 4 Loggia (+25.90m)",
            "06 - Galeri 5 Loggia (+31.70m)",
            "07 - Galeri 6 Loggia (+37.50m)",
            "08 - Ruang Lonceng Belfry (+43.30m)",
            "09 - Dek Observasi Puncak (+50.00m)",
            "10 - Parapet Puncak (+51.20m)"
        };

        List<Level> levels = new();
        using (Transaction txLvl = new(doc, "AIGRAV - Create Pisa Levels"))
        {
            TransactionFailureHandling.SuppressWarnings(txLvl);
            txLvl.Start();
            var existingLvls = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            for (int i = 0; i < tierElevations.Length; i++)
            {
                double targetElevFt = MToFt(tierElevations[i]);
                Level? matched = existingLvls.FirstOrDefault(l => Math.Abs(l.Elevation - targetElevFt) < 0.1);
                if (matched == null)
                {
                    matched = Level.Create(doc, targetElevFt);
                    matched.Name = tierNames[i];
                }
                levels.Add(matched);
            }
            txLvl.Commit();
        }

        // 5. Build Structure (Floors, Walls, Columns, Bells)
        int totalCols = 0;
        int totalFloors = 0;
        int totalWalls = 0;

        using (Transaction txBuild = new(doc, "AIGRAV - Build Leaning Tower of Pisa"))
        {
            TransactionFailureHandling.SuppressWarnings(txBuild);
            txBuild.Start();

            // 5.1 Lanskap Piazza dei Miracoli: Hamparan Rumput Hijau (76m x 76m) dengan void donat
            double grassHalf = 38.0; // 76 x 76m Piazza dei Miracoli
            double plazaRadius = 14.5; // Plaza bulat di dasar menara
            CurveLoop grassOuter = CreateRectLoopSimple(MToFt(grassHalf), MToFt(grassHalf), 0, true);
            CurveLoop grassInner = CreatePolygonLoop(0, 0, plazaRadius, 36, 0, false);
            Floor.Create(doc, new List<CurveLoop> { grassOuter, grassInner }, ftGrass.Id, levels[0].Id);
            totalFloors++;

            // 5.2 Plaza Marmer Lingkar di Dasar Menara (Radius 14.5m dengan void opening untuk Plinth 8.2m)
            CurveLoop plazaOuter = CreatePolygonLoop(0, 0, plazaRadius, 36, 0, true);
            CurveLoop plazaInner = CreatePolygonLoop(0, 0, 8.2, 32, 0, false);
            Floor.Create(doc, new List<CurveLoop> { plazaOuter, plazaInner }, ftPaving.Id, levels[0].Id);
            totalFloors++;

            // 5.3 Plinth Dasar Bertingkat (Radius 8.2m) pada Z=0.00m
            CurveLoop plinthLoop = CreatePolygonLoop(0, 0, 8.2, 32, 0, true);
            Floor.Create(doc, new List<CurveLoop> { plinthLoop }, ftCarrara.Id, levels[0].Id);
            totalFloors++;

            // 5.4 Tier 1 (Basamento): Dinding Silinder Marmer Pejal dengan Blind Arcade (Z=0 s/d 8.5m)
            // Tapered/Shifted center: Z=0 xc=0, Z=8.5 xc=8.5*tanLean
            const int segs = 30; // 30 facets around perimeter
            double rOuterBase = 7.75;
            double rCoreBase = 4.50;

            // Inner core shaft wall on Tier 1
            for (int k = 0; k < segs; k++)
            {
                double a1 = 2.0 * Math.PI * k / segs;
                double a2 = 2.0 * Math.PI * (k + 1) / segs;

                // Outer drum wall facets
                XYZ p1Out = Pt(rOuterBase * Math.Cos(a1), rOuterBase * Math.Sin(a1), 0);
                XYZ p2Out = Pt(rOuterBase * Math.Cos(a2), rOuterBase * Math.Sin(a2), 0);
                Wall wOut = Wall.Create(doc, Line.CreateBound(p1Out, p2Out), wtCarraraDrum.Id, levels[0].Id, MToFt(8.5), 0, false, false);
                totalWalls++;

                // Inner core shaft facets
                XYZ p1In = Pt(rCoreBase * Math.Cos(a1), rCoreBase * Math.Sin(a1), 0);
                XYZ p2In = Pt(rCoreBase * Math.Cos(a2), rCoreBase * Math.Sin(a2), 0);
                Wall wIn = Wall.Create(doc, Line.CreateBound(p1In, p2In), wtCarraraShaft.Id, levels[0].Id, MToFt(8.5), 0, false, false);
                totalWalls++;
            }

            // Engaged columns on Tier 1 blind arcade (15 engaged columns around outer perimeter)
            if (colSymbol != null)
            {
                for (int k = 0; k < 15; k++)
                {
                    double a = 2.0 * Math.PI * k / 15.0;
                    XYZ colPt = Pt(rOuterBase * Math.Cos(a), rOuterBase * Math.Sin(a), 0);
                    FamilyInstance ci = doc.Create.NewFamilyInstance(colPt, colSymbol, levels[0], StructuralType.NonStructural);
                    Parameter? pTop = ci.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (pTop != null && !pTop.IsReadOnly) pTop.Set(levels[1].Id);
                    totalCols++;
                }
            }

            // 5.5 Tiers 2 s/d 7: 6 Open Loggia Gallery Rings (Z=8.5 s/d 43.3m)
            // Setiap galeri memiliki:
            // - Lantai cincin donat marmer Carrara (R_out=7.75, R_in=4.5)
            // - Dinding inti silinder dalam (R=6.15m)
            // - 30 Kolom Marmer Bundar Loggia di radius R=7.35m
            // - Parapet pengaman di tepi luar setinggi 0.9m
            double rColRing = 7.35;
            double rInnerDrum = 6.15;

            for (int t = 1; t <= 6; t++)
            {
                double zBot = tierElevations[t];
                double zTop = tierElevations[t + 1];
                double hTier = zTop - zBot;
                Level lvlBot = levels[t];
                Level lvlTop = levels[t + 1];

                double xcBot = LeanOffsetM(zBot);
                double xcTop = LeanOffsetM(zTop);

                // Galeri Floor Slab (Donut Loop: Outer R=7.75, Inner R=4.5)
                CurveLoop floorOuter = CreatePolygonLoop(xcBot, 0, rOuterBase, segs, zBot, true);
                CurveLoop floorInner = CreatePolygonLoop(xcBot, 0, rCoreBase, segs, zBot, false);
                Floor.Create(doc, new List<CurveLoop> { floorOuter, floorInner }, ftCarrara.Id, lvlBot.Id);
                totalFloors++;

                // Dinding Inti Silinder Dalam (R=6.15m)
                for (int k = 0; k < segs; k++)
                {
                    double a1 = 2.0 * Math.PI * k / segs;
                    double a2 = 2.0 * Math.PI * (k + 1) / segs;

                    XYZ pw1 = Pt(xcBot + rInnerDrum * Math.Cos(a1), rInnerDrum * Math.Sin(a1), zBot);
                    XYZ pw2 = Pt(xcBot + rInnerDrum * Math.Cos(a2), rInnerDrum * Math.Sin(a2), zBot);
                    // Leave 2 openings per ring for entrance/exit to gallery
                    if (k != 0 && k != segs / 2)
                    {
                        Wall wShaft = Wall.Create(doc, Line.CreateBound(pw1, pw2), wtCarraraShaft.Id, lvlBot.Id, MToFt(hTier), 0, false, false);
                        totalWalls++;
                    }

                    // Low marble balustrade along outer rim (Height 0.9m)
                    XYZ pb1 = Pt(xcBot + rOuterBase * Math.Cos(a1), rOuterBase * Math.Sin(a1), zBot);
                    XYZ pb2 = Pt(xcBot + rOuterBase * Math.Cos(a2), rOuterBase * Math.Sin(a2), zBot);
                    Wall wBal = Wall.Create(doc, Line.CreateBound(pb1, pb2), wtBalustrade.Id, lvlBot.Id, MToFt(0.9), 0, false, false);
                    totalWalls++;
                }

                // 30 Loggia Columns around outer ring!
                if (colSymbol != null)
                {
                    for (int k = 0; k < segs; k++)
                    {
                        double a = 2.0 * Math.PI * k / segs;
                        XYZ colBase = Pt(xcBot + rColRing * Math.Cos(a), rColRing * Math.Sin(a), zBot);
                        FamilyInstance cInst = doc.Create.NewFamilyInstance(colBase, colSymbol, lvlBot, StructuralType.NonStructural);
                        Parameter? pTop = cInst.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (pTop != null && !pTop.IsReadOnly) pTop.Set(lvlTop.Id);
                        totalCols++;
                    }
                }
            }

            // 5.6 Tier 8: Cella Campanaria (Ruang Lonceng / Belfry) pada Z=43.3 s/d 50.0m
            double zBelfry = tierElevations[7]; // 43.3m
            double zRoof = tierElevations[8];   // 50.0m
            double hBelfry = zRoof - zBelfry;  // 6.7m
            Level lvlBelfry = levels[7];
            Level lvlRoof = levels[8];

            double xcBelfry = LeanOffsetM(zBelfry);
            double xcRoof = LeanOffsetM(zRoof);

            double rBelfryOuter = 5.80;
            double rBelfryCol = 5.30;
            double rBelfryCore = 3.60;

            // Belfry Floor Slab
            CurveLoop belfryFlOuter = CreatePolygonLoop(xcBelfry, 0, rBelfryOuter, 24, zBelfry, true);
            CurveLoop belfryFlInner = CreatePolygonLoop(xcBelfry, 0, rBelfryCore, 24, zBelfry, false);
            Floor.Create(doc, new List<CurveLoop> { belfryFlOuter, belfryFlInner }, ftCarrara.Id, lvlBelfry.Id);
            totalFloors++;

            // Belfry Colonnade: 16 Romanesque Columns around circumference
            const int belfryCols = 16;
            if (colSymbol != null)
            {
                for (int k = 0; k < belfryCols; k++)
                {
                    double a = 2.0 * Math.PI * k / belfryCols;
                    XYZ colPt = Pt(xcBelfry + rBelfryCol * Math.Cos(a), rBelfryCol * Math.Sin(a), zBelfry);
                    FamilyInstance cInst = doc.Create.NewFamilyInstance(colPt, colSymbol, lvlBelfry, StructuralType.NonStructural);
                    Parameter? pTop = cInst.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (pTop != null && !pTop.IsReadOnly) pTop.Set(lvlRoof.Id);
                    totalCols++;
                }
            }

            // Belfry Inner Chamber Walls
            for (int k = 0; k < belfryCols; k++)
            {
                if (k % 2 == 0) // Arched openings alternating with solid pier walls
                {
                    double a1 = 2.0 * Math.PI * k / belfryCols;
                    double a2 = 2.0 * Math.PI * (k + 1) / belfryCols;
                    XYZ pw1 = Pt(xcBelfry + rBelfryCore * Math.Cos(a1), rBelfryCore * Math.Sin(a1), zBelfry);
                    XYZ pw2 = Pt(xcBelfry + rBelfryCore * Math.Cos(a2), rBelfryCore * Math.Sin(a2), zBelfry);
                    Wall.Create(doc, Line.CreateBound(pw1, pw2), wtCarraraShaft.Id, lvlBelfry.Id, MToFt(hBelfry), 0, false, false);
                    totalWalls++;
                }
            }

            // 5.7 Top Observation Platform Slab & Parapet (Z=50.0m)
            CurveLoop roofOuter = CreatePolygonLoop(xcRoof, 0, rBelfryOuter, 24, zRoof, true);
            CurveLoop roofInner = CreatePolygonLoop(xcRoof, 0, 1.80, 16, zRoof, false);
            Floor.Create(doc, new List<CurveLoop> { roofOuter, roofInner }, ftCarrara.Id, lvlRoof.Id);
            totalFloors++;

            // Top Balustrade Parapet (Height 1.2m, Z=50.0 to 51.2m)
            for (int k = 0; k < 24; k++)
            {
                double a1 = 2.0 * Math.PI * k / 24;
                double a2 = 2.0 * Math.PI * (k + 1) / 24;
                XYZ pb1 = Pt(xcRoof + rBelfryOuter * Math.Cos(a1), rBelfryOuter * Math.Sin(a1), zRoof);
                XYZ pb2 = Pt(xcRoof + rBelfryOuter * Math.Cos(a2), rBelfryOuter * Math.Sin(a2), zRoof);
                Wall.Create(doc, Line.CreateBound(pb1, pb2), wtBalustrade.Id, lvlRoof.Id, MToFt(1.2), 0, false, false);
                totalWalls++;
            }

            // 5.8 Historic Bronze Bells in the Belfry (DirectShape OST_GenericModel with bronze material)
            try
            {
                List<GeometryObject> bellGeoms = new();
                // 3 bells at different positions inside the belfry
                double[] bellAngles = new[] { 0.0, 2.0 * Math.PI / 3.0, 4.0 * Math.PI / 3.0 };
                double rBellOrbit = 2.0;
                double zBell = zBelfry + 2.8;

                foreach (double ba in bellAngles)
                {
                    double bx = xcBelfry + rBellOrbit * Math.Cos(ba);
                    double by = rBellOrbit * Math.Sin(ba);

                    // Revolve or loft a bell shape
                    List<CurveLoop> bellLoops = new();
                    int bSegs = 12;
                    double[] bRads = new[] { 0.65, 0.50, 0.38, 0.30, 0.15 };
                    double[] bZs = new[] { 0.0, 0.40, 0.85, 1.20, 1.45 };

                    for (int bi = 0; bi < bRads.Length; bi++)
                    {
                        CurveLoop bLoop = CreatePolygonLoop(bx, by, bRads[bi], bSegs, zBell + bZs[bi], true);
                        bellLoops.Add(bLoop);
                    }

                    SolidOptions bso = new(matBronze.Id, ElementId.InvalidElementId);
                    Solid bSolid = GeometryCreationUtilities.CreateLoftGeometry(bellLoops, bso);
                    if (bSolid != null) bellGeoms.Add(bSolid);
                }

                if (bellGeoms.Count > 0)
                {
                    DirectShape dsBells = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    dsBells.SetShape(bellGeoms);
                    dsBells.Name = "Lonceng Kuno Menara Pisa (Campane della Torre di Pisa)";
                    Parameter? pMat = dsBells.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                    if (pMat != null && !pMat.IsReadOnly) pMat.Set(matBronze.Id);
                }
            }
            catch { }

            // 5.9 Set All 3D Views to Shaded View Mode and Reset Extents
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
            model = "Torre di Pisa (Leaning Tower of Pisa)",
            lean_angle_deg = 3.97,
            total_height_m = 51.2,
            total_tiers = 8,
            columns_created = totalCols,
            floors_created = totalFloors,
            walls_created = totalWalls,
            materials = new[]
            {
                "Fasad - Marmer Putih Carrara (Pisa)",
                "Lansekap - Rumput Hijau (Grass)",
                "Perkerasan - Batu Plaza Miracoli",
                "Logam - Perunggu Lonceng Pisa"
            }
        }, new JsonSerializerOptions { WriteIndented = true });
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

    private static CurveLoop CreateRectLoopSimple(double halfW, double halfH, double zFt, bool ccw)
    {
        CurveLoop loop = new();
        XYZ p1 = new(-halfW, -halfH, zFt);
        XYZ p2 = new(halfW, -halfH, zFt);
        XYZ p3 = new(halfW, halfH, zFt);
        XYZ p4 = new(-halfW, halfH, zFt);

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
}

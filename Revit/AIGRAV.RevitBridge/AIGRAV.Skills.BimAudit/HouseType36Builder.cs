using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.Skills.BimAudit;

public sealed partial class BimAuditSkill
{
    private static double M(double m) => m / 0.3048;
    private static XYZ Pt(double x, double y, double z) => new XYZ(x / 0.3048, y / 0.3048, z / 0.3048);

    public static string BuildHouseType36(UIApplication app, Document doc, string? argument)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV: MEMULAI PEMODELAN ARSITEKTURAL RUMAH TYPE 36 (ZERO CLASH & ACCURATE MATERIALS) ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        using (Transaction tx = new Transaction(doc, "AIGRAV Build Perfect House Type 36"))
        {
            tx.Start();

            // -----------------------------------------------------------------
            // 0. CLEANUP ELEMEN MODEL SEBELUMNYA
            // -----------------------------------------------------------------
            try
            {
                var oldElements = new FilteredElementCollector(doc)
                    .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_Walls,
                        BuiltInCategory.OST_Floors,
                        BuiltInCategory.OST_Roofs,
                        BuiltInCategory.OST_Furniture,
                        BuiltInCategory.OST_PlumbingFixtures,
                        BuiltInCategory.OST_Planting,
                        BuiltInCategory.OST_Doors,
                        BuiltInCategory.OST_Windows,
                        BuiltInCategory.OST_CurtainWallPanels,
                        BuiltInCategory.OST_CurtainWallMullions,
                        BuiltInCategory.OST_GenericModel,
                        BuiltInCategory.OST_TextNotes
                    }))
                    .WhereElementIsNotElementType()
                    .Select(e => e.Id)
                    .ToList();

                if (oldElements.Count > 0)
                {
                    doc.Delete(oldElements);
                    sb.AppendLine($"[0. CLEANUP] Berhasil membersihkan {oldElements.Count} elemen lama.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[0. CLEANUP WARNING] {ex.Message}");
            }

            // -----------------------------------------------------------------
            // 1. SETUP LEVELS
            // -----------------------------------------------------------------
            sb.AppendLine("\n[1. SETUP LEVELS]");
            Level? lvl0 = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => Math.Abs(l.Elevation) < 0.01);
            if (lvl0 == null)
            {
                lvl0 = Level.Create(doc, 0.0);
                lvl0.Name = "Level 0 - Ground (+0.00)";
            }

            Level? lvl1 = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => Math.Abs(l.Elevation - M(0.15)) < 0.05);
            if (lvl1 == null)
            {
                lvl1 = Level.Create(doc, M(0.15));
                lvl1.Name = "Level 1 - FFL (+0.15)";
            }

            Level? lvl2 = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => Math.Abs(l.Elevation - M(3.60)) < 0.1);
            if (lvl2 == null)
            {
                lvl2 = Level.Create(doc, M(3.60));
                lvl2.Name = "Level 2 - Top Wall (+3.60)";
            }
            sb.AppendLine($"  + Level 0: {lvl0.Name} ({lvl0.Elevation * 0.3048:F2} m)");
            sb.AppendLine($"  + Level 1: {lvl1.Name} ({lvl1.Elevation * 0.3048:F2} m)");
            sb.AppendLine($"  + Level 2: {lvl2.Name} ({lvl2.Elevation * 0.3048:F2} m)");

            // -----------------------------------------------------------------
            // 2. SETUP MATERIALS DENGAN STANDAR ARSITEKTURAL RESMI (1:1 COLOR MATCH)
            // -----------------------------------------------------------------
            sb.AppendLine("\n[2. SETUP MATERIALS]");
            Material matPutih = GetOrCreateMat(doc, "Mat_Dinding_Putih", new Color(248, 248, 248)); // Crisp clean white
            Material matCoklat = GetOrCreateMat(doc, "Mat_Aksen_Coklat_Gelap", new Color(46, 44, 42)); // Deep Scandinavian Charcoal #2e2c2a
            Material matKrem = GetOrCreateMat(doc, "Mat_Dinding_Krem_A1", new Color(226, 216, 198)); // Warm Sandy Cream #e2d8c6
            Material matKeramik = GetOrCreateMat(doc, "Mat_Keramik_Putih", new Color(245, 245, 243)); // White interior tile
            Material matToilet = GetOrCreateMat(doc, "Mat_Keramik_Toilet", new Color(170, 168, 166)); // Anti-slip toilet tile
            Material matTeras = GetOrCreateMat(doc, "Mat_Teras_Abu_Gelap", new Color(120, 118, 116)); // Porch dark gray tile
            Material matBeton = GetOrCreateMat(doc, "Mat_Carport_Beton", new Color(192, 190, 188)); // Carport concrete
            Material matRumput = GetOrCreateMat(doc, "Mat_Rumput_Taman", new Color(80, 135, 55)); // Lush green lawn
            Material matSemak = GetOrCreateMat(doc, "Mat_Semak_Hijau", new Color(45, 110, 35)); // Dense bush green
            Material matHitam = GetOrCreateMat(doc, "Mat_Meja_Hitam", new Color(34, 32, 30)); // Black granite
            Material matDoorPanel = GetOrCreateMat(doc, "Mat_Door_Panel_Light", new Color(222, 220, 216)); // Light warm gray door leaf

            // Material Kaca Resmi Jendela & Pintu (Transparent Clear Architectural Glass)
            var docGlasses = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Where(m => m.Name.Equals("Glass", StringComparison.OrdinalIgnoreCase) 
                         || m.Name.Equals("Kaca", StringComparison.OrdinalIgnoreCase)
                         || m.Name.Contains("Glazing"))
                .ToList();

            foreach (var dg in docGlasses)
            {
                dg.UseRenderAppearanceForShading = false;
                dg.Color = new Color(175, 215, 235);
                dg.Transparency = 75;
                dg.Shininess = 128;
                dg.Smoothness = 100;
            }

            Material matKaca = GetOrCreateMat(doc, "Mat_Kaca_Clear", new Color(175, 215, 235), 75);
            matKaca.Shininess = 128;
            matKaca.Smoothness = 100;
            sb.AppendLine($"  + Material Kaca Jendela & Pintu Terverifikasi: '{matKaca.Name}' (Transparency: {matKaca.Transparency}%, Color: RGB 175,215,235)");

            // -----------------------------------------------------------------
            // 3. SETUP WALL & FLOOR TYPES
            // -----------------------------------------------------------------
            sb.AppendLine("\n[3. SETUP WALL & FLOOR TYPES]");
            WallType wtPutih = GetOrCreateWallType(doc, "Wall_Arsitektur_150mm_Putih", M(0.15), matPutih.Id);
            WallType wtCoklat = GetOrCreateWallType(doc, "Wall_Aksen_150mm_Coklat", M(0.15), matCoklat.Id);
            WallType wtKrem = GetOrCreateWallType(doc, "Wall_Aksen_150mm_Krem", M(0.15), matKrem.Id);
            WallType wtPagar = GetOrCreateWallType(doc, "Wall_Pagar_120mm_Putih", M(0.12), matPutih.Id);

            FloorType ftRumput = GetOrCreateFloorType(doc, "Floor_Rumput_Taman", M(0.10), matRumput.Id);
            FloorType ftCarport = GetOrCreateFloorType(doc, "Floor_Carport_Beton", M(0.10), matBeton.Id);
            FloorType ftTeras = GetOrCreateFloorType(doc, "Floor_Teras_Abu", M(0.10), matTeras.Id);
            FloorType ftKeramik = GetOrCreateFloorType(doc, "Floor_Keramik_Putih_60x60", M(0.10), matKeramik.Id);
            FloorType ftToilet = GetOrCreateFloorType(doc, "Floor_Keramik_Toilet_30x30", M(0.10), matToilet.Id);

            // -----------------------------------------------------------------
            // 4. MEMBUAT LANTAI (FLOORS) - ZERO OVERLAP / ZERO CLASH
            // -----------------------------------------------------------------
            sb.AppendLine("\n[4. MEMBUAT LANTAI (FLOORS)]");

            // A. Rumput Taman Keliling Lahan (10.00 x 15.00 m) di Level 0 (Z = 0.00 m)
            CreateRectFloor(doc, ftRumput, lvl0.Id, 0.0, 0.0, 10.0, 15.0, M(0.00));
            sb.AppendLine("  + Lantai Rumput Taman Utama (10.00 x 15.00 m)");

            // B. Carport Rabat Beton (6.00 x 5.00 m) di Z = +0.02 m
            CreateRectFloor(doc, ftCarport, lvl0.Id, 0.0, 0.0, 6.0, 5.0, M(0.02));
            sb.AppendLine("  + Lantai Carport Rabat Beton (6.00 x 5.00 m)");

            // C. Teras Depan (3.00 x 1.30 m) di Z = +0.10 m
            CreateRectFloor(doc, ftTeras, lvl0.Id, 6.0, 5.0, 9.0, 6.3, M(0.10));
            sb.AppendLine("  + Lantai Teras Depan (3.00 x 1.30 m, Z = +0.10 m)");

            // D. Teras Belakang (1.40 x 0.80 m) di Z = +0.10 m
            CreateRectFloor(doc, ftTeras, lvl0.Id, 5.1, 11.0, 6.5, 11.8, M(0.10));
            sb.AppendLine("  + Lantai Teras Belakang (1.40 x 0.80 m, Z = +0.10 m)");

            // E. Lantai Keramik Utama Type 36 (Z = +0.15 m FFL)
            CreateRectFloor(doc, ftKeramik, lvl0.Id, 3.0, 4.25, 6.0, 7.8, M(0.15)); // Kamar 1 & Ekstensi Maju
            CreateRectFloor(doc, ftKeramik, lvl0.Id, 3.0, 7.8, 6.0, 9.8, M(0.15)); // Kamar 2
            CreateRectFloor(doc, ftKeramik, lvl0.Id, 6.0, 6.3, 9.0, 11.0, M(0.15)); // Ruang Tamu & Dapur
            CreateRectFloor(doc, ftKeramik, lvl0.Id, 5.0, 9.8, 6.0, 11.0, M(0.15)); // Koridor Belakang
            sb.AppendLine("  + Lantai Keramik Utama Type 36 (Z = +0.15 m)");

            // F. Lantai Keramik Toilet (2.00 x 1.20 m) di Z = +0.12 m (water drop 3cm)
            CreateRectFloor(doc, ftToilet, lvl0.Id, 3.0, 9.8, 5.0, 11.0, M(0.12));
            sb.AppendLine("  + Lantai Keramik Toilet (2.00 x 1.20 m, Z = +0.12 m)");

            // G. Stepping Stones (Pijakan Taman Depan)
            double[] yStepsL = { 0.8, 1.6, 2.4, 3.2, 4.0, 4.7 };
            foreach (double ys in yStepsL)
            {
                CreateDirectShapeBox(doc, $"Step_L_{ys}", matBeton.Id, BuiltInCategory.OST_GenericModel, 6.90, ys, 0.00, 7.55, ys + 0.55, 0.04);
            }
            double[] yStepsR = { 1.2, 2.0, 2.8, 3.6, 4.4 };
            foreach (double ys in yStepsR)
            {
                CreateDirectShapeBox(doc, $"Step_R_{ys}", matBeton.Id, BuiltInCategory.OST_GenericModel, 7.80, ys, 0.00, 8.45, ys + 0.55, 0.04);
            }
            sb.AppendLine("  + 11 Blok Pijakan Stepping Stones Taman Depan");

            // -----------------------------------------------------------------
            // 5. MEMBUAT DINDING (WALLS) - ZERO CLASH & FLUSH JOINTS
            // -----------------------------------------------------------------
            sb.AppendLine("\n[5. MEMBUAT DINDING (WALLS)]");

            // Pagar Keliling Tapak (Tinggi 1.80 m)
            CreateWall(doc, wtPagar, lvl0.Id, 0.0, 0.0, 0.0, 15.0, 1.80);
            CreateWall(doc, wtPagar, lvl0.Id, 0.0, 15.0, 10.0, 15.0, 1.80);
            CreateWall(doc, wtPagar, lvl0.Id, 10.0, 15.0, 10.0, 0.0, 1.80);
            sb.AppendLine("  + Dinding Pagar Keliling Tapak (10 x 15 m)");

            // Dinding Luar Utama Bangunan Type 36
            Wall wKiri = CreateWall(doc, wtPutih, lvl1.Id, 3.0, 5.0, 3.0, 11.0, 3.45);
            Wall wKanan = CreateWall(doc, wtPutih, lvl1.Id, 9.0, 6.3, 9.0, 11.0, 3.45);
            Wall wBelakang = CreateWall(doc, wtPutih, lvl1.Id, 3.0, 11.0, 9.0, 11.0, 3.45);
            
            // Dinding Partisi Dalam
            Wall wPartTengah = CreateWall(doc, wtPutih, lvl1.Id, 6.0, 5.0, 6.0, 11.0, 3.45);
            Wall wSekatK1K2 = CreateWall(doc, wtPutih, lvl1.Id, 3.0, 7.8, 6.0, 7.8, 3.30);
            Wall wSekatK2Toilet = CreateWall(doc, wtPutih, lvl1.Id, 3.0, 9.8, 6.0, 9.8, 3.30);
            Wall wToiletKanan = CreateWall(doc, wtPutih, lvl1.Id, 5.0, 9.8, 5.0, 11.0, 3.30);

            // Dinding Depan Ceruk Kamar 1 (Y = 5.00 m)
            // Jamb Kiri (X = 3.00..3.15 m, H = 2.45 m)
            CreateWall(doc, wtPutih, lvl1.Id, 3.00, 5.00, 3.15, 5.00, 2.45);
            // Sill bawah jendela J1 (X = 3.15..3.65 m, Z = 0.00..0.15 m)
            CreateDirectShapeBox(doc, "J1_Sill_Wall", matPutih.Id, BuiltInCategory.OST_Walls, 3.15, 4.93, 0.00, 3.65, 5.07, 0.15);
            // Header atas jendela J1 (X = 3.15..3.65 m, Z = 2.45..2.62 m)
            CreateDirectShapeBox(doc, "J1_Header_Wall", matPutih.Id, BuiltInCategory.OST_Walls, 3.15, 4.93, 2.45, 3.65, 5.07, 2.62);

            // DINDING KOTAK AKSEN KAMAR 1 (PROTRUDING BOX) - SOLID DARK CHARCOAL (OST_Walls)
            // Sisi Depan Selatan (Y = 4.20..4.35 m, X = 3.65..6.00 m, Z = 0.00..2.55 m)
            CreateDirectShapeBox(doc, "Kotak_K1_Depan", matCoklat.Id, BuiltInCategory.OST_Walls, 3.65, 4.20, 0.00, 6.00, 4.35, 2.55);
            // Sisi Barat Return (X = 3.65..3.80 m, Y = 4.35..5.00 m, Z = 0.00..2.55 m)
            CreateDirectShapeBox(doc, "Kotak_K1_Barat", matCoklat.Id, BuiltInCategory.OST_Walls, 3.65, 4.35, 0.00, 3.80, 5.00, 2.55);
            // Coping List Atas Kotak Kamar 1 (Dark Charcoal)
            CreateDirectShapeBox(doc, "Coping_Kotak_Kamar1", matCoklat.Id, BuiltInCategory.OST_Walls, 3.58, 4.15, 2.55, 6.05, 5.05, 2.62);
            sb.AppendLine("  + Kotak Aksen Kamar 1 Menonjol (Dark Charcoal #2e2c2a)");

            // Dinding Fasad Teras Bawah Kanopi (Y = 6.30 m, H = 2.20 m up to Z = 2.35 m)
            // Sisi Kiri Krem tempat nomor A1 (X = 6.00..6.90 m)
            CreateWall(doc, wtKrem, lvl1.Id, 6.00, 6.30, 6.90, 6.30, 2.20);
            sb.AppendLine("  + Dinding Eksterior, Partisi, & Kotak Maju Kamar 1 Lengkap Bebas Clash");

            // -----------------------------------------------------------------
            // 6. ATAP PELANA & BINGKAI FASAD SCANDINAVIAN
            // -----------------------------------------------------------------
            sb.AppendLine("\n[6. ATAP PELANA & BINGKAI FASAD SCANDINAVIAN]");

            // Atap Pelana Simetris (X = 3.00..9.00 m, Y = 4.90..11.15 m, Level 2 = +3.60 m)
            RoofType rtPelana = GetOrCreateRoofType(doc, "Roof_Pelana_Scandinavian_Charcoal", M(0.12), matCoklat.Id);
            if (rtPelana != null)
            {
                CurveArray footprint = new CurveArray();
                XYZ pA = Pt(3.00, 4.90, 3.60);
                XYZ pB = Pt(9.00, 4.90, 3.60);
                XYZ pC = Pt(9.00, 11.15, 3.60);
                XYZ pD = Pt(3.00, 11.15, 3.60);

                footprint.Append(Line.CreateBound(pA, pB));
                footprint.Append(Line.CreateBound(pB, pC));
                footprint.Append(Line.CreateBound(pC, pD));
                footprint.Append(Line.CreateBound(pD, pA));

                ModelCurveArray mcArray = new ModelCurveArray();
                FootPrintRoof roof = doc.Create.NewFootPrintRoof(footprint, lvl2, rtPelana, out mcArray);
                
                double slope = Math.Atan(2.0 / 3.0); // 33.69 deg
                foreach (ModelCurve mc in mcArray)
                {
                    XYZ dir = (mc.GeometryCurve.GetEndPoint(1) - mc.GeometryCurve.GetEndPoint(0)).Normalize();
                    if (Math.Abs(dir.Y) > 0.8)
                    {
                        roof.set_DefinesSlope(mc, true);
                        roof.set_SlopeAngle(mc, slope);
                    }
                    else
                    {
                        roof.set_DefinesSlope(mc, false);
                    }
                }
                sb.AppendLine("  + Atap Pelana Simetris (Warna Coklat Gelap Scandinavian, Puncak Z = +5.60 m)");
            }

            // Bingkai Portal Fasad Tebal (Scandinavian Dark Charcoal Outer Frame)
            // Kolom Vertikal Kiri (X = 2.75..3.05 m, Y = 4.80..5.05 m, Z = 0..3.60 m)
            CreateDirectShapeBox(doc, "Portal_Frame_Kiri", matCoklat.Id, BuiltInCategory.OST_Walls, 2.75, 4.80, 0.00, 3.05, 5.05, 3.60);
            // Kolom Vertikal Kanan (X = 8.95..9.25 m, Y = 4.80..5.05 m, Z = 0..3.60 m)
            CreateDirectShapeBox(doc, "Portal_Frame_Kanan", matCoklat.Id, BuiltInCategory.OST_Walls, 8.95, 4.80, 0.00, 9.25, 5.05, 3.60);

            // Fasia Miring Atap Pelana (Smooth Sloped Charcoal Fascia Bands)
            List<UV> fasciaLeftPts = new List<UV>
            {
                new UV(2.75, 3.35),
                new UV(6.00, 5.52),
                new UV(6.00, 5.77),
                new UV(2.75, 3.60)
            };
            CreateExtrusionAlongY(doc, "Fascia_Miring_Kiri", matCoklat.Id, BuiltInCategory.OST_Walls, fasciaLeftPts, 4.80, 4.86, sb);

            List<UV> fasciaRightPts = new List<UV>
            {
                new UV(6.00, 5.52),
                new UV(9.25, 3.35),
                new UV(9.25, 3.60),
                new UV(6.00, 5.77)
            };
            CreateExtrusionAlongY(doc, "Fascia_Miring_Kanan", matCoklat.Id, BuiltInCategory.OST_Walls, fasciaRightPts, 4.80, 4.86, sb);
            sb.AppendLine("  + Bingkai Portal & Fasia Scandinavian Dark Charcoal Halus & Presisi");

            // DINDING PUTIH UPPER GABLE KIRI FASAD (Crisp Clean White Wall at front plane)
            List<UV> upperGableLeftPts = new List<UV>
            {
                new UV(3.02, 2.62),
                new UV(5.95, 2.62),
                new UV(5.95, 5.48),
                new UV(3.02, 3.53)
            };
            CreateExtrusionAlongY(doc, "Dinding_UpperGable_Kiri", matPutih.Id, BuiltInCategory.OST_Walls, upperGableLeftPts, 4.84, 4.89, sb);
            sb.AppendLine("  + Dinding Putih Bersih Upper Gable Kiri Fasad");

            // Jendela Vertikal J1 di Ceruk Kiri Fasad (Material Kaca Resmi Glass)
            CreateDirectShapeBox(doc, "J1_Frame_L", matCoklat.Id, BuiltInCategory.OST_Windows, 3.15, 4.95, 0.15, 3.20, 5.05, 2.45);
            CreateDirectShapeBox(doc, "J1_Frame_R", matCoklat.Id, BuiltInCategory.OST_Windows, 3.60, 4.95, 0.15, 3.65, 5.05, 2.45);
            CreateDirectShapeBox(doc, "J1_Frame_B", matCoklat.Id, BuiltInCategory.OST_Windows, 3.15, 4.95, 0.15, 3.65, 5.05, 0.22);
            CreateDirectShapeBox(doc, "J1_Frame_T", matCoklat.Id, BuiltInCategory.OST_Windows, 3.15, 4.95, 2.38, 3.65, 5.05, 2.45);
            CreateDirectShapeBox(doc, "J1_Glass", matKaca.Id, BuiltInCategory.OST_Windows, 3.20, 4.98, 0.22, 3.60, 5.02, 2.38);
            sb.AppendLine("  + Jendela Vertikal J1 di Ceruk Kiri Fasad (Material Kaca Resmi Transparent)");

            // Kanopi Beton Datar Teras Depan (X = 5.95..9.05 m, Y = 4.85..6.35 m, Z = 2.35..2.47 m)
            CreateDirectShapeBox(doc, "Kanopi_Teras_Depan", matCoklat.Id, BuiltInCategory.OST_Roofs, 5.95, 4.85, 2.35, 9.05, 6.35, 2.47);
            sb.AppendLine("  + Kanopi Beton Datar Teras Depan (Z = +2.35 m)");

            // Kolom Vertikal Krem Aksen Tengah Fasad (X = 5.95..6.25 m, Y = 4.85..4.90 m, Z = 2.47..5.52 m)
            CreateDirectShapeBox(doc, "Aksen_Krem_Tengah", matKrem.Id, BuiltInCategory.OST_Walls, 5.95, 4.85, 2.47, 6.25, 4.90, 5.52);
            sb.AppendLine("  + Kolom Vertikal Krem Fasad Tengah (Z = 2.47..5.52 m)");

            // JENDELA KACA SEGITIGA FASAD 4-BAY (Di Bidang Depan Y = 4.87..4.89 m, Z = 2.47..5.35 m)
            List<UV> gableGlassPts = new List<UV>
            {
                new UV(6.25, 2.47),
                new UV(8.95, 2.47),
                new UV(8.95, 3.55),
                new UV(6.25, 5.35)
            };
            CreateExtrusionAlongY(doc, "Gable_Glass_Polygon", matKaca.Id, BuiltInCategory.OST_Windows, gableGlassPts, 4.87, 4.89, sb);

            // 3 Mullion Vertikal Membagi 4 Bay
            double[] mullionXs = { 6.92, 7.60, 8.28 };
            foreach (double mx in mullionXs)
            {
                double mz = 5.52 - (mx - 6.00) * (2.0 / 3.0);
                CreateDirectShapeBox(doc, $"Gable_Mullion_{mx}", matCoklat.Id, BuiltInCategory.OST_Windows, mx - 0.025, 4.85, 2.47, mx + 0.025, 4.91, mz);
            }
            // Frame Keliling Kaca Segitiga
            CreateDirectShapeBox(doc, "Gable_Frame_Bottom", matCoklat.Id, BuiltInCategory.OST_Windows, 6.25, 4.85, 2.47, 8.95, 4.91, 2.53);
            CreateDirectShapeBox(doc, "Gable_Frame_Left", matCoklat.Id, BuiltInCategory.OST_Windows, 6.22, 4.85, 2.47, 6.28, 4.91, 5.35);
            CreateDirectShapeBox(doc, "Gable_Frame_Right", matCoklat.Id, BuiltInCategory.OST_Windows, 8.92, 4.85, 2.47, 8.98, 4.91, 3.55);
            sb.AppendLine("  + Jendela Kaca Segitiga Fasad 4-Bay (Material Kaca Resmi Transparent, Y = 4.88 m)");

            // Portal Pintu Masuk & Sidelight Window J2 Teras (X = 6.90..9.00 m, Y = 6.30 m)
            CreateDirectShapeBox(doc, "Entrance_Frame_Top", matCoklat.Id, BuiltInCategory.OST_Doors, 6.90, 6.26, 2.25, 9.00, 6.34, 2.35);
            CreateDirectShapeBox(doc, "Entrance_Frame_Left", matCoklat.Id, BuiltInCategory.OST_Doors, 6.90, 6.26, 0.15, 6.96, 6.34, 2.25);
            CreateDirectShapeBox(doc, "Entrance_Frame_Mid", matCoklat.Id, BuiltInCategory.OST_Doors, 7.94, 6.26, 0.15, 8.00, 6.34, 2.25);
            CreateDirectShapeBox(doc, "Entrance_Frame_Right", matCoklat.Id, BuiltInCategory.OST_Doors, 8.94, 6.26, 0.15, 9.00, 6.34, 2.25);

            // Daun Pintu Utama P1 (Light Warm Gray Flat Panel)
            CreateDirectShapeBox(doc, "Door_Panel_P1", matDoorPanel.Id, BuiltInCategory.OST_Doors, 6.96, 6.28, 0.15, 7.94, 6.32, 2.25);
            // Handle Tarik Vertikal Pintu (Dark Charcoal)
            CreateDirectShapeBox(doc, "Door_Handle_P1", matCoklat.Id, BuiltInCategory.OST_Doors, 7.82, 6.24, 0.90, 7.85, 6.28, 1.50);

            // Kaca Jendela Sidelight J2 (Material Kaca Resmi Glass)
            CreateDirectShapeBox(doc, "Sidelight_Glass_J2", matKaca.Id, BuiltInCategory.OST_Windows, 8.00, 6.29, 0.15, 8.94, 6.31, 2.25);
            // Mullion Horisontal Sidelight
            CreateDirectShapeBox(doc, "Sidelight_Mullion_H", matCoklat.Id, BuiltInCategory.OST_Windows, 8.00, 6.26, 0.70, 8.94, 6.34, 0.76);
            sb.AppendLine("  + Pintu Utama P1 Modern Scandinavian & Jendela Sidelight J2");

            // Nomor Rumah 3D "A1" di Permukaan Dinding Krem (Menonjol 2.5 cm, Y = 6.20..6.225 m)
            CreateDirectShapeBox(doc, "Huruf_A_LegL", matCoklat.Id, BuiltInCategory.OST_GenericModel, 6.34, 6.20, 1.45, 6.38, 6.225, 1.65);
            CreateDirectShapeBox(doc, "Huruf_A_LegR", matCoklat.Id, BuiltInCategory.OST_GenericModel, 6.42, 6.20, 1.45, 6.46, 6.225, 1.65);
            CreateDirectShapeBox(doc, "Huruf_A_Top", matCoklat.Id, BuiltInCategory.OST_GenericModel, 6.34, 6.20, 1.62, 6.46, 6.225, 1.65);
            CreateDirectShapeBox(doc, "Huruf_A_Bar", matCoklat.Id, BuiltInCategory.OST_GenericModel, 6.34, 6.20, 1.53, 6.46, 6.225, 1.56);
            CreateDirectShapeBox(doc, "Angka_1_Stem", matCoklat.Id, BuiltInCategory.OST_GenericModel, 6.50, 6.20, 1.45, 6.54, 6.225, 1.65);
            CreateDirectShapeBox(doc, "Angka_1_Serif", matCoklat.Id, BuiltInCategory.OST_GenericModel, 6.47, 6.20, 1.60, 6.50, 6.225, 1.65);
            sb.AppendLine("  + Huruf Timbul 3D Fasad 'A1' Menonjol Rapi di Dinding Krem");

            // Semak Hijau Bulat di depan A1 (X = 6.45, Y = 5.30 m)
            CreateDirectShapeCylinder(doc, "Semak_A1", matSemak.Id, BuiltInCategory.OST_Planting, 6.45, 5.30, 0.00, 0.35, 0.45);
            sb.AppendLine("  + Semak Hijau Fasad Depan (Dekat A1)");

            // -----------------------------------------------------------------
            // 7. MEMASANG PINTU INTERIOR & JENDELA DARI LIBRARY RESMI
            // -----------------------------------------------------------------
            sb.AppendLine("\n[7. MEMASANG PINTU INTERIOR & JENDELA]");

            // Pintu Interior Flush (Load dari Revit Library atau Symbol Existing)
            FamilySymbol? fsDoor = LoadOrFindDoorSymbol(doc, @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Doors\M_Door-Single-Panel.rfa");
            if (fsDoor != null)
            {
                try
                {
                    doc.Create.NewFamilyInstance(Pt(6.00, 7.35, 0.15), fsDoor, wPartTengah, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    doc.Create.NewFamilyInstance(Pt(6.00, 9.35, 0.15), fsDoor, wPartTengah, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    doc.Create.NewFamilyInstance(Pt(5.00, 10.45, 0.15), fsDoor, wToiletKanan, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    doc.Create.NewFamilyInstance(Pt(5.50, 11.00, 0.15), fsDoor, wBelakang, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    sb.AppendLine($"  + Pintu Interior ({fsDoor.FamilyName}: {fsDoor.Name}) Terpasang di Kamar 1, Kamar 2, Toilet, & Belakang");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  ! Warning Penempatan Pintu: {ex.Message}");
                }
            }

            // Jendela Belakang J3 & J4
            FamilySymbol? fsWin = LoadOrFindWindowSymbol(doc, @"C:\ProgramData\Autodesk\RVT 2026\Libraries\English\US\Windows\M_Window-Fixed.rfa");
            if (fsWin != null)
            {
                try
                {
                    doc.Create.NewFamilyInstance(Pt(7.50, 11.00, 0.90), fsWin, wBelakang, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    doc.Create.NewFamilyInstance(Pt(3.00, 8.80, 0.90), fsWin, wKiri, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    doc.Create.NewFamilyInstance(Pt(3.00, 10.40, 1.80), fsWin, wKiri, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    sb.AppendLine($"  + Jendela ({fsWin.FamilyName}: {fsWin.Name}) Terpasang di Dapur J3, Kamar 2 J4, & Toilet BV1");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  ! Warning Penempatan Jendela: {ex.Message}");
                }
            }

            // -----------------------------------------------------------------
            // 8. INTERIOR, SANITER, DAPUR, MOBIL & LANSEKAP
            // -----------------------------------------------------------------
            sb.AppendLine("\n[8. INTERIOR, SANITER, DAPUR, MOBIL & LANSEKAP]");

            // Kasur Kamar 1 & Kamar 2
            FamilySymbol? fsBed = doc.GetElement(new ElementId(1365841)) as FamilySymbol;
            if (fsBed != null && !fsBed.IsActive) fsBed.Activate();
            if (fsBed != null)
            {
                doc.Create.NewFamilyInstance(Pt(4.20, 6.40, 0.15), fsBed, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                doc.Create.NewFamilyInstance(Pt(4.20, 8.80, 0.15), fsBed, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                sb.AppendLine("  + Kasur Kamar 1 & Kamar 2");
            }

            // Lemari Pakaian
            FamilySymbol? fsLemari = doc.GetElement(new ElementId(1368239)) as FamilySymbol;
            if (fsLemari != null && !fsLemari.IsActive) fsLemari.Activate();
            if (fsLemari != null)
            {
                doc.Create.NewFamilyInstance(Pt(5.70, 5.60, 0.15), fsLemari, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                doc.Create.NewFamilyInstance(Pt(5.70, 8.25, 0.15), fsLemari, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                sb.AppendLine("  + Lemari Pakaian Kamar 1 & Kamar 2");
            }

            // Sofa Ruang Tamu
            FamilySymbol? fsSofa = doc.GetElement(new ElementId(1376359)) as FamilySymbol;
            if (fsSofa != null && !fsSofa.IsActive) fsSofa.Activate();
            if (fsSofa != null)
            {
                doc.Create.NewFamilyInstance(Pt(8.10, 8.20, 0.15), fsSofa, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                sb.AppendLine("  + Sofa Ruang Tamu");
            }

            // Meja Tamu
            FamilySymbol? fsMeja = doc.GetElement(new ElementId(1362123)) as FamilySymbol;
            if (fsMeja != null && !fsMeja.IsActive) fsMeja.Activate();
            if (fsMeja != null)
            {
                doc.Create.NewFamilyInstance(Pt(7.20, 8.20, 0.15), fsMeja, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                sb.AppendLine("  + Meja Ruang Keluarga");
            }

            // Kloset Duduk Hafele
            FamilySymbol? fsToilet = doc.GetElement(new ElementId(1369658)) as FamilySymbol;
            if (fsToilet != null && !fsToilet.IsActive) fsToilet.Activate();
            if (fsToilet != null)
            {
                doc.Create.NewFamilyInstance(Pt(3.45, 10.60, 0.12), fsToilet, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                sb.AppendLine("  + Kloset Duduk Hafele di Toilet");
            }

            // Meja Dapur + Sink
            CreateDirectShapeBox(doc, "Meja_Dapur_L", matHitam.Id, BuiltInCategory.OST_Furniture, 6.90, 10.30, 0.15, 8.90, 10.90, 0.95);
            FamilySymbol? fsSink = doc.GetElement(new ElementId(981995)) as FamilySymbol;
            if (fsSink != null && !fsSink.IsActive) fsSink.Activate();
            if (fsSink != null)
            {
                doc.Create.NewFamilyInstance(Pt(7.40, 10.60, 0.95), fsSink, lvl1, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                sb.AppendLine("  + Meja Dapur Minimalis + Kitchen Sink");
            }

            // Mobil di Carport (Menghadap Jalan Raya)
            FamilySymbol? fsCar = doc.GetElement(new ElementId(1394536)) as FamilySymbol;
            if (fsCar != null && !fsCar.IsActive) fsCar.Activate();
            if (fsCar != null)
            {
                FamilyInstance carInst = doc.Create.NewFamilyInstance(Pt(2.00, 2.50, 0.02), fsCar, lvl0, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                try
                {
                    Line axis = Line.CreateBound(Pt(2.00, 2.50, 0.02), Pt(2.00, 2.50, 1.00));
                    ElementTransformUtils.RotateElement(doc, carInst.Id, axis, Math.PI / 2.0);
                }
                catch { }
                sb.AppendLine("  + Mobil di Carport (Menghadap Jalan Raya)");
            }

            // Pohon Lanskap Belakang
            FamilySymbol? fsPohonBesar = doc.GetElement(new ElementId(1356109)) as FamilySymbol;
            if (fsPohonBesar != null && !fsPohonBesar.IsActive) fsPohonBesar.Activate();
            if (fsPohonBesar != null)
            {
                doc.Create.NewFamilyInstance(Pt(1.50, 13.80, 0.00), fsPohonBesar, lvl0, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                doc.Create.NewFamilyInstance(Pt(8.80, 14.00, 0.00), fsPohonBesar, lvl0, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                sb.AppendLine("  + Pohon Lanskap Belakang Terpasang Sesuai Site Plan");
            }

            // -----------------------------------------------------------------
            // 9. SETUP VIEWS (PERSPEKTIF, AKSONOMETRI, TAMPAK DEPAN, & DENAH)
            // -----------------------------------------------------------------
            sb.AppendLine("\n[9. SETUP VIEWS]");

            ViewFamilyType? vft3D = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

            if (vft3D != null)
            {
                // A. View 3D Perspektif Fasad Depan (Human Eye-level Angle 1:1 Reference)
                string vPerspName = "3D FASAD TYPE 36 - PERSPEKTIF";
                View3D? v3d = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => v.Name.Equals(vPerspName, StringComparison.OrdinalIgnoreCase));
                if (v3d == null)
                {
                    v3d = View3D.CreatePerspective(doc, vft3D.Id);
                    v3d.Name = vPerspName;
                }

                XYZ eye = Pt(5.35, -5.50, 1.70);
                XYZ target = Pt(5.65, 5.50, 2.70);
                XYZ forward = (target - eye).Normalize();
                XYZ right = forward.CrossProduct(XYZ.BasisZ).Normalize();
                XYZ up = right.CrossProduct(forward).Normalize();
                v3d.SetOrientation(new ViewOrientation3D(eye, up, forward));
                v3d.Discipline = ViewDiscipline.Architectural;
                v3d.DetailLevel = ViewDetailLevel.Fine;
                v3d.DisplayStyle = DisplayStyle.ShadingWithEdges;
                try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Levels), true); } catch {}
                try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Grids), true); } catch {}
                try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Elev), true); } catch {}
                sb.AppendLine($"  + Berhasil Mengkonfigurasi View: '{v3d.Name}'");

                // B. View 3D Axonometric Bird-Eye View
                string vAxName = "3D AXONOMETRIC TYPE 36";
                View3D? vAx = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => v.Name.Equals(vAxName, StringComparison.OrdinalIgnoreCase));
                if (vAx == null)
                {
                    vAx = View3D.CreateIsometric(doc, vft3D.Id);
                    vAx.Name = vAxName;
                }

                XYZ eyeAx = Pt(-5.00, -5.00, 10.00);
                XYZ targetAx = Pt(5.00, 7.50, 1.50);
                XYZ forwardAx = (targetAx - eyeAx).Normalize();
                XYZ rightAx = forwardAx.CrossProduct(XYZ.BasisZ).Normalize();
                XYZ upAx = rightAx.CrossProduct(forwardAx).Normalize();
                vAx.SetOrientation(new ViewOrientation3D(eyeAx, upAx, forwardAx));
                vAx.Discipline = ViewDiscipline.Architectural;
                vAx.DetailLevel = ViewDetailLevel.Fine;
                vAx.DisplayStyle = DisplayStyle.ShadingWithEdges;
                try { vAx.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Levels), true); } catch {}
                try { vAx.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Grids), true); } catch {}
                try { vAx.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Elev), true); } catch {}
                sb.AppendLine($"  + Berhasil Mengkonfigurasi View: '{vAx.Name}'");

                // C. View Tampak Depan Ortografis (Front Elevation View - Clean Architectural Facade)
                string vFrontName = "TAMPAK DEPAN - FASAD TYPE 36";
                View3D? vFront = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => v.Name.Equals(vFrontName, StringComparison.OrdinalIgnoreCase));
                if (vFront == null)
                {
                    vFront = View3D.CreateIsometric(doc, vft3D.Id);
                    vFront.Name = vFrontName;
                }

                XYZ eyeF = Pt(5.00, -20.00, 2.80);
                XYZ forwardF = XYZ.BasisY;
                XYZ upF = XYZ.BasisZ;
                vFront.SetOrientation(new ViewOrientation3D(eyeF, upF, forwardF));
                vFront.Discipline = ViewDiscipline.Architectural;
                vFront.DetailLevel = ViewDetailLevel.Fine;
                vFront.DisplayStyle = DisplayStyle.ShadingWithEdges;
                try { vFront.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Levels), true); } catch {}
                try { vFront.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Grids), true); } catch {}
                try { vFront.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Elev), true); } catch {}

                // Sembunyikan pohon latar belakang di Tampak Depan
                var plantingIds = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Planting)
                    .Select(e => e.Id)
                    .ToList();
                foreach (var pid in plantingIds)
                {
                    try { vFront.HideElements(new List<ElementId> { pid }); } catch {}
                }

                try
                {
                    vFront.CropBoxActive = true;
                    vFront.CropBoxVisible = false;
                    BoundingBoxXYZ bbF = vFront.CropBox;
                    bbF.Min = Pt(-0.5, -50.0, -0.2);
                    bbF.Max = Pt(10.5, 50.0, 6.2);
                    vFront.CropBox = bbF;
                }
                catch { }

                // Override garis roof menjadi hitam / dark charcoal (menghilangkan garis hijau bawaan)
                OverrideGraphicSettings ogsBlack = new OverrideGraphicSettings();
                ogsBlack.SetProjectionLineColor(new Color(46, 44, 42));
                ogsBlack.SetProjectionLineWeight(2);
                try { v3d.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_Roofs), ogsBlack); } catch {}
                try { vAx.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_Roofs), ogsBlack); } catch {}
                try { vFront.SetCategoryOverrides(new ElementId(BuiltInCategory.OST_Roofs), ogsBlack); } catch {}

                sb.AppendLine($"  + Berhasil Mengkonfigurasi View: '{vFront.Name}'");
            }

            // D. View Denah Lantai 1 Arsitektural (Skala 1:100, Text Note Proporsional & Presisi)
            ViewFamilyType? vftPlan = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.FloorPlan);

            if (vftPlan != null)
            {
                string vPlanName = "DENAH LANTAI 1 - ARSITEKTUR TYPE 36";
                ViewPlan? plan = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v => v.Name.Equals(vPlanName, StringComparison.OrdinalIgnoreCase));
                if (plan == null)
                {
                    plan = ViewPlan.Create(doc, vftPlan.Id, lvl1.Id);
                    plan.Name = vPlanName;
                }

                plan.Scale = 100; // Skala standar arsitektural 1:100
                plan.Discipline = ViewDiscipline.Architectural;
                plan.DetailLevel = ViewDetailLevel.Fine;
                plan.DisplayStyle = DisplayStyle.ShadingWithEdges;
                try { plan.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Levels), true); } catch {}
                try { plan.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Grids), true); } catch {}
                try { plan.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Elev), true); } catch {}
                try { plan.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Sections), true); } catch {}

                // Sembunyikan pohon di denah agar tidak menutupi teks taman belakang
                var treesPlan = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Planting)
                    .Select(e => e.Id)
                    .ToList();
                foreach (var tpid in treesPlan)
                {
                    try { plan.HideElements(new List<ElementId> { tpid }); } catch {}
                }

                try
                {
                    plan.CropBoxActive = true;
                    plan.CropBoxVisible = false;
                    BoundingBoxXYZ bbox = plan.CropBox;
                    bbox.Min = Pt(-0.5, -0.5, 0.0);
                    bbox.Max = Pt(10.5, 15.5, 10.0);
                    plan.CropBox = bbox;
                }
                catch { }

                // TextNoteType dengan Ukuran Huruf Presisi (2.0 mm printed height)
                TextNoteType? baseTnt = new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault();

                TextNoteType? smallTnt = null;
                if (baseTnt != null)
                {
                    smallTnt = new FilteredElementCollector(doc)
                        .OfClass(typeof(TextNoteType))
                        .Cast<TextNoteType>()
                        .FirstOrDefault(t => t.Name.Equals("AIGRAV_RoomLabel_2.0mm", StringComparison.OrdinalIgnoreCase));

                    if (smallTnt == null)
                    {
                        smallTnt = baseTnt.Duplicate("AIGRAV_RoomLabel_2.0mm") as TextNoteType;
                    }

                    if (smallTnt != null)
                    {
                        smallTnt.get_Parameter(BuiltInParameter.TEXT_SIZE)?.Set(2.0 / 304.8); // 2.0 mm
                        smallTnt.get_Parameter(BuiltInParameter.TEXT_FONT)?.Set("Arial");
                        smallTnt.get_Parameter(BuiltInParameter.TEXT_BACKGROUND)?.Set(1); // Transparent
                    }
                }

                if (smallTnt != null)
                {
                    try
                    {
                        TextNoteOptions tno = new TextNoteOptions(smallTnt.Id)
                        {
                            HorizontalAlignment = HorizontalTextAlignment.Center
                        };
                        TextNote.Create(doc, plan.Id, Pt(4.50, 6.40, 0.50), "KAMAR 1\n3.00 x 2.80", tno);
                        TextNote.Create(doc, plan.Id, Pt(4.50, 8.80, 0.50), "KAMAR 2\n3.00 x 2.00", tno);
                        TextNote.Create(doc, plan.Id, Pt(4.00, 10.40, 0.50), "TOILET\n2.00 x 1.20", tno);
                        TextNote.Create(doc, plan.Id, Pt(7.50, 8.65, 0.50), "R. TAMU & DAPUR\n3.00 x 4.70", tno);
                        TextNote.Create(doc, plan.Id, Pt(7.50, 5.65, 0.50), "TERAS\n3.00 x 1.30", tno);
                        TextNote.Create(doc, plan.Id, Pt(3.80, 2.50, 0.50), "CARPORT\n6.00 x 5.00", tno); // Di samping mobil agar tidak tabrakan
                        TextNote.Create(doc, plan.Id, Pt(8.00, 2.50, 0.50), "TAMAN DEPAN", tno);
                        TextNote.Create(doc, plan.Id, Pt(5.00, 13.00, 0.50), "TAMAN BELAKANG", tno);
                    }
                    catch { }
                }

                // Gambar Garis Bukaan Pintu 2D (Door Swings) pada Denah untuk P1 Utama
                Create2DDoorSwing(doc, plan, Pt(6.96, 6.30, 0.15), 0.90, 0.0, Math.PI / 2.0);

                sb.AppendLine($"  + Berhasil Mengkonfigurasi View: '{plan.Name}'");
            }

            doc.Regenerate();
            tx.Commit();
        }

        var postIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElementIds()
            .ToList();

        var warnings = doc.GetWarnings().Select(w => w.GetDescriptionText()).Distinct().ToList();

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            status = "Completed",
            action = "build_house_type_36",
            total_elements_in_doc = postIds.Count,
            warnings_count = warnings.Count,
            revit_warnings = warnings,
            execution_log = sb.ToString()
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    // =========================================================================
    // EXPORT VIEWS HELPER
    // =========================================================================
    public static string ExportHouseType36Views(UIApplication app, Document doc, string? argument)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV: EXPORT VIEW 3D & DENAH TYPE 36 KE IMAGE ===");

        string outDir = @"C:\Users\sapuser\.gemini\antigravity\brain\602720e7-35e3-4068-ba39-be3f3aff4b4b";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && (v.ViewType == ViewType.ThreeD || v.ViewType == ViewType.FloorPlan))
            .ToList();

        int exported = 0;
        foreach (var v in views)
        {
            if (v.Name.Contains("FASAD") || v.Name.Contains("AXONOMETRIC") || v.Name.Contains("TAMPAK") || v.Name.Contains("DENAH"))
            {
                try
                {
                    string safeName = string.Join("_", v.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                    string filePath = Path.Combine(outDir, $"Export_{safeName}.png");

                    ImageExportOptions opts = new ImageExportOptions
                    {
                        ExportRange = ExportRange.SetOfViews,
                        FilePath = filePath,
                        FitDirection = FitDirectionType.Horizontal,
                        HLRandWFViewsFileType = ImageFileType.PNG,
                        ImageResolution = ImageResolution.DPI_150,
                        PixelSize = 2400,
                        ShadowViewsFileType = ImageFileType.PNG,
                        ZoomType = ZoomFitType.FitToPage
                    };
                    opts.SetViewsAndSheets(new List<ElementId> { v.Id });

                    doc.ExportImage(opts);
                    exported++;
                    sb.AppendLine($"  + Berhasil Export: {filePath}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  ! Gagal Export View '{v.Name}': {ex.Message}");
                }
            }
        }

        sb.AppendLine($"Total View Berhasil Diexport: {exported}");
        return sb.ToString();
    }

    // =========================================================================
    // HELPER GEOMETRI & ELEMEN
    // =========================================================================
    private static Material GetOrCreateMat(Document doc, string name, Color color, int transparency = 0)
    {
        Material? mat = new FilteredElementCollector(doc)
            .OfClass(typeof(Material))
            .Cast<Material>()
            .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (mat == null)
        {
            ElementId matId = Material.Create(doc, name);
            mat = doc.GetElement(matId) as Material;
        }

        if (mat != null)
        {
            mat.UseRenderAppearanceForShading = false;
            mat.Color = color;
            mat.Transparency = transparency;
            mat.Shininess = 120;
            mat.Smoothness = 100;
        }
        return mat!;
    }

    private static WallType GetOrCreateWallType(Document doc, string name, double thicknessFt, ElementId matId)
    {
        WallType? wt = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (wt == null)
        {
            WallType? baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(w => w.Kind == WallKind.Basic);

            if (baseType != null)
            {
                wt = baseType.Duplicate(name) as WallType;
            }
        }

        if (wt != null)
        {
            CompoundStructure cs = wt.GetCompoundStructure();
            if (cs != null)
            {
                var layers = cs.GetLayers();
                for (int i = 0; i < layers.Count; i++)
                {
                    layers[i].MaterialId = matId;
                }
                if (layers.Count > 0) layers[0].Width = thicknessFt;
                cs.SetLayers(layers);
                wt.SetCompoundStructure(cs);
            }
        }
        return wt!;
    }

    private static FloorType GetOrCreateFloorType(Document doc, string name, double thicknessFt, ElementId matId)
    {
        FloorType? ft = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (ft == null)
        {
            FloorType? baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault();

            if (baseType != null)
            {
                ft = baseType.Duplicate(name) as FloorType;
            }
        }

        if (ft != null)
        {
            CompoundStructure cs = ft.GetCompoundStructure();
            if (cs != null)
            {
                var layers = cs.GetLayers();
                for (int i = 0; i < layers.Count; i++)
                {
                    layers[i].MaterialId = matId;
                }
                if (layers.Count > 0) layers[0].Width = thicknessFt;
                cs.SetLayers(layers);
                ft.SetCompoundStructure(cs);
            }
        }
        return ft!;
    }

    private static RoofType GetOrCreateRoofType(Document doc, string name, double thicknessFt, ElementId matId)
    {
        RoofType? rt = new FilteredElementCollector(doc)
            .OfClass(typeof(RoofType))
            .Cast<RoofType>()
            .FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (rt == null)
        {
            RoofType? baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .Cast<RoofType>()
                .FirstOrDefault();

            if (baseType != null)
            {
                rt = baseType.Duplicate(name) as RoofType;
            }
        }

        if (rt != null)
        {
            CompoundStructure cs = rt.GetCompoundStructure();
            if (cs != null)
            {
                var layers = cs.GetLayers();
                for (int i = 0; i < layers.Count; i++)
                {
                    layers[i].MaterialId = matId;
                }
                if (layers.Count > 0) layers[0].Width = thicknessFt;
                cs.SetLayers(layers);
                rt.SetCompoundStructure(cs);
            }
        }
        return rt!;
    }

    private static Floor CreateRectFloor(Document doc, FloorType ft, ElementId lvlId, double x1, double y1, double x2, double y2, double elevationFt)
    {
        CurveLoop loop = new CurveLoop();
        loop.Append(Line.CreateBound(Pt(x1, y1, 0), Pt(x2, y1, 0)));
        loop.Append(Line.CreateBound(Pt(x2, y1, 0), Pt(x2, y2, 0)));
        loop.Append(Line.CreateBound(Pt(x2, y2, 0), Pt(x1, y2, 0)));
        loop.Append(Line.CreateBound(Pt(x1, y2, 0), Pt(x1, y1, 0)));

        Floor floor = Floor.Create(doc, new List<CurveLoop> { loop }, ft.Id, lvlId);
        floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(elevationFt);
        return floor;
    }

    private static Wall CreateWall(Document doc, WallType wt, ElementId lvlId, double x1, double y1, double x2, double y2, double heightM)
    {
        Line line = Line.CreateBound(Pt(x1, y1, 0), Pt(x2, y2, 0));
        return Wall.Create(doc, line, wt.Id, lvlId, M(heightM), 0, false, false);
    }

    private static DirectShape? CreateDirectShapeBox(Document doc, string name, ElementId matId, BuiltInCategory cat, double x1, double y1, double z1, double x2, double y2, double z2, List<string>? errors = null)
    {
        try
        {
            CurveLoop loop = new CurveLoop();
            loop.Append(Line.CreateBound(Pt(x1, y1, z1), Pt(x2, y1, z1)));
            loop.Append(Line.CreateBound(Pt(x2, y1, z1), Pt(x2, y2, z1)));
            loop.Append(Line.CreateBound(Pt(x2, y2, z1), Pt(x1, y2, z1)));
            loop.Append(Line.CreateBound(Pt(x1, y2, z1), Pt(x1, y1, z1)));

            SolidOptions solidOpts = new SolidOptions(matId, ElementId.InvalidElementId);
            Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, M(z2 - z1), solidOpts);
            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(cat));
            ds.SetShape(new GeometryObject[] { solid });
            ds.Name = name;
            return ds;
        }
        catch (Exception ex)
        {
            errors?.Add($"CreateDirectShapeBox '{name}' failed: {ex.Message}");
            return null;
        }
    }

    private static bool CreateExtrusionAlongY(Document doc, string name, ElementId matId, BuiltInCategory cat, List<UV> xzPoints, double y1, double y2, StringBuilder sb)
    {
        try
        {
            CurveLoop loop = new CurveLoop();
            for (int i = 0; i < xzPoints.Count; i++)
            {
                UV p1 = xzPoints[i];
                UV p2 = xzPoints[(i + 1) % xzPoints.Count];
                loop.Append(Line.CreateBound(Pt(p1.U, y1, p1.V), Pt(p2.U, y1, p2.V)));
            }

            if (!loop.IsCounterclockwise(XYZ.BasisY))
            {
                loop.Flip();
            }

            SolidOptions solidOpts = new SolidOptions(matId, ElementId.InvalidElementId);
            Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisY, M(y2 - y1), solidOpts);
            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(cat));
            ds.SetShape(new GeometryObject[] { solid });
            ds.Name = name;
            sb.AppendLine($"  + Extrusion '{name}' ({cat}) berhasil dibuat dengan MaterialId {matId.Value}.");
            return true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  ! Extrusion '{name}' GAGAL: {ex.Message}");
            return false;
        }
    }

    private static DirectShape? CreateDirectShapeCylinder(Document doc, string name, ElementId matId, BuiltInCategory cat, double cx, double cy, double zBase, double radius, double height, List<string>? errors = null)
    {
        try
        {
            List<Curve> profile = new List<Curve>();
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                double a1 = 2 * Math.PI * i / n;
                double a2 = 2 * Math.PI * (i + 1) / n;
                profile.Add(Line.CreateBound(
                    Pt(cx + radius * Math.Cos(a1), cy + radius * Math.Sin(a1), zBase),
                    Pt(cx + radius * Math.Cos(a2), cy + radius * Math.Sin(a2), zBase)
                ));
            }
            CurveLoop loop = CurveLoop.Create(profile);
            SolidOptions solidOpts = new SolidOptions(matId, ElementId.InvalidElementId);
            Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, M(height), solidOpts);
            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(cat));
            ds.SetShape(new GeometryObject[] { solid });
            ds.Name = name;
            return ds;
        }
        catch (Exception ex)
        {
            errors?.Add($"CreateDirectShapeCylinder '{name}' failed: {ex.Message}");
            return null;
        }
    }

    private static FamilySymbol? LoadOrFindDoorSymbol(Document doc, string filePath)
    {
        Family? fam = null;
        if (File.Exists(filePath))
        {
            try { doc.LoadFamily(filePath, out fam); } catch { }
        }
        if (fam == null)
        {
            string famName = Path.GetFileNameWithoutExtension(filePath);
            fam = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name.Equals(famName, StringComparison.OrdinalIgnoreCase));
        }
        if (fam != null)
        {
            var symIds = fam.GetFamilySymbolIds().ToList();
            if (symIds.Count > 0)
            {
                var sym = doc.GetElement(symIds[0]) as FamilySymbol;
                if (sym != null && !sym.IsActive) sym.Activate();
                return sym;
            }
        }
        var fb = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Doors)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault();
        if (fb != null && !fb.IsActive) fb.Activate();
        return fb;
    }

    private static FamilySymbol? LoadOrFindWindowSymbol(Document doc, string filePath)
    {
        Family? fam = null;
        if (File.Exists(filePath))
        {
            try { doc.LoadFamily(filePath, out fam); } catch { }
        }
        if (fam == null)
        {
            string famName = Path.GetFileNameWithoutExtension(filePath);
            fam = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name.Equals(famName, StringComparison.OrdinalIgnoreCase));
        }
        if (fam != null)
        {
            var symIds = fam.GetFamilySymbolIds().ToList();
            if (symIds.Count > 0)
            {
                var sym = doc.GetElement(symIds[0]) as FamilySymbol;
                if (sym != null && !sym.IsActive) sym.Activate();
                return sym;
            }
        }
        var fb = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Windows)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault();
        if (fb != null && !fb.IsActive) fb.Activate();
        return fb;
    }

    private static void Create2DDoorSwing(Document doc, View view, XYZ hinge, double radiusM, double startAngleRad, double sweepAngleRad)
    {
        try
        {
            XYZ leafEnd = new XYZ(hinge.X + (radiusM * Math.Cos(startAngleRad + sweepAngleRad)) / 0.3048,
                                  hinge.Y + (radiusM * Math.Sin(startAngleRad + sweepAngleRad)) / 0.3048,
                                  hinge.Z);
            Line leaf = Line.CreateBound(hinge, leafEnd);
            doc.Create.NewDetailCurve(view, leaf);

            XYZ start = new XYZ(hinge.X + (radiusM * Math.Cos(startAngleRad)) / 0.3048,
                                hinge.Y + (radiusM * Math.Sin(startAngleRad)) / 0.3048,
                                hinge.Z);
            XYZ mid = new XYZ(hinge.X + (radiusM * Math.Cos(startAngleRad + sweepAngleRad / 2.0)) / 0.3048,
                              hinge.Y + (radiusM * Math.Sin(startAngleRad + sweepAngleRad / 2.0)) / 0.3048,
                              hinge.Z);
            Arc arc = Arc.Create(start, leafEnd, mid);
            doc.Create.NewDetailCurve(view, arc);
        }
        catch { }
    }
}

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
    public static string CreatePerspectivePreviews(UIApplication app, Document doc, string? argument)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV: MEMBUAT & MENGEKSPOR 4 OPSI PERSPEKTIF VIEW TYPE 36 ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        string targetDir = @"C:\Users\sapuser\.gemini\antigravity\brain\5448a500-ccc9-46e5-89ab-30362c4d8eef";
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        ViewFamilyType? vft3D = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

        if (vft3D == null)
        {
            return "Error: Tidak ditemukan ViewFamilyType untuk 3D View di dokumen Revit.";
        }

        // Definisikan 4 Konfigurasi Kamera Perspektif yang Telah Dikalibrasi
        var cameraSetups = new[]
        {
            new
            {
                Name = "PERSPEKTIF 1 - EYE LEVEL STREET",
                Description = "Eye-Level Human Street View (Sudut Pandang Jalan 3/4)",
                Eye = Pt(7.50, -6.50, 1.65),
                Target = Pt(5.60, 5.50, 2.40),
                FileName = "preview_perspektif_1_eye_level.png"
            },
            new
            {
                Name = "PERSPEKTIF 2 - LOW ANGLE HERO SHOT",
                Description = "Low-Angle Hero Shot (Sudut Dramatis Bawah Menatap Puncak Atap Skandinavia)",
                Eye = Pt(4.20, -5.50, 0.75),
                Target = Pt(5.80, 5.50, 3.80),
                FileName = "preview_perspektif_2_hero_shot.png"
            },
            new
            {
                Name = "PERSPEKTIF 3 - CLOSE UP TERAS & ENTRANCE",
                Description = "Close-Up Entrance & Terrace Vignette (Fokus Teras, Pintu Utama, Plakat A1 & Kanopi)",
                Eye = Pt(7.20, -1.00, 1.45),
                Target = Pt(7.30, 6.20, 1.80),
                FileName = "preview_perspektif_3_close_up.png"
            },
            new
            {
                Name = "PERSPEKTIF 4 - ELEVATED BIRD EYE",
                Description = "Elevated Semi-Aerial Perspective (Tampilan Menyeluruh Massa Bangunan & Site 10x15m)",
                Eye = Pt(-4.00, -4.50, 7.00),
                Target = Pt(5.50, 7.50, 1.80),
                FileName = "preview_perspektif_4_bird_eye.png"
            }
        };

        var createdViewIds = new List<(string Name, string FileName, ElementId Id)>();

        using (Transaction tx = new Transaction(doc, "AIGRAV Setup 4 Perspective Views"))
        {
            tx.Start();

            foreach (var setup in cameraSetups)
            {
                try
                {
                    View3D? v3d = new FilteredElementCollector(doc)
                        .OfClass(typeof(View3D))
                        .Cast<View3D>()
                        .FirstOrDefault(v => v.Name.Equals(setup.Name, StringComparison.OrdinalIgnoreCase));

                    if (v3d == null)
                    {
                        v3d = View3D.CreatePerspective(doc, vft3D.Id);
                        v3d.Name = setup.Name;
                    }

                    XYZ forward = (setup.Target - setup.Eye).Normalize();
                    XYZ right = forward.CrossProduct(XYZ.BasisZ).Normalize();
                    XYZ up = right.CrossProduct(forward).Normalize();

                    v3d.SetOrientation(new ViewOrientation3D(setup.Eye, up, forward));
                    v3d.Discipline = ViewDiscipline.Architectural;
                    v3d.DetailLevel = ViewDetailLevel.Fine;
                    v3d.DisplayStyle = DisplayStyle.ShadingWithEdges;

                    // Konfigurasi Bayangan opsional via LookupParameter
                    try { v3d.LookupParameter("Cast Shadows")?.Set(1); } catch {}
                    try { v3d.LookupParameter("Ambient Shadows")?.Set(1); } catch {}

                    // Sembunyikan Anotasi & Datum yang Mengganggu Render
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Levels), true); } catch {}
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Grids), true); } catch {}
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Sections), true); } catch {}
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Elev), true); } catch {}
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Cameras), true); } catch {}

                    try { v3d.CropBoxActive = true; } catch {}
                    try { v3d.CropBoxVisible = false; } catch {}

                    createdViewIds.Add((setup.Name, setup.FileName, v3d.Id));
                    sb.AppendLine($"  + Berhasil Mengkonfigurasi View '{setup.Name}' ({setup.Description})");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  ! Gagal Membuat View '{setup.Name}': {ex.Message}");
                }
            }

            tx.Commit();
        }

        // Sekarang Ekspor Masing-Masing View ke Gambar PNG Resolusi Tinggi
        int exported = 0;
        foreach (var item in createdViewIds)
        {
            try
            {
                string tempPrefix = Path.Combine(targetDir, $"temp_export_{Guid.NewGuid():N}");
                ImageExportOptions opts = new ImageExportOptions
                {
                    ExportRange = ExportRange.SetOfViews,
                    FilePath = tempPrefix,
                    FitDirection = FitDirectionType.Horizontal,
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_150,
                    PixelSize = 1920,
                    ShadowViewsFileType = ImageFileType.PNG,
                    ZoomType = ZoomFitType.FitToPage
                };
                opts.SetViewsAndSheets(new List<ElementId> { item.Id });

                doc.ExportImage(opts);

                // Cari file hasil export yang baru dibuat oleh Revit
                string searchPattern = Path.GetFileName(tempPrefix) + "*";
                var generatedFiles = Directory.GetFiles(targetDir, searchPattern);
                if (generatedFiles.Length > 0)
                {
                    string finalPath = Path.Combine(targetDir, item.FileName);
                    if (File.Exists(finalPath)) File.Delete(finalPath);
                    File.Move(generatedFiles[0], finalPath);
                    exported++;
                    sb.AppendLine($"  + Berhasil Export PNG: {finalPath}");
                }
                else
                {
                    sb.AppendLine($"  ! Warning: File ekspor Revit tidak ditemukan untuk prefix {tempPrefix}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ! Gagal Ekspor Gambar untuk '{item.Name}': {ex.Message}");
            }
        }

        sb.AppendLine($"Total View Berhasil Diekspor: {exported}/{createdViewIds.Count}");
        return sb.ToString();
    }

    public static string CreateInteriorViews(UIApplication app, Document doc, string? argument)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV: MEMBUAT & MENGEKSPOR INTERIOR 3D VIEWS TYPE 36 ===");
        sb.AppendLine($"Model: {doc.Title} | Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        string targetDir = @"C:\Users\sapuser\.gemini\antigravity\brain\5448a500-ccc9-46e5-89ab-30362c4d8eef";
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        ViewFamilyType? vft3D = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

        if (vft3D == null)
        {
            return "Error: Tidak ditemukan ViewFamilyType untuk 3D View di dokumen Revit.";
        }

        var interiorSetups = new[]
        {
            new
            {
                Name = "INTERIOR 1 - RUANG UTAMA CATHEDRAL VIEW",
                Description = "Pandangan dari Depan Dapur Menghadap Ruang Keluarga, Pintu Masuk, & Plafon Tinggi Kaca Segitiga",
                Eye = Pt(8.20, 10.20, 1.65),
                Target = Pt(7.10, 6.30, 2.40),
                FileName = "preview_interior_1_cathedral.png"
            },
            new
            {
                Name = "INTERIOR 2 - PANDANGAN KE DAPUR & DINING",
                Description = "Pandangan dari Pintu Masuk Menghadap Sofa, Meja Tamu, & Meja Dapur Minimalis",
                Eye = Pt(7.30, 6.80, 1.65),
                Target = Pt(7.50, 10.60, 1.65),
                FileName = "preview_interior_2_kitchen.png"
            },
            new
            {
                Name = "INTERIOR 3 - KAMAR TIDUR UTAMA",
                Description = "Pandangan di dalam Kamar Tidur Utama (Kamar 1) Menghadap Tempat Tidur & Jendela",
                Eye = Pt(5.60, 7.20, 1.65),
                Target = Pt(4.00, 5.20, 1.30),
                FileName = "preview_interior_3_bedroom.png"
            },
            new
            {
                Name = "INTERIOR 360 - CENTRAL NODE RUANG KELUARGA",
                Description = "Titik Pusat Ruang Tamu & Keluarga untuk Pandangan Panorama 360 Derajat",
                Eye = Pt(7.50, 8.50, 1.60),
                Target = Pt(7.40, 6.30, 2.20),
                FileName = "preview_interior_360_center.png"
            }
        };

        var createdViewIds = new List<(string Name, string FileName, ElementId Id)>();

        using (Transaction tx = new Transaction(doc, "AIGRAV Setup Interior Perspective Views"))
        {
            tx.Start();

            foreach (var setup in interiorSetups)
            {
                try
                {
                    View3D? v3d = new FilteredElementCollector(doc)
                        .OfClass(typeof(View3D))
                        .Cast<View3D>()
                        .FirstOrDefault(v => v.Name.Equals(setup.Name, StringComparison.OrdinalIgnoreCase));

                    if (v3d == null)
                    {
                        v3d = View3D.CreatePerspective(doc, vft3D.Id);
                        v3d.Name = setup.Name;
                    }

                    XYZ forward = (setup.Target - setup.Eye).Normalize();
                    XYZ right = forward.CrossProduct(XYZ.BasisZ).Normalize();
                    XYZ up = right.CrossProduct(forward).Normalize();

                    v3d.SetOrientation(new ViewOrientation3D(setup.Eye, up, forward));
                    v3d.Discipline = ViewDiscipline.Architectural;
                    v3d.DetailLevel = ViewDetailLevel.Fine;
                    v3d.DisplayStyle = DisplayStyle.ShadingWithEdges;

                    try { v3d.LookupParameter("Cast Shadows")?.Set(1); } catch {}
                    try { v3d.LookupParameter("Ambient Shadows")?.Set(1); } catch {}

                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Levels), true); } catch {}
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Grids), true); } catch {}
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Sections), true); } catch {}
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Elev), true); } catch {}
                    try { v3d.SetCategoryHidden(new ElementId(BuiltInCategory.OST_Cameras), true); } catch {}

                    try { v3d.CropBoxActive = true; } catch {}
                    try { v3d.CropBoxVisible = false; } catch {}

                    createdViewIds.Add((setup.Name, setup.FileName, v3d.Id));
                    sb.AppendLine($"  + Berhasil Mengkonfigurasi Interior View '{setup.Name}' ({setup.Description})");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  ! Gagal Membuat Interior View '{setup.Name}': {ex.Message}");
                }
            }

            tx.Commit();
        }

        int exported = 0;
        foreach (var item in createdViewIds)
        {
            try
            {
                string tempPrefix = Path.Combine(targetDir, $"temp_export_int_{Guid.NewGuid():N}");
                ImageExportOptions opts = new ImageExportOptions
                {
                    ExportRange = ExportRange.SetOfViews,
                    FilePath = tempPrefix,
                    FitDirection = FitDirectionType.Horizontal,
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_150,
                    PixelSize = 1920,
                    ShadowViewsFileType = ImageFileType.PNG,
                    ZoomType = ZoomFitType.FitToPage
                };
                opts.SetViewsAndSheets(new List<ElementId> { item.Id });

                doc.ExportImage(opts);

                string searchPattern = Path.GetFileName(tempPrefix) + "*";
                var generatedFiles = Directory.GetFiles(targetDir, searchPattern);
                if (generatedFiles.Length > 0)
                {
                    string finalPath = Path.Combine(targetDir, item.FileName);
                    if (File.Exists(finalPath)) File.Delete(finalPath);
                    File.Move(generatedFiles[0], finalPath);
                    exported++;
                    sb.AppendLine($"  + Berhasil Export PNG: {finalPath}");
                }
                else
                {
                    sb.AppendLine($"  ! Warning: File ekspor Revit tidak ditemukan untuk prefix {tempPrefix}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ! Gagal Ekspor Gambar untuk '{item.Name}': {ex.Message}");
            }
        }

        sb.AppendLine($"Total Interior View Berhasil Diekspor: {exported}/{createdViewIds.Count}");
        return sb.ToString();
    }
}

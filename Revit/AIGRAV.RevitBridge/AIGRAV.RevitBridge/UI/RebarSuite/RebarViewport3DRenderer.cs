using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using AIGRAV.Skills.RebarExtension.Models;

namespace AIGRAV.RevitBridge.UI.RebarSuite;

/// <summary>
/// Builds native WPF 3D geometry (transparent concrete Solids + true 3D cylindrical extruded rebar cages
/// colored by diameter) and handles smooth orbital camera interaction inside RebarStudioWindow.
/// </summary>
public sealed class RebarViewport3DRenderer
{
    public double AzimuthDeg { get; set; } = 38.0;
    public double ElevationDeg { get; set; } = 26.0;
    public double DistanceMm { get; set; } = 7500.0;
    public Vector3D PanOffset { get; set; } = new(0, 0, 0);

    private readonly Dictionary<string, MaterialGroup> _barMaterialCache = new(StringComparer.OrdinalIgnoreCase);

    public Model3DGroup BuildSceneModel(
        RebarPreviewPackage preview,
        double concreteOpacity,
        bool showConcrete,
        bool showRebar,
        bool showStirrups)
    {
        var root = new Model3DGroup();

        // Studio 3-point CAD lighting
        root.Children.Add(new AmbientLight(Color.FromRgb(115, 130, 150)));
        root.Children.Add(new DirectionalLight(Color.FromRgb(225, 235, 248), new Vector3D(-0.6, -0.8, -1.0)));
        root.Children.Add(new DirectionalLight(Color.FromRgb(110, 145, 190), new Vector3D(0.9, 0.5, -0.4)));

        // Subtle dark ground reference grid below lowest element
        double floorZ = preview.ConcreteMeshes.Count > 0
            ? preview.ConcreteMeshes.Min(m => m.MinMm.Z) - 50.0
            : -1000.0;
        AddReferenceFloorPlate(root, Math.Max(3000.0, preview.SceneRadiusMm * 1.3), floorZ);

        // 1. Render Rebar 3D Extruded Tubes FIRST (opaque objects render before transparent concrete in WPF 3D!)
        if (showRebar && preview.RebarBars.Count > 0)
        {
            var meshByColor = new Dictionary<string, MeshGeometry3D>(StringComparer.OrdinalIgnoreCase);

            foreach (var bar in preview.RebarBars)
            {
                if (bar.IsStirrup && !showStirrups) continue;

                string colorKey = bar.ColorHex;
                if (!meshByColor.TryGetValue(colorKey, out var mesh))
                {
                    mesh = new MeshGeometry3D();
                    meshByColor[colorKey] = mesh;
                }

                // Visual tube radius scaled slightly upward for crisp legibility at building scale
                double radiusMm = Math.Max(8.0, bar.DiameterMm * 0.65);
                int sides = bar.IsStirrup ? 5 : 6;

                foreach (var poly in bar.PolylinesMm)
                {
                    if (poly.Count < 2) continue;
                    for (int i = 0; i < poly.Count - 1; i++)
                    {
                        var p0 = new Point3D(poly[i].X, poly[i].Y, poly[i].Z);
                        var p1 = new Point3D(poly[i + 1].X, poly[i + 1].Y, poly[i + 1].Z);
                        AppendCylinderSegment(mesh, p0, p1, radiusMm, sides);
                    }
                }
            }

            foreach (var kv in meshByColor)
            {
                kv.Value.Freeze();
                var mat = GetOrCreateBarMaterial(kv.Key);
                var gm = new GeometryModel3D(kv.Value, mat) { BackMaterial = mat };
                gm.Freeze();
                root.Children.Add(gm);
            }
        }

        // 2. Render Semi-Transparent Concrete Host Solids LAST for proper WPF alpha compositing!
        if (showConcrete && concreteOpacity > 0.01 && preview.ConcreteMeshes.Count > 0)
        {
            byte alpha = (byte)Math.Clamp((int)Math.Round(concreteOpacity * 255.0), 8, 230);
            var frontMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(alpha, 148, 175, 205)));
            var backMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.45), 95, 120, 150)));
            frontMat.Freeze();
            backMat.Freeze();

            var combinedConcrete = new MeshGeometry3D();
            int baseOffset = 0;
            foreach (var cm in preview.ConcreteMeshes)
            {
                foreach (var v in cm.VerticesMm)
                    combinedConcrete.Positions.Add(new Point3D(v.X, v.Y, v.Z));
                foreach (int idx in cm.TriangleIndices)
                    combinedConcrete.TriangleIndices.Add(baseOffset + idx);
                baseOffset += cm.VerticesMm.Count;
            }
            combinedConcrete.Freeze();

            var concreteModel = new GeometryModel3D(combinedConcrete, frontMat) { BackMaterial = backMat };
            concreteModel.Freeze();
            root.Children.Add(concreteModel);
        }

        return root;
    }

    public void FitCameraToScene(RebarPreviewPackage preview)
    {
        DistanceMm = Math.Clamp(preview.SceneRadiusMm * 2.55, 2200.0, 65000.0);
        PanOffset = new Vector3D(0, 0, 0);
    }

    public void ApplyCameraPreset(string preset, RebarPreviewPackage? preview)
    {
        switch (preset)
        {
            case "ISO": AzimuthDeg = 38; ElevationDeg = 26; break;
            case "FRONT": AzimuthDeg = 0; ElevationDeg = 5; break;
            case "SIDE": AzimuthDeg = 90; ElevationDeg = 5; break;
            case "TOP": AzimuthDeg = 0; ElevationDeg = 88; break;
        }
        if (preview != null) FitCameraToScene(preview);
    }

    public void UpdateCamera(PerspectiveCamera camera)
    {
        double az = AzimuthDeg * Math.PI / 180.0;
        double el = Math.Clamp(ElevationDeg, -88.0, 88.0) * Math.PI / 180.0;

        double cosEl = Math.Cos(el);
        double cx = DistanceMm * cosEl * Math.Sin(az) + PanOffset.X;
        double cy = -DistanceMm * cosEl * Math.Cos(az) + PanOffset.Y;
        double cz = DistanceMm * Math.Sin(el) + PanOffset.Z;

        camera.Position = new Point3D(cx, cy, cz);
        camera.LookDirection = new Vector3D(PanOffset.X - cx, PanOffset.Y - cy, PanOffset.Z - cz);
        camera.UpDirection = new Vector3D(0, 0, 1);
        camera.NearPlaneDistance = Math.Max(20.0, DistanceMm * 0.01);
        camera.FarPlaneDistance = Math.Max(100000.0, DistanceMm * 10.0);
    }

    private MaterialGroup GetOrCreateBarMaterial(string hex)
    {
        if (_barMaterialCache.TryGetValue(hex, out var cached)) return cached;
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(hex)!; }
        catch { c = Color.FromRgb(56, 189, 248); }

        var grp = new MaterialGroup();
        grp.Children.Add(new DiffuseMaterial(new SolidColorBrush(c)));
        grp.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 48.0));
        grp.Freeze();
        _barMaterialCache[hex] = grp;
        return grp;
    }

    private static void AddReferenceFloorPlate(Model3DGroup root, double halfSize, double z)
    {
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(-halfSize, -halfSize, z),
                new(halfSize, -halfSize, z),
                new(halfSize, halfSize, z),
                new(-halfSize, halfSize, z)
            },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };
        mesh.Freeze();
        var mat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(45, 26, 40, 62)));
        mat.Freeze();
        var gm = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
        gm.Freeze();
        root.Children.Add(gm);
    }

    private static void AppendCylinderSegment(MeshGeometry3D mesh, Point3D p0, Point3D p1, double radius, int sides)
    {
        Vector3D axis = p1 - p0;
        double len = axis.Length;
        if (len < 1.0) return;
        axis /= len;

        Vector3D refVec = Math.Abs(Vector3D.DotProduct(axis, new Vector3D(0, 0, 1))) < 0.92
            ? new Vector3D(0, 0, 1)
            : new Vector3D(1, 0, 0);

        Vector3D u = Vector3D.CrossProduct(axis, refVec);
        u.Normalize();
        Vector3D v = Vector3D.CrossProduct(axis, u);
        v.Normalize();

        int baseIndex = mesh.Positions.Count;
        for (int i = 0; i < sides; i++)
        {
            double ang = 2.0 * Math.PI * i / sides;
            Vector3D offset = (u * Math.Cos(ang) + v * Math.Sin(ang)) * radius;
            mesh.Positions.Add(p0 + offset);
            mesh.Positions.Add(p1 + offset);
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int a0 = baseIndex + i * 2;
            int a1 = a0 + 1;
            int b0 = baseIndex + next * 2;
            int b1 = b0 + 1;

            mesh.TriangleIndices.Add(a0);
            mesh.TriangleIndices.Add(b0);
            mesh.TriangleIndices.Add(b1);

            mesh.TriangleIndices.Add(a0);
            mesh.TriangleIndices.Add(b1);
            mesh.TriangleIndices.Add(a1);
        }
    }
}

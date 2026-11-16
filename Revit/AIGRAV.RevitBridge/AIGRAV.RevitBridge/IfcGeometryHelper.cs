using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace AIGRAV.RevitBridge;

/// <summary>
/// Shared geometry utilities for IFC/CAD and DirectShape reconstruction tools adopted from RevitCortex.
/// Extracts solids, planar faces, footprint curve loops, wall profiles, columns, and beams.
/// </summary>
public static class IfcGeometryHelper
{
    private const double MmPerFoot = 304.8;

    public static List<Solid> GetSolids(Element element)
    {
        var solids = new List<Solid>();
        var opts = new Options { DetailLevel = ViewDetailLevel.Fine };
        var geom = element.get_Geometry(opts);
        if (geom == null) return solids;

        foreach (var obj in geom)
        {
            if (obj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 1e-9)
            {
                solids.Add(solid);
            }
            else if (obj is GeometryInstance inst)
            {
                foreach (var instObj in inst.GetInstanceGeometry())
                {
                    if (instObj is Solid s && s.Faces.Size > 0 && s.Volume > 1e-9)
                        solids.Add(s);
                }
            }
        }
        return solids;
    }

    public static (XYZ min, XYZ max)? GetBoundingBoxMm(Element element)
    {
        var bb = element.get_BoundingBox(null);
        if (bb == null) return null;
        return (
            new XYZ(bb.Min.X * MmPerFoot, bb.Min.Y * MmPerFoot, bb.Min.Z * MmPerFoot),
            new XYZ(bb.Max.X * MmPerFoot, bb.Max.Y * MmPerFoot, bb.Max.Z * MmPerFoot)
        );
    }

    public static double GetVolumeCubicMeters(Element element)
    {
        var solids = GetSolids(element);
        double totalFt3 = solids.Sum(s => s.Volume);
        return totalFt3 * 0.0283168;
    }

    public static CurveLoop? ExtractBottomFootprint(Solid solid)
    {
        PlanarFace? bottomFace = null;
        double lowestZ = double.MaxValue;

        foreach (Face face in solid.Faces)
        {
            if (face is PlanarFace pf)
            {
                if (pf.FaceNormal.Z < -0.9)
                {
                    var origin = pf.Origin;
                    if (origin.Z < lowestZ)
                    {
                        lowestZ = origin.Z;
                        bottomFace = pf;
                    }
                }
            }
        }

        if (bottomFace == null) return null;
        var edgeLoops = bottomFace.GetEdgesAsCurveLoops();
        return edgeLoops.Count > 0 ? edgeLoops[0] : null;
    }

    public static WallProfile? ExtractWallProfile(Element element)
    {
        var bb = element.get_BoundingBox(null);
        if (bb == null) return null;

        double dx = bb.Max.X - bb.Min.X;
        double dy = bb.Max.Y - bb.Min.Y;
        double dz = bb.Max.Z - bb.Min.Z;

        double length, thickness;
        XYZ startPt, endPt;

        if (dx > dy * 3)
        {
            length = dx;
            thickness = dy;
            double midY = (bb.Min.Y + bb.Max.Y) / 2;
            startPt = new XYZ(bb.Min.X, midY, bb.Min.Z);
            endPt = new XYZ(bb.Max.X, midY, bb.Min.Z);
        }
        else if (dy > dx * 3)
        {
            length = dy;
            thickness = dx;
            double midX = (bb.Min.X + bb.Max.X) / 2;
            startPt = new XYZ(midX, bb.Min.Y, bb.Min.Z);
            endPt = new XYZ(midX, bb.Max.Y, bb.Min.Z);
        }
        else
        {
            return null;
        }

        double lengthMm = length * MmPerFoot;
        double thickMm = thickness * MmPerFoot;
        double heightMm = dz * MmPerFoot;
        if (lengthMm < 300 || thickMm < 50 || thickMm > 1000 || heightMm < 500)
            return null;

        return new WallProfile
        {
            StartPoint = startPt,
            EndPoint = endPt,
            Height = dz,
            Thickness = thickness,
            BaseElevation = bb.Min.Z,
        };
    }

    public static ColumnProfile? ExtractColumnProfile(Element element)
    {
        var bb = element.get_BoundingBox(null);
        if (bb == null) return null;

        double dx = bb.Max.X - bb.Min.X;
        double dy = bb.Max.Y - bb.Min.Y;
        double dz = bb.Max.Z - bb.Min.Z;

        double maxHorizontal = Math.Max(dx, dy);
        double heightMm = dz * MmPerFoot;
        if (heightMm < 1000 || dz < maxHorizontal * 2)
            return null;

        return new ColumnProfile
        {
            CenterPoint = new XYZ((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2, bb.Min.Z),
            Height = dz,
            BaseElevation = bb.Min.Z,
            CrossSectionWidth = dx,
            CrossSectionDepth = dy,
        };
    }

    public static BeamProfile? ExtractBeamProfile(Element element)
    {
        var bb = element.get_BoundingBox(null);
        if (bb == null) return null;

        double dx = bb.Max.X - bb.Min.X;
        double dy = bb.Max.Y - bb.Min.Y;
        double dz = bb.Max.Z - bb.Min.Z;

        double maxHoriz = Math.Max(dx, dy);
        double minHoriz = Math.Min(dx, dy);

        if (maxHoriz < minHoriz * 2.5 || maxHoriz < dz * 2.5)
            return null;

        XYZ startPt, endPt;
        double midZ = (bb.Min.Z + bb.Max.Z) / 2;

        if (dx >= dy)
        {
            double midY = (bb.Min.Y + bb.Max.Y) / 2;
            startPt = new XYZ(bb.Min.X, midY, midZ);
            endPt = new XYZ(bb.Max.X, midY, midZ);
        }
        else
        {
            double midX = (bb.Min.X + bb.Max.X) / 2;
            startPt = new XYZ(midX, bb.Min.Y, midZ);
            endPt = new XYZ(midX, bb.Max.Y, midZ);
        }

        return new BeamProfile
        {
            StartPoint = startPt,
            EndPoint = endPt,
            Elevation = midZ,
            CrossSectionWidth = Math.Min(dx, dy),
            CrossSectionDepth = dz,
        };
    }

    public static List<DirectShape> GetDirectShapes(Document doc, BuiltInCategory? category = null)
    {
        var collector = new FilteredElementCollector(doc).OfClass(typeof(DirectShape));
        if (category.HasValue)
            collector = collector.OfCategory(category.Value);
        return collector.Cast<DirectShape>().ToList();
    }

    public class WallProfile
    {
        public XYZ StartPoint { get; set; } = XYZ.Zero;
        public XYZ EndPoint { get; set; } = XYZ.Zero;
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double BaseElevation { get; set; }
    }

    public class ColumnProfile
    {
        public XYZ CenterPoint { get; set; } = XYZ.Zero;
        public double Height { get; set; }
        public double BaseElevation { get; set; }
        public double CrossSectionWidth { get; set; }
        public double CrossSectionDepth { get; set; }
    }

    public class BeamProfile
    {
        public XYZ StartPoint { get; set; } = XYZ.Zero;
        public XYZ EndPoint { get; set; } = XYZ.Zero;
        public double Elevation { get; set; }
        public double CrossSectionWidth { get; set; }
        public double CrossSectionDepth { get; set; }
    }
}

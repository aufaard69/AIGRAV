using System;
using System.Collections.Generic;
using System.Linq;
using AIGRAV.Skills.RebarExtension.Models;

namespace AIGRAV.Skills.RebarExtension.Engine;

/// <summary>
/// Clips 1D reinforcement scanlines inside an outer 2D polygon while excluding all interior void/opening polygons.
/// Operates purely in millimeters using Point3DData so slab bar void-trimming is 100% unit-testable outside Revit.
/// </summary>
public static class PolygonClipper2D
{
    public readonly record struct Interval1D(double StartMm, double EndMm);

    public static List<Interval1D> ClipScanlineAlongX(
        double yMm,
        double edgeSetbackMm,
        IReadOnlyList<Point3DData> outerMm,
        IReadOnlyList<IReadOnlyList<Point3DData>> openingsMm)
    {
        var outer = IntersectHorizontalLine(outerMm, yMm);
        outer = ShrinkIntervals(outer, edgeSetbackMm);
        if (outer.Count == 0 || openingsMm.Count == 0) return outer;

        foreach (var hole in openingsMm)
        {
            var holeIntervals = ExpandIntervals(IntersectHorizontalLine(hole, yMm), edgeSetbackMm);
            outer = SubtractIntervals(outer, holeIntervals);
            if (outer.Count == 0) break;
        }
        return outer;
    }

    public static List<Interval1D> ClipScanlineAlongY(
        double xMm,
        double edgeSetbackMm,
        IReadOnlyList<Point3DData> outerMm,
        IReadOnlyList<IReadOnlyList<Point3DData>> openingsMm)
    {
        var outer = IntersectVerticalLine(outerMm, xMm);
        outer = ShrinkIntervals(outer, edgeSetbackMm);
        if (outer.Count == 0 || openingsMm.Count == 0) return outer;

        foreach (var hole in openingsMm)
        {
            var holeIntervals = ExpandIntervals(IntersectVerticalLine(hole, xMm), edgeSetbackMm);
            outer = SubtractIntervals(outer, holeIntervals);
            if (outer.Count == 0) break;
        }
        return outer;
    }

    private static List<Interval1D> IntersectHorizontalLine(IReadOnlyList<Point3DData> polyMm, double yMm)
    {
        var xs = new List<double>();
        for (int i = 0; i < polyMm.Count; i++)
        {
            var a = polyMm[i];
            var b = polyMm[(i + 1) % polyMm.Count];

            if ((a.Y <= yMm && b.Y > yMm) || (b.Y <= yMm && a.Y > yMm))
            {
                double t = (yMm - a.Y) / (b.Y - a.Y);
                xs.Add(a.X + t * (b.X - a.X));
            }
        }
        xs.Sort();
        var res = new List<Interval1D>();
        for (int i = 0; i + 1 < xs.Count; i += 2)
        {
            if (xs[i + 1] - xs[i] > 5.0)
                res.Add(new Interval1D(xs[i], xs[i + 1]));
        }
        return res;
    }

    private static List<Interval1D> IntersectVerticalLine(IReadOnlyList<Point3DData> polyMm, double xMm)
    {
        var ys = new List<double>();
        for (int i = 0; i < polyMm.Count; i++)
        {
            var a = polyMm[i];
            var b = polyMm[(i + 1) % polyMm.Count];

            if ((a.X <= xMm && b.X > xMm) || (b.X <= xMm && a.X > xMm))
            {
                double t = (xMm - a.X) / (b.X - a.X);
                ys.Add(a.Y + t * (b.Y - a.Y));
            }
        }
        ys.Sort();
        var res = new List<Interval1D>();
        for (int i = 0; i + 1 < ys.Count; i += 2)
        {
            if (ys[i + 1] - ys[i] > 5.0)
                res.Add(new Interval1D(ys[i], ys[i + 1]));
        }
        return res;
    }

    private static List<Interval1D> ShrinkIntervals(List<Interval1D> list, double delta)
    {
        var res = new List<Interval1D>();
        foreach (var it in list)
        {
            double a = it.StartMm + delta;
            double b = it.EndMm - delta;
            if (b - a > 150.0) res.Add(new Interval1D(a, b));
        }
        return res;
    }

    private static List<Interval1D> ExpandIntervals(List<Interval1D> list, double delta)
    {
        return list.Select(it => new Interval1D(it.StartMm - delta, it.EndMm + delta)).ToList();
    }

    private static List<Interval1D> SubtractIntervals(List<Interval1D> baseList, List<Interval1D> cuts)
    {
        var current = new List<Interval1D>(baseList);
        foreach (var cut in cuts)
        {
            var next = new List<Interval1D>();
            foreach (var it in current)
            {
                if (cut.EndMm <= it.StartMm || cut.StartMm >= it.EndMm)
                {
                    next.Add(it);
                }
                else
                {
                    if (cut.StartMm - it.StartMm > 150.0)
                        next.Add(new Interval1D(it.StartMm, cut.StartMm));
                    if (it.EndMm - cut.EndMm > 150.0)
                        next.Add(new Interval1D(cut.EndMm, it.EndMm));
                }
            }
            current = next;
        }
        return current;
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace BetterGIProWpf.Services.Evaluation;

public struct PathPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public string? Label { get; set; }
    public bool IsGoal { get; set; }
    public PathPoint(double x, double y, string? label = null, bool isGoal = false)
    { X = x; Y = y; Label = label; IsGoal = isGoal; }
}

public static class PathVisualizer
{
    public static string ToSvg(List<PathPoint> points, int width = 800, int height = 600)
    {
        if (points == null || points.Count == 0)
            return $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'></svg>";
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in points)
        {
            if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
        }
        double rangeX = maxX - minX; if (rangeX < 1) rangeX = 1;
        double rangeY = maxY - minY; if (rangeY < 1) rangeY = 1;
        double pad = 40, sx = (width - 2 * pad) / rangeX, sy = (height - 2 * pad) / rangeY, s = Math.Min(sx, sy);
        double Tx(double x) => pad + (x - minX) * s;
        double Ty(double y) => pad + (y - minY) * s;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>");
        sb.Append("<rect width='100%' height='100%' fill='rgba(0,0,0,0.1)'/>");
        sb.Append("<polyline points='");
        for (int i = 0; i < points.Count; i++) { if (i > 0) sb.Append(' '); sb.Append($"{Tx(points[i].X):F1},{Ty(points[i].Y):F1}"); }
        sb.Append("' fill='none' stroke='#00d4ff' stroke-width='2' stroke-opacity='0.7'/>");
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var color = p.IsGoal ? "#e74c3c" : (i == 0 ? "#2ecc71" : "#00d4ff");
            var r = p.IsGoal ? 8 : (i == 0 ? 6 : 3);
            sb.Append($"<circle cx='{Tx(p.X):F1}' cy='{Ty(p.Y):F1}' r='{r}' fill='{color}'/>");
            if (!string.IsNullOrEmpty(p.Label)) sb.Append($"<text x='{Tx(p.X) + 10:F1}' y='{Ty(p.Y) + 4:F1}' fill='white' font-size='12'>{p.Label}</text>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    public static string SaveSvg(List<PathPoint> points, string path)
    {
        File.WriteAllText(path, ToSvg(points));
        return path;
    }
}

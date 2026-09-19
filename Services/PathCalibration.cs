namespace BetterGIProWpf.Services;

public record Pt(double X, double Y);

public static class PathCalibration
{
    public static Func<Pt, Pt> AffineCalibrate((Pt src, Pt dst)[] landmarks)
    {
        var n = landmarks.Length;
        var A = new double[6, 6];
        var b = new double[6];
        foreach (var (s0, d0) in landmarks)
        {
            double[,] rows = { { s0.X, s0.Y, 1, 0, 0, 0 }, { 0, 0, 0, s0.X, s0.Y, 1 } };
            for (int r = 0; r < 2; r++)
                for (int k = 0; k < 6; k++) A[r, k] += rows[r, k] * rows[r, k];
            b[0] += rows[0, 0] * d0.X; b[1] += rows[0, 1] * d0.X; b[2] += rows[0, 2] * d0.X;
            b[3] += rows[1, 3] * d0.Y; b[4] += rows[1, 4] * d0.Y; b[5] += rows[1, 5] * d0.Y;
        }
        var sol = Solve6(A, b);
        if (sol == null) return p => p;
        var (a, c, d, e, f, g) = (sol[0], sol[1], sol[2], sol[3], sol[4], sol[5]);
        return p => new Pt(a * p.X + c * p.Y + d, e * p.X + f * p.Y + g);
    }

    private static double[]? Solve6(double[,] A, double[] b)
    {
        var m = 6;
        var mat = new double[m, m + 1];
        for (int i = 0; i < m; i++) { for (int j = 0; j < m; j++) mat[i, j] = A[i, j]; mat[i, m] = b[i]; }
        for (int col = 0; col < m; col++)
        {
            int piv = col;
            for (int r = col + 1; r < m; r++) if (Math.Abs(mat[r, col]) > Math.Abs(mat[piv, col])) piv = r;
            if (Math.Abs(mat[piv, col]) < 1e-9) return null;
            for (int j = 0; j <= m; j++) (mat[col, j], mat[piv, j]) = (mat[piv, j], mat[col, j]);
            for (int r = 0; r < m; r++)
            {
                if (r == col) continue;
                double f = mat[r, col] / mat[col, col];
                for (int j = col; j <= m; j++) mat[r, j] -= f * mat[col, j];
            }
        }
        var x = new double[m];
        for (int i = 0; i < m; i++) x[i] = mat[i, m] / mat[i, i];
        return x;
    }

    public static double DtwAlign(double[] seqA, double[] seqB)
    {
        int n = seqA.Length, m = seqB.Length;
        var dp = new double[n + 1, m + 1];
        for (int i = 0; i <= n; i++) for (int j = 0; j <= m; j++) dp[i, j] = double.PositiveInfinity;
        dp[0, 0] = 0;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                double cost = Math.Abs(seqA[i - 1] - seqB[j - 1]);
                dp[i, j] = cost + Math.Min(Math.Min(dp[i - 1, j], dp[i, j - 1]), dp[i - 1, j - 1]);
            }
        return dp[n, m] / Math.Max(1, (n + m) / 2);
    }

    public static List<Pt> SmoothPath(List<Pt> pts, int perSegment = 8)
    {
        if (pts.Count < 3) return pts;
        var result = new List<Pt>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[Math.Max(0, i - 1)];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[Math.Min(pts.Count - 1, i + 2)];
            for (int t = 0; t < perSegment; t++)
            {
                double s = (double)t / perSegment;
                double s2 = s * s, s3 = s2 * s;
                double x = 0.5 * ((2 * p1.X) + (-p0.X + p2.X) * s + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * s2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * s3);
                double y = 0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * s + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * s2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * s3);
                result.Add(new Pt(x, y));
            }
        }
        result.Add(pts[^1]);
        return result;
    }
}

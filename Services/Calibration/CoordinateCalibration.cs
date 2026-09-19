namespace BetterGIProWpf.Services.Calibration;

public class CoordinateCalibration
{
    public record Landmark(double VideoX, double VideoY, double GameX, double GameY);

    public double[,] Matrix { get; private set; } = { { 1, 0, 0 }, { 0, 1, 0 } };
    public bool IsCalibrated { get; private set; }

    public void Fit(IReadOnlyList<Landmark> landmarks)
    {
        if (landmarks.Count < 3) throw new InvalidOperationException($"至少需要 3 个地标，当前 {landmarks.Count}");
        var n = landmarks.Count;
        var A = new double[2 * n, 6];
        var b = new double[2 * n];
        for (var i = 0; i < n; i++)
        {
            var (vx, vy, gx, gy) = landmarks[i];
            A[2 * i, 0] = vx; A[2 * i, 1] = vy; A[2 * i, 2] = 1;
            A[2 * i + 1, 3] = vx; A[2 * i + 1, 4] = vy; A[2 * i + 1, 5] = 1;
            b[2 * i] = gx; b[2 * i + 1] = gy;
        }
        var p = SolveNormalEquations(A, b);
        Matrix = new[,] { { p[0], p[1], p[2] }, { p[3], p[4], p[5] } };
        IsCalibrated = true;
    }

    public (double X, double Y) Transform(double vx, double vy) =>
        (Matrix[0, 0] * vx + Matrix[0, 1] * vy + Matrix[0, 2],
         Matrix[1, 0] * vx + Matrix[1, 1] * vy + Matrix[1, 2]);

    public double MeanError(IReadOnlyList<Landmark> landmarks)
    {
        if (!IsCalibrated || landmarks.Count == 0) return double.MaxValue;
        double s = 0;
        foreach (var l in landmarks)
        {
            var (x, y) = Transform(l.VideoX, l.VideoY);
            s += Math.Sqrt(Math.Pow(x - l.GameX, 2) + Math.Pow(y - l.GameY, 2));
        }
        return s / landmarks.Count;
    }

    private static double[] SolveNormalEquations(double[,] A, double[] b)
    {
        var rows = b.Length; var cols = 6;
        var ata = new double[cols, cols];
        var atb = new double[cols];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                atb[c] += A[r, c] * b[r];
                for (var c2 = 0; c2 < cols; c2++) ata[c, c2] += A[r, c] * A[r, c2];
            }
        var m = (double[,])ata.Clone();
        var y = (double[])atb.Clone();
        for (var col = 0; col < cols; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < cols; r++)
                if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;
            for (var c = 0; c < cols; c++) (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
            (y[col], y[pivot]) = (y[pivot], y[col]);
            for (var r = col + 1; r < cols; r++)
            {
                var f = m[r, col] / m[col, col];
                for (var c = col; c < cols; c++) m[r, c] -= f * m[col, c];
                y[r] -= f * y[col];
            }
        }
        var x = new double[cols];
        for (var r = cols - 1; r >= 0; r--)
        {
            var acc = y[r];
            for (var c = r + 1; c < cols; c++) acc -= m[r, c] * x[c];
            x[r] = acc / m[r, r];
        }
        return x;
    }
}

public class PathSmoother
{
    public static List<(double X, double Y)> Smooth(IReadOnlyList<(double X, double Y)> points, int window = 5)
    {
        if (points.Count <= 2 || window <= 1) return points.ToList();
        var w = Math.Min(window, points.Count);
        var half = w / 2;
        var result = new List<(double, double)>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            if (i < half || i >= points.Count - half) { result.Add(points[i]); continue; }
            var lo = i - half; var hi = i + half;
            double sx = 0, sy = 0; var c = 0;
            for (var j = lo; j <= hi; j++) { sx += points[j].X; sy += points[j].Y; c++; }
            result.Add((sx / c, sy / c));
        }
        return result;
    }
}

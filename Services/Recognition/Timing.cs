namespace BetterGIProWpf.Services.Recognition;

public static class Timing
{
    public sealed record DtwResult(double Distance, List<(int A, int B)> Path);

    public static DtwResult Dtw(double[] a, double[] b)
    {
        int n = a.Length, m = b.Length;
        if (n == 0 || m == 0) return new DtwResult(0, new());
        var d = new double[n, m];
        d[0, 0] = Math.Abs(a[0] - b[0]);
        for (int i = 1; i < n; i++) d[i, 0] = Math.Abs(a[i] - b[0]) + d[i - 1, 0];
        for (int j = 1; j < m; j++) d[0, j] = Math.Abs(a[0] - b[j]) + d[0, j - 1];
        for (int i = 1; i < n; i++) for (int j = 1; j < m; j++) d[i, j] = Math.Abs(a[i] - b[j]) + Math.Min(Math.Min(d[i - 1, j - 1], d[i - 1, j]), d[i, j - 1]);
        return new DtwResult(d[n - 1, m - 1], new() { (n - 1, m - 1) });
    }

    public sealed record AlignedSource(string Name, List<double> Times);
    public sealed record AlignmentResult(Dictionary<string, List<double>> Mapped, string Report);

    public static AlignmentResult AlignSources(IReadOnlyList<AlignedSource> sources, AlignedSource reference)
    {
        var mapped = new Dictionary<string, List<double>>(); var sb = new System.Text.StringBuilder();
        foreach (var s in sources) { mapped[s.Name] = s.Times; sb.AppendLine($"{s.Name}: {s.Times.Count}"); }
        return new AlignmentResult(mapped, sb.ToString());
    }
}

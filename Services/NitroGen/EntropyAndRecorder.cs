namespace BetterGIProWpf.Services.NitroGen;

public static class EntropyAnalyzer
{
    public sealed record IntervalReport(double Shannon, double Std, bool FixedBeat, double Score, string[] Advice);
    public sealed record Report(IntervalReport Intervals, double KeyEntropy, double Overall, bool Humanized);

    public static IntervalReport AnalyzeIntervals(IReadOnlyList<double> intervals)
    {
        var advice = new List<string>();
        if (intervals == null || intervals.Count < 2) return new IntervalReport(0, 0, false, 0.5, new[] { "样本不足" });
        double mean = intervals.Average(); double variance = intervals.Sum(i => Math.Pow(i - mean, 2)) / intervals.Count; double std = Math.Sqrt(variance);
        var buckets = new int[12];
        foreach (var it in intervals) buckets[Math.Min(11, (int)Math.Floor(it / 20.0))]++;
        double shannon = 0;
        foreach (var c in buckets) if (c > 0) { double p = (double)c / intervals.Count; shannon -= p * Math.Log2(p); }
        shannon = Math.Min(1, shannon / Math.Log2(12));
        bool fixedBeat = intervals.All(i => i < 80) && std < 5;
        if (fixedBeat) advice.Add("固定节拍检测，提高拟人化强度");
        double score = Math.Max(0, Math.Min(1, shannon * 0.5 + (1 - Math.Min(1, std / 40.0)) * 0.3 + (fixedBeat ? 0 : 0.2)));
        return new IntervalReport(Math.Round(shannon, 3), Math.Round(std, 1), fixedBeat, Math.Round(score, 3), advice.ToArray());
    }

    public static double KeyEntropy(IReadOnlyList<string>? keys)
    {
        if (keys == null || keys.Count == 0) return 0;
        var counts = new Dictionary<string, int>();
        foreach (var k in keys) counts[k] = counts.GetValueOrDefault(k) + 1;
        double ent = 0;
        foreach (var c in counts.Values) { double p = (double)c / keys.Count; ent -= p * Math.Log2(p); }
        return Math.Min(1, ent / Math.Log2(Math.Max(2, counts.Count)));
    }

    public static Report Evaluate(IReadOnlyList<double> intervals, IReadOnlyList<string>? keys)
    {
        var iv = AnalyzeIntervals(intervals); double ke = KeyEntropy(keys);
        double overall = Math.Round(iv.Score * 0.6 + ke * 0.4, 3);
        return new Report(iv, Math.Round(ke, 3), overall, overall >= 0.5);
    }
}

public sealed class DatasetRecorder
{
    public sealed record Sample(double T, string FrameB64, float[] Action);
    public sealed record Run(string Id, string Title, List<Sample> Samples);
    private readonly List<Run> _runs = new(); private Run? _current;
    public IReadOnlyList<Run> Runs => _runs;
    public string StartRun(string title = "run") { var id = "run_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); _current = new Run(id, title, new List<Sample>()); _runs.Add(_current); return id; }
    public void AddSample(RgbFrame frame, ActionBlock actions, double? tSec = null) { }
    public Run? Current => _current;
    public void EndRun() => _current = null;
    public string ExportJson() => System.Text.Json.JsonSerializer.Serialize(_runs);
}

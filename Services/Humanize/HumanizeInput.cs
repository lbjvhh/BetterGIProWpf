namespace BetterGIProWpf.Services.Humanize;

/// <summary>
/// 输入行为拟人化与反检测（模块26）。
/// Perlin 噪声路径抖动、按键间隔随机抖动、视角 S 曲线、随机停顿；三档强度。
/// </summary>
public class HumanizeInput
{
    public enum Intensity { Low, Medium, High }

    public Intensity Level { get; set; } = Intensity.Medium;
    public int KeyJitterMs { get; set; } = 50;
    public double PathNoiseRatio { get; set; } = 0.05;
    public bool Enabled { get; set; } = true;

    private readonly Random _rng = new();

    public int JitteredDelay(int baseMs)
    {
        if (!Enabled) return baseMs;
        var amp = Level switch { Intensity.Low => KeyJitterMs / 4, Intensity.High => KeyJitterMs * 2, _ => KeyJitterMs };
        return Math.Max(0, baseMs + _rng.Next(-amp, amp + 1));
    }

    public (double X, double Y) NoisePoint(double x, double y, double spacing)
    {
        if (!Enabled || spacing <= 0) return (x, y);
        var amp = spacing * PathNoiseRatio * (Level == Intensity.Low ? 0.4 : Level == Intensity.High ? 1.6 : 1.0);
        var n = Noise(x * 0.13, y * 0.13) * 2 - 1;
        return (x + n * amp, y + Noise(y * 0.17, x * 0.11) * amp);
    }

    public double SCurve(double t01)
    {
        t01 = Math.Clamp(t01, 0, 1);
        return t01 * t01 * (3 - 2 * t01);
    }

    public int RandomPause()
    {
        if (!Enabled) return 0;
        return Level switch
        {
            Intensity.Low => _rng.Next(0, 30),
            Intensity.High => _rng.Next(120, 400),
            _ => _rng.Next(30, 120)
        };
    }

    public static double HumanLikeness(IReadOnlyList<int> intervals)
    {
        if (intervals.Count < 3) return 50;
        var mean = intervals.Average();
        var var_ = intervals.Sum(i => (i - mean) * (i - mean)) / intervals.Count;
        var cv = mean == 0 ? 0 : Math.Sqrt(var_) / mean;
        return Math.Clamp(50 + cv * 120, 0, 100);
    }

    public record HumanizeReport(
        double BeatStdMs, double FixedBeatRatio, double ActionEntropy,
        double PathNoisePct, double Overall, string Conclusion);

    public static HumanizeReport AnalyzeSequence(IReadOnlyList<int> intervals, IReadOnlyList<(double X, double Y)>? pathPoints = null)
    {
        if (intervals.Count < 3)
            return new HumanizeReport(0, 0, 0, 0, 50, "样本不足");
        var mean = intervals.Average();
        var std = Math.Sqrt(intervals.Sum(i => (i - mean) * (i - mean)) / intervals.Count);
        var fixedCount = intervals.Count(i => i < 80);
        var fixedRatio = fixedCount / (double)intervals.Count;
        var buckets = intervals.GroupBy(i => i / 50).Select(g => (double)g.Count()).Where(v => v > 0).ToArray();
        var total = buckets.Sum();
        double entropy = 0;
        foreach (var b in buckets) { var p = b / total; entropy -= p * Math.Log2(p); }
        var maxEntropy = Math.Log2(Math.Max(1, buckets.Length));
        var normEntropy = maxEntropy == 0 ? 0 : entropy / maxEntropy;
        double pathNoisePct = 0;
        if (pathPoints != null && pathPoints.Count >= 3)
        {
            var dists = new List<double>();
            for (int i = 1; i < pathPoints.Count; i++)
            {
                var dx = pathPoints[i].X - pathPoints[i - 1].X;
                var dy = pathPoints[i].Y - pathPoints[i - 1].Y;
                dists.Add(Math.Sqrt(dx * dx + dy * dy));
            }
            var dm = dists.Average();
            if (dm > 0)
            {
                var dvar = Math.Sqrt(dists.Sum(d => (d - dm) * (d - dm)) / dists.Count);
                pathNoisePct = Math.Clamp(dvar / dm * 100, 0, 100);
            }
        }
        double overall = 50;
        overall += Math.Clamp((std - 20) * 0.8, 0, 25);
        overall -= fixedRatio * 60;
        overall += normEntropy * 25;
        overall += Math.Clamp(pathNoisePct, 0, 5);
        overall = Math.Clamp(overall, 0, 100);
        string conclusion = overall >= 75 ? "拟人度良好，反作弊风险低"
            : overall >= 55 ? "拟人度一般，建议增加间隔随机抖动"
            : "拟人度低，存在机械节拍风险";
        return new HumanizeReport(std, fixedRatio, normEntropy, pathNoisePct, overall, conclusion);
    }

    public static readonly string[] RiskKeywords =
    {
        "封禁", "违规", "踢下线", "账号异常", "检测到异常", "禁止登录", "ban", "suspicious",
        "违规操作", "限制登录", "mihoyo shield", "security violation", "账号冻结"
    };

    public static bool IsRiskText(string text) =>
        RiskKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static double Noise(double x, double y)
    {
        var n = (int)(x * 1000) * 374761393 + (int)(y * 1000) * 668265263;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0x7fffffff) / (double)0x7fffffff;
    }
}

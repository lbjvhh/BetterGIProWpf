namespace BetterGIProWpf.Services.NitroGen;

public sealed class ConfidenceScorer
{
    private readonly List<ActionBlock> _history = new();
    private readonly float[] _stickAvg = { 0.5f, 0.5f, 0.5f, 0.5f };
    public sealed record Score(float Value, float Entropy, float Consistency, float Deviation, string Detail);

    public Score Evaluate(ActionBlock actions, string? scene = null)
    {
        float ent = actions.Entropy();
        float clarity = Math.Max(0f, 1f - ent * 1.1f);
        float consistency = 0.5f;
        if (_history.Count > 0) { float d = _history[^1].StickDelta(actions); consistency = Math.Max(0f, 1f - d * 2.2f); }
        float dev = 0;
        for (int c = 0; c < 4; c++) dev += Math.Abs(actions.Data[c] - _stickAvg[c]);
        dev /= 4f;
        float deviationScore = Math.Max(0f, 1f - dev * 2.5f);
        float scenePenalty = (scene == "加载" || scene == "菜单") ? 0.25f : 0f;
        float score = Math.Clamp(clarity * 0.35f + consistency * 0.35f + deviationScore * 0.2f + 0.1f - scenePenalty, 0.05f, 0.97f);
        _history.Add(actions);
        if (_history.Count > 12) _history.RemoveAt(0);
        for (int c = 0; c < 4; c++) _stickAvg[c] = _stickAvg[c] * 0.9f + actions.Data[c] * 0.1f;
        return new Score((float)Math.Round(score, 3), (float)Math.Round(ent, 3), (float)Math.Round(consistency, 3), (float)Math.Round(deviationScore, 3), $"熵{ent:0.00}");
    }

    public void Reset() { _history.Clear(); for (int i = 0; i < 4; i++) _stickAvg[i] = 0.5f; }
}

public static class ComplexityAnalyzer
{
    public const string Fp32 = "fp32", Fp16 = "fp16", Int8 = "int8";
    public sealed record Metrics(float Complexity, float Edge, float ColorDiversity, float Lum);
    public sealed record PrecisionChoice(string Precision, string Reason);

    public static Metrics Analyze(byte[] data, int w, int h, float motionMag = 0f)
    {
        const int tw = 32, th = 18;
        int step = Math.Max(1, (w * h) / (tw * th));
        double lumSum = 0; int n = 0; var colors = new HashSet<int>(); double? prevLum = null; int edgeN = 0;
        for (int i = 0; i < data.Length; i += 4 * step)
        {
            int r = data[i], g = data[i + 1], b = data[i + 2];
            double l = 0.299 * r + 0.587 * g + 0.114 * b;
            lumSum += l; colors.Add((r >> 5) * 32 + (g >> 5) * 4 + (b >> 5));
            if (prevLum is not null && Math.Abs(l - prevLum.Value) > 24) edgeN++;
            prevLum = l; n++;
        }
        double edge = Math.Min(1, (double)edgeN / n * 6);
        double colorDiversity = Math.Min(1, colors.Count / 64.0);
        double lum = lumSum / n / 255.0;
        double complexity = Math.Min(1, edge * 0.35 + colorDiversity * 0.35 + Math.Min(1, motionMag * 3) * 0.2 + (1 - Math.Abs(lum - 0.5) * 2) * 0.1);
        return new Metrics((float)Math.Round(complexity, 3), (float)Math.Round(edge, 3), (float)Math.Round(colorDiversity, 3), (float)Math.Round(lum, 3));
    }

    public static PrecisionChoice ChoosePrecision(Metrics metrics, float motionMag, bool isDecisionPoint = false)
    {
        if (isDecisionPoint || metrics.Complexity > 0.78f || motionMag > 0.5f) return new PrecisionChoice(Fp32, $"高复杂度({metrics.Complexity})");
        if (metrics.Complexity > 0.45f) return new PrecisionChoice(Fp16, $"复杂({metrics.Complexity})");
        return new PrecisionChoice(Int8, $"简单({metrics.Complexity})");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterGIProWpf.Services.Humanize;

public class InputSample
{
    public string Key { get; set; } = "";
    public double TimestampMs { get; set; }
    public double HoldMs { get; set; }
}

public class HumanLikeReport
{
    public double EntropyScore { get; set; }
    public double JitterScore { get; set; }
    public double HoldVarianceScore { get; set; }
    public double OverallScore { get; set; }
    public string Suggestion { get; set; } = "";
}

public static class HumanLikeAnalyzer
{
    public static HumanLikeReport Analyze(List<InputSample> samples)
    {
        var report = new HumanLikeReport();
        if (samples == null || samples.Count < 5) { report.Suggestion = "样本太少，至少 5 次操作"; return report; }
        var keyGroups = samples.GroupBy(s => s.Key).Select(g => (double)g.Count()).ToList();
        double total = keyGroups.Sum();
        double entropy = 0;
        foreach (var c in keyGroups) { double p = c / total; entropy -= p * Math.Log(p, 2); }
        double maxEntropy = Math.Log(keyGroups.Count, 2);
        report.EntropyScore = maxEntropy > 0 ? Math.Round(entropy / maxEntropy * 100, 1) : 0;
        var intervals = new List<double>();
        for (int i = 1; i < samples.Count; i++) intervals.Add(samples[i].TimestampMs - samples[i - 1].TimestampMs);
        double avgInterval = intervals.Average();
        double stdInterval = Math.Sqrt(intervals.Select(x => Math.Pow(x - avgInterval, 2)).Average());
        report.JitterScore = Math.Min(100, Math.Round(stdInterval / Math.Max(1, avgInterval) * 100, 1));
        var holds = samples.Select(s => s.HoldMs).Where(h => h > 0).ToList();
        if (holds.Count >= 3)
        {
            double avgHold = holds.Average();
            double stdHold = Math.Sqrt(holds.Select(h => Math.Pow(h - avgHold, 2)).Average());
            report.HoldVarianceScore = Math.Min(100, Math.Round(stdHold / Math.Max(1, avgHold) * 100, 1));
        }
        report.OverallScore = Math.Round(report.EntropyScore * 0.3 + report.JitterScore * 0.4 + report.HoldVarianceScore * 0.3, 1);
        var suggestions = new List<string>();
        if (report.JitterScore < 20) suggestions.Add("按键间隔过于固定，加入 ±50ms 随机抖动");
        if (report.EntropyScore < 30) suggestions.Add("按键种类太少，增加操作多样性");
        if (report.HoldVarianceScore < 20) suggestions.Add("按住时长太一致，加入随机按住时间");
        if (suggestions.Count == 0) suggestions.Add("拟人化程度良好");
        report.Suggestion = string.Join("；", suggestions);
        return report;
    }
}

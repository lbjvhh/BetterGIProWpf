using System.IO;
using System.Text.Json;

namespace BetterGIProWpf.Services.Evaluation;

public record RegressionCase(string Id, string TaskType, string VideoRef, string ScriptJson, string Expected);
public record RegressionResult(string CaseId, bool Passed, double TaskCompletion, double PathDeviation,
    double SyncError, double RecoveryRate, double DurationSec, string? FailureDetail);

public class RegressionSuite
{
    public static readonly (string Id, string Type)[] StandardCases =
    {
        ("basic-run", "跑图"), ("basic-combat", "战斗"), ("basic-gather", "采集"),
        ("basic-dialogue", "对话"), ("basic-teleport", "传送"),
        ("adv-chain-1", "复合任务链"), ("adv-chain-2", "复合任务链"),
        ("edge-death", "异常:死亡"), ("edge-network", "异常:网络错误"), ("edge-load", "异常:加载卡住"),
    };

    private readonly List<RegressionResult> _results = new();
    private readonly object _lock = new();

    public List<RegressionResult> Run(IEnumerable<RegressionCase> cases,
        Func<RegressionCase, (double completion, double deviation, double sync, double recovery, double duration)> run)
    {
        var results = new List<RegressionResult>();
        foreach (var c in cases)
        {
            try
            {
                var (completion, deviation, sync, recovery, duration) = run(c);
                var passed = completion >= 0.75 && deviation < 100 && sync < 2.0 && recovery >= 0.8;
                var r = new RegressionResult(c.Id, passed, completion, deviation, sync, recovery, duration, passed ? null : "指标未达阈值");
                results.Add(r);
                lock (_lock) _results.Add(r);
            }
            catch (Exception ex)
            {
                var r = new RegressionResult(c.Id, false, 0, 999, 99, 0, 0, $"执行异常: {ex.Message}");
                results.Add(r);
                lock (_lock) _results.Add(r);
            }
        }
        return results;
    }

    public double PassRate { get { lock (_lock) return _results.Count == 0 ? 0 : (double)_results.Count(r => r.Passed) / _results.Count; } }
    public IReadOnlyList<RegressionResult> Results { get { lock (_lock) return _results.ToArray(); } }

    public string ReportMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# 回归测试报告");
        sb.AppendLine();
        sb.AppendLine("| 用例 | 结果 | 完成率 | 路径偏差 | 同步误差 | 恢复率 | 耗时 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        lock (_lock)
            foreach (var r in _results)
                sb.AppendLine($"| {r.CaseId} | {(r.Passed ? "✔" : "✘")} | {r.TaskCompletion:P0} | {r.PathDeviation:0}px | {r.SyncError:0.0}s | {r.RecoveryRate:P0} | {r.DurationSec:0.0}s |");
        sb.AppendLine();
        sb.AppendLine($"**通过率: {PassRate:P0}**");
        return sb.ToString();
    }

    public string ReportJunitXml()
    {
        var sb = new System.Text.StringBuilder();
        lock (_lock)
        {
            sb.AppendLine($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine($"<testsuite name=\"bettergipro.regression\" tests=\"{_results.Count}\" failures=\"{_results.Count(r => !r.Passed)}\">");
            foreach (var r in _results)
            {
                sb.AppendLine($"  <testcase name=\"{r.CaseId}\" time=\"{r.DurationSec:0.00}\">");
                if (!r.Passed) sb.AppendLine($"    <failure message=\"{r.FailureDetail}\" />");
                sb.AppendLine("  </testcase>");
            }
            sb.AppendLine("</testsuite>");
        }
        return sb.ToString();
    }

    public string SaveFailureArtifacts(string caseId, string baseDir, string logText, string? screenshotPath, string? clipPath)
    {
        var dir = Path.Combine(baseDir, "failures", caseId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "log.txt"), logText);
        if (screenshotPath != null && File.Exists(screenshotPath))
            File.Copy(screenshotPath, Path.Combine(dir, Path.GetFileName(screenshotPath)), true);
        if (clipPath != null && File.Exists(clipPath))
            File.Copy(clipPath, Path.Combine(dir, Path.GetFileName(clipPath)), true);
        return dir;
    }
}

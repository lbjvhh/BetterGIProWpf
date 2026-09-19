using System.IO;
namespace BetterGIProWpf.Services.Evaluation;
public record RegressionCase(string Id, string TaskType, string VideoRef, string ScriptJson, string Expected);
public record RegressionResult(string CaseId, bool Passed, double TaskCompletion, double PathDeviation, double SyncError, double RecoveryRate, double DurationSec, string? FailureDetail);
public class RegressionSuite
{
    public static readonly (string Id, string Type)[] StandardCases = { ("basic-run","跑图"),("basic-combat","战斗"),("basic-gather","采集"),("basic-dialogue","对话"),("basic-teleport","传送") };
    private readonly List<RegressionResult> _results = new();
    private readonly object _lock = new();
    public List<RegressionResult> Run(IEnumerable<RegressionCase> cases, Func<RegressionCase, (double completion, double deviation, double sync, double recovery, double duration)> run)
    {
        var results = new List<RegressionResult>();
        foreach (var c in cases) try { var (co, de, sy, re, du) = run(c); var passed = co >= 0.75 && de < 100 && sy < 2.0 && re >= 0.8; var r = new RegressionResult(c.Id, passed, co, de, sy, re, du, passed ? null : "未达阈值"); results.Add(r); lock (_lock) _results.Add(r); }
        catch (Exception ex) { var r = new RegressionResult(c.Id, false, 0, 999, 99, 0, 0, ex.Message); results.Add(r); lock (_lock) _results.Add(r); }
        return results;
    }
    public double PassRate { get { lock (_lock) return _results.Count == 0 ? 0 : (double)_results.Count(r => r.Passed) / _results.Count; } }
    public string ReportJunitXml()
    {
        var sb = new System.Text.StringBuilder();
        lock (_lock) { sb.AppendLine($"<testsuite name=\"regression\" tests=\"{_results.Count}\" failures=\"{_results.Count(r => !r.Passed)}\">"); foreach (var r in _results) { sb.AppendLine($"  <testcase name=\"{r.CaseId}\" time=\"{r.DurationSec:0.00}\">{(r.Passed ? "" : $"<failure message=\"{r.FailureDetail}\" />")}</testcase>"); } sb.AppendLine("</testsuite>"); }
        return sb.ToString();
    }
}

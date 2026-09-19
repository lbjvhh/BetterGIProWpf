using System.Text.Json;
namespace BetterGIProWpf.Services.Evaluation;
public static class TaskEvaluator
{
    public sealed record EvalResult(double TaskAccuracy, double SceneAccuracy, double TypeAccuracy, int Total, string Report);
    public static EvalResult Evaluate(string goldJson, string predJson)
    {
        using var gold = JsonDocument.Parse(goldJson); using var pred = JsonDocument.Parse(predJson);
        var gt = gold.RootElement.TryGetProperty("tasks", out var gta) ? gta : default;
        var pr = pred.RootElement.TryGetProperty("tasks", out var pra) ? pra : default;
        var gtT = gt.ValueKind == JsonValueKind.Array ? gt.EnumerateArray().ToList() : new();
        var prT = pr.ValueKind == JsonValueKind.Array ? pr.EnumerateArray().ToList() : new();
        var hit = 0; var typeHit = 0;
        foreach (var g in gtT) { var gg = Get(g, "goal"); var best = prT.Select(p => Sim(p, gg)).DefaultIfEmpty(0).Max(); if (best >= 0.8) hit++; var gt = Get(g, "type"); if (prT.Any(p => Get(p, "type") == gt)) typeHit++; }
        var taskAcc = gtT.Count > 0 ? (double)hit / gtT.Count : 0;
        var typeAcc = gtT.Count > 0 ? (double)typeHit / gtT.Count : 0;
        var report = $"任务要素提取准确率: {taskAcc:P0} ({hit}/{gtT.Count}); 类型准确率: {typeAcc:P0}";
        return new EvalResult(taskAcc, 0, typeAcc, gtT.Count, report);
    }
    private static string Get(JsonElement el, string name) => el.TryGetProperty(name, out var v) ? v.ToString() : "";
    private static double Sim(JsonElement p, string g) { var pg = Get(p, "goal"); if (pg == g) return 1; if (pg.Contains(g, StringComparison.OrdinalIgnoreCase)) return 0.9; return 0; }
}

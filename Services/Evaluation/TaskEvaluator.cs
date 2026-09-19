using System.Text.Json;

namespace BetterGIProWpf.Services.Evaluation;

public static class TaskEvaluator
{
    public sealed record EvalResult(double TaskAccuracy, double SceneAccuracy, double TypeAccuracy, int Total, string Report);

    public static EvalResult Evaluate(string goldJson, string predJson)
    {
        using var gold = JsonDocument.Parse(goldJson);
        using var pred = JsonDocument.Parse(predJson);
        var gt = gold.RootElement.TryGetProperty("tasks", out var gtA) ? gtA : default;
        var pr = pred.RootElement.TryGetProperty("tasks", out var prA) ? prA : default;
        var gtTasks = gt.ValueKind == JsonValueKind.Array ? gt.EnumerateArray().ToList() : new();
        var prTasks = pr.ValueKind == JsonValueKind.Array ? pr.EnumerateArray().ToList() : new();
        var hit = 0; var typeHit = 0;
        foreach (var g in gtTasks)
        {
            var gGoal = Get(g, "goal");
            var best = prTasks.Select(p => Sim(p, gGoal)).DefaultIfEmpty(0).Max();
            if (best >= 0.8) hit++;
            var gType = Get(g, "type");
            if (prTasks.Any(p => Get(p, "type") == gType)) typeHit++;
        }
        var taskAcc = gtTasks.Count > 0 ? (double)hit / gtTasks.Count : 0;
        var gtS = gold.RootElement.TryGetProperty("scenes", out var gsA) && gsA.ValueKind == JsonValueKind.Array
            ? gsA.EnumerateArray().Select(s => s.GetProperty("start").GetDouble()).ToList() : new List<double>();
        var prS = pred.RootElement.TryGetProperty("scenes", out var psA) && psA.ValueKind == JsonValueKind.Array
            ? psA.EnumerateArray().Select(s => s.GetProperty("start").GetDouble()).ToList() : new List<double>();
        var sceneAcc = gtS.Count > 0 ? Math.Max(0, 1 - Math.Abs(gtS.Count - prS.Count) / (double)Math.Max(gtS.Count, 1)) : 0;
        var report = string.Join("\n", new[] {
            $"任务数: 标注 {gtTasks.Count} / 预测 {prTasks.Count}",
            $"任务要素准确率: {taskAcc:P0}",
            $"任务类型准确率: {(gtTasks.Count > 0 ? typeHit / (double)gtTasks.Count : 0):P0}",
            $"场景分割准确率: {sceneAcc:P0}",
        });
        return new EvalResult(taskAcc, sceneAcc, gtTasks.Count > 0 ? typeHit / (double)gtTasks.Count : 0, gtTasks.Count, report);
    }

    private static string Get(JsonElement el, string name) => el.TryGetProperty(name, out var v) ? v.ToString() : "";

    private static double Sim(JsonElement p, string gGoal)
    {
        var pGoal = Get(p, "goal");
        if (pGoal == gGoal) return 1;
        if (pGoal.Contains(gGoal, StringComparison.OrdinalIgnoreCase) || gGoal.Contains(pGoal, StringComparison.OrdinalIgnoreCase)) return 0.9;
        if (pGoal.Length > 0 && gGoal.Length > 0 && pGoal[0] == gGoal[0]) return 0.6;
        return 0;
    }
}

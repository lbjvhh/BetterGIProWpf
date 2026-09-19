using System.Text.RegularExpressions;
using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf.Services.LocalAI;

public static class LocalIntentEngine
{
    public record IntentResult(CompanionTaskType? TaskType, string? Target, string Raw, string? Entity, LocalGameKnowledge.EntityKind? EntityKind, int? Quantity);

    private static readonly Regex _qty = new("(\\d+)\\s*(只|个|次|名|条|波)");

    public static IntentResult Parse(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return new IntentResult(null, null, t, null, null, null);
        foreach (var w in new[] { "请", "帮我", "麻烦", "快", "立刻", "马上", "给我" }) t = t.Replace(w, "", StringComparison.Ordinal);
        var entity = LocalGameKnowledge.FindEntities(t).FirstOrDefault();
        var qty = _qty.Match(t);
        int? quantity = qty.Success ? int.Parse(qty.Groups[1].Value) : null;
        CompanionTaskType? type = null;
        if (ContainsAny(t, "跟随", "跟着我")) type = CompanionTaskType.Follow;
        else if (ContainsAny(t, "打", "击杀", "清怪", "战斗", "打怪")) type = CompanionTaskType.Combat;
        else if (ContainsAny(t, "采集", "捡", "收集", "拾取")) type = CompanionTaskType.Gather;
        else if (ContainsAny(t, "传送", "前往", "去", "锚点", "神像")) type = CompanionTaskType.Teleport;
        else if (ContainsAny(t, "探索", "找", "搜")) type = CompanionTaskType.Explore;
        return new IntentResult(type, entity?.Name, (text ?? "").Trim(), entity?.Name, entity?.Kind, quantity);
    }

    public static ParsedCommand ToParsedCommand(string text) { var r = Parse(text); return new ParsedCommand(r.TaskType, r.Target ?? r.Entity, r.Raw); }

    public static string? ExtractTarget(string text)
    {
        var m = Regex.Match(text, "(?:前往|去|到|打|击杀|采集|找)([\\u4e00-\\u9fa5A-Za-z0-9]{2,16})");
        return m.Success ? m.Groups[1].Value.TrimEnd('的', '里', '那', '这') : null;
    }

    public static string Answer(string question, string ocrText, LocalGameKnowledge.Entity? entity)
    {
        if (entity != null)
        {
            if (question.Contains("在哪") || question.Contains("位置")) return $"{entity.Name}：可在地图标记找到。";
            if (entity.Kind == LocalGameKnowledge.EntityKind.Boss && question.Contains("弱")) return $"{entity.Name} 弱点：{LocalGameKnowledge.FindWeakness(entity.Name) ?? "暂无数据"}。";
            if (entity.Kind == LocalGameKnowledge.EntityKind.Material && (question.Contains("采集") || question.Contains("资源"))) return LocalGameKnowledge.MaterialAdvice(entity.Name, 0, null);
        }
        if (!string.IsNullOrWhiteSpace(ocrText)) return $"画面识别到：{string.Join("；", ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3))}";
        return "未识别到有效信息，请移动视角再问。";
    }

    private static bool ContainsAny(string text, params string[] keys) => keys.Any(text.Contains);
}

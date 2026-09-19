using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>本地意图引擎：规则词库 + 知识库，零外部模型。</summary>
public static class LocalIntentEngine
{
    public record IntentResult(CompanionTaskType? TaskType, string? Target, string Raw, string? Entity, LocalGameKnowledge.EntityKind? EntityKind, int? Quantity);
    private static readonly Regex _quote = new("[\"“”『』「」]([^\"“”『』「」]{1,24})[\"“”『』「」]");
    private static readonly Regex _qty = new("(\\d+)\\s*(只|个|次|名|条|波)");

    public static IntentResult Parse(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return new IntentResult(null, null, t, null, null, null);
        foreach (var w in new[] { "请", "帮我", "麻烦", "我想要", "快", "立刻", "马上", "给我" })
            t = t.Replace(w, "", StringComparison.Ordinal);
        var quote = _quote.Match(t);
        var rawTarget = quote.Success ? quote.Groups[1].Value : ExtractTarget(t);
        var entity = LocalGameKnowledge.FindEntities(t).FirstOrDefault();
        var qty = _qty.Match(t);
        int? quantity = qty.Success ? int.Parse(qty.Groups[1].Value) : null;
        CompanionTaskType? type = null;
        if (ContainsAny(t, "跟随", "跟着我", "跟紧")) type = CompanionTaskType.Follow;
        else if (ContainsAny(t, "打", "击杀", "消灭", "清怪", "战斗", "打怪", "打败")) type = CompanionTaskType.Combat;
        else if (ContainsAny(t, "采集", "捡", "收集", "拾取", "拿")) type = CompanionTaskType.Gather;
        else if (ContainsAny(t, "传送", "前往", "去", "到", "锚点", "神像")) type = CompanionTaskType.Teleport;
        else if (ContainsAny(t, "探索", "找", "搜", "看看")) type = CompanionTaskType.Explore;
        return new IntentResult(type, rawTarget, (text ?? "").Trim(), entity?.Name, entity?.Kind, quantity);
    }

    public static ParsedCommand ToParsedCommand(string text) => new(Parse(text).TaskType, Parse(text).Target ?? Parse(text).Entity, text);

    public static string? ExtractTarget(string text)
    {
        var m = Regex.Match(text, "(?:前往|去|到|传送到|打|击杀|消灭|采集|收集|拾取|找|搜索|打怪|打败)([\\u4e00-\\u9fa5A-Za-z0-9]{2,16})");
        if (m.Success)
        {
            var t = m.Groups[1].Value.TrimEnd('的', '里', '那', '这');
            foreach (var suf in new[] { "一下", "附近" })
                if (t.EndsWith(suf, StringComparison.Ordinal)) t = t[..^suf.Length];
            return t.Length == 0 ? null : t;
        }
        return null;
    }

    public static string Answer(string question, string ocrText, LocalGameKnowledge.Entity? entity)
    {
        if (entity != null)
        {
            if (question.Contains("在哪", StringComparison.Ordinal) || question.Contains("位置", StringComparison.Ordinal))
                return $"{entity.Name}：{(entity.Kind == LocalGameKnowledge.EntityKind.Boss ? "可在地图 Boss 标记找到" : entity.Kind == LocalGameKnowledge.EntityKind.Material ? "沿采集路线可找到" : "可在游戏地图中找到")}。";
            if (entity.Kind == LocalGameKnowledge.EntityKind.Boss && question.Contains("弱", StringComparison.Ordinal))
                return $"{entity.Name} 的弱点：{LocalGameKnowledge.FindWeakness(entity.Name) ?? "暂无数据"}。";
            if (entity.Kind == LocalGameKnowledge.EntityKind.Material)
                return LocalGameKnowledge.MaterialAdvice(entity.Name, 0, null);
        }
        if (!string.IsNullOrWhiteSpace(ocrText))
        {
            var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3);
            return $"从当前画面识别到：{string.Join("；", lines).Trim()}";
        }
        return "当前画面未识别到有效信息，请移动视角或打开地图后再问。";
    }

    private static bool ContainsAny(string text, params string[] keys) => keys.Any(text.Contains);
}

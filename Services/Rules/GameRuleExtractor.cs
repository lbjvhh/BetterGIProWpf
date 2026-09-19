using System.Text.Json;

namespace BetterGIProWpf.Services.Rules;

public class GameRule
{
    public required string Name { get; set; }
    public required string Trigger { get; set; }
    public required string Effect { get; set; }
    public double SourceTimestampSec { get; set; }
    public string Category { get; set; } = "探索";
}

public class GameRuleExtractor
{
    public const double MinSimilarity = 0.85;
    private readonly List<GameRule> _rules = new();
    private readonly object _lock = new();
    public int Count { get { lock (_lock) return _rules.Count; } }

    public record TaskClip(string Action, double TimestampSec, string? HintCategory = null);

    public List<GameRule> Extract(IEnumerable<TaskClip> clips)
    {
        var added = new List<GameRule>();
        foreach (var c in clips)
            foreach (var rule in RulesFromClip(c))
                if (TryAdd(rule)) added.Add(rule);
        return added;
    }

    public async Task<List<GameRule>> ExtractFromTextAsync(string transcript, double startTs = 0)
    {
        if (AppConfig.Ai.UseExternal && !string.IsNullOrWhiteSpace(AppConfig.Ai.ApiKey))
        {
            try
            {
                var sys = "你是游戏规则抽取器。从给定的游戏视频解说/文本中提取游戏机制规则。严格输出 JSON 数组，每个元素含 name/trigger/effect/category。category 只能是：战斗/探索/UI/经济/角色养成。不要解释。";
                var raw = await AppState.Ai.ChatAsync(sys, transcript);
                raw = raw.Trim();
                if (raw.StartsWith("```")) { raw = raw.Split('\n', 2)[1..][0]; raw = raw.TrimEnd('`').Trim(); }
                var arr = JsonSerializer.Deserialize<List<GameRuleDto>>(raw) ?? new();
                var added = new List<GameRule>();
                foreach (var d in arr)
                {
                    var r = new GameRule { Name = d.Name ?? "未命名规则", Trigger = d.Trigger ?? "", Effect = d.Effect ?? "", Category = d.Category ?? "探索", SourceTimestampSec = startTs };
                    if (TryAdd(r)) added.Add(r);
                }
                return added;
            }
            catch { }
        }
        return new();
    }

    private class GameRuleDto { public string? Name { get; set; } public string? Trigger { get; set; } public string? Effect { get; set; } public string? Category { get; set; } }

    public bool TryAdd(GameRule rule)
    {
        lock (_lock)
        {
            if (_rules.Any(r => Similarity(r.Name, rule.Name) >= MinSimilarity && r.Category == rule.Category)) return false;
            _rules.Add(rule); return true;
        }
    }

    public string ExportMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# 游戏规则知识库\n");
        foreach (var group in _rules.GroupBy(r => r.Category).OrderBy(g => g.Key))
        {
            sb.AppendLine($"## {group.Key}");
            foreach (var r in group)
            {
                sb.AppendLine($"- **{r.Name}**（来源 @{TimeSpan.FromSeconds(r.SourceTimestampSec):mm\\:ss}）");
                sb.AppendLine($"  - 触发：{r.Trigger}");
                sb.AppendLine($"  - 效果：{r.Effect}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public string ExportJson() { lock (_lock) return JsonSerializer.Serialize(_rules, new JsonSerializerOptions { WriteIndented = true }); }

    public string ExportHtml()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"zh\"><head><meta charset=\"utf-8\"><title>游戏规则知识库</title></head><body>");
        sb.AppendLine("<h1>游戏规则知识库</h1>");
        foreach (var group in _rules.GroupBy(r => r.Category).OrderBy(g => g.Key))
        {
            sb.AppendLine($"<h2>{group.Key}</h2><ul>");
            foreach (var r in group)
                sb.AppendLine($"<li><b>{r.Name}</b>（@{TimeSpan.FromSeconds(r.SourceTimestampSec):mm\\:ss}）<br/>触发：{r.Trigger}<br/>效果：{r.Effect}</li>");
            sb.AppendLine("</ul>");
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static IEnumerable<GameRule> RulesFromClip(TaskClip c)
    {
        var rules = new List<GameRule>();
        var a = c.Action;
        if (a.Contains("传送", StringComparison.Ordinal)) rules.Add(new GameRule { Name = "传送至" + a.Replace("传送", ""), Trigger = "需要快速移动时", Effect = a, SourceTimestampSec = c.TimestampSec, Category = "探索" });
        if (a.Contains("出击", StringComparison.Ordinal) || a.Contains("战斗", StringComparison.Ordinal)) rules.Add(new GameRule { Name = a.Length > 12 ? a[..12] + "…" : a, Trigger = "遭遇敌人时", Effect = a, SourceTimestampSec = c.TimestampSec, Category = "战斗" });
        if (a.Contains("采集", StringComparison.Ordinal) || a.Contains("拾取", StringComparison.Ordinal)) rules.Add(new GameRule { Name = "采集流程", Trigger = "到达采集点时", Effect = a, SourceTimestampSec = c.TimestampSec, Category = "经济" });
        if (a.Contains("对话", StringComparison.Ordinal)) rules.Add(new GameRule { Name = "NPC 对话", Trigger = "靠近 NPC 时", Effect = a, SourceTimestampSec = c.TimestampSec, Category = "UI" });
        if (a.Contains("升级", StringComparison.Ordinal) || a.Contains("养成", StringComparison.Ordinal)) rules.Add(new GameRule { Name = "角色养成", Trigger = "资源充足时", Effect = a, SourceTimestampSec = c.TimestampSec, Category = "角色养成" });
        if (rules.Count == 0 && !string.IsNullOrWhiteSpace(a)) rules.Add(new GameRule { Name = a.Length > 12 ? a[..12] + "…" : a, Trigger = "对应场景触发", Effect = a, SourceTimestampSec = c.TimestampSec, Category = c.HintCategory ?? "探索" });
        return rules;
    }

    public static double Similarity(string a, string b)
    {
        var sa = a.ToCharArray().Distinct().ToHashSet();
        var sb = b.ToCharArray().Distinct().ToHashSet();
        var inter = sa.Intersect(sb).Count();
        var union = sa.Union(sb).Count();
        return union == 0 ? 0 : (double)inter / union;
    }
}

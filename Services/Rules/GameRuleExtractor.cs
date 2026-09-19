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
    private readonly List<GameRule> _rules = new();
    private readonly object _lock = new();
    public int Count { get { lock (_lock) return _rules.Count; } }
    public record TaskClip(string Action, double TimestampSec, string? HintCategory = null);

    public List<GameRule> Extract(IEnumerable<TaskClip> clips)
    {
        var added = new List<GameRule>();
        foreach (var c in clips)
        {
            if (c.Action.Contains("传送")) added.Add(new GameRule { Name = "传送", Trigger = "快速移动", Effect = c.Action, SourceTimestampSec = c.TimestampSec, Category = "探索" });
            if (c.Action.Contains("战斗")) added.Add(new GameRule { Name = "战斗", Trigger = "遇敌", Effect = c.Action, SourceTimestampSec = c.TimestampSec, Category = "战斗" });
            if (c.Action.Contains("采集")) added.Add(new GameRule { Name = "采集", Trigger = "到达采集点", Effect = c.Action, SourceTimestampSec = c.TimestampSec, Category = "经济" });
        }
        return added;
    }

    public string ExportMarkdown()
    {
        var sb = new System.Text.StringBuilder(); sb.AppendLine("# 游戏规则知识库");
        foreach (var r in _rules) sb.AppendLine($"- {r.Name}: {r.Effect}");
        return sb.ToString();
    }

    public string ExportJson() { lock (_lock) return JsonSerializer.Serialize(_rules); }
}

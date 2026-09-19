using System.Text.Json; namespace BetterGIProWpf.Services.Rules;
public class GameRule { public required string Name { get; set; } public required string Trigger { get; set; } public required string Effect { get; set; } public double Ts { get; set; } public string Category { get; set; } = "探索"; }
public class GameRuleExtractor {
    private readonly List<GameRule> _r = new(); private readonly object _lock = new();
    public int Count { get { lock (_lock) return _r.Count; } }
    public record Clip(string Action, double Ts, string? Hint=null);
    public List<GameRule> Extract(IEnumerable<Clip> clips) {
        var added = new List<GameRule>();
        foreach (var c in clips) foreach (var r in FromClip(c)) if (TryAdd(r)) added.Add(r);
        return added;
    }
    public bool TryAdd(GameRule r) { lock (_lock) { if (_r.Any(x => Sim(x.Name,r.Name)>=0.85 && x.Category==r.Category)) return false; _r.Add(r); return true; } }
    public string ExportMd() { var sb=new System.Text.StringBuilder(); sb.AppendLine("# 游戏规则"); foreach (var g in _r.GroupBy(x=>x.Category)) { sb.AppendLine($"## {g.Key}"); foreach (var r in g) sb.AppendLine($"- **{r.Name}** 触发:{r.Trigger} 效果:{r.Effect}"); } return sb.ToString(); }
    public string ExportJson() { lock (_lock) return JsonSerializer.Serialize(_r); }
    private static IEnumerable<GameRule> FromClip(Clip c) {
        var a = c.Action; var r = new List<GameRule>();
        if (a.Contains("传送")) r.Add(new GameRule{Name="传送",Trigger="快速移动",Effect=a,Ts=c.Ts,Category="探索"});
        if (a.Contains("战斗")||a.Contains("击杀")) r.Add(new GameRule{Name=a.Length>12?a[..12]:a,Trigger="遇敌",Effect=a,Ts=c.Ts,Category="战斗"});
        if (a.Contains("采集")) r.Add(new GameRule{Name="采集流程",Trigger="到采集点",Effect=a,Ts=c.Ts,Category="经济"});
        return r;
    }
    public static double Sim(string a, string b) { var sa=a.ToCharArray().Distinct().ToHashSet(); var sb=b.ToCharArray().Distinct().ToHashSet(); var u=sa.Union(sb).Count(); return u==0?0:(double)sa.Intersect(sb).Count()/u; }
}

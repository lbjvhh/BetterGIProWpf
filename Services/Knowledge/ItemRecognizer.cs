namespace BetterGIProWpf.Services.Knowledge;
public class ItemRecognizer
{
    public record ItemTemplate(string Name, string Category, int IconHash);
    public record EquipSuggestion(string Item, string Reason, string Action);
    private readonly Dictionary<int, ItemTemplate> _templates = new();
    private readonly List<string> _inventory = new();
    private readonly List<string> _equipped = new();
    private readonly object _lock = new();
    public event Action<string>? Log;
    public ItemRecognizer() { var seed = new[] { ("绝缘之旗印","圣遗物"),("如雷的盛怒","圣遗物"),("宗室套","圣遗物"),("天空之翼","武器"),("护摩之杖","武器"),("雷电将军","角色"),("甘雨","角色"),("水晶块","材料") }; for (var i = 0; i < seed.Length; i++) _templates[i+1] = new ItemTemplate(seed[i].Item1, seed[i].Item2, i+1); }
    public List<string> Recognize(IEnumerable<int> iconHashes) { var f = new List<string>(); lock (_lock) foreach (var h in iconHashes) if (_templates.TryGetValue(h, out var t)) f.Add(t.Name); return f.Distinct().ToList(); }
    public void SetState(IEnumerable<string> inv, IEnumerable<string> eq) { lock (_lock) { _inventory.Clear(); _inventory.AddRange(inv); _equipped.Clear(); _equipped.AddRange(eq); } }
    public List<EquipSuggestion> CompareWithRecommendation(string recSet, string[] recPieces)
    {
        var r = new List<EquipSuggestion>();
        lock (_lock) { if (!_equipped.Contains(recSet)) r.Add(new EquipSuggestion(recSet, $"视频推荐「{recSet}」，你当前「{string.Join("/", _equipped)}」", "建议更换")); foreach (var p in recPieces) if (!_inventory.Contains(p, StringComparer.OrdinalIgnoreCase)) r.Add(new EquipSuggestion(p, $"推荐需要「{p}」", "建议获取")); }
        Log?.Invoke($"装备对比：{r.Count} 条建议"); return r;
    }
    public record FilterRule(string MainStat, string SubStat, string SetEffect);
    public List<string> ApplyRules(IEnumerable<FilterRule> rules, IEnumerable<string> artifacts)
    {
        var actions = new List<string>();
        foreach (var a in artifacts) foreach (var r in rules) { if (r.MainStat.Length > 0 && !a.Contains(r.MainStat, StringComparison.OrdinalIgnoreCase)) continue; if (r.SubStat.Length > 0 && !a.Contains(r.SubStat, StringComparison.OrdinalIgnoreCase)) continue; actions.Add($"锁定「{a}」"); break; }
        return actions;
    }
}

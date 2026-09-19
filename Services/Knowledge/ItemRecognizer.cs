using System.Text.Json;

namespace BetterGIProWpf.Services.Knowledge;

public class ItemRecognizer
{
    public record ItemTemplate(string Name, string Category, int IconHash, string[] Aliases);
    public record EquipSuggestion(string Item, string Reason, string Action);
    public record FilterRule(string MainStat, string SubStat, string SetEffect);

    private readonly Dictionary<int, ItemTemplate> _templates = new();
    private readonly List<string> _inventory = new();
    private readonly List<string> _equipped = new();
    private readonly object _lock = new();
    public event Action<string>? Log;

    public ItemRecognizer()
    {
        var seed = new[] {
            ("绝缘之旗印", "圣遗物"), ("如雷的盛怒", "圣遗物"), ("宗室套", "圣遗物"),
            ("风套", "圣遗物"), ("角斗士", "圣遗物"), ("天空之翼", "武器"),
            ("护摩之杖", "武器"), ("雾切之回光", "武器"), ("薙草之稻光", "武器"),
            ("班尼特", "角色"), ("雷电将军", "角色"), ("甘雨", "角色"),
            ("水晶块", "材料"), ("白铁矿", "材料"), ("摩拉", "材料"), ("原石", "材料"),
        };
        for (var i = 0; i < seed.Length; i++)
            _templates[i + 1] = new ItemTemplate(seed[i].Item1, seed[i].Item2, i + 1, new[] { seed[i].Item1 });
    }

    public void RegisterTemplate(ItemTemplate t) { lock (_lock) _templates[t.IconHash] = t; }

    public List<string> Recognize(IEnumerable<int> iconHashes)
    {
        var found = new List<string>();
        lock (_lock)
            foreach (var h in iconHashes) if (_templates.TryGetValue(h, out var t)) found.Add(t.Name);
        return found.Distinct().ToList();
    }

    public void SetState(IEnumerable<string> inventory, IEnumerable<string> equipped)
    {
        lock (_lock) { _inventory.Clear(); _inventory.AddRange(inventory); _equipped.Clear(); _equipped.AddRange(equipped); }
    }

    public List<EquipSuggestion> CompareWithRecommendation(string recommendedSet, string[] recommendedPieces)
    {
        var result = new List<EquipSuggestion>();
        lock (_lock)
        {
            if (!_equipped.Contains(recommendedSet))
                result.Add(new EquipSuggestion(recommendedSet, $"视频推荐「{recommendedSet}」，当前「{string.Join("/", _equipped)}", "建议更换"));
            foreach (var p in recommendedPieces)
                if (!_inventory.Contains(p, StringComparer.OrdinalIgnoreCase) && !_equipped.Contains(p))
                    result.Add(new EquipSuggestion(p, $"需「{p}」", "建议获取"));
        }
        return result;
    }

    public List<string> ApplyRules(IEnumerable<FilterRule> rules, IEnumerable<string> artifacts)
    {
        var actions = new List<string>();
        foreach (var a in artifacts)
            foreach (var r in rules)
            {
                if (r.MainStat.Length > 0 && !a.Contains(r.MainStat, StringComparison.OrdinalIgnoreCase)) continue;
                if (r.SubStat.Length > 0 && !a.Contains(r.SubStat, StringComparison.OrdinalIgnoreCase)) continue;
                if (r.SetEffect.Length > 0 && !a.Contains(r.SetEffect, StringComparison.OrdinalIgnoreCase)) continue;
                actions.Add($"锁定「{a}」");
                break;
            }
        return actions;
    }

    public int TemplateCount { get { lock (_lock) return _templates.Count; } }
}

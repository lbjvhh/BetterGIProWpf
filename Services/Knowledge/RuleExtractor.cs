namespace BetterGIProWpf.Services.Knowledge;

/// <summary>模块6: 游戏规则自动提取。</summary>
public class RuleExtractor
{
    public record GameRule(string Category, string Name, string Trigger, string Effect, string Source);

    private readonly Dictionary<string, (string Category, string Effect)> _rules = new()
    {
        ["元素反应"] = ("战斗", "元素反应造成额外伤害"),
        ["超载"] = ("战斗", "火+雷爆炸造成范围伤害"),
        ["蒸发"] = ("战斗", "火+水伤害提升"),
        ["融化"] = ("战斗", "火+冰伤害提升"),
        ["感电"] = ("战斗", "雷+水持续伤害"),
        ["传送锚点"] = ("探索", "解锁后可快速传送"),
        ["七天神像"] = ("探索", "恢复体力和生命值"),
        ["宝箱"] = ("探索", "开启获得奖励"),
        ["神瞳"] = ("探索", "供奉提升神像等级"),
        ["地图"] = ("UI", "按M打开地图"),
        ["背包"] = ("UI", "按B打开背包"),
        ["角色"] = ("UI", "按C打开角色面板"),
        ["摩拉"] = ("经济", "游戏货币"),
        ["圣遗物"] = ("角色养成", "装备提升角色属性"),
        ["武器"] = ("角色养成", "装备提升攻击力"),
    };

    public event Action<string>? Log;

    public List<GameRule> Extract(string text)
    {
        var results = new List<GameRule>();
        foreach (var (keyword, (cat, effect)) in _rules)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new GameRule(cat, keyword, $"文本包含「{keyword}」", effect, "OCR提取"));
                Log?.Invoke($"[规则] {cat}: {keyword} → {effect}");
            }
        }
        return results;
    }

    public string ExportJson(List<GameRule> rules)
    {
        return System.Text.Json.JsonSerializer.Serialize(rules, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}

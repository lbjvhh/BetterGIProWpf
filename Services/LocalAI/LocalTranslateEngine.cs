using BetterGIProWpf.Services.Knowledge;

namespace BetterGIProWpf.Services.LocalAI;

public class LocalTranslateEngine : TranslationService.ITranslateBackend
{
    private static readonly Dictionary<string, string> Dict = new() {
        ["地图"] = "Map", ["背包"] = "Inventory", ["角色"] = "Character", ["设置"] = "Settings",
        ["任务"] = "Quest", ["传送"] = "Teleport", ["锚点"] = "Waypoint", ["神像"] = "Statue",
        ["战斗"] = "Combat", ["探索"] = "Explore", ["采集"] = "Gather", ["对话"] = "Dialogue",
        ["武器"] = "Weapon", ["圣遗物"] = "Artifact", ["原石"] = "Primogem", ["摩拉"] = "Mora",
        ["体力"] = "Stamina", ["敌人"] = "Enemy", ["弱点"] = "Weakness",
        ["蒙德"] = "Mondstadt", ["璃月"] = "Liyue", ["稻妻"] = "Inazuma", ["须弥"] = "Sumeru",
        ["枫丹"] = "Fontaine", ["纳塔"] = "Natlan", ["风起地"] = "Windrise",
        ["旅行者"] = "Traveler", ["温迪"] = "Venti", ["钟离"] = "Zhongli", ["雷电将军"] = "Raiden",
        ["绝缘之旗印"] = "Emblem of Severed Fate", ["如雷的盛怒"] = "Thundering Fury",
    };

    public Task<string> TranslateAsync(string text, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult(text);
        var result = text;
        foreach (var kv in Dict.OrderByDescending(k => k.Key.Length)) result = result.Replace(kv.Key, kv.Value, StringComparison.Ordinal);
        return Task.FromResult(result);
    }
}

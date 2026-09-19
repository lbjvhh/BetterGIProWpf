using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BetterGIProWpf.Services.Knowledge;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>本地翻译引擎：内置游戏常用双语词典 + 官方译名术语表，完全离线。</summary>
public class LocalTranslateEngine : TranslationService.ITranslateBackend
{
    private static readonly Dictionary<string, string> Dict = new()
    {
        ["地图"]="Map",["背包"]="Inventory",["角色"]="Character",["设置"]="Settings",["任务"]="Quest",["传送"]="Teleport",
        ["锚点"]="Waypoint",["神像"]="Statue",["对话"]="Dialogue",["战斗"]="Combat",["探索"]="Explore",["采集"]="Gather",
        ["武器"]="Weapon",["圣遗物"]="Artifact",["天赋"]="Talent",["命之座"]="Constellation",["原石"]="Primogem",["摩拉"]="Mora",
        ["体力"]="Stamina",["生命值"]="HP",["攻击力"]="ATK",["防御力"]="DEF",["元素"]="Element",
        ["火"]="Pyro",["水"]="Hydro",["雷"]="Electro",["冰"]="Cryo",["风"]="Anemo",["岩"]="Geo",["草"]="Dendro",
        ["敌人"]="Enemy",["Boss"]="Boss",["弱点"]="Weakness",
        ["蒙德"]="Mondstadt",["璃月"]="Liyue",["稻妻"]="Inazuma",["须弥"]="Sumeru",["枫丹"]="Fontaine",["纳塔"]="Natlan",
        ["风起地"]="Windrise",["晨曦酒庄"]="Dawn Winery",["龙脊雪山"]="Dragonspine",["层岩巨渊"]="The Chasm",
        ["旅行者"]="Traveler",["安柏"]="Amber",["凯亚"]="Kaeya",["丽莎"]="Lisa",["钟离"]="Zhongli",["温迪"]="Venti",
        ["雷电将军"]="Raiden Shogun",["纳西妲"]="Nahida",["芙宁娜"]="Furina",["那维莱特"]="Neuvillette",
        ["绝缘之旗印"]="Emblem of Severed Fate",["如雷的盛怒"]="Thundering Fury",["角斗士的终幕礼"]="Gladiator's Finale",
    };
    private static readonly Dictionary<string, string> JaDict = new() { ["地图"]="マップ",["任务"]="クエスト",["传送"]="テレポート",["战斗"]="バトル",["蒙德"]="モンド",["璃月"]="璃月",["稻妻"]="稲妻" };
    private static readonly Dictionary<string, string> KoDict = new() { ["地图"]="지도",["任务"]="임무",["传送"]="텔레포트",["战斗"]="전투",["蒙德"]="몬드",["璃月"]="리월",["稻妻"]="이나즈마" };
    private static readonly Dictionary<string, string> RuDict = new() { ["地图"]="Карта",["任务"]="Задание",["传送"]="Телепорт",["战斗"]="Бой",["蒙德"]="Мондштадт",["璃月"]="Лиюэ",["稻妻"]="Инадзума" };

    public string[] SupportedTargets { get; } = { "en-US", "ja-JP", "ko-KR", "ru-RU" };

    public Task<string> TranslateAsync(string text, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult(text);
        var target = to.ToLowerInvariant();
        var dict = target switch { "ja" or "ja-jp"=>JaDict, "ko" or "ko-kr"=>KoDict, "ru" or "ru-ru"=>RuDict, _=>Dict };
        var builder = text;
        foreach (var kv in dict.OrderByDescending(k => k.Key.Length))
            builder = builder.Replace(kv.Key, kv.Value, StringComparison.Ordinal);
        return Task.FromResult(builder);
    }
}

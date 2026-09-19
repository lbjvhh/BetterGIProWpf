using System.Collections.Generic;
using System.Linq;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>纯本地游戏知识库：地点/角色/Boss/材料/套装/元素，零外部模型。</summary>
public static class LocalGameKnowledge
{
    public enum EntityKind { Place, Character, Boss, Material, Set, Element, None }
    public record Entity(string Name, EntityKind Kind, string? Weakness = null);
    private static readonly Dictionary<string, Entity> _index = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<Entity> Entities { get; }

    static LocalGameKnowledge()
    {
        void Add(string name, EntityKind kind, string? weakness = null) => _index[name] = new Entity(name, kind, weakness);
        foreach (var p in new[] { "蒙德城","璃月港","稻妻城","须弥城","枫丹廷","风起地","星落湖","望风角","晨曦酒庄","誓言岬","奔狼领","风龙废墟","清泉镇","龙脊雪山","层岩巨渊","轻策庄","石门","归离原","渌华池","瑶光滩","孤云阁","望舒客栈","渊下宫","鹤观","清籁岛","海祇岛","鸣神岛","八酝岛","踏鞴砂","珊瑚宫","影向山","镇守之森","稻妻城天守阁","神里屋敷","荒海","奥摩斯港","化城郭","净善宫","阿如村","赤王陵","千壑沙地","枫丹廷区","梅洛彼得堡","柔灯港","新枫丹科学院","厄里那斯","纳塔","回声之子","流泉之众","悬木人","花羽会","烟谜主","圣火竞技场","七天神像" })
            Add(p, EntityKind.Place);
        foreach (var c in new[] { "旅行者","空","荧","安柏","凯亚","丽莎","琴","迪卢克","温迪","可莉","莫娜","班尼特","诺艾尔","菲谢尔","雷泽","芭芭拉","砂糖","重云","香菱","行秋","凝光","北斗","钟离","甘雨","魈","胡桃","申鹤","刻晴","夜兰","白术","赛诺","提纳里","柯莱","妮露","艾尔海森","纳西妲","流浪者","久岐忍","荒泷一斗","五郎","托马","宵宫","神里绫华","神里绫人","珊瑚宫心海","八重神子","雷电将军","芙宁娜","那维莱特","林尼","克洛琳德","阿蕾奇诺","玛薇卡","丘丘人","丘丘暴徒","深渊法师","遗迹守卫","遗迹猎者","史莱姆","飘浮灵","盗宝团","魔偶剑鬼" })
            Add(c, EntityKind.Character);
        Add("北风狼王", EntityKind.Boss, "冰元素抗性高，弱火");
        Add("风魔龙特瓦林", EntityKind.Boss, "弱元素伤害，攻击头顶毒瘤");
        Add("无相之风", EntityKind.Boss, "风核心，弱任何元素");
        Add("无相之岩", EntityKind.Boss, "岩核，需大剑/爆炸破柱");
        Add("无相之雷", EntityKind.Boss, "雷核心，弱冰火");
        Add("无相之火", EntityKind.Boss, "火核心，弱水");
        Add("无相之水", EntityKind.Boss, "水核心，弱冰雷");
        Add("无相之冰", EntityKind.Boss, "冰核心，弱火");
        Add("急冻树", EntityKind.Boss, "弱火，破核心");
        Add("爆炎树", EntityKind.Boss, "弱水，破核心");
        Add("纯水精灵", EntityKind.Boss, "弱冰雷，打水幻形");
        Add("若陀龙王", EntityKind.Boss, "按元素形态切换克制属性");
        Add("黄金王兽", EntityKind.Boss, "岩元素护盾，用岩伤破");
        Add("雷音权现", EntityKind.Boss, "雷免疫，弱冰");
        Add("兆载永劫龙兽", EntityKind.Boss, "弱点翅膀核心，用弓箭");
        Add("恒常机关阵列", EntityKind.Boss, "弱雷，破坏核心");
        Add("风蚀沙虫", EntityKind.Boss, "弱冰火，钻地时攻击");
        Add("翠翎恐蕈", EntityKind.Boss, "草抗高，弱火雷");
        Add("掣电树", EntityKind.Boss, "弱火，破核心");
        foreach (var m in new[] { "水晶块","白铁块","星银矿石","夜泊石","电气水晶","魔晶块","铁矿","烈焰花","冰雾花","蒲公英籽","风车菊","琉璃百合","霓裳花","清心","琉璃袋","塞西莉亚花","小灯草","慕风蘑菇","落落莓","钩钩果","绝云椒椒","松茸","树王圣体菇","帕蒂沙兰","湖光铃兰","幽光星星","甜甜花","薄荷","日落果","苹果","禽肉","兽肉","鱼肉","鸟蛋","经验书","摩拉","原石","纠缠之缘","相遇之缘","脆弱树脂","浓缩树脂","圣遗物" })
            Add(m, EntityKind.Material);
        foreach (var s in new[] { "绝缘之旗印","如雷的盛怒","追忆之注连","角斗士的终幕礼","流浪大地的乐团","沉沦之心","昔日宗室之仪","翠绿之影","苍白之火","千岩牢固","辰砂往生录","海染砗磲","饰金之梦","深林的记忆","乐园遗落之花","逐影猎人","黄金剧团","花海甘露之光","教官","流放者","战狂","冒险家","游医" })
            Add(s, EntityKind.Set);
        foreach (var e in new[] { "火元素","水元素","雷元素","冰元素","风元素","岩元素","草元素","蒸发","融化","感电","超载","冻结","超导","扩散","结晶","激化","绽放","燃烧","元素战技","元素爆发","元素反应" })
            Add(e, EntityKind.Element);
        Entities = _index.Values.OrderByDescending(e => e.Name.Length).ToArray();
    }

    public static IReadOnlyList<Entity> FindEntities(string text)
    {
        var hits = new List<Entity>(); var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in Entities)
            if (text.Contains(e.Name, StringComparison.OrdinalIgnoreCase) && matched.Add(e.Name)) hits.Add(e);
        return hits;
    }

    public static string? FindWeakness(string bossName)
    {
        if (_index.TryGetValue(bossName, out var e) && e.Weakness != null) return e.Weakness;
        var hit = Entities.FirstOrDefault(x => x.Kind == EntityKind.Boss && x.Weakness != null
            && (x.Name.Contains(bossName, StringComparison.OrdinalIgnoreCase) || bossName.Contains(x.Name, StringComparison.OrdinalIgnoreCase)));
        return hit?.Weakness;
    }

    public static string MaterialAdvice(string material, int currentStock, int? target)
    {
        if (target is int t && currentStock >= t) return $"{material} 已达目标 {t}，无需继续采集";
        var need = target is int t2 ? t2 - currentStock : Math.Max(0, 50 - currentStock);
        return need <= 0 ? $"{material} 当前库存 {currentStock}，建议按需采集"
            : $"{material} 当前库存 {currentStock}，距目标还差 {need}，建议沿攻略路线采集";
    }
}

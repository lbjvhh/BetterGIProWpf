namespace BetterGIProWpf.Services.LocalAI;

public static class LocalGameKnowledge
{
    public enum EntityKind { Place, Character, Boss, Material, Set, Element, None }
    public record Entity(string Name, EntityKind Kind, string? Weakness = null);

    private static readonly Dictionary<string, Entity> _index = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<Entity> Entities { get; }

    static LocalGameKnowledge()
    {
        void Add(string n, EntityKind k, string? w = null) => _index[n] = new Entity(n, k, w);
        foreach (var p in new[] { "蒙德城","璃月港","稻妻城","须弥城","枫丹廷","风起地","星落湖","望风角","晨曦酒庄","奔狼领","风龙废墟","摘星崖","清泉镇","龙脊雪山","层岩巨渊","轻策庄","石门","归离原","孤云阁","渊下宫","鹤观","清籁岛","鸣神岛","珊瑚宫","神里屋敷","须弥雨林","奥摩斯港","化城郭","赤王陵","千壑沙地","枫丹廷区","梅洛彼得堡","歌剧院","纳塔","七天神像" }) Add(p, EntityKind.Place);
        foreach (var c in new[] { "旅行者","安柏","凯亚","丽莎","琴","迪卢克","温迪","可莉","班尼特","诺艾尔","菲谢尔","雷泽","芭芭拉","砂糖","重云","香菱","行秋","凝光","北斗","钟离","甘雨","魈","胡桃","刻晴","夜兰","纳西妲","流浪者","久岐忍","荒泷一斗","宵宫","神里绫华","神里绫人","心海","八重神子","雷电将军","芙宁娜","那维莱特","林尼","琳妮特","玛拉妮","基尼奇","阿蕾奇诺","玛薇卡","丘丘人","深渊法师","愚人众","遗迹守卫","遗迹猎者","史莱姆","魔偶剑鬼" }) Add(c, EntityKind.Character);
        Add("北风狼王", EntityKind.Boss, "弱火");
        Add("风魔龙特瓦林", EntityKind.Boss, "攻击头顶毒瘤");
        Add("无相之风", EntityKind.Boss, "弱任何元素");
        Add("无相之岩", EntityKind.Boss, "需大剑/爆炸破柱");
        Add("无相之雷", EntityKind.Boss, "弱冰火");
        Add("无相之火", EntityKind.Boss, "弱水");
        Add("急冻树", EntityKind.Boss, "弱火");
        Add("爆炎树", EntityKind.Boss, "弱水");
        Add("纯水精灵", EntityKind.Boss, "弱冰雷");
        Add("古岩龙蜥", EntityKind.Boss, "弱冰火雷");
        Add("若陀龙王", EntityKind.Boss, "按元素克制");
        Add("黄金王兽", EntityKind.Boss, "岩护盾");
        Add("雷音权现", EntityKind.Boss, "弱冰");
        Add("兆载永劫龙兽", EntityKind.Boss, "弓箭射翅膀");
        Add("风蚀沙虫", EntityKind.Boss, "弱冰火");
        Add("翠翎恐蕈", EntityKind.Boss, "弱火雷");
        Add("掣电树", EntityKind.Boss, "弱火");
        Add("隐山猊兽", EntityKind.Boss, "弱物理");
        foreach (var m in new[] { "水晶块","白铁块","星银矿石","夜泊石","电气水晶","魔晶块","铁矿","烈焰花","冰雾花","蒲公英籽","风车菊","琉璃百合","清心","小灯草","落落莓","松茸","摩拉","原石","纠缠之缘","脆弱树脂","浓缩树脂","圣遗物" }) Add(m, EntityKind.Material);
        foreach (var s in new[] { "绝缘之旗印","如雷的盛怒","追忆之注连","角斗士","流浪大地","沉沦之心","宗室","翠绿之影","苍白之火","千岩牢固","辰砂往生录","海染砗磲","华馆梦醒","饰金之梦","深林的记忆","黄金剧团","逐影猎人" }) Add(s, EntityKind.Set);
        foreach (var e in new[] { "火","水","雷","冰","风","岩","草","蒸发","融化","感电","超载","冻结","超导","扩散","结晶","激化","绽放","燃烧","元素战技","元素爆发" }) Add(e, EntityKind.Element);
        Entities = _index.Values.OrderByDescending(e => e.Name.Length).ToArray();
    }

    public static IReadOnlyList<Entity> FindEntities(string text)
    {
        var hits = new List<Entity>(); var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in Entities) if (text.Contains(e.Name, StringComparison.OrdinalIgnoreCase) && matched.Add(e.Name)) hits.Add(e);
        return hits;
    }

    public static string? FindFirst(string text, EntityKind kind)
    {
        foreach (var e in Entities) if (e.Kind == kind && text.Contains(e.Name, StringComparison.OrdinalIgnoreCase)) return e.Name;
        return null;
    }

    public static string? FindWeakness(string bossName)
    {
        if (_index.TryGetValue(bossName, out var e) && e.Weakness != null) return e.Weakness;
        var hit = Entities.FirstOrDefault(x => x.Kind == EntityKind.Boss && x.Weakness != null && (x.Name.Contains(bossName, StringComparison.OrdinalIgnoreCase) || bossName.Contains(x.Name, StringComparison.OrdinalIgnoreCase)));
        return hit?.Weakness;
    }

    public static string MaterialAdvice(string material, int currentStock, int? target)
    {
        if (target is int t && currentStock >= t) return $"{material} 已达目标 {t}";
        var need = target is int t2 ? t2 - currentStock : Math.Max(0, 50 - currentStock);
        return need <= 0 ? $"{material} 库存 {currentStock}，按需采集" : $"{material} 库存 {currentStock}，还差 {need}";
    }
}

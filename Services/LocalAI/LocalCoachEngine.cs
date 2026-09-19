using BetterGIProWpf.Services.Coach;

namespace BetterGIProWpf.Services.LocalAI;

public static class LocalCoachEngine
{
    public static List<CoachSuggestion> Suggest(string ocrText, double staminaRatio, string currentCharacter, string? bossInFight, bool[] skillReady, int maxCount = 3)
    {
        var list = new List<CoachSuggestion>();
        if (staminaRatio < 0.25) list.Add(new CoachSuggestion(SuggestionKind.Stamina, "体力", "体力不足，建议传送七天神像回复", 0.85));
        if (!string.IsNullOrEmpty(bossInFight))
        {
            var weak = LocalGameKnowledge.FindWeakness(bossInFight);
            if (weak != null) list.Add(new CoachSuggestion(SuggestionKind.EnemyWeakness, "弱点", $"「{bossInFight}」弱点: {weak}", 0.95));
        }
        if (skillReady.Any(x => x) && list.Count < maxCount)
            list.Add(new CoachSuggestion(SuggestionKind.SkillTiming, "技能", $"{currentCharacter} 技能就绪", 0.7));
        foreach (var kw in new[] { "蒸发", "融化", "感电", "超载", "冻结", "激化" })
            if (ocrText.Contains(kw)) { list.Add(new CoachSuggestion(SuggestionKind.ElementReaction, "元素反应", $"检测到「{kw}」", 0.6)); break; }
        return list.OrderByDescending(s => s.Relevance).Take(maxCount).ToList();
    }
}

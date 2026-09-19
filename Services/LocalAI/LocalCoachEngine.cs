using System;
using System.Collections.Generic;
using System.Linq;
using BetterGIProWpf.Services.Coach;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>本地策略教练规则库（模块 2）：5 类建议，全部本地规则。</summary>
public static class LocalCoachEngine
{
    public static List<CoachSuggestion> Suggest(
        string ocrText, double staminaRatio, string currentCharacter,
        string? bossInFight, bool[] skillReady, int maxCount = 3)
    {
        var list = new List<CoachSuggestion>();
        if (staminaRatio < 0.25)
            list.Add(new CoachSuggestion(SuggestionKind.Stamina, "体力管理", "当前体力不足，建议先传送到七天神像回复", 0.85));
        if (!string.IsNullOrEmpty(bossInFight))
        {
            var weak = LocalGameKnowledge.FindWeakness(bossInFight);
            if (weak != null)
                list.Add(new CoachSuggestion(SuggestionKind.EnemyWeakness, "敌人弱点", $"「{bossInFight}」弱点：{weak}，建议切换对应元素角色", 0.95));
        }
        if (skillReady.Any(x => x) && list.Count < maxCount)
            list.Add(new CoachSuggestion(SuggestionKind.SkillTiming, "技能时机", $"当前 {currentCharacter} 技能已就绪，建议按视频攻略节点释放", 0.7));
        foreach (var kw in new[] { "蒸发", "融化", "感电", "超载", "冻结", "超导", "激化", "绽放" })
            if (ocrText.Contains(kw, StringComparison.Ordinal))
            {
                list.Add(new CoachSuggestion(SuggestionKind.ElementReaction, "元素反应", $"画面检测到「{kw}」，建议继续触发该元素反应提升输出", 0.6 + list.Count * 0.05));
                break;
            }
        if (ocrText.Contains("传送点", StringComparison.OrdinalIgnoreCase) || ocrText.Contains("岔路", StringComparison.OrdinalIgnoreCase))
            list.Add(new CoachSuggestion(SuggestionKind.Route, "路线优化", "当前路径与视频攻略存在分叉，建议按地图标记校准方向", 0.55));
        return list.OrderByDescending(s => s.Relevance).Take(maxCount).ToList();
    }
}

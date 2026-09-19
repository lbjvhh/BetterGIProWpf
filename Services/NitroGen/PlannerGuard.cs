using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterGIProWpf.Services.NitroGen;

public sealed class PlannerGuard
{
    public enum Phase { Navigate, Battle, Collect, Dialogue, Idle }

    public sealed record TaskStep(string Action, double DurationSec, string? Param = null);

    public static List<TaskStep> Bind(string intent, Phase phase)
    {
        var steps = new List<TaskStep>();
        var norm = intent?.ToLowerInvariant() ?? "";
        if (norm.Contains("打") || norm.Contains("杀") || norm.Contains("boss") || norm.Contains("战斗"))
        {
            steps.Add(new TaskStep("battle", 3.0));
            if (norm.Contains("元素战技") || norm.Contains("e")) steps.Add(new TaskStep("skill", 1.0));
            if (norm.Contains("爆发") || norm.Contains("q")) steps.Add(new TaskStep("burst", 1.0));
        }
        else if (norm.Contains("采集") || norm.Contains("挖") || norm.Contains("矿"))
        {
            steps.Add(new TaskStep("walk", 2.0));
            steps.Add(new TaskStep("interact", 0.8));
        }
        else if (norm.Contains("传送") || norm.Contains("锚点"))
        {
            steps.Add(new TaskStep("openmap", 0.6));
            steps.Add(new TaskStep("teleport", 2.0));
        }
        else if (norm.Contains("对话") || norm.Contains("npc"))
        {
            steps.Add(new TaskStep("walk", 1.0));
            steps.Add(new TaskStep("interact", 0.6));
        }
        else { steps.Add(new TaskStep("walk", 1.5)); }
        if (phase is Phase.Dialogue or Phase.Idle && steps.Any(s => s.Action == "battle"))
            steps = steps.Where(s => s.Action != "battle").ToList();
        return steps;
    }

    public static bool ShouldCallVla(Phase phase) => phase is Phase.Battle;

    public static Phase Classify(bool warmRegion, bool ocrMenu, bool dialogueUi)
    {
        if (dialogueUi) return Phase.Dialogue;
        if (ocrMenu) return Phase.Idle;
        if (warmRegion) return Phase.Battle;
        return Phase.Navigate;
    }
}

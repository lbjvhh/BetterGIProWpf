using System.Text.Json;

namespace BetterGIProWpf.Services.Coach;

public enum SuggestionKind { ElementReaction, Stamina, Route, SkillTiming, EnemyWeakness }
public record CoachSuggestion(SuggestionKind Kind, string Title, string Advice, double Relevance);
public record FrameGap(double ColorDiff, double TextOverlap, double ActionableDiff);
public enum CoachFrequency { Off, Low, Normal, High }

public class StrategyCoach
{
    public class VideoContext
    {
        public required double[] Histogram { get; init; }
        public required string[] KeyTexts { get; init; }
        public required string SceneName { get; init; }
        public List<string> Actions { get; init; } = new();
        public Dictionary<string, string> Weaknesses { get; init; } = new();
    }

    private readonly Random _rng = new();
    public CoachFrequency Frequency { get; set; } = CoachFrequency.Normal;
    public double TriggerThreshold { get; set; } = 0.35;
    public bool VoiceEnabled { get; set; } = true;
    public Func<string, Task>? TtsSpeak { get; set; }
    public event Action<CoachSuggestion>? SuggestionIssued;

    public List<CoachSuggestion> Analyze(VideoContext video, double[] gameHistogram, string[] gameTexts,
        double staminaRatio, string currentCharacter, string? bossInFight, bool[] skillReady)
    {
        if (Frequency == CoachFrequency.Off) return new();
        var gap = Compare(video.Histogram, gameHistogram, video.KeyTexts, gameTexts);
        if (gap.ActionableDiff < TriggerThreshold) return new();

        var results = new List<CoachSuggestion>();
        var allow = Frequency switch { CoachFrequency.Low => 1, CoachFrequency.Normal => 2, CoachFrequency.High => 4, _ => 0 };

        if (video.Actions.Count > 0 && gap.TextOverlap < 0.5 && skillReady.Any(x => x))
            results.Add(new CoachSuggestion(SuggestionKind.SkillTiming, "技能时机", $"视频中此处使用了「{video.Actions[0]}」，建议你也使用", 0.9));
        if (bossInFight != null && video.Weaknesses.TryGetValue(bossInFight, out var weak))
            results.Add(new CoachSuggestion(SuggestionKind.EnemyWeakness, "敌人弱点", $"视频中「{bossInFight}」弱点是 {weak}，建议切换对应元素角色", 0.95));
        if (staminaRatio < 0.25)
            results.Add(new CoachSuggestion(SuggestionKind.Stamina, "体力管理", "当前体力不足，建议先传送到七天神像回复", 0.8));
        if (gap.ColorDiff > 0.55)
            results.Add(new CoachSuggestion(SuggestionKind.Route, "路线优化", $"与视频参考画面偏差较大，建议确认行进方向（参考「{video.SceneName}」）", 0.6));
        if (currentCharacter.Length > 0 && video.Actions.Any(a => a.Contains("元素", StringComparison.Ordinal)))
            results.Add(new CoachSuggestion(SuggestionKind.ElementReaction, "元素反应", "视频中利用元素反应提升了输出，建议优先触发对应元素附着", 0.5));

        var picked = results.OrderByDescending(r => r.Relevance).Take(allow).ToList();
        foreach (var s in picked) { SuggestionIssued?.Invoke(s); if (VoiceEnabled && TtsSpeak != null) _ = TtsSpeak(s.Advice); }
        return picked;
    }

    public async Task<List<CoachSuggestion>> AnalyzeWithAiAsync(VideoContext video, double[] gameHistogram, string[] gameTexts,
        double staminaRatio, string currentCharacter, string? bossInFight, bool[] skillReady)
    {
        var local = Analyze(video, gameHistogram, gameTexts, staminaRatio, currentCharacter, bossInFight, skillReady);
        if (!AppConfig.Ai.UseExternal || string.IsNullOrWhiteSpace(AppConfig.Ai.ApiKey)) return local;
        try
        {
            var sys = "你是原神游戏教练。基于当前游戏状态和视频参考，用一句不超过 40 字的中文给出最关键的操作建议。只输出建议本身。";
            var user = $"场景：{video.SceneName}；当前角色：{currentCharacter}；体力：{staminaRatio:P0}；Boss：{bossInFight ?? "无"}；视频此处动作：{string.Join("、", video.Actions)}；本地规则建议：{string.Join("；", local.Select(s => s.Advice))}";
            var advice = await AppState.Ai.ChatAsync(sys, user);
            if (!string.IsNullOrWhiteSpace(advice))
                local.Insert(0, new CoachSuggestion(SuggestionKind.SkillTiming, "AI 教练", advice.Trim(), 0.99));
        }
        catch { }
        return local;
    }

    public static FrameGap Compare(double[] a, double[] b, string[] videoTexts, string[] gameTexts)
    {
        var color = 1.0 - Cosine(a, b);
        var overlap = videoTexts.Length == 0 ? 1.0 : videoTexts.Count(vt => gameTexts.Any(gt => gt.Contains(vt, StringComparison.OrdinalIgnoreCase))) / (double)videoTexts.Length;
        var actionable = Math.Min(1.0, color * 0.7 + (1 - overlap) * 0.3);
        return new FrameGap(color, overlap, actionable);
    }

    private static double Cosine(double[] a, double[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    public static double[] HistogramFromRgb(byte[] rgb, int width, int height)
    {
        var hist = new double[16];
        for (var i = 0; i + 2 < rgb.Length && i / 3 < width * height; i += 3)
        {
            var r = rgb[i] >> 5; var g = rgb[i+1] >> 5; var b = rgb[i+2] >> 5;
            hist[(r << 2) | (g << 1) | b]++;
        }
        var total = Math.Max(1, rgb.Length / 3d);
        for (var i = 0; i < hist.Length; i++) hist[i] /= total;
        return hist;
    }
}

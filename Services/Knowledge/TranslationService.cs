using System.Text.Json;

namespace BetterGIProWpf.Services.Knowledge;

public class TranslationService
{
    public Dictionary<string, string> OfficialTerms { get; } = new()
    {
        ["Mondstadt"] = "蒙德", ["Liyue"] = "璃月", ["Inazuma"] = "稻妻",
        ["Windrise"] = "风起地", ["Statue of The Seven"] = "七天神像",
        ["Venti"] = "温迪", ["Klee"] = "可莉",
        ["Emblem of Severed Fate"] = "绝缘之旗印", ["Thundering Fury"] = "如雷的盛怒",
    };

    public interface ITranslateBackend { Task<string> TranslateAsync(string text, string from, string to); }
    public ITranslateBackend? Backend { get; set; }
    public string[] SupportedLanguages { get; } = { "zh-CN", "en-US", "ja-JP", "ko-KR", "ru-RU" };

    public string LocalTranslate(string text, string to = "zh-CN")
    {
        var result = text;
        foreach (var (k, v) in OfficialTerms) result = result.Replace(k, v, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public async Task<string> TranslateAsync(string text, string from, string to)
    {
        try
        {
            if (Backend != null) { var t = await Backend.TranslateAsync(text, from, to); return ApplyOfficialTerms(t); }
        }
        catch { }
        return LocalTranslate(text, to);
    }

    public string ApplyOfficialTerms(string text)
    {
        var result = text;
        foreach (var (k, v) in OfficialTerms) result = result.Replace(k, v, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public static string[] ExtractTerms(string mixedOcr)
    {
        return mixedOcr.Split(new[] { ' ', '，', '。', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 2).Take(50).ToArray();
    }
}

public sealed class OpenAiTranslateBackend : TranslationService.ITranslateBackend
{
    private readonly AiService _ai;
    public OpenAiTranslateBackend(AiService ai) { _ai = ai; }

    public async Task<string> TranslateAsync(string text, string from, string to)
    {
        var sys = $"你是游戏本地化翻译。将用户文本从 {from} 翻译为 {to}，只输出译文，不要解释。游戏专有名词用官方译名（Mondstadt=蒙德、Liyue=璃月、Inazuma=稻妻）。";
        var r = await _ai.ChatAsync(sys, text);
        return r.Trim().Trim('"').Trim();
    }
}

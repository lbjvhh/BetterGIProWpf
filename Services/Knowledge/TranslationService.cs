using System.Text.Json;

namespace BetterGIProWpf.Services.Knowledge;

public class TranslationService
{
    public Dictionary<string, string> OfficialTerms { get; } = new()
    {
        ["Mondstadt"] = "蒙德", ["蒙德"] = "蒙德",
        ["Liyue"] = "璃月", ["璃月"] = "璃月",
        ["Inazuma"] = "稻妻", ["稻妻"] = "稻妻",
        ["Windrise"] = "风起地", ["风起地"] = "风起地",
        ["Statue of The Seven"] = "七天神像", ["七天神像"] = "七天神像",
        ["Venti"] = "温迪", ["温迪"] = "温迪",
        ["Klee"] = "可莉", ["可莉"] = "可莉",
        ["Emblem of Severed Fate"] = "绝缘之旗印", ["绝缘之旗印"] = "绝缘之旗印",
        ["Thundering Fury"] = "如雷的盛怒", ["如雷的盛怒"] = "如雷的盛怒",
    };

    public interface ITranslateBackend { Task<string> TranslateAsync(string text, string from, string to); }

    public ITranslateBackend? Backend { get; set; }
    public string[] SupportedLanguages { get; } = { "zh-CN", "en-US", "ja-JP", "ko-KR", "ru-RU" };

    public string LocalTranslate(string text, string to = "zh-CN")
    {
        var result = text;
        foreach (var (k, v) in OfficialTerms)
            result = result.Replace(k, v, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public async Task<string> TranslateAsync(string text, string from, string to)
    {
        try
        {
            if (Backend != null)
            {
                var t = await Backend.TranslateAsync(text, from, to);
                return ApplyOfficialTerms(t);
            }
        }
        catch { }
        return LocalTranslate(text, to);
    }

    public string ApplyOfficialTerms(string text)
    {
        var result = text;
        foreach (var (k, v) in OfficialTerms)
            result = result.Replace(k, v, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public static string[] ExtractTerms(string mixedOcr)
    {
        return mixedOcr.Split(new[] { ' ', '，', '。', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2).Take(50).ToArray();
    }
}

/// <summary>模块14: MyMemory 免费翻译后端（无需 API key，每天 5000 字符）。</summary>
public sealed class MyMemoryTranslateBackend : TranslationService.ITranslateBackend
{
    public async Task<string> TranslateAsync(string text, string from, string to)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var src = from.Replace("-", "").ToLower();
        var dst = to.Replace("-", "").ToLower();
        var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={src}|{dst}";
        var resp = await http.GetStringAsync(url);
        using var doc = System.Text.Json.JsonDocument.Parse(resp);
        return doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString() ?? text;
    }
}

/// <summary>OpenAI 兼容翻译后端。</summary>
public sealed class OpenAiTranslateBackend : TranslationService.ITranslateBackend
{
    private readonly AiService _ai;
    public OpenAiTranslateBackend(AiService ai) { _ai = ai; }

    public async Task<string> TranslateAsync(string text, string from, string to)
    {
        var sys = $"你是游戏本地化翻译。将用户文本从 {from} 翻译为 {to}，只输出译文本身，不要解释、不要加引号。" +
                  "游戏专有名词保留官方译名（如 Mondstadt=蒙德、Liyue=璃月、Inazuma=稻妻）。";
        var r = await _ai.ChatAsync(sys, text);
        return r.Trim().Trim('"').Trim();
    }
}

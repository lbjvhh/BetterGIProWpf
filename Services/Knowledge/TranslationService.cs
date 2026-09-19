namespace BetterGIProWpf.Services.Knowledge;
public class TranslationService
{
    public Dictionary<string, string> OfficialTerms { get; } = new() { ["Mondstadt"]="蒙德",["Liyue"]="璃月",["Inazuma"]="稻妻",["Windrise"]="风起地",["Statue of The Seven"]="七天神像",["Venti"]="温迪",["Klee"]="可莉",["Emblem of Severed Fate"]="绝缘之旗印",["Thundering Fury"]="如雷的盛怒" };
    public interface ITranslateBackend { Task<string> TranslateAsync(string text, string from, string to); }
    public ITranslateBackend? Backend { get; set; }
    public string[] SupportedLanguages { get; } = { "zh-CN", "en-US", "ja-JP", "ko-KR", "ru-RU" };
    public string LocalTranslate(string text, string to = "zh-CN") { var r = text; foreach (var (k, v) in OfficialTerms) r = r.Replace(k, v, StringComparison.OrdinalIgnoreCase); return r; }
    public async Task<string> TranslateAsync(string text, string from, string to) { try { if (Backend != null) return ApplyOfficialTerms(await Backend.TranslateAsync(text, from, to)); } catch { } return LocalTranslate(text, to); }
    public string ApplyOfficialTerms(string text) { var r = text; foreach (var (k, v) in OfficialTerms) r = r.Replace(k, v, StringComparison.OrdinalIgnoreCase); return r; }
}

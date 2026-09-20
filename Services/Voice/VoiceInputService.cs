using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.Voice;

public class VoiceInputService
{
    private readonly string _whisperUrl;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public VoiceInputService(string? whisperUrl = null) { _whisperUrl = whisperUrl ?? "http://127.0.0.1:5004/whisper"; }

    public async Task<string> TranscribeAsync(string wavPath)
    {
        try
        {
            if (!File.Exists(wavPath)) return "";
            var bytes = await File.ReadAllBytesAsync(wavPath);
            var b64 = Convert.ToBase64String(bytes);
            var payload = $"{{\"audio\":\"{b64}\"}}";
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(_whisperUrl, content);
            if (!resp.IsSuccessStatusCode) return "";
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
        }
        catch (Exception ex) { AppLogger.Error("[Voice] transcribe failed", ex); return ""; }
    }

    public static string MapCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var lower = text.ToLowerInvariant();
        if (lower.Contains("攻击") || lower.Contains("打")) return "attack";
        if (lower.Contains("技能") || lower.Contains("e")) return "skill_e";
        if (lower.Contains("大招") || lower.Contains("q") || lower.Contains("爆发")) return "burst_q";
        if (lower.Contains("跑") || lower.Contains("前进")) return "run";
        if (lower.Contains("停") || lower.Contains("停止")) return "stop";
        if (lower.Contains("跟随")) return "follow";
        return "unknown";
    }
}

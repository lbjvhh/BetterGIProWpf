using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.QA;

public class GameStateQaService
{
    private readonly string _vlmUrl;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly List<string> _contextHistory = new();

    public GameStateQaService(string? vlmUrl = null)
    {
        _vlmUrl = vlmUrl ?? "http://127.0.0.1:5004/vlm_qa";
    }

    public async Task<string> AskAsync(string question, string? frameB64 = null)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { question, image = frameB64, context = string.Join("\n", _contextHistory) });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(_vlmUrl, content);
            if (!resp.IsSuccessStatusCode) return FallbackAnswer(question);
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var answer = doc.RootElement.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "";
            _contextHistory.Add($"Q: {question}\nA: {answer}");
            if (_contextHistory.Count > 10) _contextHistory.RemoveAt(0);
            return answer;
        }
        catch { return FallbackAnswer(question); }
    }

    private string FallbackAnswer(string question)
    {
        var lower = question.ToLowerInvariant();
        if (lower.Contains("在哪") || lower.Contains("位置")) return "[离线] 无法识别当前位置";
        if (lower.Contains("宝箱")) return "[离线] 无法识别宝箱状态";
        if (lower.Contains("boss") || lower.Contains("怪")) return "[离线] 无法识别敌人状态";
        return $"[离线] 收到: {question}";
    }

    public void ClearHistory() => _contextHistory.Clear();
}

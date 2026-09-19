using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace BetterGIProWpf.Services;

public class AiConfig
{
    public string Provider { get; set; } = "openai";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o";
    public bool UseExternal { get; set; }
}

public class AiService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly AiConfig _cfg;

    public AiService(AiConfig cfg) => _cfg = cfg;

    private static bool IsVisionModel(string model)
    {
        var m = model.ToLowerInvariant();
        return m.Contains("gpt-4o") || m.Contains("gpt-4-vision") || m.Contains("vl") ||
               m.Contains("qwen-vl") || m.Contains("gemini") || m.Contains("claude");
    }

    public async Task<string> ChatAsync(string system, string user)
    {
        return await PostAsync(new[] {
            new { role = "system", content = system },
            new { role = "user", content = user },
        });
    }

    public async Task<string> ChatWithImagesAsync(string system, string user, IEnumerable<string> imagePaths)
    {
        var list = imagePaths?.ToList() ?? new List<string>();
        if (!IsVisionModel(_cfg.Model) || list.Count == 0)
            return await ChatAsync(system, user);
        var content = new List<object> { new { type = "text", text = user } };
        foreach (var path in list)
        {
            if (!File.Exists(path)) continue;
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            var mime = ext == "png" ? "image/png" : "image/jpeg";
            var b64 = Convert.ToBase64String(File.ReadAllBytes(path));
            content.Add(new { type = "image_url", image_url = new { url = $"data:{mime};base64,{b64}", detail = "low" } });
        }
        return await PostAsync(new object[] {
            new { role = "system", content = system },
            new { role = "user", content },
        });
    }

    private async Task<string> PostAsync(object messages)
    {
        if (string.IsNullOrWhiteSpace(_cfg.ApiKey))
            throw new InvalidOperationException("未配置 API Key");
        using var req = new HttpRequestMessage(HttpMethod.Post, _cfg.BaseUrl.TrimEnd('/') + "/chat/completions");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _cfg.ApiKey);
        var payload = new { model = _cfg.Model, messages, temperature = 0.3 };
        req.Content = JsonContent.Create(payload);
        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"AI 接口 {resp.StatusCode}: {(body.Length > 500 ? body[..500] : body)}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}

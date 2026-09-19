namespace BetterGIProWpf.Services.Adapters;

/// <summary>模块5: 跨游戏自动化适配框架。</summary>
public interface IGameAdapter
{
    string GameName { get; }
    string Version { get; }
    Task<string> FindWindowAsync();
    Task<byte[]> CaptureFrameAsync();
    Task InjectKeysAsync(string[] keys, int delayMs = 30);
    Task<string> RecognizeUiAsync(byte[] frame);
}

/// <summary>原神适配器。</summary>
public class GenshinAdapter : IGameAdapter
{
    public string GameName => "原神";
    public string Version => "5.0+";

    public async Task<string> FindWindowAsync()
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        return await http.GetStringAsync("http://127.0.0.1:5005/windows");
    }

    public async Task<byte[]> CaptureFrameAsync()
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var resp = await http.PostAsync("http://127.0.0.1:5005/grab",
            new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var b64 = doc.RootElement.GetProperty("frame_b64").GetString();
        return Convert.FromBase64String(b64!);
    }

    public async Task InjectKeysAsync(string[] keys, int delayMs = 30)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var body = System.Text.Json.JsonSerializer.Serialize(new { keys, delay_ms = delayMs });
        await http.PostAsync("http://127.0.0.1:5005/inject",
            new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
    }

    public async Task<string> RecognizeUiAsync(byte[] frame)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var b64 = Convert.ToBase64String(frame);
        var body = System.Text.Json.JsonSerializer.Serialize(new { image = b64 });
        var resp = await http.PostAsync("http://127.0.0.1:5004/ocr",
            new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("text").GetString() ?? "";
    }
}

/// <summary>鸣潮适配器（骨架）。</summary>
public class WutheringWavesAdapter : IGameAdapter
{
    public string GameName => "鸣潮";
    public string Version => "1.0+";

    public Task<string> FindWindowAsync() => Task.FromResult("鸣潮窗口（待实现）");
    public Task<byte[]> CaptureFrameAsync() => Task.FromResult(Array.Empty<byte>());
    public Task InjectKeysAsync(string[] keys, int delayMs = 30) => Task.CompletedTask;
    public Task<string> RecognizeUiAsync(byte[] frame) => Task.FromResult("鸣潮 UI 识别（待实现）");
}

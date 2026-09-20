using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.Sidekick;

public enum SidekickMode { Follow, Auto, Idle }

public class SidekickAgent
{
    private readonly string _llmUrl;
    private SidekickMode _mode = SidekickMode.Follow;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public event Action<string>? OnSay;
    public event Action? OnTakeOver;

    public SidekickMode Mode => _mode;
    public bool IsActive { get; private set; }

    public SidekickAgent(string? llmUrl = null) { _llmUrl = llmUrl ?? "http://127.0.0.1:5004/sidekick"; }

    public void Start() { IsActive = true; OnSay?.Invoke("AI 队友已启动，跟随模式"); }
    public void Stop() { IsActive = false; OnSay?.Invoke("AI 队友已停止"); }
    public void SwitchMode(SidekickMode mode) { _mode = mode; OnSay?.Invoke(mode.ToString()); }
    public void TakeOver() { _mode = SidekickMode.Follow; OnTakeOver?.Invoke(); OnSay?.Invoke("收到接管，切回跟随"); }

    public async Task<string> HandleCommandAsync(string command)
    {
        if (!IsActive) return "队友未启动";
        try
        {
            var payload = JsonSerializer.Serialize(new { command, mode = _mode.ToString() });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(_llmUrl, content);
            if (!resp.IsSuccessStatusCode) return $"[离线] 收到: {command}";
            var result = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(result);
            var reply = doc.RootElement.TryGetProperty("reply", out var r) ? r.GetString() ?? "" : "";
            OnSay?.Invoke(reply); return reply;
        }
        catch
        {
            var reply = command.Contains("打") ? "好的，我来攻击目标" : command.Contains("停") ? "已停止" : $"[离线] 收到: {command}";
            OnSay?.Invoke(reply); return reply;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BetterGIProWpf.Services.Streaming;

/// <summary>
/// 串流桥接（stream_bridge.py :5005）：窗口捕获 + 输入注入 + 安全停机。
/// </summary>
public sealed class StreamBridgeService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly string _url;

    public sealed record WinInfo(int Hwnd, string Title, int Pid);
    public sealed record GrabFrame(byte[] Jpeg, int Width, int Height, double Ms);

    public StreamBridgeService(string? url = null)
    {
        _url = url ?? "http://127.0.0.1:5005";
    }

    public bool Ready
    {
        get
        {
            try { return _http.GetAsync(_url + "/windows").GetAwaiter().GetResult().IsSuccessStatusCode; }
            catch { return false; }
        }
    }

    public List<WinInfo> ListWindows()
    {
        var list = new List<WinInfo>();
        try
        {
            var node = JsonNode.Parse(_http.GetAsync(_url + "/windows").GetAwaiter().GetResult()
                .Content.ReadAsStringAsync().GetAwaiter().GetResult());
            foreach (var w in node?["windows"]?.AsArray() ?? default!)
                list.Add(new WinInfo(w["hwnd"]!.GetValue<int>(),
                                     w["title"]!.GetValue<string>(),
                                     w["pid"]!.GetValue<int>()));
        }
        catch { }
        return list;
    }

    public GrabFrame? Grab(int hwnd)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { hwnd });
            var resp = _http.PostAsync(_url + "/grab",
                new StringContent(payload, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return null;
            var node = JsonNode.Parse(resp.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return new GrabFrame(
                Convert.FromBase64String(node!["frame_b64"]!.GetValue<string>()),
                node["width"]!.GetValue<int>(), node["height"]!.GetValue<int>(),
                node["ms"]!.GetValue<double>());
        }
        catch { return null; }
    }

    public bool InjectKeys(string[] keys, int delayMs = 30)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { keys, delay_ms = delayMs });
            var resp = _http.PostAsync(_url + "/inject",
                new StringContent(payload, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return false;
            var node = JsonNode.Parse(resp.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return node?["ok"]?.GetValue<bool>() ?? false;
        }
        catch { return false; }
    }

    public (bool Emergency, string Reason) SafetyStatus()
    {
        try
        {
            var node = JsonNode.Parse(_http.GetAsync(_url + "/safety_status").GetAwaiter().GetResult()
                .Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return (node?["emergency"]?.GetValue<bool>() ?? false,
                    node?["reason"]?.GetValue<string>() ?? "");
        }
        catch { return (false, ""); }
    }

    public void SafetyReset()
    {
        try { _http.PostAsync(_url + "/safety_reset", new StringContent("{}", Encoding.UTF8, "application/json")).Wait(); }
        catch { }
    }
}

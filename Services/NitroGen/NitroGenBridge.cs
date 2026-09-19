using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace BetterGIProWpf.Services.NitroGen;

public sealed class NitroGenBridge
{
    public sealed record ServiceInfo(bool CheckpointAvailable, string CheckpointPath, bool ServiceReachable, string? ServiceUrl);

    public string ServiceUrl { get; }
    public string CheckpointPath { get; }

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly LocalVisionEngine _local = new();
    private bool? _reachable;

    public NitroGenBridge(string? checkpointPath = null, string? serviceUrl = null)
    {
        CheckpointPath = checkpointPath ?? Path.Combine(AppContext.BaseDirectory, "Models", "ng.pt");
        ServiceUrl = serviceUrl ?? "http://127.0.0.1:5003";
    }

    public bool CheckpointAvailable => File.Exists(CheckpointPath);

    public bool ServiceReachable
    {
        get
        {
            if (_reachable is not null) return _reachable.Value;
            try { using var resp = _http.GetAsync(ServiceUrl + "/health").GetAwaiter().GetResult(); _reachable = true; }
            catch { try { using var r2 = _http.GetAsync(ServiceUrl).GetAwaiter().GetResult(); _reachable = true; } catch { _reachable = false; } }
            return _reachable.Value;
        }
    }

    public ServiceInfo Info() => new(CheckpointAvailable, CheckpointPath, ServiceReachable, ServiceUrl);

    public sealed record InferResult(ActionBlock Actions, float Confidence, string Mode, string Detail);

    public InferResult Infer(RgbFrame[] frames)
    {
        if (frames.Length < 2) return new InferResult(new ActionBlock(), 0f, "insufficient", "输入帧不足");
        if (CheckpointAvailable && ServiceReachable)
        {
            try
            {
                var act = CallService(frames[^1]);
                return new InferResult(act, 0.6f, "external-nitrogen", "外部 NitroGen (serve.py) · 21x16 动作块已解析");
            }
            catch (Exception ex) { return new InferResult(new ActionBlock(), 0f, "external-error", ex.Message); }
        }
        var local = _local.Infer(frames);
        string mode = CheckpointAvailable ? "local-fallback(服务未启动)" : "local-fallback(无权重)";
        return new InferResult(local.Actions, local.Confidence, mode, $"本地视觉-动作引擎兜底 · 模式 {local.Mode}");
    }

    private ActionBlock CallService(RgbFrame frame)
    {
        var payload = new JsonObject
        {
            ["image"] = NitroGenBridge.ToBase64(frame, 256, 256),
            ["width"] = 256, ["height"] = 256,
        };
        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = _http.PostAsync(ServiceUrl + "/predict", content).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var node = JsonNode.Parse(body);
        var arr = node?["action"]?.AsArray() ?? node?.AsArray();
        var block = new ActionBlock();
        if (arr is not null)
        {
            int n = Math.Min(arr.Count, block.Data.Length);
            for (int i = 0; i < n; i++)
                block.Data[i] = arr[i]?.GetValue<float?>() ?? 0f;
        }
        return block;
    }

    public static string ToBase64(RgbFrame f, int tw, int th)
    {
        using var bmp = new System.Drawing.Bitmap(tw, th, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        for (int y = 0; y < th; y++)
        {
            int sy = (int)Math.Floor((y / (double)th) * f.Height);
            for (int x = 0; x < tw; x++)
            {
                int sx = (int)Math.Floor((x / (double)tw) * f.Width);
                int i = (sy * f.Width + sx) * 4;
                bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(f.Data[i], f.Data[i + 1], f.Data[i + 2]));
            }
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    public void ResetLocal() => _local.Reset();
}

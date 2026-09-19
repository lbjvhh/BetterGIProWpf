using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BetterGIProWpf.Services.NitroGen;

namespace BetterGIProWpf.Services.Vision;

public sealed class VisionBridge
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _url;

    public sealed record DetBox(float X1, float Y1, float X2, float Y2, float Score, int ClassId);

    public VisionBridge(string? url = null) { _url = url ?? "http://127.0.0.1:5004"; }

    public bool Ready
    {
        get
        {
            try { using var r = _http.GetAsync(_url + "/health").GetAwaiter().GetResult(); return r.IsSuccessStatusCode; }
            catch { return false; }
        }
    }

    public List<DetBox> Detect(RgbFrame frame)
    {
        var boxes = new List<DetBox>();
        if (!Ready) return boxes;
        try
        {
            var b64 = ToBase64(frame);
            var payload = JsonSerializer.Serialize(new { image = b64 });
            using var resp = _http.PostAsync(_url + "/yolo",
                new StringContent(payload, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return boxes;
            var node = JsonNode.Parse(resp.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var arr = node?["boxes"]?.AsArray();
            var scores = node?["scores"]?.AsArray();
            var cls = node?["classes"]?.AsArray();
            if (arr is null) return boxes;
            for (int i = 0; i < arr.Count; i++)
            {
                var b = arr[i]?.AsArray();
                if (b is null || b.Count < 4) continue;
                boxes.Add(new DetBox(
                    b[0].GetValue<float>(), b[1].GetValue<float>(),
                    b[2].GetValue<float>(), b[3].GetValue<float>(),
                    scores?[i]?.GetValue<float>() ?? 0f,
                    cls?[i]?.GetValue<int>() ?? 0));
            }
        }
        catch { }
        return boxes;
    }

    public string Ocr(RgbFrame frame)
    {
        if (!Ready) return "";
        try
        {
            var b64 = ToBase64(frame);
            var payload = JsonSerializer.Serialize(new { image = b64 });
            using var resp = _http.PostAsync(_url + "/ocr",
                new StringContent(payload, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return "";
            var node = JsonNode.Parse(resp.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return node?["text"]?.GetValue<string>() ?? "";
        }
        catch { return ""; }
    }

    public string Transcribe(byte[] wav)
    {
        if (!Ready) return "";
        try
        {
            var payload = JsonSerializer.Serialize(new { audio = Convert.ToBase64String(wav) });
            using var resp = _http.PostAsync(_url + "/whisper",
                new StringContent(payload, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return "";
            var node = JsonNode.Parse(resp.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return node?["text"]?.GetValue<string>() ?? "";
        }
        catch { return ""; }
    }

    private static string ToBase64(RgbFrame f)
    {
        using var bmp = new System.Drawing.Bitmap(f.Width, f.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        for (int y = 0; y < f.Height; y++)
            for (int x = 0; x < f.Width; x++)
            {
                int i = (y * f.Width + x) * 4;
                bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(f.Data[i], f.Data[i + 1], f.Data[i + 2]));
            }
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }
}

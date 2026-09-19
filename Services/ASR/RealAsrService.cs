using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

namespace BetterGIProWpf.Services.ASR;

/// <summary>
/// 真实语音识别：录麦克风 → WAV → vision_server(5004) /whisper。
/// </summary>
public sealed class RealAsrService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private const string VisionUrl = "http://127.0.0.1:5004";

    public byte[] RecordWav(int seconds = 3)
    {
        using var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1)
        };
        using var ms = new MemoryStream();
        var tcs = new TaskCompletionSource();
        waveIn.DataAvailable += (s, e) => ms.Write(e.Buffer, 0, e.BytesRecorded);
        waveIn.RecordingStopped += (s, e) => tcs.SetResult();
        waveIn.StartRecording();
        tcs.Task.Wait(TimeSpan.FromSeconds(seconds + 1));
        waveIn.StopRecording();
        using var outMs = new MemoryStream();
        using var writer = new WaveFileWriter(outMs, waveIn.WaveFormat);
        var bytes = ms.ToArray();
        writer.Write(bytes, 0, bytes.Length);
        return outMs.ToArray();
    }

    public async Task<string> RecognizeAsync(int seconds = 3)
    {
        var wav = RecordWav(seconds);
        var b64 = Convert.ToBase64String(wav);
        var body = JsonSerializer.Serialize(new { audio = b64 });
        var req = new HttpRequestMessage(HttpMethod.Post, $"{VisionUrl}/whisper")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return "";
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("text").GetString() ?? "";
    }
}

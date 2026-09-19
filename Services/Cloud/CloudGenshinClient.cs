namespace BetterGIProWpf.Services.Cloud;

public enum CloudSessionState { Disconnected, Connecting, Streaming, Reconnecting, Error }

public record CloudFrameStats(double RttMs, double BitrateMbps, int DroppedFrames, double CompressionArtifacts);

public class CloudGenshinClient
{
    public CloudSessionState State { get; private set; } = CloudSessionState.Disconnected;
    public double CurrentRttMs { get; private set; }
    public event Action<string>? Log;

    public async Task<(bool Ok, string Message)> ConnectAsync(string endpoint)
    {
        State = CloudSessionState.Connecting;
        Log?.Invoke($"连接云原神 {endpoint}…");
        await Task.Delay(300);
        State = CloudSessionState.Streaming;
        Log?.Invoke("云原神流已建立（骨架）");
        return (true, "云原神流已建立");
    }

    public async Task<(bool Ok, string Message)> ReconnectAsync()
    {
        State = CloudSessionState.Reconnecting;
        await Task.Delay(500);
        State = CloudSessionState.Streaming;
        Log?.Invoke("重连成功，从断点继续");
        return (true, "重连成功");
    }

    public byte[]? CaptureFrame(int w, int h)
    {
        if (State != CloudSessionState.Streaming) return null;
        var frame = new byte[w * h * 3];
        for (var i = 0; i < frame.Length; i += 3) { frame[i] = 30; frame[i + 1] = 30; frame[i + 2] = 40; }
        return frame;
    }

    public double AdjustConfidence(double rawConfidence, CloudFrameStats stats)
    {
        double adjusted = rawConfidence;
        if (stats.RttMs > 150) adjusted *= 0.85;
        else if (stats.RttMs > 80) adjusted *= 0.93;
        if (stats.CompressionArtifacts > 0.4) adjusted *= 0.8;
        adjusted *= Math.Max(0.85, 1.0 - stats.DroppedFrames * 0.01);
        return Math.Clamp(adjusted, 0, 1);
    }

    public void SendInput(string action)
    {
        if (State != CloudSessionState.Streaming) { Log?.Invoke("流未就绪"); return; }
        Log?.Invoke($"[云] {action}");
    }

    public void UpdateRtt(double ms) => CurrentRttMs = ms;
    public void Disconnect() { State = CloudSessionState.Disconnected; Log?.Invoke("已断开"); }
}

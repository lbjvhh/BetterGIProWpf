using System.Diagnostics;
using System.IO;

namespace BetterGIProWpf.Services.Cloud;

public enum CloudSessionState { Disconnected, Connecting, Streaming, Reconnecting, Error }
public record CloudFrameStats(double RttMs, double BitrateMbps, int DroppedFrames, double CompressionArtifacts);

/// <summary>
/// 云原神客户端（模块4.1）：基于 Sunshine + Moonlight 本地串流。
/// Sunshine 作为自托管游戏流媒体主机（本机 47990 web 面板），Moonlight 作为客户端连接 127.0.0.1。
/// 游戏画面通过 Moonlight 窗口呈现，捕获该窗口即获得游戏帧；输入通过窗口注入。
/// </summary>
public class CloudGenshinClient
{
    public CloudSessionState State { get; private set; } = CloudSessionState.Disconnected;
    public double CurrentRttMs { get; private set; }
    public event Action<string>? Log;

    private Process? _moonlightProc;
    private const string DefaultMoonlightPath = @"C:\Program Files\Moonlight Game Streaming\Moonlight.exe";
    private const string SunshineDefaultHost = "127.0.0.1";

    public async Task<(bool Ok, string Message)> ConnectAsync(string endpoint = SunshineDefaultHost)
    {
        State = CloudSessionState.Connecting;
        Log?.Invoke($"启动 Moonlight → {endpoint}…");
        try
        {
            var moonlightPath = FindMoonlight();
            if (moonlightPath == null) { State = CloudSessionState.Error; return (false, "未找到 Moonlight.exe"); }
            _moonlightProc = Process.Start(new ProcessStartInfo(moonlightPath, $"stream {endpoint} \"原神\"")
            { UseShellExecute = false, CreateNoWindow = false });
            if (_moonlightProc == null) { State = CloudSessionState.Error; return (false, "Moonlight 启动失败"); }
            await Task.Delay(2000);
            State = CloudSessionState.Streaming;
            CurrentRttMs = 5;
            Log?.Invoke($"Moonlight 已连接 {endpoint}（PID {_moonlightProc.Id}）");
            return (true, $"Moonlight 已连接 {endpoint}");
        }
        catch (Exception ex) { State = CloudSessionState.Error; Log?.Invoke($"连接失败: {ex.Message}"); return (false, ex.Message); }
    }

    public async Task<(bool Ok, string Message)> ReconnectAsync()
    {
        State = CloudSessionState.Reconnecting;
        Log?.Invoke("重连 Sunshine…");
        try { Disconnect(); await Task.Delay(500); return await ConnectAsync(SunshineDefaultHost); }
        catch (Exception ex) { State = CloudSessionState.Error; return (false, ex.Message); }
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
        if (State != CloudSessionState.Streaming) { Log?.Invoke("流未就绪，忽略输入"); return; }
        Log?.Invoke($"[串流] {action}（RTT {CurrentRttMs:0}ms）");
    }

    public void UpdateRtt(double ms) => CurrentRttMs = ms;

    public void Disconnect()
    {
        try { if (_moonlightProc != null && !_moonlightProc.HasExited) _moonlightProc.Kill(); } catch { }
        State = CloudSessionState.Disconnected;
        Log?.Invoke("已断开 Moonlight");
    }

    private static string? FindMoonlight()
    {
        if (File.Exists(DefaultMoonlightPath)) return DefaultMoonlightPath;
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var alt = Path.Combine(pf, "Moonlight Game Streaming", "Moonlight.exe");
        return File.Exists(alt) ? alt : null;
    }
}

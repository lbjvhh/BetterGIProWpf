using System;
using System.Diagnostics;
using System.IO;

namespace BetterGIProWpf.Services.Streaming;

public class MoonlightClient
{
    private readonly string _moonlightPath;
    private Process? _proc;

    public MoonlightClient(string? moonlightPath = null)
    {
        _moonlightPath = moonlightPath ?? @"C:\Program Files\Moonlight Game Streaming\Moonlight.exe";
    }

    public bool Connect(string host = "127.0.0.1", string appName = "Desktop")
    {
        try
        {
            if (!File.Exists(_moonlightPath)) return false;
            var psi = new ProcessStartInfo { FileName = _moonlightPath, Arguments = $"stream {host} \"{appName}\"", UseShellExecute = false, CreateNoWindow = false };
            _proc = Process.Start(psi);
            return true;
        }
        catch (Exception ex) { AppLogger.Error("[Moonlight] connect failed", ex); return false; }
    }

    public void Disconnect() { try { _proc?.Kill(); } catch { } }
    public bool IsConnected => _proc != null && !_proc.HasExited;

    public static bool IsSunshineRunning()
    {
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            var ar = tcp.BeginConnect("127.0.0.1", 47990, null, null);
            bool ok = ar.AsyncWaitHandle.WaitOne(1000);
            return ok && tcp.Connected;
        }
        catch { return false; }
    }
}

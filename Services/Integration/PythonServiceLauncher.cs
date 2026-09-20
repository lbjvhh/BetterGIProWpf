using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.Integration;

public class PythonServiceLauncher
{
    private readonly string _pythonDir;
    private readonly string _pythonExe;
    private Process? _visionProc;
    private Process? _streamProc;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public PythonServiceLauncher(string? pythonDir = null, string? pythonExe = null)
    {
        _pythonDir = pythonDir ?? @"C:\better\nitrogen_bridge";
        _pythonExe = pythonExe ?? "python";
    }

    public async Task StartAllAsync() { await StartVisionAsync(); await StartStreamAsync(); }

    public async Task<bool> StartVisionAsync()
    {
        if (await IsHealthyAsync(5004)) return true;
        try
        {
            var psi = new ProcessStartInfo { FileName = _pythonExe, Arguments = "vision_server.py", WorkingDirectory = _pythonDir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            _visionProc = Process.Start(psi);
            for (int i = 0; i < 30; i++) { await Task.Delay(2000); if (await IsHealthyAsync(5004)) return true; }
            return false;
        }
        catch (Exception ex) { AppLogger.Error("[Launcher] vision_server start failed", ex); return false; }
    }

    public async Task<bool> StartStreamAsync()
    {
        if (await IsHealthyAsync(5005)) return true;
        try
        {
            var psi = new ProcessStartInfo { FileName = _pythonExe, Arguments = "stream_bridge.py", WorkingDirectory = _pythonDir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            _streamProc = Process.Start(psi);
            for (int i = 0; i < 15; i++) { await Task.Delay(1000); if (await IsHealthyAsync(5005)) return true; }
            return false;
        }
        catch (Exception ex) { AppLogger.Error("[Launcher] stream_bridge start failed", ex); return false; }
    }

    public void StopAll() { try { _visionProc?.Kill(); } catch { } try { _streamProc?.Kill(); } catch { } }

    private static async Task<bool> IsHealthyAsync(int port)
    {
        try { var resp = await _http.GetAsync($"http://127.0.0.1:{port}/health"); return resp.IsSuccessStatusCode; }
        catch { return false; }
    }
}

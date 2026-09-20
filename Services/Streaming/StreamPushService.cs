using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BetterGIProWpf.Services.Streaming;

public class StreamPushService
{
    private Process? _ffmpegProc;
    private readonly string _ffmpegPath;

    public StreamPushService(string? ffmpegPath = null) { _ffmpegPath = ffmpegPath ?? "ffmpeg"; }

    public bool Start(string rtmpUrl, string? windowTitle = null, int bitrateKbps = 2500)
    {
        try
        {
            if (_ffmpegProc != null && !_ffmpegProc.HasExited) return true;
            var args = new StringBuilder();
            args.Append("-y ");
            args.Append(string.IsNullOrEmpty(windowTitle) ? "-f gdigrab -i desktop " : $"-f gdigrab -draw_mouse 0 -i title=\"{windowTitle}\" ");
            args.Append($"-c:v libx264 -preset veryfast -b:v {bitrateKbps}k -maxrate {bitrateKbps}k -bufsize {bitrateKbps * 2}k -pix_fmt yuv420p -g 30 ");
            args.Append($"-f flv \"{rtmpUrl}\"\");
            var psi = new ProcessStartInfo { FileName = _ffmpegPath, Arguments = args.ToString(), UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            _ffmpegProc = Process.Start(psi);
            return true;
        }
        catch (Exception ex) { AppLogger.Error("[StreamPush] start failed", ex); return false; }
    }

    public void Stop() { try { if (_ffmpegProc != null && !_ffmpegProc.HasExited) _ffmpegProc.Kill(); } catch { } }
    public bool IsRunning => _ffmpegProc != null && !_ffmpegProc.HasExited;
}

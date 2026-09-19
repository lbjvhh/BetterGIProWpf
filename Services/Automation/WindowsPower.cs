using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BetterGIProWpf.Services.Automation;

/// <summary>Windows 电源管理：休眠/睡眠、定时唤醒（powercfg /waketimer）。</summary>
public static class WindowsPower
{
    [DllImport("powrprof.dll")]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    /// <summary>立即睡眠（不强制、不关闭唤醒事件）。</summary>
    public static void Sleep() => SetSuspendState(false, false, false);

    /// <summary>立即休眠。</summary>
    public static void Hibernate() => SetSuspendState(true, false, false);

    /// <summary>设置定时唤醒（系统 RTC 唤醒计时器）。返回错误信息，空字符串表示成功。</summary>
    public static string SetWakeTimer(DateTime wakeTime)
    {
        try
        {
            var ps = new ProcessStartInfo("powercfg", $"/waketimer {wakeTime:yyyy-MM-ddTHH:mm:ss}")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            using var p = Process.Start(ps)!;
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit(10000);
            return p.ExitCode == 0 ? "" : (string.IsNullOrWhiteSpace(err) ? $"powercfg 退出码 {p.ExitCode}" : err.Trim());
        }
        catch (Exception ex) { return ex.Message; }
    }

    /// <summary>清除定时唤醒。</summary>
    public static string ClearWakeTimer()
    {
        try
        {
            var ps = new ProcessStartInfo("powercfg", "/waketimer")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            using var p = Process.Start(ps)!;
            _ = p.StandardOutput.ReadToEnd();
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit(10000);
            return p.ExitCode == 0 ? "" : (string.IsNullOrWhiteSpace(err) ? "powercfg 退出码非 0" : err.Trim());
        }
        catch (Exception ex) { return ex.Message; }
    }
}

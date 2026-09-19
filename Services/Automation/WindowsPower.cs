using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BetterGIProWpf.Services.Automation;

public static class WindowsPower
{
    [DllImport("powrprof.dll")]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public static void Sleep() => SetSuspendState(false, false, false);
    public static void Hibernate() => SetSuspendState(true, false, false);

    public static string SetWakeTimer(DateTime wakeTime)
    {
        try
        {
            var ps = new ProcessStartInfo("powercfg", $"/waketimer {wakeTime:yyyy-MM-ddTHH:mm:ss}")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var p = Process.Start(ps)!;
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit(10000);
            return p.ExitCode == 0 ? "" : (string.IsNullOrWhiteSpace(err) ? $"powercfg 退出码 {p.ExitCode}" : err.Trim());
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string ClearWakeTimer()
    {
        try
        {
            var ps = new ProcessStartInfo("powercfg", "/waketimer")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var p = Process.Start(ps)!;
            _ = p.StandardOutput.ReadToEnd();
            p.WaitForExit(10000);
            return p.ExitCode == 0 ? "" : "powercfg 清除失败";
        }
        catch (Exception ex) { return ex.Message; }
    }
}

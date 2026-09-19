using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace BetterGIProWpf.Services.Game;

/// <summary>
/// 游戏自动启动（原神）。注册表定位路径。
/// </summary>
public sealed class GameLauncher
{
    private const string GenshinProc = "YuanShen";

    public bool IsRunning()
    {
        foreach (var p in Process.GetProcessesByName(GenshinProc))
            return !p.HasExited;
        return false;
    }

    public string? FindGamePath()
    {
        string[] keys = {
            @"HKEY_CURRENT_USER\Software\miHoYo\Genshin Impact",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\miHoYo\Genshin Impact",
        };
        foreach (var k in keys)
        {
            try
            {
                var v = Registry.GetValue(k, "GameInstallPath", null) as string;
                if (!string.IsNullOrEmpty(v))
                {
                    var exe = System.IO.Path.Combine(v, "GenshinImpact.exe");
                    if (System.IO.File.Exists(exe)) return exe;
                    exe = System.IO.Path.Combine(v, "YuanShen.exe");
                    if (System.IO.File.Exists(exe)) return exe;
                }
            }
            catch { }
        }
        return null;
    }

    public bool Launch()
    {
        if (IsRunning()) return true;
        var path = FindGamePath();
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }
}

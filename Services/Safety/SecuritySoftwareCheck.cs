using System.Diagnostics;

namespace BetterGIProWpf.Services.Safety;

public sealed record SecuritySoftwareInfo(string Name, string ProcessName, string Advice);

public static class SecuritySoftwareCheck
{
    private static readonly SecuritySoftwareInfo[] Known = new[] {
        new("360安全卫士", "360safe", "添加白名单"),
        new("火绒", "HipsTray", "添加信任"),
        new("腾讯电脑管家", "QQPCRTP", "加入白名单"),
    };

    public static IReadOnlyList<SecuritySoftwareInfo> Detect()
    {
        var found = new List<SecuritySoftwareInfo>();
        try { var running = Process.GetProcesses().Select(p => { try { return p.ProcessName; } catch { return null; } }).Where(n => n != null).ToHashSet(StringComparer.OrdinalIgnoreCase); foreach (var k in Known) if (running.Contains(k.ProcessName)) found.Add(k); }
        catch { }
        return found;
    }

    public static string BuildAdvice()
    {
        var found = Detect(); return found.Count == 0 ? "无冲突" : "检测到: " + string.Join(",", found.Select(f => f.Name));
    }
}

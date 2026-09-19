using System.Diagnostics;
namespace BetterGIProWpf.Services.Safety;
public sealed record SecuritySoftwareInfo(string Name, string ProcessName, string Advice);
public static class SecuritySoftwareCheck
{
    private static readonly SecuritySoftwareInfo[] Known = new[] {
        new SecuritySoftwareInfo("360 安全卫士","360safe","模拟点击可能被拦截：添加信任"),
        new SecuritySoftwareInfo("360 杀毒","360sd","模拟输入可能被拦截：添加白名单"),
        new SecuritySoftwareInfo("Windows Defender","MsMpEng","检查篡改防护/攻击面减少规则"),
        new SecuritySoftwareInfo("火绒安全","HipsTray","若按键被吞，添加信任"),
        new SecuritySoftwareInfo("腾讯电脑管家","QQPCRTP","加入白名单"),
    };
    public static IReadOnlyList<SecuritySoftwareInfo> Detect()
    {
        var found = new List<SecuritySoftwareInfo>();
        try { var running = Process.GetProcesses().Select(p => { try { return p.ProcessName; } catch { return null; } }).Where(n => n != null).ToHashSet(StringComparer.OrdinalIgnoreCase); foreach (var k in Known) if (running.Contains(k.ProcessName)) found.Add(k); }
        catch { }
        return found;
    }
    public static string BuildAdvice() { var f = Detect(); return f.Count == 0 ? "未检测到常见安全软件冲突" : "检测到：\n" + string.Join("\n", f.Select(x => $"· {x.Name}：{x.Advice}")); }
}

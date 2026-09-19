namespace BetterGIProWpf.Services.Integration;
public sealed record VersionCompatibilityEntry(string BetterGiVersion, string GameVersionRange, string Issue, string Advice, bool RequiresExtraCheck);
public static class VersionCompatibility
{
    public static readonly IReadOnlyList<VersionCompatibilityEntry> Matrix = new[] {
        new VersionCompatibilityEntry("0.42.0","5.4","游戏 5.4 更新地图 UI，0.42 前无法用地图追踪","升级 0.42+ 或用 UI 适配", false),
        new VersionCompatibilityEntry("0.44.4","*","WGC 截图器只能取一张图","启用截图自动切换 WGC→BitBlt→DXGI", true),
        new VersionCompatibilityEntry("0.44.0","*","0.44 修改了截图器与调度，不完全稳定","升级前备份，升级后截图自检", true),
        new VersionCompatibilityEntry("0.63.0","*","新增桌面分身与遮罩指标栏","后台/仪表盘需 0.63+", false),
        new VersionCompatibilityEntry("*","5.5","5.5 变更圣遗物分解交互，自动分解选项反转","更新分解脚本参数", true),
    };
    public static IEnumerable<VersionCompatibilityEntry> Match(string bv, string? gv = null)
    {
        foreach (var e in Matrix) { var bOk = e.BetterGiVersion == "*" || VersionAtLeast(bv, e.BetterGiVersion); if (!bOk) continue; if (gv != null && e.GameVersionRange != "*" && !gv.StartsWith(e.GameVersionRange, StringComparison.OrdinalIgnoreCase)) continue; yield return e; }
    }
    public static bool RequiresExtraStabilityCheck(string bv) { foreach (var e in Matrix) if (e.RequiresExtraCheck && VersionAtLeast(bv, e.BetterGiVersion)) return true; return false; }
    public static bool VersionAtLeast(string a, string b) { var pa = Parse(a); var pb = Parse(b); for (int i = 0; i < 3; i++) if (pa[i] != pb[i]) return pa[i] > pb[i]; return true; }
    private static int[] Parse(string v) { var parts = v.Trim().Split('.'); var r = new[] { 0, 0, 0 }; for (int i = 0; i < parts.Length && i < 3; i++) if (int.TryParse(parts[i], out var n)) r[i] = n; return r; }
}

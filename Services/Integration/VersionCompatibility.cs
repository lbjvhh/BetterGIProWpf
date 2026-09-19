using System.Text.Json.Nodes;

namespace BetterGIProWpf.Services.Integration;

public sealed record VersionCompatibilityEntry(
    string BetterGiVersion, string GameVersionRange, string Issue, string Advice, bool RequiresExtraCheck);

public static class VersionCompatibility
{
    public static readonly IReadOnlyList<VersionCompatibilityEntry> Matrix = new[] {
        new VersionCompatibilityEntry("0.42.0", "5.4", "5.4 地图 UI 变更", "升级 0.42.0+", false),
        new VersionCompatibilityEntry("0.44.4", "*", "WGC 截图仅一帧", "切换 BitBlt", true),
        new VersionCompatibilityEntry("0.44.0", "*", "0.44 不稳定", "备份", true),
        new VersionCompatibilityEntry("0.63.0", "*", "桌面分身/遮罩指标", "需 0.63+", false),
        new VersionCompatibilityEntry("*", "5.5", "5.5 圣遗物分解交互变更", "更新脚本", true),
    };

    public static IEnumerable<VersionCompatibilityEntry> Match(string betterGiVersion, string? gameVersion = null)
    {
        foreach (var e in Matrix)
        {
            var bOk = e.BetterGiVersion == "*" || VersionAtLeast(betterGiVersion, e.BetterGiVersion);
            if (!bOk) continue;
            if (gameVersion != null && e.GameVersionRange != "*" && !gameVersion.StartsWith(e.GameVersionRange, StringComparison.OrdinalIgnoreCase)) continue;
            yield return e;
        }
    }

    public static bool RequiresExtraStabilityCheck(string betterGiVersion)
    {
        foreach (var e in Matrix)
            if (e.RequiresExtraCheck && VersionAtLeast(betterGiVersion, e.BetterGiVersion)) return true;
        return false;
    }

    public static (bool Ok, string Message) ValidateManifestMinVersion(JsonNode? manifest, string currentBetterGiVersion)
    {
        var min = manifest?["min_bettergi_version"]?.GetValue<string>();
        if (string.IsNullOrEmpty(min)) return (true, "未声明最低版本");
        if (VersionAtLeast(currentBetterGiVersion, min)) return (true, $"当前 ≥ {min}");
        return (false, $"需 ≥ {min}，当前 {currentBetterGiVersion}");
    }

    public static bool VersionAtLeast(string a, string b)
    {
        var pa = Parse(a); var pb = Parse(b);
        for (int i = 0; i < 3; i++) if (pa[i] != pb[i]) return pa[i] > pb[i];
        return true;
    }

    private static int[] Parse(string v)
    {
        var parts = v.Trim().Split('.');
        var r = new[] { 0, 0, 0 };
        for (int i = 0; i < parts.Length && i < 3; i++) if (int.TryParse(parts[i], out var n)) r[i] = n;
        return r;
    }
}

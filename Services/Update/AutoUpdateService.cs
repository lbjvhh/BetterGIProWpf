using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.Update;

public class UpdateInfo
{
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
}

public class AutoUpdateService
{
    private readonly string _owner;
    private readonly string _repo;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public AutoUpdateService(string owner = "lbjvhh", string repo = "BetterGIProWpf") { _owner = owner; _repo = repo; }

    public static string CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    public async Task<UpdateInfo> CheckAsync()
    {
        var info = new UpdateInfo { CurrentVersion = CurrentVersion };
        try
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("BetterGIProWpf");
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            info.LatestVersion = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            info.ReleaseNotes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            if (root.TryGetProperty("assets", out var assets))
                foreach (var asset in assets.EnumerateArray())
                {
                    var dl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    if (dl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { info.DownloadUrl = dl; break; }
                }
            info.HasUpdate = CompareVersion(info.CurrentVersion, info.LatestVersion) < 0;
        }
        catch (Exception ex) { AppLogger.Error("[Update] check failed", ex); }
        return info;
    }

    public async Task<string?> DownloadAsync(UpdateInfo info)
    {
        if (string.IsNullOrEmpty(info.DownloadUrl)) return null;
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"BetterGIPro_update_{DateTime.Now:yyyyMMddHHmmss}.zip");
            var bytes = await _http.GetByteArrayAsync(info.DownloadUrl);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch (Exception ex) { AppLogger.Error("[Update] download failed", ex); return null; }
    }

    private static int CompareVersion(string current, string latest)
    {
        try { return new Version(current.TrimStart('v', 'V')).CompareTo(new Version(latest.TrimStart('v', 'V'))); }
        catch { return 0; }
    }
}

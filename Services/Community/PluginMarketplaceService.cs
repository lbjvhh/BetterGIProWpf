using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.Community;

public class PluginInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public long Size { get; set; }
    public DateTime PublishedAt { get; set; }
}

public class PluginMarketplaceService
{
    private readonly string _userDir;
    private readonly string _owner;
    private readonly string _repo;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public PluginMarketplaceService(string owner = "lbjvhh", string repo = "BetterGIProWpf", string? userDir = null)
    {
        _owner = owner; _repo = repo;
        _userDir = userDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterGIProWpf", "User");
    }

    public async Task<List<PluginInfo>> ListPluginsAsync()
    {
        var result = new List<PluginInfo>();
        try
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases";
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("BetterGIProWpf");
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                var name = rel.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var tag = rel.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
                var body = rel.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                var published = rel.TryGetProperty("published_at", out var p) && p.TryGetDateTime(out var dt) ? dt : DateTime.MinValue;
                if (rel.TryGetProperty("assets", out var assets))
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var dlUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                        if (dlUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            result.Add(new PluginInfo { Name = name, Version = tag, Description = body, DownloadUrl = dlUrl, Size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0, PublishedAt = published });
                    }
            }
        }
        catch (Exception ex) { AppLogger.Error("[Marketplace] list failed", ex); }
        return result;
    }

    public async Task<(bool ok, string message)> InstallAsync(PluginInfo plugin)
    {
        try
        {
            if (string.IsNullOrEmpty(plugin.DownloadUrl)) return (false, "无下载地址");
            var zipPath = Path.Combine(Path.GetTempPath(), $"plugin_{Guid.NewGuid():N}.zip");
            var bytes = await _http.GetByteArrayAsync(plugin.DownloadUrl);
            await File.WriteAllBytesAsync(zipPath, bytes);
            Directory.CreateDirectory(_userDir);
            ZipFile.ExtractToDirectory(zipPath, _userDir, overwriteFiles: true);
            File.Delete(zipPath);
            return (true, $"已安装 {plugin.Name} {plugin.Version}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public List<string> ListInstalled()
    {
        var result = new List<string>();
        try
        {
            if (!Directory.Exists(_userDir)) return result;
            foreach (var dir in Directory.GetDirectories(_userDir))
                if (File.Exists(Path.Combine(dir, "manifest.json"))) result.Add(Path.GetFileName(dir));
        }
        catch { }
        return result;
    }
}

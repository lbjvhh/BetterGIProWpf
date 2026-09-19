using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace BetterGIProWpf.Services.Community;

public record RepoScript
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Path { get; init; }
    public string DownloadUrl { get; init; } = "";
    public string? Description { get; init; }
    public long Size { get; init; }
}

/// <summary>
/// BetterGI 中央脚本仓库直连（模块19）。
/// 仓库 huiyadanli/bettergi-scripts-list，分类 repo/js、repo/pathing、repo/combat、repo/tcg。
/// </summary>
public class ScriptRepoClient
{
    private const string Owner = "huiyadanli";
    private const string Repo = "bettergi-scripts-list";
    private const string Branch = "main";
    private static readonly string[] Categories = { "js", "pathing", "combat", "tcg" };
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly string _cachePath;
    private readonly object _lock = new();
    private List<RepoScript> _cache = new();
    public DateTime? LastRefreshUtc { get; private set; }

    public ScriptRepoClient()
    {
        _cachePath = Path.Combine(AppContext.BaseDirectory, "repo_cache.json");
        TryLoadCache();
    }

    public async Task<(int Count, bool FromCache, string Message)> RefreshAsync()
    {
        var list = new List<RepoScript>();
        foreach (var cat in Categories)
        {
            try
            {
                var url = $"https://api.github.com/repos/{Owner}/{Repo}/contents/repo/{cat}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("BetterGIProWpf");
                using var resp = await Http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) continue;
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var path = el.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                    var size = el.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0;
                    if (string.IsNullOrEmpty(name)) continue;
                    list.Add(new RepoScript
                    {
                        Name = Path.GetFileNameWithoutExtension(name),
                        Category = cat, Path = path, Size = size,
                        DownloadUrl = $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/{path}"
                    });
                }
            }
            catch { }
        }
        if (list.Count > 0)
        {
            lock (_lock) { _cache = list; LastRefreshUtc = DateTime.UtcNow; }
            TrySaveCache();
            return (list.Count, false, $"已从仓库刷新 {list.Count} 个脚本");
        }
        lock (_lock)
            return (_cache.Count, true, _cache.Count > 0 ? $"使用缓存 {_cache.Count} 个" : "仓库不可达");
    }

    public List<RepoScript> Search(string? keyword, string? category = null)
    {
        lock (_lock)
        {
            IEnumerable<RepoScript> q = _cache;
            if (!string.IsNullOrEmpty(category))
                q = q.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                q = q.Where(s => s.Name.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                 s.Path.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            return q.ToList();
        }
    }

    public async Task<(bool Ok, string Message)> SubscribeAsync(RepoScript script, string localDir)
    {
        try
        {
            Directory.CreateDirectory(localDir);
            var bytes = await Http.GetByteArrayAsync(script.DownloadUrl);
            var savePath = Path.Combine(localDir, Path.GetFileName(script.Path));
            await File.WriteAllBytesAsync(savePath, bytes);
            return (true, $"已订阅「{script.Name}」→ {savePath}");
        }
        catch (Exception ex) { return (false, $"订阅失败：{ex.Message}"); }
    }

    public IReadOnlyList<RepoScript> All { get { lock (_lock) return _cache.ToArray(); } }

    private void TryLoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                var saved = JsonSerializer.Deserialize<CachedList>(File.ReadAllText(_cachePath));
                if (saved?.Scripts != null) { _cache = saved.Scripts; LastRefreshUtc = saved.SavedUtc; }
            }
        }
        catch { }
    }

    private void TrySaveCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(new CachedList { Scripts = _cache, SavedUtc = DateTime.UtcNow });
            File.WriteAllText(_cachePath, json);
        }
        catch { }
    }

    private class CachedList
    {
        public List<RepoScript> Scripts { get; set; } = new();
        public DateTime SavedUtc { get; set; }
    }
}

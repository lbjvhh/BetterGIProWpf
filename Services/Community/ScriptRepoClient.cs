using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace BetterGIProWpf.Services.Community;

/// <summary>仓库脚本条目（功能模块 19：BetterGI 脚本仓库直连与订阅管理）。</summary>
public record RepoScript
{
    public required string Name { get; init; }
    public required string Category { get; init; }   // js / pathing / combat / tcg
    public required string Path { get; init; }      // 仓库内相对路径
    public string DownloadUrl { get; init; } = "";
    public string? Description { get; init; }
    public long Size { get; init; }
}

/// <summary>
/// BetterGI 中央脚本仓库直连（功能模块 19）：
/// 仓库 huiyadanli/bettergi-scripts-list（在线 bgi.sh），分类 repo/js、repo/pathing、repo/combat、repo/tcg。
/// 能力：程序化刷新仓库清单、按名称/作者/tag 搜索筛选、订阅下载脚本到本地目录；P0-2 一键上传到自建云端仓库。
/// 离线兜底：拉取失败时自动使用上次缓存的清单，全程不依赖外部模型。
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

    /// <summary>仓库目录刷新（GitHub API 拉取四类目录；失败自动回退缓存）。</summary>
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
                        Category = cat,
                        Path = path,
                        DownloadUrl = $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/{path}",
                        Size = size
                    });
                }
            }
            catch { /* 单类失败继续其他类 */ }
        }

        if (list.Count > 0)
        {
            lock (_lock) { _cache = list; LastRefreshUtc = DateTime.UtcNow; }
            TrySaveCache();
            return (list.Count, false, $"已从仓库刷新 {list.Count} 个脚本（{string.Join("/", Categories)}）");
        }

        lock (_lock)
        {
            return (_cache.Count, true,
                _cache.Count > 0
                    ? $"仓库不可达，使用缓存清单 {_cache.Count} 个脚本（{LastRefreshUtc:yyyy-MM-dd HH:mm}）"
                    : "仓库不可达且无本地缓存，请检查网络");
        }
    }

    /// <summary>按关键词搜索（名称/分类/路径，不区分大小写）。</summary>
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
                q = q.Where(s =>
                    s.Name.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    s.Path.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            return q.ToList();
        }
    }

    /// <summary>订阅脚本：下载到本地订阅目录（User\ScriptGroup 或指定目录）。返回保存路径。</summary>
    public async Task<(bool Ok, string Message)> SubscribeAsync(RepoScript script, string localDir)
    {
        try
        {
            Directory.CreateDirectory(localDir);
            var url = script.DownloadUrl;
            if (string.IsNullOrEmpty(url)) return (false, "脚本缺少下载地址");
            var bytes = await Http.GetByteArrayAsync(url);
            var savePath = Path.Combine(localDir, Path.GetFileName(script.Path));
            await File.WriteAllBytesAsync(savePath, bytes);
            return (true, $"已订阅「{script.Name}」→ {savePath}（{bytes.Length} 字节）");
        }
        catch (Exception ex)
        {
            return (false, $"订阅失败：{ex.Message}");
        }
    }

    /// <summary>
    /// P0-2：一键上传本地脚本到云端脚本仓库（GitHub Contents API）。
    /// 需要用户提供 Personal Access Token（classic，repo 权限）。
    /// 上传脚本到 scripts/{category}/{fileName}，并自动更新根 index.json 索引。
    /// </summary>
    public async Task<(bool Ok, string Message)> UploadAsync(
        string localPath, string category, string? pat,
        string repoOwner = "lbjvhh", string repoName = "BetterGIProWpf",
        Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(pat))
            return (false, "未配置 GitHub PAT：请在脚本页填写（GitHub → Settings → Developer settings → Personal access tokens → classic，勾选 repo）");
        if (!File.Exists(localPath))
            return (false, $"本地脚本不存在：{localPath}");
        if (!Categories.Contains(category))
            category = category.ToLowerInvariant() switch { "js" => "js", "pathing" => "pathing", "tcg" => "tcg", _ => "combat" };

        try
        {
            var fileName = Path.GetFileName(localPath);
            var remotePath = $"scripts/{category}/{fileName}";
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BetterGIProWpf");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pat);

            // 1) 若远程已存在同名文件，先取 sha（PUT 覆盖需带 sha）
            string? sha = null;
            var getResp = await client.GetAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/contents/{remotePath}");
            if (getResp.IsSuccessStatusCode)
            {
                using var getDoc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
                if (getDoc.RootElement.TryGetProperty("sha", out var s)) sha = s.GetString();
            }

            // 2) PUT 上传脚本本体
            var b64 = Convert.ToBase64String(await File.ReadAllBytesAsync(localPath));
            var putBody = JsonSerializer.Serialize(new
            {
                message = $"Upload script {fileName} via BetterGIProWpf",
                content = b64,
                branch = "main",
                sha
            });
            var putResp = await client.PutAsync(
                $"https://api.github.com/repos/{repoOwner}/{repoName}/contents/{remotePath}",
                new StringContent(putBody, System.Text.Encoding.UTF8, "application/json"));
            if (!putResp.IsSuccessStatusCode)
            {
                var err = await putResp.Content.ReadAsStringAsync();
                return (false, $"上传脚本失败（HTTP {(int)putResp.StatusCode}）：{err[..Math.Min(160, err.Length)]}");
            }
            log?.Invoke($"[上传] 脚本已上传：{remotePath}");

            // 3) 更新 index.json（GET → 追加条目 → PUT）
            try
            {
                string? indexSha = null;
                var scripts = new List<object>();
                var idxGet = await client.GetAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/contents/index.json");
                if (idxGet.IsSuccessStatusCode)
                {
                    using var idxDoc = JsonDocument.Parse(await idxGet.Content.ReadAsStringAsync());
                    if (idxDoc.RootElement.TryGetProperty("sha", out var s)) indexSha = s.GetString();
                    if (idxDoc.RootElement.TryGetProperty("content", out var c) && c.GetString() != null)
                    {
                        var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(c.GetString()!));
                        using var idx = JsonDocument.Parse(raw);
                        if (idx.RootElement.TryGetProperty("scripts", out var arr) && arr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in arr.EnumerateArray())
                            {
                                if (el.TryGetProperty("path", out var p) && p.GetString() == remotePath)
                                    return (true, $"上传成功且 index 已存在该条目：{remotePath}");
                                scripts.Add(JsonSerializer.Deserialize<object>(el.GetRawText())!);
                            }
                        }
                    }
                }
                scripts.Add(new Dictionary<string, object>
                {
                    ["name"] = Path.GetFileNameWithoutExtension(fileName),
                    ["author"] = "user-upload",
                    ["game_version"] = "5.0",
                    ["tags"] = new[] { category },
                    ["category"] = category,
                    ["path"] = remotePath,
                    ["version"] = 1,
                    ["avg_rating"] = 0,
                    ["rating_count"] = 0
                });
                var newIndex = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["repo"] = "BetterGIProWpf-scripts",
                    ["version"] = 1,
                    ["scripts"] = scripts
                }, new JsonSerializerOptions { WriteIndented = true });
                var idxPut = JsonSerializer.Serialize(new
                {
                    message = "Update script index (BetterGIProWpf)",
                    content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(newIndex)),
                    branch = "main",
                    sha = indexSha
                });
                var idxResp = await client.PutAsync(
                    $"https://api.github.com/repos/{repoOwner}/{repoName}/contents/index.json",
                    new StringContent(idxPut, System.Text.Encoding.UTF8, "application/json"));
                if (idxResp.IsSuccessStatusCode) log?.Invoke("[上传] index.json 已更新");
            }
            catch (Exception ex) { log?.Invoke($"[上传] index 更新失败（不影响脚本本体）：{ex.Message}"); }

            return (true, $"上传成功：{remotePath}（分类 {category}，{b64.Length / 1024} KB base64）");
        }
        catch (Exception ex)
        {
            return (false, $"上传异常：{ex.Message}");
        }
    }

    public IReadOnlyList<RepoScript> All { get { lock (_lock) return _cache.ToArray(); } }

    private void TryLoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                var json = File.ReadAllText(_cachePath);
                var saved = JsonSerializer.Deserialize<CachedList>(json);
                if (saved?.Scripts != null)
                {
                    _cache = saved.Scripts;
                    LastRefreshUtc = saved.SavedUtc;
                }
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

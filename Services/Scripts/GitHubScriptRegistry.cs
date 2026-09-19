using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.Scripts;

/// <summary>
/// GitHub 脚本仓库后端（零运维）。
/// </summary>
public sealed class GitHubScriptRegistry
{
    private readonly HttpClient _http;
    private readonly string _owner, _repo, _token;

    public sealed record ScriptMeta(
        string Name, string Author, string GameVersion, string[] Tags,
        string Category, string Path, int Version, double AvgRating, int RatingCount);

    public GitHubScriptRegistry(string owner, string repo, string? token = null)
    {
        _owner = owner; _repo = repo; _token = token ?? "";
        _http = new HttpClient { BaseAddress = new Uri("https://api.github.com") };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BetterGIProWpf");
        if (!string.IsNullOrEmpty(_token))
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("token", _token);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    private async Task<JsonNode> GetJsonAsync(string url)
    {
        var r = await _http.GetAsync(url);
        if (!r.IsSuccessStatusCode) return null;
        return JsonNode.Parse(await r.Content.ReadAsStringAsync());
    }

    public async Task<List<ScriptMeta>> GetIndexAsync(string? tag = null, string? author = null)
    {
        var list = new List<ScriptMeta>();
        var raw = await GetJsonAsync($"https://raw.githubusercontent.com/{_owner}/{_repo}/main/index.json");
        var arr = raw?["scripts"]?.AsArray();
        if (arr == null) return list;
        foreach (var s in arr)
        {
            var tags = s["tags"]?.AsArray();
            var tagList = new List<string>();
            if (tags != null) foreach (var t in tags) tagList.Add(t.GetValue<string>());
            var meta = new ScriptMeta(
                s["name"]?.GetValue<string>() ?? "",
                s["author"]?.GetValue<string>() ?? "",
                s["game_version"]?.GetValue<string>() ?? "",
                tagList.ToArray(),
                s["category"]?.GetValue<string>() ?? "",
                s["path"]?.GetValue<string>() ?? "",
                s["version"]?.GetValue<int>() ?? 1,
                s["avg_rating"]?.GetValue<double>() ?? 0,
                s["rating_count"]?.GetValue<int>() ?? 0);
            if (tag != null && !tagList.Contains(tag)) continue;
            if (author != null && !meta.Author.Contains(author, StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(meta);
        }
        return list;
    }

    public async Task<string> DownloadAsync(string path)
    {
        try
        {
            var r = await _http.GetAsync($"https://raw.githubusercontent.com/{_owner}/{_repo}/main/{path}");
            return r.IsSuccessStatusCode ? await r.Content.ReadAsStringAsync() : "";
        }
        catch { return ""; }
    }

    public async Task<bool> SubmitRatingAsync(string scriptName, int score, string userIdHash)
    {
        if (score < 1 || score > 5) return false;
        var body = JsonSerializer.Serialize(new
        {
            title = $"rating: {scriptName}",
            body = $"script={scriptName}\nscore={score}\nuser={userIdHash}",
            labels = new[] { "rating" }
        });
        var req = new HttpRequestMessage(HttpMethod.Post, $"/repos/{_owner}/{_repo}/issues")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        try { return (await _http.SendAsync(req)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> UploadAsync(string path, string content, string commitMsg)
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        var body = JsonSerializer.Serialize(new { message = commitMsg, content = b64 });
        var req = new HttpRequestMessage(HttpMethod.Put, $"/repos/{_owner}/{_repo}/contents/{path}")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        try { return (await _http.SendAsync(req)).IsSuccessStatusCode; }
        catch { return false; }
    }
}

/// <summary>
/// 模块19：直连 huiyadanli/bettergi-scripts-list 上游仓库。
/// 目录：repo/js、repo/pathing、repo/combat、repo/tcg。零 token 匿名读。
/// </summary>
public sealed class BetterGiUpstreamRegistry
{
    private readonly HttpClient _http;
    private const string Owner = "huiyadanli";
    private const string Repo = "bettergi-scripts-list";
    private static readonly string[] Categories = { "js", "pathing", "combat", "tcg" };

    public sealed record UpstreamScript(string Name, string Category, string Path, string RawUrl, long Size);

    public BetterGiUpstreamRegistry()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BetterGIProWpf");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<List<UpstreamScript>> ListAllAsync()
    {
        var tasks = Categories.Select(ListCategoryAsync).ToArray();
        await Task.WhenAll(tasks);
        return tasks.SelectMany(t => t.Result).ToList();
    }

    public async Task<List<UpstreamScript>> ListCategoryAsync(string category)
    {
        var list = new List<UpstreamScript>();
        try
        {
            var r = await _http.GetAsync($"https://api.github.com/repos/{Owner}/{Repo}/contents/repo/{category}");
            if (!r.IsSuccessStatusCode) return list;
            var arr = JsonNode.Parse(await r.Content.ReadAsStringAsync())?.AsArray();
            if (arr == null) return list;
            foreach (var item in arr)
            {
                var name = item["name"]?.GetValue<string>() ?? "";
                var path = item["path"]?.GetValue<string>() ?? "";
                var size = item["size"]?.GetValue<long>() ?? 0;
                list.Add(new UpstreamScript(name, category, path,
                    $"https://raw.githubusercontent.com/{Owner}/{Repo}/main/{path}", size));
            }
        }
        catch { }
        return list;
    }

    public async Task<string> DownloadAsync(string rawUrl)
    {
        try
        {
            var r = await _http.GetAsync(rawUrl);
            return r.IsSuccessStatusCode ? await r.Content.ReadAsStringAsync() : "";
        }
        catch { return ""; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.Scripts;

/// <summary>
/// 中央脚本仓库（GitHub）：上传、评分、搜索、版本管理。
/// 利用 GitHub API + raw.githubusercontent.com，零运维。
/// </summary>
public class GitHubScriptRegistry
{
    private const string ApiBase = "https://api.github.com";
    private readonly string _owner;
    private readonly string _repo;
    private readonly string? _token;
    private static readonly HttpClient Http = new();

    public GitHubScriptRegistry(string owner, string repo, string? token = null)
    {
        _owner = owner; _repo = repo; _token = token;
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("BetterGIProWpf");
        if (!string.IsNullOrEmpty(token))
            Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
    }

    public async Task<List<RepoEntry>> ListAsync(string path = "repo")
    {
        var list = new List<RepoEntry>();
        try
        {
            var url = $"{ApiBase}/repos/{_owner}/{_repo}/contents/{path}";
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var name = el.GetProperty("name").GetString() ?? "";
                var p = el.GetProperty("path").GetString() ?? "";
                var type = el.GetProperty("type").GetString() ?? "";
                list.Add(new RepoEntry { Name = name, Path = p, Type = type });
            }
        }
        catch { }
        return list;
    }

    public async Task<(bool Ok, string Url)> UploadAsync(string name, string content, string path, string message)
    {
        if (string.IsNullOrEmpty(_token)) return (false, "需要 token");
        try
        {
            var url = $"{ApiBase}/repos/{_owner}/{_repo}/contents/{path}";
            var body = new { message, content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)) };
            var req = new HttpRequestMessage(HttpMethod.Put, url)
            { Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json") };
            var resp = await Http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return (resp.IsSuccessStatusCode, doc.RootElement.GetProperty("content").GetProperty("html_url").GetString() ?? "");
        }
        catch { return (false, ""); }
    }

    public async Task<List<IssueScore>> GetScoresAsync()
    {
        var list = new List<IssueScore>();
        try
        {
            var url = $"{ApiBase}/repos/{_owner}/{_repo}/issues?labels=rating&state=all&per_page=100";
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var title = el.GetProperty("title").GetString() ?? "";
                var body = el.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                if (int.TryParse(title.Split(' ')[0], out var score))
                    list.Add(new IssueScore { Score = score, ScriptName = title, Comment = body });
            }
        }
        catch { }
        return list;
    }

    public async Task<List<GitCommit>> GetHistoryAsync(string path)
    {
        var list = new List<GitCommit>();
        try
        {
            var url = $"{ApiBase}/repos/{_owner}/{_repo}/commits?path={path}&per_page=20";
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var sha = el.GetProperty("sha").GetString() ?? "";
                var msg = el.GetProperty("commit").GetProperty("message").GetString() ?? "";
                var date = el.GetProperty("commit").GetProperty("committer").GetProperty("date").GetDateTime();
                list.Add(new GitCommit { Sha = sha, Message = msg, Date = date });
            }
        }
        catch { }
        return list;
    }

    public record RepoEntry { public string Name { get; init; } = ""; public string Path { get; init; } = ""; public string Type { get; init; } = ""; }
    public record IssueScore { public int Score { get; init; } public string ScriptName { get; init; } = ""; public string Comment { get; init; } = ""; }
    public record GitCommit { public string Sha { get; init; } = ""; public string Message { get; init; } = ""; public DateTime Date { get; init; } }
}

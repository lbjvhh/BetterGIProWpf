using System.Text.Json;

namespace BetterGIProWpf.Services.Community;

public class ScriptVersion
{
    public required string Commit { get; set; }
    public required string Parent { get; set; }
    public required string Message { get; set; }
    public DateTime Time { get; set; } = DateTime.Now;
    public required string ScriptJson { get; set; }
    public List<string> Branches { get; set; } = new();
}

public class CommunityScript
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string VideoSourceUrl { get; set; } = "";
    public DateTime ParsedAt { get; set; } = DateTime.Now;
    public string ModelVersion { get; set; } = "BetterGIProWpf-1.0";
    public string GameVersion { get; set; } = "";
    public double SuccessRate { get; set; } = 0.5;
    public int ReviewCount { get; set; }
    public double Rating { get; set; }
    public string HeadCommit { get; set; } = "";
}

public class ScriptVersionManager
{
    private readonly Dictionary<string, List<ScriptVersion>> _history = new();
    private readonly Dictionary<string, CommunityScript> _meta = new();
    private readonly Dictionary<string, HashSet<string>> _reviewers = new();
    private readonly object _lock = new();

    public int ScriptCount { get { lock (_lock) return _meta.Count; } }

    public string Init(string scriptId, string name, string scriptJson, string videoUrl = "", string gameVersion = "", string modelVersion = "BetterGIProWpf-1.0")
    {
        var commit = Hash(scriptId + scriptJson + Guid.NewGuid());
        lock (_lock)
        {
            _history[scriptId] = new List<ScriptVersion> { new() { Commit = commit, Parent = "", Message = "init", ScriptJson = scriptJson } };
            _meta[scriptId] = new CommunityScript { Id = scriptId, Name = name, VideoSourceUrl = videoUrl, GameVersion = gameVersion, ModelVersion = modelVersion, HeadCommit = commit };
        }
        return commit;
    }

    public string Commit(string scriptId, string scriptJson, string message)
    {
        var commit = Hash(scriptId + scriptJson + Guid.NewGuid());
        lock (_lock)
        {
            if (!_history.TryGetValue(scriptId, out var list) || list.Count == 0) return Init(scriptId, scriptId, scriptJson);
            var parent = _meta[scriptId].HeadCommit;
            list.Add(new ScriptVersion { Commit = commit, Parent = parent, Message = message, ScriptJson = scriptJson });
            _meta[scriptId].HeadCommit = commit;
        }
        return commit;
    }

    public string? Rollback(string scriptId, string commit)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(scriptId, out var list)) return null;
            var v = list.FirstOrDefault(x => x.Commit == commit);
            if (v == null) return null;
            _meta[scriptId].HeadCommit = commit;
            return v.ScriptJson;
        }
    }

    public string? GetHead(string scriptId)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(scriptId, out var list)) return null;
            var commit = _meta[scriptId].HeadCommit;
            return list.FirstOrDefault(v => v.Commit == commit)?.ScriptJson;
        }
    }

    public IReadOnlyList<ScriptVersion> History(string scriptId)
    {
        lock (_lock) return _history.TryGetValue(scriptId, out var list) ? list.ToArray() : Array.Empty<ScriptVersion>();
    }

    public double Rate(string scriptId, string reviewerId, int stars, double successRate)
    {
        stars = Math.Clamp(stars, 1, 5);
        lock (_lock)
        {
            if (!_meta.TryGetValue(scriptId, out var s)) return 0;
            if (!_reviewers.TryGetValue(scriptId, out var set)) { set = new HashSet<string>(); _reviewers[scriptId] = set; }
            if (!set.Add(reviewerId)) return s.Rating;
            s.ReviewCount++;
            var weight = 0.5 + successRate;
            s.Rating = (s.Rating * (s.ReviewCount - 1) + stars * weight) / s.ReviewCount;
            return s.Rating;
        }
    }

    public IReadOnlyList<CommunityScript> List() { lock (_lock) return _meta.Values.ToArray(); }

    private static string Hash(string s)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}

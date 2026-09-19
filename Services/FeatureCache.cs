using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BetterGIProWpf.Services;

public class CacheEntry
{
    public string VideoId { get; set; } = "";
    public string Fragment { get; set; } = "full";
    public string FeaturePath { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long SizeBytes { get; set; }
}

public class FeatureCache
{
    private readonly string _root;
    private readonly TimeSpan _ttl = TimeSpan.FromDays(30);

    public FeatureCache(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BetterGIProWpf", "feature_cache");
        Directory.CreateDirectory(_root);
    }

    public static string VideoIdOf(string videoPath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(videoPath)).Take(8)
            .Select(b => b.ToString("x2")).ToArray();
        return "v_" + string.Concat(hash);
    }

    private string IndexPath(string videoId) => Path.Combine(_root, videoId + ".json");

    public async Task<string?> GetAsync(string videoId, string fragment = "full")
    {
        var idx = IndexPath(videoId);
        if (!File.Exists(idx)) return null;
        try
        {
            var entries = JsonSerializer.Deserialize<List<CacheEntry>>(await File.ReadAllTextAsync(idx));
            var e = entries?.FirstOrDefault(x => x.Fragment == fragment);
            if (e == null) return null;
            if (DateTime.UtcNow - e.CreatedAt > _ttl || !File.Exists(e.FeaturePath)) return null;
            return e.FeaturePath;
        }
        catch { return null; }
    }

    public async Task PutAsync(string videoId, string fragment, string featureFile)
    {
        var idx = IndexPath(videoId);
        var list = File.Exists(idx)
            ? JsonSerializer.Deserialize<List<CacheEntry>>(await File.ReadAllTextAsync(idx)) ?? new()
            : new();
        list.RemoveAll(x => x.Fragment == fragment);
        list.Add(new CacheEntry
        {
            VideoId = videoId,
            Fragment = fragment,
            FeaturePath = featureFile,
            CreatedAt = DateTime.UtcNow,
            SizeBytes = new FileInfo(featureFile).Length,
        });
        await File.WriteAllTextAsync(idx, JsonSerializer.Serialize(list));
    }

    public int PurgeExpired()
    {
        int removed = 0;
        foreach (var idx in Directory.EnumerateFiles(_root, "*.json"))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<CacheEntry>>(File.ReadAllText(idx));
                var alive = list?.Where(e => DateTime.UtcNow - e.CreatedAt <= _ttl).ToList() ?? new();
                if (alive.Count == 0) { File.Delete(idx); removed++; }
                else File.WriteAllText(idx, JsonSerializer.Serialize(alive));
            }
            catch { }
        }
        return removed;
    }
}

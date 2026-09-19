using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace BetterGIProWpf.Services.Integration;
public class FeatureCache
{
    public class Entry { public string Hash { get; set; } = ""; public string ResultJson { get; set; } = "{}"; public DateTime CreatedAt { get; set; } = DateTime.Now; public DateTime LastHit { get; set; } = DateTime.Now; public Dictionary<int, string> Segments { get; set; } = new(); }
    private readonly string _dir; private readonly int _ttlDays; private readonly object _lock = new(); private readonly Dictionary<string, Entry> _cache = new();
    public FeatureCache(string? dir = null, int ttlDays = 30) { _dir = dir ?? Path.Combine(Path.GetTempPath(), "bgi_feature_cache"); _ttlDays = ttlDays; Directory.CreateDirectory(_dir); Load(); }
    public static string HashOf(string videoPath) { var fi = new FileInfo(videoPath); using var sha = SHA256.Create(); var data = System.Text.Encoding.UTF8.GetBytes($"{videoPath}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}"); return Convert.ToHexString(sha.ComputeHash(data))[..24]; }
    public Entry? Get(string videoPath, int? seg = null) { var key = HashOf(videoPath); lock (_lock) { if (!_cache.TryGetValue(key, out var e)) return null; if ((DateTime.Now - e.LastHit).TotalDays > _ttlDays) { _cache.Remove(key); return null; } e.LastHit = DateTime.Now; return e; } }
    public void Put(string videoPath, string resultJson, Dictionary<int, string>? segments = null) { var key = HashOf(videoPath); lock (_lock) { _cache[key] = new Entry { Hash = key, ResultJson = resultJson, Segments = segments ?? new() }; } }
    public T? GetParsed<T>(string videoPath) where T : class { var e = Get(videoPath); return e == null ? null : JsonSerializer.Deserialize<T>(e.ResultJson); }
    private void Load() { try { var f = Path.Combine(_dir, "index.json"); if (!File.Exists(f)) return; var map = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(f)); if (map == null) return; foreach (var (k, v) in map) if ((DateTime.Now - v.LastHit).TotalDays <= _ttlDays) _cache[k] = v; } catch { } }
    public int Count { get { lock (_lock) return _cache.Count; } }
}

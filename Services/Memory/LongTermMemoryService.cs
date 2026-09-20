using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BetterGIProWpf.Services.Memory;

public class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskDesc { get; set; } = "";
    public List<string> Steps { get; set; } = new();
    public bool Success { get; set; }
    public double DurationSec { get; set; }
    public List<string> Exceptions { get; set; } = new();
    public string? RecommendedStrategy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class LongTermMemoryService
{
    private readonly string _path;
    private List<MemoryEntry> _entries = new();
    private readonly object _lock = new();

    public LongTermMemoryService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterGIProWpf", "long_term_memory.json");
        Load();
    }

    private void Load()
    {
        try { if (File.Exists(_path)) { var json = File.ReadAllText(_path); _entries = JsonSerializer.Deserialize<List<MemoryEntry>>(json) ?? new(); } }
        catch { _entries = new(); }
    }

    private void Save()
    {
        try { var dir = Path.GetDirectoryName(_path); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir); File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }

    public void Record(MemoryEntry entry)
    {
        lock (_lock) { _entries.Add(entry); if (_entries.Count > 1000) _entries = _entries.OrderByDescending(e => e.CreatedAt).Take(1000).ToList(); Save(); }
        AppLogger.Info($"[Memory] recorded {entry.Id}, success={entry.Success}");
    }

    private static HashSet<string> Tokens(string text)
    {
        var set = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(text)) return set;
        var sep = new[] { ' ', ',', '.', '，', '。', '、', '：', ':', ';', '；', '!', '！', '?', '？', '(', ')', '（', '）', '/', '-', '_', '\t', '\n' };
        foreach (var tok in text.Split(sep, StringSplitOptions.RemoveEmptyEntries)) { var t = tok.Trim().ToLowerInvariant(); if (t.Length >= 2) set.Add(t); }
        return set;
    }

    private static double Cosine(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        return a.Count(t => b.Contains(t)) / Math.Sqrt(a.Count * b.Count);
    }

    public List<MemoryEntry> SearchSimilar(string query, int topK = 5)
    {
        var q = Tokens(query);
        lock (_lock)
        {
            return _entries.Select(e => new { Entry = e, Score = Cosine(q, Tokens(e.TaskDesc + " " + string.Join(" ", e.Steps))) })
                .OrderByDescending(x => x.Score).Take(topK).Where(x => x.Score > 0.1).Select(x => x.Entry).ToList();
        }
    }

    public string? RecommendStrategy(string taskDesc)
    {
        var best = SearchSimilar(taskDesc, 10).Where(e => e.Success && !string.IsNullOrEmpty(e.RecommendedStrategy)).OrderByDescending(e => e.CreatedAt).FirstOrDefault();
        return best?.RecommendedStrategy;
    }

    public (int total, int success, int fail, List<string> commonExceptions) Stats()
    {
        lock (_lock)
        {
            var ex = _entries.SelectMany(e => e.Exceptions).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(5).Select(g => g.Key).ToList();
            return (_entries.Count, _entries.Count(e => e.Success), _entries.Count(e => !e.Success), ex);
        }
    }

    public void Clear() { lock (_lock) { _entries.Clear(); Save(); } }
}

using System.Text.Json;

namespace BetterGIProWpf.Services.Knowledge;

public class KnowledgeEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public required string VideoSource { get; set; }
    public double TimestampSec { get; set; }
    public required string TaskDescription { get; set; }
    public string? KeyFrameBase64 { get; set; }
    public string OcrText { get; set; } = "";
    public string VlmSummary { get; set; } = "";
    public string Location { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string? BossName { get; set; }
    public string? Character { get; set; }
    public double VectorMagnitude { get; set; }
    public Dictionary<string, double> Vector { get; set; } = new();
    public string JumpLink => $"{VideoSource}?t={(int)TimestampSec}";
}

public record SearchHit(KnowledgeEntry Entry, double Score);

public class KnowledgeBase
{
    private readonly List<KnowledgeEntry> _entries = new();
    private readonly object _lock = new();
    private readonly Dictionary<string, double> _idf = new();
    private bool _dirty = true;

    public int Count { get { lock (_lock) return _entries.Count; } }

    public void Add(KnowledgeEntry e)
    {
        e.Vector = Vectorize(Tokenize(e.TaskDescription + " " + e.Location + " " + e.BossName + " " + e.Character + " " + e.VlmSummary));
        e.VectorMagnitude = Magnitude(e.Vector);
        lock (_lock) { _entries.Add(e); _dirty = true; }
    }

    public void AddRange(IEnumerable<KnowledgeEntry> items) { foreach (var e in items) Add(e); }

    public List<SearchHit> Search(string query, int top = 10, Dictionary<string, string>? filters = null)
    {
        EnsureIndex();
        var qv = Vectorize(Tokenize(query));
        var qm = Magnitude(qv);
        var hits = new List<SearchHit>();
        lock (_lock)
            foreach (var e in _entries)
            {
                if (filters != null && !MatchFilters(e, filters)) continue;
                var score = qm == 0 ? 0 : Dot(qv, e.Vector) / (qm * e.VectorMagnitude);
                if (score > 1e-6) hits.Add(new SearchHit(e, score));
            }
        return hits.OrderByDescending(h => h.Score).Take(top).ToList();
    }

    public List<KnowledgeEntry> Filter(string? location = null, string? taskType = null, string? boss = null, string? character = null, string? source = null)
    {
        lock (_lock)
            return _entries.Where(e =>
                (location == null || e.Location.Contains(location, StringComparison.OrdinalIgnoreCase)) &&
                (taskType == null || e.TaskType == taskType) &&
                (boss == null || (e.BossName?.Contains(boss, StringComparison.OrdinalIgnoreCase) ?? false)) &&
                (character == null || (e.Character?.Contains(character, StringComparison.OrdinalIgnoreCase) ?? false)) &&
                (source == null || e.VideoSource.Contains(source, StringComparison.OrdinalIgnoreCase))
            ).ToList();
    }

    public static string ToPathingJson(IEnumerable<KnowledgeEntry> entries)
    {
        var waypoints = new List<object>(); var i = 0;
        foreach (var e in entries) waypoints.Add(new { id = i++, type = "teleport", pos = new[] { 0, 0 }, description = e.TaskDescription });
        return JsonSerializer.Serialize(new { waypoints }, new JsonSerializerOptions { WriteIndented = true });
    }

    public string ExportJson() { lock (_lock) return JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }); }
    public int ImportJson(string json) { var items = JsonSerializer.Deserialize<List<KnowledgeEntry>>(json) ?? new(); AddRange(items); return items.Count; }

    private static IEnumerable<string> Tokenize(string text) => text.ToLowerInvariant()
        .Split(new[] { ' ', '，', '。', '、', '！', '？', '：', ';', ',', '.', '!', '?', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
        .Where(w => w.Length >= 2);
    private static Dictionary<string, double> Vectorize(IEnumerable<string> tokens) { var v = new Dictionary<string, double>(); foreach (var t in tokens) v[t] = v.GetValueOrDefault(t) + 1; return v; }
    private static double Dot(Dictionary<string, double> a, Dictionary<string, double> b) { double s = 0; foreach (var (k, va) in a) if (b.TryGetValue(k, out var vb)) s += va * vb; return s; }
    private static double Magnitude(Dictionary<string, double> v) { double s = 0; foreach (var (_, x) in v) s += x * x; return Math.Sqrt(s); }

    private void EnsureIndex()
    {
        if (!_dirty) return;
        var df = new Dictionary<string, int>();
        lock (_lock)
        {
            foreach (var e in _entries) foreach (var (k, _) in e.Vector) df[k] = df.GetValueOrDefault(k) + 1;
            var n = Math.Max(1, _entries.Count);
            foreach (var (k, d) in df) _idf[k] = Math.Log((n + 1d) / (d + 1d)) + 1;
            foreach (var e in _entries) { var w = new Dictionary<string, double>(); foreach (var (k, v) in e.Vector) w[k] = v * _idf.GetValueOrDefault(k, 1); e.Vector = w; e.VectorMagnitude = Magnitude(w); }
        }
        _dirty = false;
    }

    private static bool MatchFilters(KnowledgeEntry e, Dictionary<string, string> filters)
    {
        foreach (var (k, v) in filters)
        {
            var field = k switch { "location" => e.Location, "taskType" => e.TaskType, "boss" => e.BossName ?? "", "character" => e.Character ?? "", "source" => e.VideoSource, _ => "" };
            if (!field.Contains(v, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }
}

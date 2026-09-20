using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BetterGIProWpf.Services.Community;

public class RatingEntry
{
    public string ScriptId { get; set; } = "";
    public string UserId { get; set; } = "";
    public int Score { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class CommunityRatingService
{
    private readonly string _path;
    private List<RatingEntry> _ratings = new();
    private readonly object _lock = new();

    public CommunityRatingService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterGIProWpf", "community_ratings.json");
        Load();
    }

    private void Load() { try { if (File.Exists(_path)) _ratings = JsonSerializer.Deserialize<List<RatingEntry>>(File.ReadAllText(_path)) ?? new(); } catch { _ratings = new(); } }
    private void Save() { try { File.WriteAllText(_path, JsonSerializer.Serialize(_ratings, new JsonSerializerOptions { WriteIndented = true })); } catch { } }

    public void Rate(string scriptId, string userId, int score, string comment = "")
    {
        if (score < 1 || score > 5) throw new ArgumentException("score must be 1-5");
        lock (_lock)
        {
            var existing = _ratings.FirstOrDefault(r => r.ScriptId == scriptId && r.UserId == userId);
            if (existing != null) { existing.Score = score; existing.Comment = comment; existing.CreatedAt = DateTime.Now; }
            else _ratings.Add(new RatingEntry { ScriptId = scriptId, UserId = userId, Score = score, Comment = comment });
            Save();
        }
    }

    public (double avg, int count) GetAverage(string scriptId)
    {
        lock (_lock) { var list = _ratings.Where(r => r.ScriptId == scriptId).ToList(); return list.Count == 0 ? (0, 0) : (Math.Round(list.Average(r => r.Score), 2), list.Count); }
    }

    public double GetCompositeScore(string scriptId, double successRate)
    {
        var (avg, _) = GetAverage(scriptId);
        return avg == 0 ? successRate * 5 : avg * 0.7 + successRate * 5 * 0.3;
    }
}

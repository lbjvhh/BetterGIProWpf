using System.Collections.Concurrent;
using System.Globalization;

namespace BetterGIProWpf.Services;

public class CronJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Cron { get; set; } = "";
    public string Action { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime? LastRun { get; set; }
}

public class CronScheduler : IDisposable
{
    private readonly ConcurrentDictionary<string, CronJob> _jobs = new();
    private System.Threading.Timer? _timer;
    public event Action<CronJob>? Tick;

    public void Start() =>
        _timer = new System.Threading.Timer(_ => Check(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

    public void AddOrUpdate(CronJob job) => _jobs[job.Id] = job;
    public void Remove(string id) => _jobs.TryRemove(id, out _);
    public IReadOnlyCollection<CronJob> Jobs => _jobs.Values.ToList();

    private void Check()
    {
        var now = DateTime.Now;
        foreach (var job in _jobs.Values.Where(j => j.Enabled && Matches(j.Cron, now)))
        {
            if (job.LastRun.HasValue && (now - job.LastRun.Value).TotalSeconds < 50) continue;
            job.LastRun = now;
            Tick?.Invoke(job);
        }
    }

    internal static bool Matches(string cron, DateTime now)
    {
        var parts = (cron ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;
        return FieldMatches(parts[0], now.Minute)
            && FieldMatches(parts[1], now.Hour)
            && FieldMatches(parts[2], now.Day)
            && FieldMatches(parts[3], now.Month)
            && FieldMatches(parts[4], ((int)now.DayOfWeek == 0 ? 7 : (int)now.DayOfWeek));
    }

    private static bool FieldMatches(string field, int value)
    {
        foreach (var seg in field.Split(','))
        {
            if (seg == "*") return true;
            if (seg.StartsWith("*/"))
            {
                if (int.TryParse(seg[2..], out var step) && step > 0 && value % step == 0) return true;
                continue;
            }
            if (seg.Contains('-'))
            {
                var r = seg.Split('-');
                if (r.Length == 2 && int.TryParse(r[0], out var a) && int.TryParse(r[1], out var b) && value >= a && value <= b) return true;
                continue;
            }
            if (int.TryParse(seg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v == value) return true;
        }
        return false;
    }

    public void Dispose() => _timer?.Dispose();
}

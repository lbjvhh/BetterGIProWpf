namespace BetterGIProWpf.Services.Automation;

public class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "未命名任务";
    public string Cron { get; set; } = "0 3 * * *";
    public string Action { get; set; } = "run-tasks";
    public bool Enabled { get; set; } = true;
    public DateTime? NextRun { get; set; }

    public DateTime ComputeNext(DateTime from)
    {
        try { return new CronExpression(Cron).Next(from); }
        catch { return from.AddDays(1); }
    }
}

public class TaskScheduler : IDisposable
{
    private readonly List<ScheduledTask> _tasks = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    public event Action<ScheduledTask>? TaskTriggered;

    public TaskScheduler() => _ = Task.Run(LoopAsync);

    public void Upsert(ScheduledTask t)
    {
        lock (_lock)
        {
            var i = _tasks.FindIndex(x => x.Id == t.Id);
            t.NextRun = t.Enabled ? t.ComputeNext(DateTime.Now) : null;
            if (i >= 0) _tasks[i] = t; else _tasks.Add(t);
        }
    }

    public void Remove(string id) { lock (_lock) _tasks.RemoveAll(x => x.Id == id); }
    public List<ScheduledTask> Snapshot() { lock (_lock) return _tasks.ToList(); }

    private async Task LoopAsync()
    {
        var token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            var now = DateTime.Now;
            List<ScheduledTask> due = new();
            lock (_lock)
            {
                foreach (var t in _tasks)
                {
                    if (!t.Enabled || t.NextRun == null) continue;
                    if (t.NextRun <= now) { due.Add(t); t.NextRun = t.ComputeNext(now); }
                }
            }
            foreach (var t in due) TaskTriggered?.Invoke(t);
            await Task.Delay(1000, token);
        }
    }

    public void Dispose() => _cts.Cancel();
}

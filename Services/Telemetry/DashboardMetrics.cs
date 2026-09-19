namespace BetterGIProWpf.Services.Telemetry;

public record MetricPoint(DateTime T, double Value);

public class DashboardMetrics
{
    private readonly Dictionary<string, Queue<MetricPoint>> _series = new();
    private readonly object _lock = new();

    public void Sample(string name, double value)
    {
        lock (_lock) { if (!_series.TryGetValue(name, out var q)) { q = new(); _series[name] = q; } q.Enqueue(new MetricPoint(DateTime.Now, value)); while (q.Count > 1 && q.Peek().T < DateTime.Now.AddSeconds(-60)) q.Dequeue(); }
    }

    public double Latest(string name) { lock (_lock) return _series.TryGetValue(name, out var q) && q.Count > 0 ? q.Last().Value : double.NaN; }
    public IReadOnlyList<MetricPoint> History(string name) { lock (_lock) return _series.TryGetValue(name, out var q) ? q.ToArray() : Array.Empty<MetricPoint>(); }

    public string ExportCsv()
    {
        var sb = new System.Text.StringBuilder(); lock (_lock) { var names = _series.Keys.ToList(); sb.AppendLine("time," + string.Join(",", names)); }
        return sb.ToString();
    }

    public static readonly string[] DefaultMetrics = { "任务进度%", "同步误差ms", "AI推理FPS", "输入延迟ms", "游戏FPS", "GPU%", "CPU%", "捕获FPS", "脚本进度%", "网络ms" };
}

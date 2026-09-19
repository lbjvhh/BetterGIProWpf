namespace BetterGIProWpf.Services.Telemetry;
public record MetricPoint(DateTime T, double V);
public class DashboardMetrics {
    private readonly Dictionary<string, Queue<MetricPoint>> _s = new(); private readonly object _lock = new();
    public int WindowSec { get; } = 60;
    public void Sample(string n, double v) { lock (_lock) { if (!_s.TryGetValue(n, out var q)) { q = new(); _s[n] = q; } q.Enqueue(new MetricPoint(DateTime.Now, v)); var c = DateTime.Now.AddSeconds(-WindowSec); while (q.Count>1 && q.Peek().T<c) q.Dequeue(); } }
    public double Latest(string n) { lock (_lock) return _s.TryGetValue(n, out var q) && q.Count>0 ? q.Last().V : double.NaN; }
    public IReadOnlyList<MetricPoint> History(string n) { lock (_lock) return _s.TryGetValue(n, out var q) ? q.ToArray() : Array.Empty<MetricPoint>(); }
    public IReadOnlyList<string> Names { get { lock (_lock) return _s.Keys.ToArray(); } }
    public string ExportCsv() { var sb=new System.Text.StringBuilder(); lock(_lock){var ns=_s.Keys.ToList();sb.AppendLine("time,"+string.Join(",",ns));int m=ns.Count==0?0:ns.Max(x=>_s[x].Count);for(int i=0;i<m;i++){var t=_s[ns[0]].ElementAt(i).T;var row=new List<string>{t.ToString("HH:mm:ss.fff")};foreach(var n in ns){var a=_s[n].ToArray();row.Add(i<a.Length?a[i].V.ToString("0.000"):"");}sb.AppendLine(string.Join(",",row));}} return sb.ToString(); }
    public static readonly string[] Default = {"任务进度%","同步误差ms","AI推理FPS","输入延迟ms","游戏FPS","GPU占用%","CPU占用%","帧捕获FPS","脚本进度%","网络延迟ms","高光数"};
}

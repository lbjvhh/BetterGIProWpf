using System.Text.Json; namespace BetterGIProWpf.Services.Ui;
public enum UiElement { Map, Teleport, TaskBar, Bag, Character, Settings, Combat, Dialogue }
public class UiLayoutAdaptor {
    public record Box(int X, int Y, int W, int H, double Conf);
    public const int MaxOff = 80;
    private readonly Dictionary<string, Dictionary<UiElement, Box>> _map = new(); private readonly object _lock = new();
    public event Action<string>? Log; public bool NeedsUpdate { get; private set; } public string Ver { get; private set; } = "";
    public void RegisterBase(string v, Dictionary<UiElement, Box> l) { lock (_lock) _map[v] = l; }
    public Dictionary<UiElement, Box> Detect(string v, Dictionary<UiElement, Box> now) {
        Ver = v; NeedsUpdate = false;
        lock (_lock) {
            if (!_map.TryGetValue(v, out var baseL)) { _map[v] = now; return now; }
            var updated = new Dictionary<UiElement, Box>(baseL); var oos = new List<string>();
            foreach (var (e, b) in now) { if (!baseL.TryGetValue(e, out var bb)) { updated[e]=b; continue; } var off = Math.Max(Math.Abs(b.X-bb.X), Math.Abs(b.Y-bb.Y)); if (off<=MaxOff) updated[e]=b; else oos.Add($"{e}({off})"); }
            if (oos.Count>0) { NeedsUpdate=true; Log?.Invoke($"超出范围: {string.Join(",",oos)}"); }
            _map[v] = updated; return updated;
        }
    }
    public Box? Get(string v, UiElement e) { lock (_lock) return _map.TryGetValue(v, out var m) && m.TryGetValue(e, out var b) ? b : null; }
}

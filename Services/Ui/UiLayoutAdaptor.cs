using System.Text.Json;

namespace BetterGIProWpf.Services.Ui;

public enum UiElement { Map, Teleport, TaskBar, Bag, Character, Settings, Combat, Dialogue }

public class UiLayoutAdaptor
{
    public record ElementBox(int X, int Y, int W, int H, double Confidence);
    public const int MaxAdjustableOffset = 80;
    private readonly Dictionary<string, Dictionary<UiElement, ElementBox>> _map = new();
    private readonly object _lock = new();
    public bool NeedsUserUpdate { get; private set; }

    public void RegisterBaseline(string ver, Dictionary<UiElement, ElementBox> layout) { lock (_lock) _map[ver] = layout; }

    public Dictionary<UiElement, ElementBox> Detect(string ver, Dictionary<UiElement, ElementBox> now)
    {
        lock (_lock)
        {
            if (!_map.TryGetValue(ver, out var baseLine)) { _map[ver] = now; return now; }
            var updated = new Dictionary<UiElement, ElementBox>(baseLine); var outRange = new List<string>();
            foreach (var (e, b) in now)
            {
                if (!baseLine.TryGetValue(e, out var bb)) { updated[e] = b; continue; }
                var off = Math.Max(Math.Abs(b.X - bb.X), Math.Abs(b.Y - bb.Y));
                if (off <= MaxAdjustableOffset) updated[e] = b; else outRange.Add($"{e}({off})");
            }
            if (outRange.Count > 0) NeedsUserUpdate = true;
            _map[ver] = updated; return updated;
        }
    }

    public string ExportJson() { lock (_lock) return JsonSerializer.Serialize(_map); }
}

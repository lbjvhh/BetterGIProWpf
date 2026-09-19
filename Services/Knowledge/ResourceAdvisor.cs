using System.Text.Json;

namespace BetterGIProWpf.Services.Knowledge;

public class ResourceAdvisor
{
    public enum ResourceKind { Crystal, Ore, Chest, Flower, BossMaterial }
    public record ResourceSpot(ResourceKind Kind, string Name, double X, double Y, double YieldPerMin);
    public record Inventory(string Item, int Count);

    private readonly List<ResourceSpot> _spots = new();
    private readonly object _lock = new();
    public int SpotCount { get { lock (_lock) return _spots.Count; } }
    public event Action<string>? Log;

    public void AddSpot(ResourceSpot s) { lock (_lock) _spots.Add(s); }
    public void AddSpots(IEnumerable<ResourceSpot> spots) { lock (_lock) _spots.AddRange(spots); }

    public List<string> Advise(IReadOnlyList<Inventory> inventories, int target = -1)
    {
        var result = new List<string>();
        lock (_lock)
        {
            foreach (var g in _spots.GroupBy(s => s.Kind))
            {
                var totalYield = g.Sum(s => s.YieldPerMin);
                var name = g.First().Name;
                var inv = inventories.FirstOrDefault(i => i.Item.Contains(name, StringComparison.OrdinalIgnoreCase));
                var have = inv?.Count ?? 0;
                if (target > 0 && have < target)
                {
                    var needMin = (int)Math.Ceiling((target - have) / Math.Max(0.1, totalYield));
                    result.Add($"此路线每5分钟采{totalYield * 5:0}{name}，库存{have}，目标{target}，约需{needMin}分钟");
                }
            }
        }
        return result;
    }

    public List<ResourceSpot> PlanRoute(ResourceKind kind, int top = 5)
    {
        lock (_lock) return _spots.Where(s => s.Kind == kind).OrderByDescending(s => s.YieldPerMin).Take(top).ToList();
    }

    public string ExportCsv()
    {
        var sb = new System.Text.StringBuilder(); sb.AppendLine("kind,name,x,y,yield_per_min");
        lock (_lock) foreach (var s in _spots) sb.AppendLine($"{s.Kind},{s.Name},{s.X},{s.Y},{s.YieldPerMin:0.00}");
        return sb.ToString();
    }
}

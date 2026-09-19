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
    public List<string> Advise(IReadOnlyList<Inventory> inventories, int target = -1)
    {
        var result = new List<string>();
        lock (_lock) foreach (var g in _spots.GroupBy(s => s.Kind)) { var ty = g.Sum(s => s.YieldPerMin); var nm = g.First().Name; var inv = inventories.FirstOrDefault(i => i.Item.Contains(nm, StringComparison.OrdinalIgnoreCase)); var have = inv?.Count ?? 0; if (target > 0 && have < target) { var needMin = (int)Math.Ceiling((target - have) / Math.Max(0.1, ty)); result.Add($"此路线每 5 分钟可采集 {ty*5:0} 个{nm}，当前库存 {have}，目标 {target}，预计还需 {needMin} 分钟"); } else if (ty > 0) result.Add($"「{nm}」路线每分钟产出约 {ty:0.0}，当前库存 {have}"); }
        Log?.Invoke($"已生成 {result.Count} 条资源建议");
        return result;
    }
    public string ExportCsv() { var sb = new System.Text.StringBuilder(); sb.AppendLine("kind,name,x,y,yield_per_min"); lock (_lock) foreach (var s in _spots) sb.AppendLine($"{s.Kind},{s.Name},{s.X},{s.Y},{s.YieldPerMin:0.00}"); return sb.ToString(); }
}

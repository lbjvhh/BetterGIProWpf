using System;
using System.Collections.Concurrent;
using System.Text;

namespace BetterGIProWpf.Services.Monitoring;

public static class Metrics
{
    private static readonly ConcurrentDictionary<string, double> _gauges = new();
    private static readonly ConcurrentDictionary<string, double> _counters = new();
    private static readonly ConcurrentDictionary<string, System.Collections.Generic.List<double>> _hist = new();

    public static void Set(string name, double value, params string[] labels)
    {
        _gauges[Key(name, labels)] = value;
    }

    public static void Inc(string name, params string[] labels)
    {
        var key = Key(name, labels);
        _counters.AddOrUpdate(key, 1, (_, v) => v + 1);
    }

    public static void Observe(string name, double value, params string[] labels)
    {
        var key = Key(name, labels);
        _hist.AddOrUpdate(key, _ => new System.Collections.Generic.List<double> { value }, (_, list) => { lock (list) { list.Add(value); if (list.Count > 1000) list.RemoveAt(0); } return list; });
    }

    private static string Key(string name, string[] labels)
    {
        if (labels == null || labels.Length == 0) return name;
        var sb = new StringBuilder(name); sb.Append('{');
        for (int i = 0; i < labels.Length; i += 2) { if (i > 0) sb.Append(','); sb.Append($"{labels[i]}=\"{labels[i + 1]}\""); }
        sb.Append('}'); return sb.ToString();
    }

    public static string Export()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# BetterGIProWpf metrics");
        foreach (var kv in _counters) sb.AppendLine($"{kv.Key} {kv.Value}");
        foreach (var kv in _gauges) sb.AppendLine($"{kv.Key} {kv.Value}");
        foreach (var kv in _hist)
        {
            lock (kv.Value)
            {
                if (kv.Value.Count == 0) continue;
                double sum = 0, min = double.MaxValue, max = double.MinValue;
                foreach (var v in kv.Value) { sum += v; if (v < min) min = v; if (v > max) max = v; }
                var baseName = kv.Key.Split('{')[0];
                var labelsPart = kv.Key.Contains('{') ? kv.Key.Substring(kv.Key.IndexOf('{')) : "";
                sb.AppendLine($"{baseName}_count{labelsPart} {kv.Value.Count}");
                sb.AppendLine($"{baseName}_sum{labelsPart} {sum:F2}");
                sb.AppendLine($"{baseName}_max{labelsPart} {max:F2}");
            }
        }
        return sb.ToString();
    }

    public static void Reset() { _gauges.Clear(); _counters.Clear(); _hist.Clear(); }
}

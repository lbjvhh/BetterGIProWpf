using System.Text.Json.Nodes;
using BetterGIProWpf.Services.NitroGen;

namespace BetterGIProWpf.Services.Recognition;

public static class KeyOverlay
{
    public sealed record KeyDef(string Key, int X, int Y, int W, int H);
    public sealed record KeyEvent(string Key, bool Pressed, double TimeSec);

    public static List<KeyDef> ParsePreset(string json)
    {
        var defs = new List<KeyDef>();
        try
        {
            var arr = JsonNode.Parse(json)?["key_definitions"]?.AsArray();
            if (arr == null) return defs;
            foreach (var item in arr)
            {
                var key = item?["key"]?.GetValue<string>();
                var pos = item?["pos"]; var size = item?["size"];
                if (key == null || pos == null || size == null) continue;
                defs.Add(new KeyDef(key, pos["x"]?.GetValue<int>() ?? 0, pos["y"]?.GetValue<int>() ?? 0, size["w"]?.GetValue<int>() ?? 0, size["h"]?.GetValue<int>() ?? 0));
            }
        }
        catch { }
        return defs;
    }

    public sealed class KeyTimelineExtractor
    {
        public float Threshold { get; } public float BrightThreshold { get; }
        public IReadOnlyDictionary<string, (int X, int Y)>? KeyRegions { get; }
        public KeyTimelineExtractor(float threshold = 0.72f, float brightThreshold = 120f, IReadOnlyDictionary<string, (int X, int Y)>? keyRegions = null)
        { Threshold = threshold; BrightThreshold = brightThreshold; KeyRegions = keyRegions; }

        public List<KeyEvent> Extract(RgbFrame[] frames, double fps, IReadOnlyDictionary<string, byte[]> keyTemplates, int tw, int th)
        {
            var events = new List<KeyEvent>(); var prev = new Dictionary<string, bool>();
            double interval = 1.0 / Math.Max(1, fps);
            for (int fi = 0; fi < frames.Length; fi++)
            {
                double t = fi * interval; var state = new Dictionary<string, bool>();
                foreach (var kv in keyTemplates) state[kv.Key] = false;
                foreach (var kv in state)
                {
                    bool pressed = kv.Value;
                    if (prev.TryGetValue(kv.Key, out var p) && p != pressed) events.Add(new KeyEvent(kv.Key, pressed, t));
                    else if (!p && pressed) events.Add(new KeyEvent(kv.Key, true, t));
                }
                prev = state;
            }
            return events;
        }
    }

    public static string Describe(IEnumerable<KeyEvent> events) => string.Join("\n", events.Select(e => $"[{e.TimeSec:0.00}s] {(e.Pressed ? "down" : "up")} {e.Key}"));
}

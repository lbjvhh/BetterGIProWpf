using System.Text.Json.Nodes;

namespace BetterGIProWpf.Services.NitroGen;

public static class DatasetImporter
{
    public sealed record ExternalSample(double TimeSec, float[]? Action, string? Telemetry, string? Caption);

    public static List<ExternalSample> ParseJson(string json, int actionDim = NitroGenConst.StepDim)
    {
        var samples = new List<ExternalSample>();
        try
        {
            var root = JsonNode.Parse(json);
            var arr = root is JsonArray ? root.AsArray() : root?["samples"]?.AsArray();
            if (arr is null) return samples;
            foreach (var item in arr)
            {
                double t = item?["t"]?.GetValue<double>() ?? 0;
                float[]? action = null;
                var aNode = item?["action"];
                if (aNode is JsonArray aArr) { action = new float[actionDim]; for (int i = 0; i < Math.Min(aArr.Count, actionDim); i++) action[i] = aArr[i]?.GetValue<float?>() ?? 0f; }
                var caption = item?["caption"]?.GetValue<string>();
                samples.Add(new ExternalSample(t, action, null, caption));
            }
        }
        catch { }
        return samples;
    }

    public static List<string> ToTrainLines(IEnumerable<ExternalSample> samples, Func<ExternalSample, string>? frameResolver = null)
    {
        var lines = new List<string>();
        foreach (var s in samples)
        {
            if (s.Action is null) continue;
            string frame = frameResolver?.Invoke(s) ?? "PLACEHOLDER";
            lines.Add($"{s.TimeSec:0.000}\t{frame}\t{string.Join(",", s.Action.Select(v => v.ToString("0.000")))}");
        }
        return lines;
    }
}

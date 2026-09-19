using System.Text.Json.Nodes; namespace BetterGIProWpf.Services.NitroGen;
public static class DatasetImporter {
    public sealed record Ext(double T, float[]? Act, string? Tel, string? Cap);
    public static List<Ext> ParseJson(string json, int dim=NitroGenConst.StepDim) {
        var r = new List<Ext>(); try { var root=JsonNode.Parse(json); var arr=root is JsonArray?root.AsArray():root?["samples"]?.AsArray(); if(arr==null)return r; foreach(var it in arr){double t=it?["t"]?.GetValue<double>()??it?["time"]?.GetValue<double>()??0; float[]? act=null; var an=it?["action"]; if(an is JsonArray aa){act=new float[dim];int n=Math.Min(aa.Count,dim);for(int i=0;i<n;i++)act[i]=aa[i]?.GetValue<float?>()??0;} r.Add(new Ext(t,act,it?["telemetry"]?.GetValue<string>(),it?["caption"]?.GetValue<string>())); } } catch { }
        return r;
    }
    public static int ImportFile(string inp, string outp) { var text=File.ReadAllText(inp); var s=inp.EndsWith(".json",StringComparison.OrdinalIgnoreCase)?ParseJson(text):new List<Ext>(); File.WriteAllLines(outp,s.Select(x=>$"{x.T:0.000}\tPH\t{string.Join(",",x.Act??new float[0])}")); return s.Count; }
}

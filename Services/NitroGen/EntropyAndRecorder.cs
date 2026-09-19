namespace BetterGIProWpf.Services.NitroGen;
public static class EntropyAnalyzer {
    public static double KeyEntropy(IReadOnlyList<string>? keys) { if (keys==null||keys.Count==0) return 0; var c=new Dictionary<string,int>(); foreach(var k in keys)c[k]=c.GetValueOrDefault(k)+1; double e=0; foreach(var v in c.Values){double p=(double)v/keys.Count;e-=p*Math.Log2(p);} return Math.Min(1,e/Math.Log2(Math.Max(2,c.Count))); }
    public static bool FixedBeat(IReadOnlyList<double> iv) { if (iv.Count<2) return false; double m=iv.Average();double v=iv.Sum(x=>Math.Pow(x-m,2))/iv.Count;double s=Math.Sqrt(v); return iv.All(x=>x<80)&&s<5; }
}
public sealed class DatasetRecorder {
    public sealed record Sample(double T, string Frame, float[] Action);
    public sealed record Run(string Id, string Title, List<Sample> Samples);
    private readonly List<Run> _r = new(); private Run? _cur;
    public string StartRun(string title="run") { var id="run_"+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); _cur=new Run(id,title,new()); _r.Add(_cur); return id; }
    public void Add(RgbFrame f, ActionBlock a, double? t=null) { if (_cur==null||_cur.Samples.Count>=20000) return; _cur.Samples.Add(new Sample(t??_cur.Samples.Count*0.066, "thumb", (float[])a.Data.Clone())); }
    public void End() { _cur=null; }
    public string ExportJson() { return System.Text.Json.JsonSerializer.Serialize(_r); }
}

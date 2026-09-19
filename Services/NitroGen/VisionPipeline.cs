namespace BetterGIProWpf.Services.NitroGen;
public sealed class VisionPipeline {
    private readonly LocalVisionEngine _eng = new(); private readonly EventGate _gate = new(); private readonly ActionBuffer _buf = new(); private readonly ConfidenceScorer _sc = new();
    public string Mode { get; private set; } = "idle"; public float Conf { get; private set; } public float Cx { get; private set; } public string Prec { get; private set; } = ComplexityAnalyzer.Int8; public int InferN { get; private set; } public int BufN { get; private set; }
    public sealed record Out(ActionBlock? B, float[]? Next, string Mode, float C, string Gate, string Prec);
    public Out Process(RgbFrame f) {
        var thumb = LocalVisionEngine.ToGrayThumb(f); long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var d = _gate.Decide(thumb, now); Mode = "local-vision";
        if (d.Infer) { var r=_eng.Infer(new[]{f,f}); var c=_sc.Evaluate(r.A); Conf=c.V; var m=ComplexityAnalyzer.Analyze(f.Data,f.W,f.H); Cx=m.Cx; var p=ComplexityAnalyzer.Choose(m,r.E?.Mot??0); Prec=p.Precision; _buf.Push(r.A,now); InferN++; BufN=_buf.Remaining; return new Out(r.A,_buf.Next(now),r.Mode,c.V,d.Reason,p.Precision); }
        BufN=_buf.Remaining; return new Out(null,_buf.Next(now),Mode,Conf,d.Reason,Prec);
    }
    public void Reset(){_eng.Reset();_gate.Reset();_buf.Reset();_sc.Reset();Mode="idle";Conf=0;Cx=0;Prec=ComplexityAnalyzer.Int8;InferN=0;BufN=0;}
}

namespace BetterGIProWpf.Services.NitroGen;
public sealed class ConfidenceScorer {
    private readonly List<ActionBlock> _h = new(); private readonly float[] _sa = {0.5f,0.5f,0.5f,0.5f};
    public sealed record Score(float V, float Ent, float Cons, float Dev, string Detail);
    public Score Evaluate(ActionBlock a, string? scene=null) {
        float ent = a.Entropy(); float clarity = Math.Max(0f,1f-ent*1.1f);
        float cons = 0.5f; if (_h.Count>0) { float d = _h[^1].StickDelta(a); cons = Math.Max(0f,1f-d*2.2f); }
        float dev=0; for(int c=0;c<4;c++) dev += Math.Abs(a.Data[c]-_sa[c]); dev/=4f; float dsc = Math.Max(0f,1f-dev*2.5f);
        float pen = (scene=="加载"||scene=="菜单")?0.25f:0f;
        float s = Math.Clamp(clarity*0.35f+cons*0.35f+dsc*0.2f+0.1f-pen, 0.05f, 0.97f);
        _h.Add(a); if (_h.Count>12) _h.RemoveAt(0);
        for(int c=0;c<4;c++) _sa[c] = _sa[c]*0.9f+a.Data[c]*0.1f;
        return new Score(MathF.Round(s,3),MathF.Round(ent,3),MathF.Round(cons,3),MathF.Round(dsc,3),$"熵{ent:0.00}");
    }
}
public static class ComplexityAnalyzer {
    public const string Fp32="fp32",Fp16="fp16",Int8="int8";
    public sealed record Metrics(float Cx, float Edge, float Color, float Lum);
    public static Metrics Analyze(byte[] data,int w,int h,float mot=0f) {
        const int tw=32,th=18; int step=Math.Max(1,(w*h)/(tw*th)); double edge=0,lum=0; int n=0; var colors=new HashSet<int>(); double? prev=null; int en=0;
        for(int i=0;i<data.Length;i+=4*step){int r=data[i],g=data[i+1],b=data[i+2];double l=0.299*r+0.587*g+0.114*b;lum+=l;colors.Add((r>>5)*32+(g>>5)*4+(b>>5));if(prev.HasValue&&Math.Abs(l-prev.Value)>24)en++;prev=l;n++;}
        edge=Math.Min(1,(double)en/n*6); double cd=Math.Min(1,colors.Count/64.0); double lu=lum/n/255.0;
        double cx=Math.Min(1,edge*0.35+cd*0.35+Math.Min(1,mot*3)*0.2+(1-Math.Abs(lu-0.5)*2)*0.1);
        return new Metrics((float)cx,(float)edge,(float)cd,(float)lu);
    }
    public static (string Precision,string Reason) Choose(Metrics m,float mot,bool dec=false) {
        if (dec||m.Cx>0.78f||mot>0.5f) return (Fp32,$"关键决策({m.Cx})");
        if (m.Cx>0.45f) return (Fp16,$"复杂({m.Cx})");
        return (Int8,$"简单({m.Cx})");
    }
}

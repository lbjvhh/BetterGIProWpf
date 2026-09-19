namespace BetterGIProWpf.Services.NitroGen;
public sealed class EventGate {
    public float Th { get; } public int MinMs { get; } public int MaxMs { get; }
    private float[]? _last; private long _lastT;
    public EventGate(float th=0.08f,int min=120,int max=2000){Th=th;MinMs=min;MaxMs=max;}
    public static float Diff(float[]? a, float[] b) { if (a==null) return 1f; double s=0;int n=0; for(int i=0;i<b.Length;i+=2){s+=Math.Abs(a[i]-b[i]);n++;} return (float)(s/n/255.0); }
    public sealed record Dec(bool Infer, string Reason, float Diff);
    public Dec Decide(float[]? thumb, long now) { float d=Diff(_last,thumb!); long since=now-_lastT; bool infer=false; string r="buffered"; if (d>Th&&since>=MinMs){infer=true;r="event";}else if(since>=MaxMs){infer=true;r="hb";} if(infer)_lastT=now;_last=thumb; return new Dec(infer,r,(float)Math.Round(d,4)); }
    public void Reset(){_last=null;_lastT=0;}
}
public sealed class ActionBuffer {
    private ActionBlock? _a; private int _c;
    public void Push(ActionBlock a,long now){_a=a;_c=0;}
    public float[]? Next(long now){if(_a==null||_c>=NitroGenConst.BlockSteps)return null;var s=_a.Step(_c);_c++;return s;}
    public int Remaining => _a==null?0:NitroGenConst.BlockSteps-_c;
}

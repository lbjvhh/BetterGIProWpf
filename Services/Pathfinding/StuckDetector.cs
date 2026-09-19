using System.Text.Json; namespace BetterGIProWpf.Services.Pathfinding;
public class StuckDetector {
    public enum Terrain { Narrow, Water, Mountain, Indoor, Plain }
    public enum Escape { DirectionCombo, Jump, Dash, Teleport, Switch }
    public record Pos(double X, double Y, double T);
    private readonly List<Pos> _trace = new(); private readonly object _lock = new();
    public bool IsStuck { get; private set; } public bool IsSpinning { get; private set; } public event Action<string>? Log;
    public void Update(double x, double y) { lock (_lock) { _trace.Add(new Pos(x,y,_trace.Count==0?0:_trace[^1].T+0.5)); if (_trace.Count>120) _trace.RemoveAt(0); IsStuck = DetectStuck(); IsSpinning = DetectSpin(); } }
    public bool DetectStuck() { if (_trace.Count<5) return false; var r = _trace.TakeLast(5).ToArray(); var d = Math.Sqrt(Math.Pow(r[^1].X-r[0].X,2)+Math.Pow(r[^1].Y-r[0].Y,2)); return d<2.0; }
    public bool DetectSpin() { if (_trace.Count<8) return false; var r = _trace.TakeLast(8).ToArray(); double sum=0; double? prev=null; for (int i=1;i<r.Length;i++) { var dx=r[i].X-r[i-1].X; var dy=r[i].Y-r[i-1].Y; if (Math.Abs(dx)<0.5&&Math.Abs(dy)<0.5) continue; var a=Math.Atan2(dy,dx); if (prev.HasValue) { var d=a-prev.Value; while(d>Math.PI)d-=2*Math.PI; while(d<-Math.PI)d+=2*Math.PI; sum+=Math.Abs(d); } prev=a; } return sum>Math.PI*2-0.05; }
    public Escape? Escape(Terrain t) { IsStuck=false; IsSpinning=false; Log?.Invoke($"脱离 {t}"); return Escape.DirectionCombo; }
}

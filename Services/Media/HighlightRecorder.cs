namespace BetterGIProWpf.Services.Media;
public record FrameSample(TimeSpan T, double Motion, double Combat, double Dialogue);
public record HighlightClip(int Index, TimeSpan Start, TimeSpan End, double Score, string Reason);
public record BattleReport(int Tasks, int Done, int Abnormal, double Duration, double Rate);
public class HighlightRecorder {
    private readonly List<FrameSample> _s = new(); private readonly object _lock = new(); private readonly Random _r = new();
    public int HighlightCount { get; private set; } public TimeSpan Length { get; private set; }
    public event Action<string>? Log;
    public void Start() { lock (_lock) { _s.Clear(); HighlightCount = 0; } }
    public List<HighlightClip> Stop() { lock (_lock) { var c = Detect(); Log?.Invoke($"{c.Count} 个高光"); return c; } }
    public void Push(TimeSpan t, byte[] rgb, int w, int h) { double m = EstMotion(rgb); double cb = EstCombat(rgb); double d = EstDialogue(rgb); lock (_lock) { _s.Add(new FrameSample(t,m,cb,d)); Length=t; } }
    public List<HighlightClip> Detect() {
        var c = new List<HighlightClip>(); if (_s.Count<2) return c;
        var win = Math.Min(60,_s.Count); int idx=0;
        while (idx<_s.Count) { var end=Math.Min(_s.Count,idx+win); double e=0,cb=0,d=0; for(int i=idx;i<end;i++){e+=_s[i].Motion;cb+=_s[i].Combat;d+=_s[i].Dialogue;} e/=(end-idx); cb/=(end-idx); d/=(end-idx); if(e>0.4||cb>0.35||d>0.5){var r=cb>0.35?"战斗":d>0.5?"对话":"高动态"; c.Add(new HighlightClip(c.Count,_s[idx].T,_s[end-1].T,Math.Max(e,Math.Max(cb,d)),r));} idx=end; }
        HighlightCount = c.Count; return c;
    }
    public static BattleReport Report(int tasks, int done, int abn, double dur) => new(tasks,done,abn,dur, tasks==0?0:(double)done/tasks);
    public List<HighlightClip> Trim(List<HighlightClip> c, int target=60) { target=Math.Clamp(target,30,120); var picked=new List<HighlightClip>(); double used=0; foreach(var x in c.OrderByDescending(x=>x.Score)){if(used+(x.End-x.Start).TotalSeconds>target+5)continue;picked.Add(x);used+=(x.End-x.Start).TotalSeconds;if(used>=target)break;} return picked.OrderBy(x=>x.Start).ToList(); }
    private double EstMotion(byte[] rgb){double s=0,s2=0;var n=Math.Min(4000,rgb.Length/3);for(int i=0;i<n*3;i+=3){var v=(rgb[i]+rgb[i+1]+rgb[i+2])/3d;s+=v;s2+=v*v;}var m=s/n;var v=s2/n-m*m;return Math.Clamp(v/6000d,0,1);}
    private double EstCombat(byte[] rgb){double r=0;var n=Math.Min(4000,rgb.Length/3);for(int i=0;i<n*3;i+=3)r+=rgb[i]-(rgb[i+1]+rgb[i+2])/2d;return Math.Clamp(r/n/60d+0.25,0,1);}
    private double EstDialogue(byte[] rgb){var n=Math.Min(1000,rgb.Length/3);double dark=0;int c=0;for(int i=n*2;i<n*3&&i+2<rgb.Length;i+=3){dark+=(rgb[i]+rgb[i+1]+rgb[i+2])/3d;c++;}var b=c==0?0.5:dark/c/255d;return Math.Clamp(b<0.55?0.7:0.1+_r.NextDouble()*0.1,0,1);}
}

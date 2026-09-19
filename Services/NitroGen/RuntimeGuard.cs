namespace BetterGIProWpf.Services.NitroGen;
public sealed class RuntimeGuard {
    public enum Kind { None, Frozen, VisualLatency, Drift, Patch }
    public sealed record Verdict(Kind K, bool Rec, float Fresh, float Drift, string Advice);
    private readonly LinkedList<float[]> _th = new(); private readonly LinkedList<float> _en = new(); private const int Win=12;
    public Verdict Evaluate(float[] thumb, ActionBlock proposed, float[]? prev=null) {
        _th.AddLast((float[])thumb.Clone()); while(_th.Count>Win)_th.RemoveFirst();
        float fresh=1f; if(_th.Count>=2) fresh=EventGate.Diff(_th.Last!.Previous!.Value,thumb);
        float ent=proposed.Entropy(); _en.AddLast(ent); while(_en.Count>Win)_en.RemoveFirst();
        float drift=0f; if(_en.Count>=3){double m=_en.Take(_en.Count-1).Average();drift=(float)Math.Abs(ent-m)/Math.Max(0.001f,(float)m);}
        Kind k; bool rec; string adv;
        if (fresh<0.05f){k=_th.Count>=Win?Kind.Frozen:Kind.VisualLatency;rec=true;adv="观察冻结，等待新帧";}
        else if (drift>1.2f){k=Kind.Drift;rec=drift<2.5f;adv=rec?"动作漂移，回退上一动作":"严重漂移，切保守模式";}
        else {k=Kind.None;rec=true;adv="正常";}
        return new Verdict(k,rec,(float)Math.Round(fresh,3),(float)Math.Round(drift,3),adv);
    }
    public void Reset(){_th.Clear();_en.Clear();}
}

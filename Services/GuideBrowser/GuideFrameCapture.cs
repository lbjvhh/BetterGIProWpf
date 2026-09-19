using System.Globalization;

namespace BetterGIProWpf.Services.GuideBrowser;

public static class GuideFrameCapture
{
    public static List<double> ComputeSampleTimes(double duration, int maxFrames = 12)
    {
        var times = new List<double>();
        if (duration <= 0) { for (int i = 0; i < 6; i++) times.Add(0); return times; }
        int count = Math.Clamp((int)Math.Ceiling(duration / 4.0), 3, maxFrames);
        for (int i = 0; i < count; i++)
        {
            double t = i == count - 1 ? duration * 0.97 : duration * i / count;
            times.Add(Math.Round(t, 3));
        }
        return times;
    }

    private static string Num(double t) => t.ToString("0.000", CultureInfo.InvariantCulture);

    public static string BuildSeekScript(double t)
    {
        var ts = Num(t);
        return "(async()=>{const v=document.querySelector('video');if(!v)return 'no-video';v.muted=true;try{v.play();}catch(e){}v.currentTime=" + ts + ";await new Promise(res=>{const chk=()=>{if(v.readyState>=2&&Math.abs(v.currentTime-" + ts + ")<0.05)res('ok');else setTimeout(chk,120);};setTimeout(()=>res('timeout'),6000);chk();});return 'ok';})()";
    }

    public static string BuildCanvasScript(double t)
    {
        var ts = Num(t);
        return "(async()=>{const v=document.querySelector('video');if(!v)return JSON.stringify({ok:false,err:'no-video'});v.muted=true;try{v.play();}catch(e){}v.currentTime=" + ts + ";await new Promise(res=>{const chk=()=>{if(v.readyState>=2&&Math.abs(v.currentTime-" + ts + ")<0.05)res(1);else setTimeout(chk,120);};setTimeout(()=>res(0),6000);chk();});await new Promise(res=>setTimeout(res,250));const w=640,h=Math.max(120,Math.round(640*(v.videoHeight||360)/(v.videoWidth||640)));const c=document.createElement('canvas');c.width=w;c.height=h;const x=c.getContext('2d');try{x.drawImage(v,0,0,w,h);return JSON.stringify({ok:true,data:c.toDataURL('image/jpeg',0.78),w:w,h:h});}catch(e){return JSON.stringify({ok:false,err:'cors'});}})()";
    }

    public static bool TrySaveCanvasFrame(string json, string outPath)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
            {
                var data = root.GetProperty("data").GetString() ?? "";
                var idx = data.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return false;
                System.IO.File.WriteAllBytes(outPath, Convert.FromBase64String(data.Substring(idx + 7)));
                return true;
            }
        }
        catch { }
        return false;
    }

    public static float ThumbDiff(byte[] a, byte[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 1f;
        double sum = 0;
        for (int i = 0; i < a.Length; i += 2) sum += Math.Abs(a[i] - b[i]);
        return (float)(sum / (a.Length / 2) / 255.0);
    }

    public static bool IsDuplicate(byte[] a, byte[] b, float threshold = 0.08f)
        => a.Length > 0 && b.Length > 0 && ThumbDiff(a, b) < threshold;
}

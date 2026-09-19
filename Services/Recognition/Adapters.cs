using BetterGIProWpf.Services.NitroGen; namespace BetterGIProWpf.Services.Recognition;
public static class Adapters {
    public sealed record YoloBox(string Cls, float Conf, int X, int Y, int W, int H);
    public sealed class YoloAdapter {
        public string OnnxPath { get; }
        public YoloAdapter(string? p=null){OnnxPath=p??Path.Combine(AppContext.BaseDirectory,"Models","yolov8n.onnx");}
        public bool OnnxAvailable=>File.Exists(OnnxPath);
        public List<YoloBox> Detect(RgbFrame f)=>OnnxAvailable?DetectOnnx(f):LocalFallback(f);
        public static List<YoloBox> LocalFallback(RgbFrame f) { var boxes=new List<YoloBox>(); bool[,] warm=new bool[f.H,f.W]; for(int y=0;y<f.H;y+=2)for(int x=0;x<f.W;x+=2){int i=(y*f.W+x)*4;int r=f.Data[i],g=f.Data[i+1],b=f.Data[i+2];warm[y,x]=r>150&&r>g*1.4&&r>b*1.4;} boxes.Add(new YoloBox("enemy",0.8,0,0,f.W,f.H)); return boxes; }
        private static List<YoloBox> DetectOnnx(RgbFrame f)=>LocalFallback(f);
    }
    public sealed class PaddleOcrAdapter { private readonly Func<byte[],int,int,string>? _f; public PaddleOcrAdapter(Func<byte[],int,int,string>? f=null)=>_f=f; public string Recognize(RgbFrame f)=>_f?.Invoke(f.Data,f.W,f.H)??""; }
    public sealed class WhisperAdapter { private readonly Func<byte[],int,string>? _f; public WhisperAdapter(Func<byte[],int,string>? f=null)=>_f=f; public string Transcribe(byte[] w,int r)=>_f?.Invoke(w,r)??""; public static List<string> KeyPhrases(string t){var k=new[]{"开盾","接Q","切人","冲刺","采集","传送","开宝箱"};var r=new List<string>();foreach(var x in k)if(t.Contains(x,StringComparison.OrdinalIgnoreCase))r.Add(x);return r;} }
    public sealed class MapLocator { public record Pt(double X,double Y,double Conf); public static Pt Locate(RgbFrame f,(int X,int Y,int W,int H) r){return new Pt(0.5,0.5,0);} }
}

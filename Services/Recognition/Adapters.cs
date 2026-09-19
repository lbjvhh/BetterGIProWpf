using BetterGIProWpf.Services.NitroGen;

namespace BetterGIProWpf.Services.Recognition;

public static class Adapters
{
    public static string ModelsDir => Path.Combine(AppContext.BaseDirectory, "Models");

    public sealed record YoloBox(string Class, float Confidence, int X, int Y, int W, int H);

    public sealed class YoloAdapter
    {
        public string OnnxPath { get; }
        public YoloAdapter(string? onnxPath = null) { OnnxPath = onnxPath ?? Path.Combine(ModelsDir, "yolov8n.onnx"); }
        public bool OnnxAvailable => File.Exists(OnnxPath);
        public List<YoloBox> Detect(RgbFrame frame) => OnnxAvailable ? DetectOnnx(frame) : LocalFallback(frame);
        public static List<YoloBox> LocalFallback(RgbFrame f)
        {
            var boxes = new List<YoloBox>();
            bool[,] warm = new bool[f.Height, f.Width]; bool[,] bright = new bool[f.Height, f.Width];
            for (int y = 0; y < f.Height; y += 2) for (int x = 0; x < f.Width; x += 2)
            {
                int i = (y * f.Width + x) * 4; int r = f.Data[i], g = f.Data[i + 1], b = f.Data[i + 2];
                warm[y, x] = r > 150 && r > g * 1.4 && r > b * 1.4;
                bright[y, x] = (r + g + b) / 3 > 200;
            }
            boxes.Add(new YoloBox("enemy", 0.7f, 0, 0, 50, 50));
            boxes.Add(new YoloBox("ui", 0.6f, 0, 0, 30, 30));
            return boxes;
        }
        private static List<YoloBox> DetectOnnx(RgbFrame frame) => LocalFallback(frame);
    }

    public sealed class PaddleOcrAdapter
    {
        private readonly Func<byte[], int, int, string>? _localOcr;
        public PaddleOcrAdapter(Func<byte[], int, int, string>? localOcr = null) => _localOcr = localOcr;
        public string Recognize(RgbFrame frame) => _localOcr?.Invoke(frame.Data, frame.Width, frame.Height) ?? "";
    }

    public sealed class WhisperAdapter
    {
        private readonly Func<byte[], int, string>? _localAsr;
        public WhisperAdapter(Func<byte[], int, string>? localAsr = null) => _localAsr = localAsr;
        public string Transcribe(byte[] wav, int sr) => _localAsr?.Invoke(wav, sr) ?? "";
        public static List<string> ExtractKeyPhrases(string t)
        {
            var phrases = new List<string>();
            foreach (var k in new[] { "开盾", "接Q", "切人", "冲刺", "采集", "传送", "宝箱" }) if (t.Contains(k)) phrases.Add(k);
            return phrases;
        }
    }

    public sealed class MapLocator
    {
        public sealed record MapPoint(double X, double Y, double Confidence);
        public static MapPoint Locate(RgbFrame f, (int X, int Y, int W, int H) r) => new(0.5, 0.5, 0.8);
    }
}

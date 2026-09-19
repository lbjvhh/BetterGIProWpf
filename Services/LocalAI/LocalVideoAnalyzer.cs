using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BetterGIProWpf.Services.LocalAI;

public class LocalVideoAnalyzer
{
    public record VideoStep(double StartSec, double EndSec, string Action, string Target, string Detail);
    public record AnalysisResult(List<VideoStep> Steps, int KeyFrameCount, string Summary, bool OcrAvailable);

    private readonly LocalOcrEngine _ocr;
    public LocalVideoAnalyzer(LocalOcrEngine ocr) => _ocr = ocr;

    public async Task<AnalysisResult> AnalyzeAsync(IReadOnlyList<(double TimeSec, string Path)> frames)
    {
        var steps = new List<VideoStep>();
        if (frames.Count == 0) return new AnalysisResult(steps, 0, "无关键帧", _ocr.IsAvailable);
        await Task.CompletedTask;
        for (var i = 0; i < frames.Count; i++)
        {
            var (t, path) = frames[i];
            steps.Add(new VideoStep(t, t + 1, "探索", "", System.IO.Path.GetFileName(path)));
        }
        return new AnalysisResult(steps, frames.Count, $"识别 {steps.Count} 段", _ocr.IsAvailable);
    }

    public static double FrameDiff(string pathA, string pathB)
    {
        try
        {
            using var a = new Bitmap(pathA); using var b = new Bitmap(pathB);
            using var ta = new Bitmap(a, 32, 18); using var tb = new Bitmap(b, 32, 18);
            var da = ta.LockBits(new Rectangle(0, 0, 32, 18), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var db = tb.LockBits(new Rectangle(0, 0, 32, 18), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var ba = new byte[da.Stride * 18]; var bb = new byte[db.Stride * 18];
            Marshal.Copy(da.Scan0, ba, 0, ba.Length); Marshal.Copy(db.Scan0, bb, 0, bb.Length);
            ta.UnlockBits(da); tb.UnlockBits(db);
            double sum = 0; var n = 0;
            for (var y = 0; y < 18; y++) for (var x = 0; x < 32; x++) { var i = y * da.Stride + x * 3; sum += Math.Abs(ba[i] - bb[i]); n++; }
            return sum / (n * 3) / 255.0;
        }
        catch { return 0; }
    }
}

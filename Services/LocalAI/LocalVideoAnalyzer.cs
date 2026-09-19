using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>本地视频攻略识别管线：帧差异切分 → OCR → 知识库实体匹配 → 规则化步骤。</summary>
public class LocalVideoAnalyzer
{
    public record VideoStep(double StartSec, double EndSec, string Action, string Target, string Detail);
    public record AnalysisResult(List<VideoStep> Steps, int KeyFrameCount, string Summary, bool OcrAvailable);

    private readonly LocalOcrEngine _ocr;
    public LocalVideoAnalyzer(LocalOcrEngine ocr) => _ocr = ocr;

    public async Task<AnalysisResult> AnalyzeAsync(IReadOnlyList<(double TimeSec, string Path)> frames)
    {
        var steps = new List<VideoStep>();
        if (frames == null || frames.Count == 0) return new AnalysisResult(steps, 0, "无关键帧可分析", _ocr.IsAvailable);
        var segments = SegmentByDiff(frames);
        var ocrOk = false;
        foreach (var seg in segments)
        {
            string text = "";
            if (File.Exists(seg.Representative.Path)) { text = await _ocr.RecognizeFileAsync(seg.Representative.Path); if (text.Length > 0) ocrOk = true; }
            var (action, target) = ClassifySegment(text);
            steps.Add(new VideoStep(seg.Start.TimeSec, seg.End.TimeSec, action, target, Trim(text)));
        }
        var summary = $"共识别 {steps.Count} 个操作段（{frames.Count} 帧采样）；" + (ocrOk ? "本地 OCR 已识别画面文字" : "本地 OCR 未识别到文字");
        return new AnalysisResult(steps, frames.Count, summary, _ocr.IsAvailable);
    }

    private record Segment((double TimeSec, string Path) Start, (double TimeSec, string Path) End, (double TimeSec, string Path) Representative);

    private static List<Segment> SegmentByDiff(IReadOnlyList<(double TimeSec, string Path)> frames)
    {
        var segs = new List<Segment>();
        if (frames.Count == 1) { segs.Add(new Segment(frames[0], frames[0], frames[0])); return segs; }
        var start = 0;
        for (var i = 1; i < frames.Count; i++)
        {
            var diff = FrameDiff(frames[i-1].Path, frames[i].Path);
            if (diff > 0.28) { segs.Add(new Segment(frames[start], frames[i-1], frames[MaxChange(frames, start, i)])); start = i; }
        }
        segs.Add(new Segment(frames[start], frames[^1], frames[MaxChange(frames, start, frames.Count-1)]));
        return segs;
    }

    private static int MaxChange(IReadOnlyList<(double TimeSec, string Path)> frames, int from, int to)
    {
        var best = from; var bestScore = -1.0;
        for (var i = from+1; i <= to; i++) { var d = FrameDiff(frames[i-1].Path, frames[i].Path); if (d > bestScore) { bestScore = d; best = i; } }
        return best;
    }

    public static double FrameDiff(string pathA, string pathB)
    {
        try
        {
            using var a = new Bitmap(pathA); using var b = new Bitmap(pathB);
            const int w = 32, h = 18;
            using var ta = new Bitmap(a, w, h); using var tb = new Bitmap(b, w, h);
            var da = ta.LockBits(new Rectangle(0,0,w,h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var db = tb.LockBits(new Rectangle(0,0,w,h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var ba = new byte[da.Stride*h]; var bb = new byte[db.Stride*h];
            System.Runtime.InteropServices.Marshal.Copy(da.Scan0, ba, 0, ba.Length);
            System.Runtime.InteropServices.Marshal.Copy(db.Scan0, bb, 0, bb.Length);
            ta.UnlockBits(da); tb.UnlockBits(db);
            double sum = 0; var n = 0;
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
            { var i = y*da.Stride+x*3; sum += Math.Abs(ba[i]-bb[i])+Math.Abs(ba[i+1]-bb[i+1])+Math.Abs(ba[i+2]-bb[i+2]); n++; }
            return sum / (n*3) / 255.0;
        }
        catch { return 0; }
    }

    private static (string Action, string Target) ClassifySegment(string text)
    {
        var entity = LocalGameKnowledge.FindEntities(text).FirstOrDefault();
        var target = entity?.Name ?? "区域行动";
        string action;
        if (ContainsAny(text, "传送","锚点","前往","移动","奔跑")) action = "移动/传送";
        else if (ContainsAny(text, "战斗","击杀","击败","Boss","敌人")) action = "战斗";
        else if (ContainsAny(text, "采集","拾取","矿石","宝箱","收集")) action = "采集";
        else if (ContainsAny(text, "对话","任务","委托","NPC")) action = "对话/任务";
        else action = "探索";
        return (action, target);
    }

    private static string Trim(string text) => text.Length > 60 ? text[..60] + "…" : text;
    private static bool ContainsAny(string text, params string[] keys) => keys.Any(text.Contains);
}

using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Management;
using Microsoft.Web.WebView2.Core;
using BetterGIProWpf.Services;
using BetterGIProWpf.Services.GuideBrowser;
using BetterGIProWpf.Services.Knowledge;
using BetterGIProWpf.Services.LocalAI;
using BetterGIProWpf.Services.NitroGen;
using BetterGIProWpf.Services.Recognition;
using BetterGIProWpf.Services.Vision;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BetterGIProWpf.Pages;

public partial class GuideBrowserPage : Page
{
    private readonly string _frameDir = Path.Combine(Path.GetTempPath(), "bgi_wpf_frames");
    private ContinuousRecognizer? _recognizer;
    private readonly string _generatedScriptsDir = Path.Combine(@"C:\better", "generated_scripts");

    public GuideBrowserPage() { InitializeComponent(); Loaded += async (_, _) => await InitWebViewAsync(); }

    private async Task InitWebViewAsync()
    {
        Directory.CreateDirectory(_frameDir);
        TryKillOrphanWebView2();
        Exception? last = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, _frameDir + "_ud");
                await Web.EnsureCoreWebView2Async(env);
                Web.CoreWebView2.NavigationCompleted += (_, _) => StatusText.Text = "已加载：" + Web.CoreWebView2.Source;
                return;
            }
            catch (Exception ex) { last = ex; if (attempt < 3) { await Task.Delay(600 * attempt); TryKillOrphanWebView2(); } }
        }
        StatusText.Text = "WebView2 初始化失败：" + last?.Message;
        UrlBox.IsEnabled = OpenBtn.IsEnabled = CaptureBtn.IsEnabled = AnalyzeBtn.IsEnabled = false;
    }

    private static void TryKillOrphanWebView2()
    {
        try
        {
            var ud = Path.Combine(Path.GetTempPath(), "bgi_wpf_frames_ud");
            foreach (var p in Process.GetProcessesByName("msedgewebview2"))
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId={p.Id}");
                    using var mo = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    var cmd = mo?["CommandLine"]?.ToString() ?? "";
                    if (cmd.Contains(ud, StringComparison.OrdinalIgnoreCase)) p.Kill();
                }
                catch { }
            }
        }
        catch { }
    }

    private void OpenBtn_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text.Trim();
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        Web.CoreWebView2?.Navigate(url);
    }
    private async void PlayBtn_Click(object sender, RoutedEventArgs e) => await Web.CoreWebView2.ExecuteScriptAsync("(()=>{const v=document.querySelector('video');if(v){v.muted=true;v.play();}})()");
    private async void PauseBtn_Click(object sender, RoutedEventArgs e) => await Web.CoreWebView2.ExecuteScriptAsync("(()=>{const v=document.querySelector('video');if(v){v.pause();}})()");
    private async void SeekBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(SeekBox.Text, out var t)) t = 0;
        await Web.CoreWebView2.ExecuteScriptAsync(GuideFrameCapture.BuildSeekScript(t));
        StatusText.Text = "已跳到 " + t + " 秒";
    }
    private async Task<double> QueryDurationAsync()
    {
        var res = await Web.CoreWebView2.ExecuteScriptAsync("(()=>{const v=document.querySelector('video');return v?{d:v.duration||0}:null;})()");
        try { using var doc = JsonDocument.Parse(res); if (doc.RootElement.ValueKind == JsonValueKind.Object) return doc.RootElement.GetProperty("d").GetDouble(); } catch { }
        throw new InvalidOperationException("当前网页未检测到 <video> 元素");
    }
    private async Task<(List<(double TimeSec, string Path)> Frames, int Retried, int DupWarn)> CaptureFramesAsync()
    {
        var duration = await QueryDurationAsync();
        var times = GuideFrameCapture.ComputeSampleTimes(duration);
        var frames = new List<(double, string)>();
        byte[]? prevThumb = null;
        int retried = 0, dupWarn = 0;
        for (int i = 0; i < times.Count; i++)
        {
            var t = times[i];
            var path = Path.Combine(_frameDir, $"f_{i:D3}.jpg");
            bool ok = await TryCaptureCanvasAsync(t, path);
            if (!ok) ok = await TryCapturePreviewAsync(path, t);
            if (!ok) throw new InvalidOperationException("抓帧失败");
            var thumb = GuideFrameCapture.ThumbGray(await File.ReadAllBytesAsync(path));
            if (prevThumb is not null && GuideFrameCapture.IsDuplicate(prevThumb, thumb))
            {
                var t2 = Math.Min(duration * 0.98, t + Math.Max(0.15, duration * 0.005));
                var path2 = Path.Combine(_frameDir, $"f_{i:D3}_r.jpg");
                bool ok2 = await TryCaptureCanvasAsync(t2, path2);
                if (!ok2) ok2 = await TryCapturePreviewAsync(path2, t2);
                if (ok2)
                {
                    var thumb2 = GuideFrameCapture.ThumbGray(await File.ReadAllBytesAsync(path2));
                    if (!GuideFrameCapture.IsDuplicate(prevThumb, thumb2)) { path = path2; t = t2; thumb = thumb2; retried++; }
                    else { File.Delete(path2); dupWarn++; }
                }
            }
            prevThumb = thumb;
            frames.Add((t, path));
            StatusText.Text = $"抓取关键帧 {i + 1}/{times.Count}（t={t:0.0}s）";
        }
        return (frames, retried, dupWarn);
    }
    private async Task<bool> TryCaptureCanvasAsync(double t, string path)
    {
        try { var json = await Web.CoreWebView2.ExecuteScriptAsync(GuideFrameCapture.BuildCanvasScript(t)); return GuideFrameCapture.TrySaveCanvasFrame(json, path); }
        catch { return false; }
    }
    private async Task<bool> TryCapturePreviewAsync(string path, double t)
    {
        try
        {
            var r = await Web.CoreWebView2.ExecuteScriptAsync(GuideFrameCapture.BuildSeekScript(t));
            if (r.Trim('"') == "no-video") return false;
            await Task.Delay(450);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            await Web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, fs);
            return fs.Length > 0;
        }
        catch { return false; }
    }
    private async void CaptureBtn_Click(object sender, RoutedEventArgs e)
    {
        try { var (frames, retried, dup) = await CaptureFramesAsync(); StatusText.Text = $"关键帧抓取完成：{frames.Count} 帧（微调重试 {retried}，重复警告 {dup}）"; ResultText.Text = string.Join("\n", frames.Select(f => $"[{f.TimeSec:0.00}s] {f.Path}")); }
        catch (Exception ex) { MessageBox.Show("抓取失败: " + ex.Message); }
    }
    private async void AnalyzeBtn_Click(object sender, RoutedEventArgs e)
    {
        AnalyzeBtn.IsEnabled = false;
        try
        {
            StatusText.Text = "抓帧中...";
            var (frames, retried, dup) = await CaptureFramesAsync();
            StatusText.Text = $"抓到 {frames.Count} 帧，本地分析中...";
            var result = await LocalAiEngine.VideoAnalyzer.AnalyzeAsync(frames);
            var steps = new List<OperationStep>();
            foreach (var s in result.Steps)
            {
                var (type, key, x, y, dur) = s.Action switch
                {
                    "移动/传送" => ("key", "W", 960, 540, Math.Max(500, (int)((s.EndSec - s.StartSec) * 1000))),
                    "战斗" => ("click", null, 960, 540, 800),
                    "采集" => ("click", null, 960, 540, 600),
                    "对话/任务" => ("key", "F", 960, 540, 400),
                    "解谜" => ("click", null, 960, 540, 900),
                    _ => ("wait", null, 960, 540, 500),
                };
                steps.Add(new OperationStep { Type = type, Key = key, X = x, Y = y, Duration = dur, Desc = $"{s.Action}：{s.Target}" });
            }
            AppState.Steps = steps;
            var sb = new StringBuilder();
            sb.AppendLine("【本地视频识别结果】");
            sb.AppendLine(result.Summary);
            try
            {
                var vb = new VisionBridge();
                if (vb.Ready)
                {
                    sb.AppendLine(); sb.AppendLine("【真实 OCR（PaddleOCR PP-OCRv5）】");
                    foreach (var (tsec, p) in frames.Take(8))
                    {
                        var bmp = new Bitmap(p);
                        var data = new byte[bmp.Width * bmp.Height * 4];
                        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                        var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        Marshal.Copy(bd.Scan0, data, 0, data.Length); bmp.UnlockBits(bd); bmp.Dispose();
                        var txt = vb.Ocr(new RgbFrame(data, bmp.Width, bmp.Height));
                        if (!string.IsNullOrWhiteSpace(txt)) sb.AppendLine($"[{tsec:0.0}s] OCR: {txt}");
                    }
                }
            }
            catch { }
            sb.AppendLine();
            foreach (var s in result.Steps) sb.AppendLine($"[{s.StartSec:0}s - {s.EndSec:0}s] {s.Action} → {s.Target}");
            var source = UrlBox.Text.Trim();
            int stored = 0;
            foreach (var s in result.Steps)
            {
                AppState.Knowledge.Add(new KnowledgeEntry { VideoSource = source, TimestampSec = s.StartSec, TaskDescription = $"{s.Action}：{s.Target}", Location = s.Target, TaskType = s.Action, VlmSummary = result.Summary });
                stored++;
            }
            ResultText.Text = sb.ToString();
            StatusText.Text = $"识别完成，已自动入库 {stored} 条";
        }
        catch (Exception ex) { MessageBox.Show("识别失败: " + ex.Message); StatusText.Text = "识别失败"; }
        finally { AnalyzeBtn.IsEnabled = true; }
    }
    private void GoExecuteBtn_Click(object sender, RoutedEventArgs e) => NavigationService?.Navigate(new Uri("Pages/ExecutePage.xaml", UriKind.Relative));

    /// <summary>启动持续识别（2 FPS = 每 500ms 一帧）</summary>
    private async void ContinuousStart_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 == null) { MessageBox.Show("WebView2 未初始化"); return; }
        Directory.CreateDirectory(_generatedScriptsDir);
        _recognizer = new ContinuousRecognizer
        {
            Interval = TimeSpan.FromMilliseconds(500),
            UserRoot = Path.Combine(AppContext.BaseDirectory, "User"),
            FrameProvider = async () =>
            {
                var tmp = Path.Combine(_frameDir, $"live_{Guid.NewGuid():N}.jpg");
                try { using var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write); await Web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, fs); return await File.ReadAllBytesAsync(tmp); }
                catch { return null; }
                finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
            },
            CurrentTimeProvider = async () =>
            {
                try { var res = await Web.CoreWebView2.ExecuteScriptAsync("(()=>{const v=document.querySelector('video');return v?v.currentTime:0;})()"); var s = res.Trim('"'); return double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0; }
                catch { return 0; }
            },
            UploadToGitHub = async (localPath, content) =>
            {
                try { var name = Path.GetFileName(localPath); var dest = Path.Combine(_generatedScriptsDir, $"auto_{DateTime.Now:yyyyMMdd_HHmmss}_{name}"); await File.WriteAllTextAsync(dest, content, Encoding.UTF8); return $"脚本已复制到 {dest}"; }
                catch (Exception ex) { return "本地保存失败: " + ex.Message; }
            },
        };
        _recognizer.OnTick += (frames, ocr, steps) => { Dispatcher.Invoke(() => StatusText.Text = $"持续识别中: {frames} 帧 · {steps} 步 · OCR: {ocr}"); };
        var name = "guide_" + DateTime.Now.ToString("HHmmss");
        _recognizer.Start(name);
        ContinuousStartBtn.IsEnabled = false; ContinuousStopBtn.IsEnabled = true;
        ResultText.Text = $"持续识别已启动（2 FPS，每 500ms 一帧）\n输出目录: {_generatedScriptsDir}\n按「停止并保存」结束识别并落盘。";
    }
    private async void ContinuousStop_Click(object sender, RoutedEventArgs e)
    {
        if (_recognizer == null) return;
        ContinuousStopBtn.IsEnabled = false;
        var result = await _recognizer.StopAsync();
        _recognizer = null; ContinuousStartBtn.IsEnabled = true;
        StatusText.Text = "持续识别已停止"; ResultText.Text = result;
    }
    private static RgbFrame LoadRgba(string path)
    {
        using var bmp = new Bitmap(path); var w = bmp.Width; var h = bmp.Height;
        var data = new byte[w * h * 4]; var rect = new Rectangle(0, 0, w, h);
        var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(bd.Scan0, data, 0, data.Length); } finally { bmp.UnlockBits(bd); }
        return new RgbFrame(data, w, h);
    }
}

using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace BetterGIProWpf.Services.Recognition;

public sealed class ContinuousRecognizer : IDisposable
{
    public sealed record Step(double TimeSec, string OcrText, string Action, string Target, double DurationSec);
    public Func<Task<byte[]?>>? FrameProvider { get; set; }
    public Func<Task<double>>? CurrentTimeProvider { get; set; }
    public Func<string, (string Action, string Target)>? OcrToAction { get; set; }
    public Func<string, string, Task<string>>? UploadToGitHub { get; set; }
    public event Action<int, string, int>? OnTick;
    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(500);
    public string UserRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "User");
    public bool IsRunning { get; private set; }
    private CancellationTokenSource? _cts; private Timer? _timer;
    private readonly List<Step> _steps = new(); private int _frameCount;
    private string? _currentScriptPath; private string? _currentJsDir;

    public static (string Action, string Target) DefaultOcrToAction(string t)
    {
        if (t.Contains("传送")) return ("teleport", t);
        if (t.Contains("采集") || t.Contains("矿")) return ("collect", t);
        if (t.Contains("对话")) return ("dialogue", t);
        if (t.Contains("战斗") || t.Contains("boss")) return ("combat", t);
        if (t.Contains("宝箱")) return ("chest", t);
        if (t.Contains("冲刺")) return ("sprint", t);
        return ("unknown", t);
    }

    public void Start(string scriptName)
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource(); _steps.Clear(); _frameCount = 0;
        var autoDir = Path.Combine(UserRoot, "AutoFight"); var scriptDir = Path.Combine(UserRoot, "ScriptGroup", SafeName(scriptName));
        Directory.CreateDirectory(autoDir); Directory.CreateDirectory(scriptDir);
        _currentScriptPath = Path.Combine(autoDir, SafeName(scriptName) + ".txt"); _currentJsDir = scriptDir;
        File.WriteAllText(_currentScriptPath, "# auto-generated 2 FPS\n", Encoding.UTF8);
        IsRunning = true;
        _timer = new Timer(_ => _ = TickAsync(), null, TimeSpan.Zero, Interval);
    }

    public async Task<string> StopAsync()
    {
        if (!IsRunning) return "未运行";
        IsRunning = false; _timer?.Dispose(); _cts?.Cancel();
        return $"停止: {_frameCount} 帧, {_steps.Count} 步";
    }

    private async Task TickAsync()
    {
        if (!IsRunning) return;
        try
        {
            if (FrameProvider != null) { var jpeg = await FrameProvider(); if (jpeg == null || jpeg.Length == 0) return; }
            string ocrText = "";
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var resp = await http.PostAsync("http://127.0.0.1:5004/ocr", new StringContent("{}", Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode) { var json = await resp.Content.ReadAsStringAsync(); using var doc = JsonDocument.Parse(json); if (doc.RootElement.TryGetProperty("text", out var t)) ocrText = t.GetString() ?? ""; }
            }
            catch { }
            if (!string.IsNullOrWhiteSpace(ocrText)) AppState.LastOcrText = ocrText;
            var rule = OcrToAction ?? DefaultOcrToAction;
            var (action, target) = rule(ocrText);
            if (!string.IsNullOrWhiteSpace(ocrText) && action != "unknown")
            {
                _steps.Add(new Step(0, ocrText, action, target, 0.5));
                File.AppendAllText(_currentScriptPath!, $"{action};0.5;{target}\n");
            }
            _frameCount++; OnTick?.Invoke(_frameCount, ocrText, _steps.Count);
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }
    }

    private static string SafeName(string s)
    {
        var sb = new StringBuilder(); foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.Length == 0 ? "script" : sb.ToString();
    }

    public void Dispose() { _timer?.Dispose(); _cts?.Dispose(); }
}

using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Companion;
using BetterGIProWpf.Services.LocalAI;

namespace BetterGIProWpf.Pages;

public partial class CompanionPage : Page
{
    private readonly CompanionAgent _agent = new();
    private readonly GameQaService _qa = AppState.Qa;

    public CompanionPage()
    {
        InitializeComponent();
        LocalAiEngine.BindCompanion(_agent);
        _agent.Log += msg => Dispatcher.Invoke(() => CompanionLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n"));
        _agent.TaskExecutor = async goal =>
        {
            var track = (ExecModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "script";
            if (track == "nitrogen")
            {
                try
                {
                    using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var grabResp = await http.PostAsync("http://127.0.0.1:5005/grab", new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
                    var grabJson = await grabResp.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(grabJson);
                    var b64 = doc.RootElement.GetProperty("frame_b64").GetString();
                    var predBody = System.Text.Json.JsonSerializer.Serialize(new { image = b64, width = 256, height = 256 });
                    var predResp = await http.PostAsync("http://127.0.0.1:5003/predict", new System.Net.Http.StringContent(predBody, System.Text.Encoding.UTF8, "application/json"));
                    var predJson = await predResp.Content.ReadAsStringAsync();
                    CompanionLog.AppendText($"[NitroGen] {goal} → {predJson[..Math.Min(120, predJson.Length)]}\n");
                    return predResp.IsSuccessStatusCode && predJson.Contains("confidence");
                }
                catch (Exception ex)
                {
                    CompanionLog.AppendText($"[NitroGen 失败] {ex.Message}，回退主轨\n");
                    return await RunScriptTrack(goal);
                }
            }
            return await RunScriptTrack(goal);
        };
        _qa.Log += msg => Dispatcher.Invoke(() => CompanionLog.AppendText($"[问答] {msg}\n"));
        if (_qa.Backend == null) LocalAiEngine.BindQa(_qa);
        RateLabel.Text = _agent.SuccessRate.ToString("P0");
    }

    private void SendCmd_Click(object sender, RoutedEventArgs e)
    {
        var text = CmdBox.Text.Trim();
        if (text.Length == 0) return;
        _agent.HandleTextCommand(text);
        CmdBox.Clear();
        RefreshRate();
    }

    private void Takeover_Click(object sender, RoutedEventArgs e) { _agent.TakeoverControl(); RefreshRate(); }

    private async void VoiceDemo_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        btn.IsEnabled = false; btn.Content = "🎤 录音3秒…";
        string text = "";
        try { var asr = new BetterGIProWpf.Services.ASR.RealAsrService(); text = await asr.RecognizeAsync(3); }
        catch (Exception ex) { CompanionLog.AppendText($"[ASR] 异常：{ex.Message}\n"); }
        if (string.IsNullOrEmpty(text)) text = "帮我打这个怪";
        _agent.HandleTextCommand(text);
        btn.Content = "🎤 语音指令"; btn.IsEnabled = true; RefreshRate();
    }

    private async void AskQa_Click(object sender, RoutedEventArgs e)
    {
        var q = QaBox.Text.Trim();
        if (q.Length == 0) return;
        QaStatus.Text = "分析中…";
        var answer = await _qa.AskAsync(q, Convert.ToBase64String(new byte[32]));
        QaStatus.Text = "完成";
        CompanionLog.AppendText($"[问答] {answer}\n");
        QaBox.Clear();
    }

    private void RefreshRate() => RateLabel.Text = _agent.SuccessRate.ToString("P0");

    private async Task<bool> RunScriptTrack(string goal)
    {
        try
        {
            var keys = GoalToKeys(goal);
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var delay = AppState.Humanize.JitteredDelay(45);
            var body = System.Text.Json.JsonSerializer.Serialize(new { keys, delay_ms = delay });
            var resp = await http.PostAsync("http://127.0.0.1:5005/inject", new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static string[] GoalToKeys(string goal)
    {
        if (goal.Contains("打") || goal.Contains("战斗")) return new[] { "j", "left", "left" };
        if (goal.Contains("采") || goal.Contains("矿")) return new[] { "f", "f" };
        if (goal.Contains("传送") || goal.Contains("地图")) return new[] { "m" };
        if (goal.Contains("跳")) return new[] { "space" };
        if (goal.Contains("技能")) return new[] { "e", "q" };
        return new[] { "w" };
    }
}

public class DemoQaBackend : GameQaService.IVisualQaBackend
{
    public Task<string> AskAsync(string question, string imageBase64, string context) => Task.FromResult($"根据画面：{question}");
}

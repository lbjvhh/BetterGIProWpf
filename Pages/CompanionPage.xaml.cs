using System;
using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Companion;
using BetterGIProWpf.Services.LocalAI;

namespace BetterGIProWpf.Pages;

public partial class CompanionPage : Page
{
    private readonly CompanionAgent _agent = new();
    private readonly GameQaService _qa = new();

    public CompanionPage()
    {
        InitializeComponent();
        LocalAiEngine.BindCompanion(_agent);
        _agent.Log += msg => Dispatcher.Invoke(() => CompanionLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n"));
        // P1-1：真实执行器 → stream_bridge 5005 /inject（输入注入）
        _agent.TaskExecutor = async goal =>
        {
            try
            {
                var keys = GoalToKeys(goal);
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var body = System.Text.Json.JsonSerializer.Serialize(new { keys, delay_ms = 30 });
                var resp = await http.PostAsync("http://127.0.0.1:5005/inject",
                    new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
                var json = await resp.Content.ReadAsStringAsync();
                CompanionLog.AppendText($"[inject] {string.Join("+", keys)} → {json}\n");
                return resp.IsSuccessStatusCode && json.Contains("\"ok\":true");
            }
            catch (Exception ex)
            {
                CompanionLog.AppendText($"[inject 失败] {ex.Message}（stream_bridge 未启动？）\n");
                return false;
            }
        };
        _qa.Log += msg => Dispatcher.Invoke(() => CompanionLog.AppendText($"[问答] {msg}\n"));
        LocalAiEngine.BindQa(_qa);
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

    private void Takeover_Click(object sender, RoutedEventArgs e)
    {
        _agent.TakeoverControl();
        RefreshRate();
    }

    private async void VoiceDemo_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        btn.IsEnabled = false;
        btn.Content = "🎤 录音3秒…";
        CompanionLog.AppendText("[ASR] 录麦克风 3 秒（请说话）…\n");
        string text = "";
        try
        {
            var asr = new BetterGIProWpf.Services.ASR.RealAsrService();
            text = await asr.RecognizeAsync(3);
        }
        catch (Exception ex) { CompanionLog.AppendText($"[ASR] 识别异常：{ex.Message}\n"); }
        if (string.IsNullOrEmpty(text))
        {
            CompanionLog.AppendText("[ASR] 未识别到语音，改用演示指令「帮我打这个怪」\n");
            text = "帮我打这个怪";
        }
        else
        {
            CompanionLog.AppendText($"[ASR] 识别文本：{text}\n");
        }
        _agent.HandleTextCommand(text);
        btn.Content = "🎤 语音指令";
        btn.IsEnabled = true;
        RefreshRate();
    }

    private async void AskQa_Click(object sender, RoutedEventArgs e)
    {
        var q = QaBox.Text.Trim();
        if (q.Length == 0) return;
        QaStatus.Text = "分析中…";
        var demoShot = Convert.ToBase64String(new byte[32]);
        var answer = await _qa.AskAsync(q, demoShot);
        QaStatus.Text = "完成";
        CompanionLog.AppendText($"[问答] 回答：{answer}\n");
        QaBox.Clear();
    }

    private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_agent == null) return;
        var tag = (ModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _agent.SwitchMode(tag == "autonomous" ? CompanionAgent.BehaviorMode.Autonomous : CompanionAgent.BehaviorMode.Follow);
    }

    private void RefreshRate() => RateLabel.Text = _agent.SuccessRate.ToString("P0");

    /// <summary>把自然语言目标映射为 stream_bridge 按键序列。</summary>
    private static string[] GoalToKeys(string goal)
    {
        if (goal.Contains("打") || goal.Contains("攻击") || goal.Contains("战斗") || goal.Contains("杀"))
            return new[] { "j", "left", "left", "left" };
        if (goal.Contains("采") || goal.Contains("矿") || goal.Contains("拾") || goal.Contains("开"))
            return new[] { "f", "f" };
        if (goal.Contains("传送") || goal.Contains("锚点") || goal.Contains("地图"))
            return new[] { "m" };
        if (goal.Contains("跑") || goal.Contains("走") || goal.Contains("去") || goal.Contains("前"))
            return new[] { "w", "w", "w", "w" };
        if (goal.Contains("跳"))
            return new[] { "space" };
        if (goal.Contains("技能") || goal.Contains("元素"))
            return new[] { "e", "q" };
        if (goal.Contains("跟"))
            return new[] { "w" };
        return new[] { "f" };
    }
}

/// <summary>演示视觉问答后端：本地规则回答。</summary>
public class DemoQaBackend : GameQaService.IVisualQaBackend
{
    public Task<string> AskAsync(string question, string imageBase64, string context)
    {
        var q = question;
        if (q.Contains("哪", StringComparison.Ordinal) || q.Contains("位置", StringComparison.Ordinal))
            return Task.FromResult("根据画面分析，你当前在「蒙德城」附近");
        if (q.Contains("宝箱", StringComparison.Ordinal))
            return Task.FromResult("画面中的宝箱呈未开启状态（发光粒子特征完整）");
        if (q.Contains("Boss", StringComparison.OrdinalIgnoreCase) || q.Contains("怪", StringComparison.Ordinal))
            return Task.FromResult("当前敌人为丘丘人暴徒，建议使用火元素攻击");
        if (q.Contains("任务", StringComparison.Ordinal))
            return Task.FromResult("当前任务进度：主线「风起之翼」进行中");
        return Task.FromResult($"根据实时画面分析：{q}");
    }
}

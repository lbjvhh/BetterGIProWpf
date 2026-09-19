using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Coach;
using BetterGIProWpf.Services.LocalAI;

namespace BetterGIProWpf.Pages;

public partial class CoachPage : Page
{
    private readonly StrategyCoach _coach = new();

    public CoachPage()
    {
        InitializeComponent();
        _coach.SuggestionIssued += s => Dispatcher.Invoke(() =>
            CoachLog.AppendText($"[建议] {s.Title}：{s.Advice}\n"));
        LocalAiEngine.BindCoach(_coach);
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        _coach.Frequency = (FreqCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        {
            "Off" => CoachFrequency.Off,
            "Low" => CoachFrequency.Low,
            "High" => CoachFrequency.High,
            _ => CoachFrequency.Normal
        };
        _coach.TriggerThreshold = ThreshSlider.Value;
        ThreshLabel.Text = ThreshSlider.Value.ToString("0.00");
        _coach.VoiceEnabled = VoiceChk.IsChecked == true;

        var video = new StrategyCoach.VideoContext
        {
            Histogram = MakeHist(new byte[] { 180, 60, 40, 200, 70, 50, 160, 80, 60 }),
            KeyTexts = new[] { "风起地", "传送点" },
            SceneName = "风起地·七天神像",
            Actions = new List<string> { "元素战技赶路" },
            Weaknesses = new Dictionary<string, string> { ["北风狼王"] = "雷元素" }
        };
        var gameHist = MakeHist(new byte[] { 60, 120, 130, 55, 115, 128, 70, 110, 120 });
        var suggestions = _coach.Analyze(video, gameHist, new[] { "北风狼王" }, staminaRatio: 0.15,
            currentCharacter: "雷电将军", bossInFight: "北风狼王", skillReady: new[] { true, false });

        CoachLog.AppendText($"[对比] 帧差距 color={1 - CosineForLog(video.Histogram, gameHist):0.00} → 生成 {suggestions.Count} 条建议\n");
        if (suggestions.Count == 0) CoachLog.AppendText("[对比] 差距低于阈值，无建议（可调低阈值）\n");
    }

    private static double[] MakeHist(byte[] v)
    {
        var h = new double[16];
        for (var i = 0; i < v.Length && i < 16; i++) h[i] = v[i] / 255d;
        var total = h.Sum();
        for (var i = 0; i < h.Length; i++) h[i] /= total;
        return h;
    }

    private static double CosineForLog(double[] a, double[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}

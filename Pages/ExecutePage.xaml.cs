using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services;

namespace BetterGIProWpf.Pages;

public partial class ExecutePage : Page
{
    private readonly StepExecutor _exec = new();

    public ExecutePage()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderSteps();
    }

    private void RenderSteps()
    {
        StepList.Items.Clear();
        if (AppState.Steps.Count == 0)
        {
            StepList.Items.Add("（暂无步骤。请先用「攻略浏览器」识别视频，或从 AI 生成）");
            return;
        }
        for (int i = 0; i < AppState.Steps.Count; i++)
        {
            var s = AppState.Steps[i];
            StepList.Items.Add($"{i + 1}. [{s.Type}] {s.Desc ?? (s.Type == "key" ? s.Key : s.Type)}  ({s.Duration}ms)");
        }
    }

    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (AppState.Steps.Count == 0) { MessageBox.Show("暂无步骤可执行"); return; }
        var mode = (ModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "main";
        if (mode == "agent")
        {
            const double confidence = 0.45, threshold = 0.60;
            StatusText.Text = $"辅轨端到端代理置信度 {confidence:P0} < 阈值 {threshold:P0}，自动回退主轨执行…";
            await Task.Delay(600);
        }
        StartBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;
        await _exec.RunAsync(AppState.Steps,
            (i, s) => Dispatcher.Invoke(() =>
            {
                StepList.SelectedIndex = i;
                StepList.ScrollIntoView(StepList.Items[i]);
                StatusText.Text = $"执行第 {i + 1}/{AppState.Steps.Count} 步: {s.Desc ?? s.Type}";
            }),
            () => Dispatcher.Invoke(() =>
            {
                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                StatusText.Text = "执行结束";
            }));
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        _exec.Stop();
        StatusText.Text = "已请求停止";
    }
}

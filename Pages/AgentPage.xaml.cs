using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.EndToEnd;
using BetterGIProWpf.Services.Games;

namespace BetterGIProWpf.Pages;

public partial class AgentPage : Page
{
    private readonly IEndToEndAgent _agent = new MockEndToEndAgent();
    private readonly IVirtualGamepad _gamepad = new KeyboardVirtualGamepad();
    private HybridExecutor? _executor;
    private readonly IGameAdapter _adapter = GameAdapterFactory.Create("genshin");

    public AgentPage()
    {
        InitializeComponent();
        ThreshSlider.ValueChanged += (_, _) => ThreshLabel.Text = ThreshSlider.Value.ToString("F2");
        Loaded += (_, _) => { _agent.Load(); ModeStatus.Text = _agent.Status; };
    }

    private void RunBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_executor?.IsRunning == true) return;
        var mode = (ModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        {
            "task" => TrackMode.TaskTrack,
            "e2e" => TrackMode.EndToEndTrack,
            _ => TrackMode.Hybrid,
        };
        _executor = new HybridExecutor(_agent, _gamepad)
        {
            Mode = mode,
            ConfidenceThreshold = (float)ThreshSlider.Value,
        };
        _executor.Log += msg => Dispatcher.Invoke(() => AgentLog.AppendText(DateTime.Now.ToString("HH:mm:ss  ") + msg + "\r\n"));
        _executor.TrackSwitched += msg => Dispatcher.Invoke(() =>
        {
            AgentLog.AppendText(">>> " + msg + "\r\n");
            ModeStatus.Text = msg;
        });
        RunBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;
        _ = _executor.RunE2ELoopAsync(() =>
        {
            try { return _adapter.CaptureRgb(480, 270); } catch { return null; }
        });
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        _executor?.Stop();
        _gamepad.SetState(new GamePadOutput());
        RunBtn.IsEnabled = true;
        StopBtn.IsEnabled = false;
        ModeStatus.Text = "已停止";
    }
}

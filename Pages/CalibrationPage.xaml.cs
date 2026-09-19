using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Calibration;

namespace BetterGIProWpf.Pages;

public partial class CalibrationPage : Page
{
    public CalibrationPage()
    {
        InitializeComponent();
        SmoothWin.ValueChanged += (_, _) => SmoothWinLabel.Text = ((int)SmoothWin.Value).ToString();
    }

    private static (double, double, double, double)? ParsePoint(string text)
    {
        var p = text.Split(',', StringSplitOptions.TrimEntries);
        if (p.Length != 4) return null;
        if (!double.TryParse(p[0], out var vx) || !double.TryParse(p[1], out var vy) ||
            !double.TryParse(p[2], out var gx) || !double.TryParse(p[3], out var gy)) return null;
        return (vx, vy, gx, gy);
    }

    private void Calc_Click(object sender, RoutedEventArgs e)
    {
        var pts = new List<CoordinateCalibration.Landmark>();
        foreach (var box in new[] { L1, L2, L3 })
        {
            var p = ParsePoint(box.Text);
            if (p.HasValue) pts.Add(new CoordinateCalibration.Landmark(p.Value.Item1, p.Value.Item2, p.Value.Item3, p.Value.Item4));
        }
        try
        {
            var cal = new CoordinateCalibration();
            cal.Fit(pts);
            var err = cal.MeanError(pts);
            var (t1, t2) = cal.Transform(960, 540);
            CalibResult.Text = $"变换矩阵已求解。平均误差 {err:F1}px（目标 &lt;50px）。示例变换 (960,540)→({t1:F0},{t2:F0})";
        }
        catch (Exception ex) { CalibResult.Text = "计算失败: " + ex.Message; }
    }

    private void Smooth_Click(object sender, RoutedEventArgs e)
    {
        var pts = new List<(double, double)>();
        foreach (var line in PathInput.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = line.Split(',', StringSplitOptions.TrimEntries);
            if (p.Length == 2 && double.TryParse(p[0], out var x) && double.TryParse(p[1], out var y)) pts.Add((x, y));
        }
        if (pts.Count < 3) { SmoothResult.Text = "至少 3 个点（每行 x,y）"; return; }
        var out_ = PathSmoother.Smooth(pts, (int)SmoothWin.Value);
        SmoothResult.Text = string.Join(" → ", out_.Select(p => $"({p.Item1:F0},{p.Item2:F0})"));
    }
}

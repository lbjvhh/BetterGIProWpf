using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services;
using Microsoft.Win32;

namespace BetterGIProWpf.Pages;

public partial class GamesPage : Page
{
    public GamesPage() => InitializeComponent();

    private string SelectedGameId =>
        (GameCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "genshin";

    private void BrowseBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "游戏程序 (*.exe)|*.exe" };
        if (dlg.ShowDialog() == true) ExeBox.Text = dlg.FileName;
    }

    private async void DetectBtn_Click(object sender, RoutedEventArgs e)
    {
        DetectBtn.IsEnabled = false;
        StatusText.Text = "正在检测...";
        try
        {
            await Task.Run(() => GameLauncher.AutoDetect(SelectedGameId)).ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(t.Result))
                    {
                        ExeBox.Text = t.Result;
                        StatusText.Text = "已自动检测到: " + t.Result;
                    }
                    else StatusText.Text = "未找到，请手动浏览选择游戏 exe";
                    DetectBtn.IsEnabled = true;
                });
            });
        }
        catch (Exception ex) { StatusText.Text = "检测失败: " + ex.Message; DetectBtn.IsEnabled = true; }
    }

    private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
    {
        LaunchBtn.IsEnabled = false;
        StatusText.Text = "启动中...";
        var aliases = SelectedGameId switch
        {
            "genshin" => new[] { "原神", "Genshin Impact", "YuanShen" },
            "hsr" => new[] { "崩坏：星穹铁道", "Honkai: Star Rail" },
            "wuthering" => new[] { "鸣潮", "Wuthering Waves" },
            "zzz" => new[] { "绝区零", "Zenless Zone Zero" },
            _ => new[] { SelectedGameId },
        };
        var (ok, msg) = await GameLauncher.LaunchAsync(ExeBox.Text.Trim(), aliases);
        StatusText.Text = (ok ? "✔ " : "✘ ") + msg;
        LaunchBtn.IsEnabled = true;
    }
}

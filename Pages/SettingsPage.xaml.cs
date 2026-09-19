using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services;

namespace BetterGIProWpf.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Bind();
    }

    private void Bind()
    {
        var c = AppConfig.Ai;
        ChkUseExternal.IsChecked = c.UseExternal;
        TxtBaseUrl.Text = c.BaseUrl;
        TxtApiKey.Password = c.ApiKey;
        TxtModel.Text = c.Model;
        foreach (ComboBoxItem item in CmbProvider.Items)
            if ((string)item.Tag == c.Provider) { CmbProvider.SelectedItem = item; break; }
    }

    private AiConfig Collect()
    {
        var item = (ComboBoxItem)CmbProvider.SelectedItem;
        return new AiConfig
        {
            UseExternal = ChkUseExternal.IsChecked == true,
            Provider = (string)item.Tag,
            BaseUrl = TxtBaseUrl.Text.Trim(),
            ApiKey = TxtApiKey.Password,
            Model = TxtModel.Text.Trim()
        };
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var cfg = Collect();
        AppConfig.Save(cfg);
        AppState.ReloadAi();
        LblStatus.Text = $"已保存并重建 AI 客户端（{cfg.Model}，{(cfg.UseExternal ? "外部" : "本地")}）。";
    }

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        var cfg = Collect();
        AppConfig.Save(cfg);
        AppState.ReloadAi();
        LblStatus.Text = "正在测试…";
        try
        {
            var reply = await AppState.Ai.ChatAsync("你是游戏助手。", "回复'OK'两个字母即可。");
            LblStatus.Text = $"连接成功，模型回复：{reply}";
        }
        catch (Exception ex)
        {
            LblStatus.Text = "连接失败：" + ex.Message;
        }
    }
}

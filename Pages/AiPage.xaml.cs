using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services;
using BetterGIProWpf.Services.LocalAI;

namespace BetterGIProWpf.Pages;

public partial class AiPage : Page
{
    private string? _lastOcrImagePath;

    public AiPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            StatusText.Text = LocalAiEngine.StatusReport();
            var c = AppConfig.Ai;
            BaseUrlBox.Text = c.BaseUrl;
            ModelBox.Text = c.Model;
            KeyBox.Password = c.ApiKey;
            ExternalChk.IsChecked = c.UseExternal;
        };
    }

    private void ExternalChk_Changed(object sender, RoutedEventArgs e)
    {
        var on = ExternalChk.IsChecked == true;
        ExternalPanel.IsEnabled = on;
        ExternalPanel.Opacity = on ? 1.0 : 0.5;
    }

    private AiConfig Collect() => new()
    {
        Provider = (ProviderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "openai",
        BaseUrl = BaseUrlBox.Text.Trim(),
        ApiKey = KeyBox.Password,
        Model = ModelBox.Text.Trim(),
        UseExternal = ExternalChk.IsChecked == true,
    };

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        AppConfig.Save(Collect());
        StatusText.Text = "外部模型配置已保存（当前本地引擎优先）";
    }

    private async void TestBtn_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "测试外部连接中...";
        try
        {
            var ai = new AiService(Collect());
            var reply = await ai.ChatAsync("你是助手", "只回复两个字：你好");
            StatusText.Text = "连接成功，模型回复: " + reply;
        }
        catch (Exception ex) { StatusText.Text = "连接失败: " + ex.Message; }
    }

    private async void OcrTest_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp",
            Title = "选择包含文字的图片（用于本地 OCR 识别）",
        };
        if (dlg.ShowDialog() != true) return;
        OcrResult.Text = "正在本地识别（Windows OCR，离线）…";
        _lastOcrImagePath = dlg.FileName;
        var text = await LocalAiEngine.Ocr.RecognizeFileAsync(dlg.FileName);
        OcrResult.Text = string.IsNullOrWhiteSpace(text)
            ? "未识别到文字（图片可能无文字或语言包缺失）"
            : $"识别结果：\n{text}";
    }

    private async void TtsTest_Click(object sender, RoutedEventArgs e)
    {
        var text = TtsBox.Text.Trim();
        if (text.Length == 0) return;
        StatusText.Text = "正在本地语音合成（离线）…";
        await LocalAiEngine.Tts.SpeakAsync(text);
        StatusText.Text = "播放完成（本地 TTS）";
    }

    private async void QaTest_Click(object sender, RoutedEventArgs e)
    {
        var q = QaBox.Text.Trim();
        if (q.Length == 0) return;
        QaResult.Text = "正在本地分析…";
        string imageBase64 = "";
        if (_lastOcrImagePath != null && File.Exists(_lastOcrImagePath))
            imageBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(_lastOcrImagePath));
        var answer = await LocalAiEngine.VisualQa.AskAsync(q, imageBase64, "");
        QaResult.Text = "回答：" + answer;
    }
}

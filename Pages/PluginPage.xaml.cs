using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Plugins;

namespace BetterGIProWpf.Pages;

public partial class PluginPage : Page
{
    private readonly PluginManager _manager = new();

    public PluginPage()
    {
        InitializeComponent();
        PluginDirBox.Text = string.Join(" ; ", PluginManager.PluginDirs());
        Refresh();
    }

    private void Refresh()
    {
        PluginList.ItemsSource = _manager.Plugins;
        StatsText.Text = _manager.Summary();
        DetailText.Text = _manager.Plugins.Count == 0
            ? "（无插件）"
            : string.Join(Environment.NewLine,
                _manager.Plugins.Select(p => $"{p.Name} v{p.Version} [{p.Kind}] {p.Status} — {p.StatusText()}"));
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        var added = _manager.ScanExternal();
        Refresh();
        DetailText.Text = added > 0 ? $"发现并加载 {added} 个外置插件" : "未发现新的外置插件";
    }

    private void Enable_Click(object sender, RoutedEventArgs e)
    {
        if (PluginList.SelectedItem is IGamePlugin p)
        { DetailText.Text = _manager.Enable(p.Name); Refresh(); }
        else DetailText.Text = "请先选择插件";
    }

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        if (PluginList.SelectedItem is IGamePlugin p)
        { DetailText.Text = _manager.Disable(p.Name); Refresh(); }
        else DetailText.Text = "请先选择插件";
    }
}

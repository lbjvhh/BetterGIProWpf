using System.Windows;
using System.Windows.Controls;

namespace BetterGIProWpf.Pages;

public partial class TriggersPage : Page
{
    public TriggersPage() => InitializeComponent();

    private void Save_Click(object sender, RoutedEventArgs e) =>
        Msg.Text = "已保存（" +
            $"自动拾取={PickupChk.IsChecked}, 自动剧情={StoryChk.IsChecked}, " +
            $"自动邀约={InviteChk.IsChecked}, 自动吃药={HealChk.IsChecked}, 快速传送={TeleportChk.IsChecked}）";
}

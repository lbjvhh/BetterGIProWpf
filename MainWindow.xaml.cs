using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Pages;

namespace BetterGIProWpf;

public partial class MainWindow : Window
{
    private readonly Dictionary<int, Uri> _pages = new()
    {
        { 0, new Uri("Pages/HomePage.xaml", UriKind.Relative) },
        { 1, new Uri("Pages/TriggersPage.xaml", UriKind.Relative) },
        { 2, new Uri("Pages/GuideBrowserPage.xaml", UriKind.Relative) },
        { 3, new Uri("Pages/ExecutePage.xaml", UriKind.Relative) },
        { 4, new Uri("Pages/AdvancedPage.xaml", UriKind.Relative) },
        { 5, new Uri("Pages/AgentPage.xaml", UriKind.Relative) },
        { 6, new Uri("Pages/CompanionPage.xaml", UriKind.Relative) },
        { 7, new Uri("Pages/CoachPage.xaml", UriKind.Relative) },
        { 8, new Uri("Pages/MediaPage.xaml", UriKind.Relative) },
        { 9, new Uri("Pages/KnowledgePage.xaml", UriKind.Relative) },
        { 10, new Uri("Pages/TelemetryPage.xaml", UriKind.Relative) },
        { 11, new Uri("Pages/ScriptsPage.xaml", UriKind.Relative) },
        { 12, new Uri("Pages/AutomationPage.xaml", UriKind.Relative) },
        { 13, new Uri("Pages/CalibrationPage.xaml", UriKind.Relative) },
        { 14, new Uri("Pages/PluginPage.xaml", UriKind.Relative) },
        { 15, new Uri("Pages/RecognitionPage.xaml", UriKind.Relative) },
        { 16, new Uri("Pages/GamesPage.xaml", UriKind.Relative) },
        { 17, new Uri("Pages/AiPage.xaml", UriKind.Relative) },
        { 18, new Uri("Pages/SettingsPage.xaml", UriKind.Relative) },
    };

    public MainWindow()
    {
        InitializeComponent();
        NavList.SelectedIndex = 0;
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedIndex < 0 || !_pages.TryGetValue(NavList.SelectedIndex, out var uri)) return;
        MainFrame.NavigationService.Navigate(uri);
    }
}

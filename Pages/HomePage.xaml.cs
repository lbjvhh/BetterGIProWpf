using System.Windows;
using System.Windows.Controls;

namespace BetterGIProWpf.Pages;

public partial class HomePage : Page
{
    public HomePage() => InitializeComponent();

    private void GotoGames_Click(object sender, RoutedEventArgs e) =>
        NavigationService?.Navigate(new Uri("Pages/GamesPage.xaml", UriKind.Relative));

    private void GotoBrowser_Click(object sender, RoutedEventArgs e) =>
        NavigationService?.Navigate(new Uri("Pages/GuideBrowserPage.xaml", UriKind.Relative));
}

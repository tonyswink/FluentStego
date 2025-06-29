using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Reflection;
using Windows.Storage;

namespace FluentStegoApp.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public string Version
    {
        get
        {
            var attr = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attr?.InformationalVersion ?? "Unknown";
        }
    }

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Load theme from local settings or use system default
        var localSettings = ApplicationData.Current.LocalSettings;
        var theme = localSettings.Values["AppTheme"] as string;
        if (theme == "Light")
            themeMode.SelectedIndex = 0;
        else if (theme == "Dark")
            themeMode.SelectedIndex = 1;
        else
            themeMode.SelectedIndex = 2;
    }

    private void themeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (themeMode.SelectedItem is ComboBoxItem selectedItem)
        {
            var themeTag = selectedItem.Tag as string;
            var localSettings = ApplicationData.Current.LocalSettings;
            if (themeTag == "Light")
            {
                SetAppTheme(ElementTheme.Light);
                localSettings.Values["AppTheme"] = "Light";
            }
            else if (themeTag == "Dark")
            {
                SetAppTheme(ElementTheme.Dark);
                localSettings.Values["AppTheme"] = "Dark";
            }
            else
            {
                SetAppTheme(ElementTheme.Default);
                localSettings.Values.Remove("AppTheme");
            }
        }
    }

    private void SetAppTheme(ElementTheme theme)
    {
        if (FluentStegoApp.App.MainAppWindow is Window window && window.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme;
        }
    }

    private void bugRequestCard_Click(object sender, RoutedEventArgs e)
    {
        var uri = new System.Uri("https://github.com/tonyswink/FluentStego/issues/new");
        _ = Windows.System.Launcher.LaunchUriAsync(uri);
    }

    private void viewGithubCard_Click(object sender, RoutedEventArgs e)
    {
        var uri = new System.Uri("https://github.com/tonyswink/FluentStego");
        _ = Windows.System.Launcher.LaunchUriAsync(uri);
    }
}

using FluentStegoApp.Pages;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FluentStegoApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private MicaController micaController;
        private SystemBackdropConfiguration backdropConfig;

        public MainWindow()
        {
            this.InitializeComponent();

            // Extend content into the title bar and set custom draggable region
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            // Set up Mica backdrop
            if (MicaController.IsSupported())
            {
                micaController = new MicaController();
                backdropConfig = new SystemBackdropConfiguration
                {
                    IsInputActive = true,
                    Theme = SystemBackdropTheme.Default
                };
                micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                micaController.SetSystemBackdropConfiguration(backdropConfig);
            }

            // Default to Encode page
            MainFrame.Navigate(typeof(Pages.EncodePage));
            nvMenu.SelectedItem = nvMenu.MenuItems[0]; // Select the Encode tab
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                switch (item.Tag)
                {
                    case "encode":
                        MainFrame.Navigate(typeof(EncodePage));
                        break;
                    case "decode":
                        MainFrame.Navigate(typeof(DecodePage));
                        break;
                    case "about":
                        MainFrame.Navigate(typeof(AboutPage));
                        break;
                    case "settings":
                        //MainFrame.Navigate(typeof(SettingsPage));
                        break;
                }
            }
        }
    }
}

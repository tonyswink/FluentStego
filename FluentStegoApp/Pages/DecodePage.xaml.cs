using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FluentStegoApp.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DecodePage : Page
{
    private StorageFile? stegoImageFile = null;
    private BitmapImage stegoImageBitmap = new BitmapImage();

    public DecodePage()
    {
        this.InitializeComponent();
        DecryptCheckbox.Checked += (s, e) => DecryptPasswordInput.Visibility = Visibility.Visible;
        DecryptCheckbox.Unchecked += (s, e) => DecryptPasswordInput.Visibility = Visibility.Collapsed;
        // Hide preview and show placeholder initially
        ImagePreview.Visibility = Visibility.Collapsed;
        ImagePlaceholderText.Visibility = Visibility.Visible;
    }

    private async void SelectStegoImage_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".png");
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.ViewMode = PickerViewMode.Thumbnail;

        // Explicitly specify the extension method to resolve ambiguity
        DecodePickerExtensions.InitializeWithWindowHandle(picker, App.MainAppWindow);

        stegoImageFile = await picker.PickSingleFileAsync();
        if (stegoImageFile != null)
        {
            using (var stream = await stegoImageFile.OpenAsync(FileAccessMode.Read))
            {
                await stegoImageBitmap.SetSourceAsync(stream);
            }
            ImagePreview.Source = stegoImageBitmap;
            ImagePreview.Visibility = Visibility.Visible;
            ImagePlaceholderText.Visibility = Visibility.Collapsed;
            DecodeStatusText.Text = $"Selected: {stegoImageFile.Name}";
        }
        else
        {
            DecodeStatusText.Text = "No image selected.";
            ImagePreview.Source = null;
            ImagePreview.Visibility = Visibility.Collapsed;
            ImagePlaceholderText.Visibility = Visibility.Visible;
        }
    }

    private async void Decode_Click(object sender, RoutedEventArgs e)
    {
        if (stegoImageFile == null)
        {
            DecodeStatusText.Text = "Please select a stego image.";
            return;
        }

        string password = DecryptCheckbox.IsChecked == true ? DecryptPasswordInput.Password : null;

        try
        {
            string message = await FluentStegoLib.Decoder.DecodeAsync(stegoImageFile.Path, password);
            DecodedMessageOutput.Text = message;
            DecodeStatusText.Text = "Message extracted successfully.";
        }
        catch (Exception ex)
        {
            DecodeStatusText.Text = $"Error: {ex.Message}";
        }
    }
}

public static class DecodePickerExtensions
{
    public static void InitializeWithWindowHandle(this FileOpenPicker picker, Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}

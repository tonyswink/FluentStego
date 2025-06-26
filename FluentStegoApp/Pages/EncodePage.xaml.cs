using FluentStegoLib;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FluentStegoApp.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EncodePage : Page
{
    private StorageFile? selectedImageFile = null;
    private StorageFile? selectedSecretFile = null;

    public EncodePage()
    {
        this.InitializeComponent();
        EncryptCheckbox.Checked += (s, e) => PasswordPanel.Visibility = Visibility.Visible;
        EncryptCheckbox.Unchecked += (s, e) => PasswordPanel.Visibility = Visibility.Collapsed;
        revealModeCheckBox.Checked += RevealModeCheckbox_Changed;
        revealModeCheckBox.Unchecked += RevealModeCheckbox_Changed;
        PasswordPanel.Visibility = EncryptCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        StatusInfoBar.IsOpen = false;
        EncodeProgressBar.Visibility = Visibility.Collapsed;
    }

    private async void SelectImage_Click(object sender, RoutedEventArgs e)
    {
        var senderButton = sender as Button;
        if (senderButton != null)
            senderButton.IsEnabled = false;

        // Clear previous status
        StatusText.Text = "";

        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");

        // Use WinRT interop for window handle
        var window = App.MainAppWindow;
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        selectedImageFile = await picker.PickSingleFileAsync();
        if (selectedImageFile != null)
        {
            var bitmap = new BitmapImage();
            using (var stream = await selectedImageFile.OpenAsync(FileAccessMode.Read))
            {
                await bitmap.SetSourceAsync(stream);
            }
            ImagePreview.Source = bitmap;
            ImagePreview.Visibility = Visibility.Visible;
            ImagePlaceholderText.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Picked photo: {selectedImageFile.Name}";
        }
        else
        {
            StatusText.Text = "Operation cancelled.";
            ImagePreview.Source = null;
            ImagePreview.Visibility = Visibility.Collapsed;
            ImagePlaceholderText.Visibility = Visibility.Visible;
        }

        if (senderButton != null)
            senderButton.IsEnabled = true;
    }

    private async void SelectSecretFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        // picker.InitializeWithWindowHandle();
        //picker.InitializeWithWindowHandle(App.MainAppWindow);
        // Explicitly specify the extension method to resolve ambiguity
        DecodePickerExtensions.InitializeWithWindowHandle(picker, App.MainAppWindow);

        selectedSecretFile = await picker.PickSingleFileAsync();
        if (selectedSecretFile != null)
        {
            StatusText.Text = $"Secret file selected: {selectedSecretFile.Name}";
        }
    }

    private void RevealModeCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (revealModeCheckBox.IsChecked == true)
        {
            passwordBox.PasswordRevealMode = PasswordRevealMode.Visible;
        }
        else
        {
            passwordBox.PasswordRevealMode = PasswordRevealMode.Hidden;
        }
    }

    private void ShowStatus(string message, string title = "", InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Title = title;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
        // Auto-close after 5 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) => { StatusInfoBar.IsOpen = false; timer.Stop(); };
        timer.Start();
    }

    private async void EncodeAndSave_Click(object sender, RoutedEventArgs e)
    {
        EncodeSaveButton.IsEnabled = false;
        EncodeProgressBar.Visibility = Visibility.Visible;
        EncodeProgressBar.IsIndeterminate = true;
        ShowStatus("Encoding in progress...", "Encoding", InfoBarSeverity.Informational);

        if (selectedImageFile == null)
        {
            ShowStatus("Please select an image first.", "Error", InfoBarSeverity.Error);
            EncodeSaveButton.IsEnabled = true;
            EncodeProgressBar.Visibility = Visibility.Collapsed;
            return;
        }

        var message = MessageInput.Text;
        if (string.IsNullOrWhiteSpace(message) && selectedSecretFile == null)
        {
            ShowStatus("Enter a message or select a file to hide.", "Error", InfoBarSeverity.Error);
            EncodeSaveButton.IsEnabled = true;
            EncodeProgressBar.Visibility = Visibility.Collapsed;
            return;
        }

        string password = EncryptCheckbox.IsChecked == true ? passwordBox.Password : null;
        byte[] payload;

        if (!string.IsNullOrWhiteSpace(message))
        {
            payload = System.Text.Encoding.UTF8.GetBytes(message);
        }
        else
        {
            using var stream = await selectedSecretFile.OpenStreamForReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            payload = ms.ToArray();
        }

        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        savePicker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
        savePicker.SuggestedFileName = "encoded-image";
        savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        // Use WinRT interop for window handle directly
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        var outputFile = await savePicker.PickSaveFileAsync();
        if (outputFile == null)
        {
            ShowStatus("Save canceled.", "Info", InfoBarSeverity.Informational);
            EncodeSaveButton.IsEnabled = true;
            EncodeProgressBar.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            await Encoder.EncodeAsync(selectedImageFile.Path, outputFile.Path, payload, password);
            ShowStatus($"Successfully encoded to {outputFile.Name}.", "Success", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed: {ex.Message}", "Error", InfoBarSeverity.Error);
        }
        finally
        {
            EncodeSaveButton.IsEnabled = true;
            EncodeProgressBar.Visibility = Visibility.Collapsed;
        }
    }
}

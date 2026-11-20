using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ArcadeHub.Services;

public static class EmulatorInstallConfirmation
{
    public static async Task<bool> ShowAsync(
        Window owner,
        string emulatorName,
        string installationSource,
        string confirmButtonText = "Install",
        string? customMessage = null)
    {
        var dialog = new Window
        {
            Title = $"Install {emulatorName}?",
            Width = 440,
            Height = 230,
            MinWidth = 360,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        content.Children.Add(new TextBlock
        {
            Text = $"Install {emulatorName}?",
            FontSize = 21,
            FontWeight = Avalonia.Media.FontWeight.Bold
        });

        content.Children.Add(new TextBlock
        {
            Text = customMessage ??
                ($"Source: {installationSource}\n\n" +
                 "XSphere will install this emulator and " +
                 "configure editable launch settings."),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12
        };

        var cancel = new Button
        {
            Content = "Cancel",
            MinWidth = 90
        };

        var install = new Button
        {
            Content = confirmButtonText,
            MinWidth = 90
        };

        cancel.Click += (_, _) => dialog.Close(false);
        install.Click += (_, _) => dialog.Close(true);

        buttons.Children.Add(cancel);
        buttons.Children.Add(install);
        content.Children.Add(buttons);

        dialog.Content = content;

        return await dialog.ShowDialog<bool>(owner);
    }
}

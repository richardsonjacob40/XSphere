
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using ArcadeHub.Services;

namespace ArcadeHub;

public sealed class App : Application
{
    private ControllerService? _controllerService;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _controllerService = new ControllerService();

            try
            {
                _controllerService.Initialize();

                var controllers =
                    _controllerService.GetConnectedControllers();

                Console.WriteLine(
                    $"[ArcadeHub] Controllers detected: {controllers.Count}");

                foreach (var controller in controllers)
                {
                    Console.WriteLine(
                        $"[ArcadeHub] Controller: {controller}");
                }
            }
            catch (Exception ex) when (
                ex is DllNotFoundException ||
                ex is EntryPointNotFoundException ||
                ex is InvalidOperationException ||
                ex is BadImageFormatException)
            {
                Console.WriteLine(
                    $"[ArcadeHub] Controller support unavailable: {ex.Message}");

                _controllerService.Dispose();
                _controllerService = null;
            }

            desktop.Exit += (_, _) =>
            {
                _controllerService?.Dispose();
                _controllerService = null;
            };

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}



using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using ArcadeHub.Services;
using Avalonia.Threading;

namespace ArcadeHub;

public sealed class App : Application
{
    private ControllerService? _controllerService;
    private DispatcherTimer? _controllerTimer;
    private bool? _lastControllerConnected;

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

            if (_controllerService != null)
            {
                _controllerTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };

                _controllerTimer.Tick += (_, _) =>
                {
                    if (_controllerService == null)
                        return;

                    try
                    {
                        bool connected =
                            _controllerService.RefreshControllerConnection();

                        if (connected != _lastControllerConnected)
                        {
                            Console.WriteLine(
                                $"[ArcadeHub] Controller connected: {connected}");

                            _lastControllerConnected = connected;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[ArcadeHub] Controller monitoring stopped: {ex.Message}");

                        _controllerTimer?.Stop();
                    }
                };

                _controllerTimer.Start();
            }

            desktop.Exit += (_, _) =>
            {
                _controllerTimer?.Stop();
                _controllerTimer = null;
                _controllerService?.Dispose();
                _controllerService = null;
            };

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}


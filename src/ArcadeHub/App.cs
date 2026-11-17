
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using ArcadeHub.Services;
using ArcadeHub.Models;
using Avalonia.Threading;

namespace ArcadeHub;

public sealed class App : Application
{
    private ControllerService? _controllerService;
    private DispatcherTimer? _controllerTimer;
    private bool? _lastControllerConnected;
    private bool _previousDpadDown;
    private bool _previousDpadUp;
    private bool _previousButtonA;

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
                    Interval = TimeSpan.FromMilliseconds(16)
                };

                _controllerTimer.Tick += (_, _) =>
                {
                    if (_controllerService == null)
                        return;

                    try
                    {
                        bool connected =
                            _controllerService.RefreshControllerConnection();

                        if (connected)
                        {
                            _controllerService.UpdateInput();

                            bool dpadDown =
                                _controllerService.IsButtonPressed(
                                    ControllerButton.DpadDown);

                            bool dpadUp =
                                _controllerService.IsButtonPressed(
                                    ControllerButton.DpadUp);

                            bool buttonA =
                                _controllerService.IsButtonPressed(
                                    ControllerButton.South);

                            if (desktop.MainWindow is MainWindow window)
                            {
                                if (dpadDown && !_previousDpadDown)
                                    window.ControllerMoveSidebarSelection(1);

                                if (dpadUp && !_previousDpadUp)
                                    window.ControllerMoveSidebarSelection(-1);

                                if (buttonA && !_previousButtonA)
                                    window.ControllerActivateSidebarSelection();
                            }

                            _previousDpadDown = dpadDown;
                            _previousDpadUp = dpadUp;
                            _previousButtonA = buttonA;
                        }
                        else
                        {
                            _previousDpadDown = false;
                            _previousDpadUp = false;
                            _previousButtonA = false;
                        }

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


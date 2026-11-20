
using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using ArcadeHub.Services;
using ArcadeHub.Models;
using Avalonia.Threading;

namespace ArcadeHub;

public sealed class App : Application
{
    private Process? _windowsBridgeProcess;
    private ControllerService? _controllerService;
    private ControllerBridgeService? _controllerBridgeService;
    private DispatcherTimer? _controllerTimer;
    private bool? _lastControllerConnected;

    private void StartWindowsControllerBridge()
    {
        if (!OperatingSystem.IsLinux() ||
            (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_INTEROP")) &&
             string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"))))
            return;

        const string bridgePath =
            @"E:\ArcadeHub Project\ControllerBridge\ControllerBridge.ps1";

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(bridgePath);

            _windowsBridgeProcess = Process.Start(start);

            Console.WriteLine(
                "[ArcadeHub] Windows controller bridge launch requested");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[ArcadeHub] Could not start Windows controller bridge: {ex.Message}");
        }
    }

    private void StopWindowsControllerBridge()
    {
        try
        {
            if (_windowsBridgeProcess is { HasExited: false } process)
            {
                process.Kill();
                process.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[ArcadeHub] Could not stop Windows controller bridge: {ex.Message}");
        }
        finally
        {
            _windowsBridgeProcess?.Dispose();
            _windowsBridgeProcess = null;
        }
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            bool runningInWsl =
                OperatingSystem.IsLinux() &&
                (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_INTEROP")) ||
                 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME")));

            if (runningInWsl)
            {
                _controllerBridgeService = new ControllerBridgeService();
                _controllerBridgeService.Start();
                StartWindowsControllerBridge();
            }

            // Unified Windows-to-WSL controller input handler.
            var controllerInputHandler = new ControllerInputHandler();

            controllerInputHandler.InputPressed += input =>
            {
                if (desktop.MainWindow is MainWindow window)
                    window.ControllerHandleInput(input);
            };

            if (_controllerBridgeService is not null)
            _controllerBridgeService.ControllerStateReceived += values =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var state = ControllerInputState.FromValues(values);
                        controllerInputHandler.Process(state);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[ArcadeHub] Controller input error: {ex.Message}");
                    }
                });
            };

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

                            if (!runningInWsl)
                            {
                                var state = SdlControllerStateAdapter.Read(
                                    _controllerService);

                                controllerInputHandler.Process(state);
                            }
                        }
                        else
                        {
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
                StopWindowsControllerBridge();
                _controllerBridgeService?.Dispose();
                _controllerBridgeService = null;
            };

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}


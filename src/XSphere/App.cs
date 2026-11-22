
using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using XSphere.Services;
using XSphere.Models;
using Avalonia.Threading;

namespace XSphere;

public sealed class App : Application
{
    private Process? _windowsBridgeProcess;
    private ControllerService? _controllerService;
    private ControllerBridgeService? _controllerBridgeService;
    private DispatcherTimer? _controllerTimer;
    private bool? _lastControllerConnected;

    private static string? ResolveWindowsControllerBridgePath()
    {
        var configured = Environment.GetEnvironmentVariable(
            "XSPHERE_CONTROLLER_BRIDGE");

        if (!string.IsNullOrWhiteSpace(configured) &&
            File.Exists(configured))
            return configured;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "ControllerBridge",
                "ControllerBridge.ps1");

            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        foreach (var candidate in new[]
        {
            "/mnt/e/XSphere Project/ControllerBridge/ControllerBridge.ps1",
            "/mnt/e/ArcadeHub Project/ControllerBridge/ControllerBridge.ps1"
        })
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private void StartWindowsControllerBridge()
    {
        if (!OperatingSystem.IsLinux() ||
            (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_INTEROP")) &&
             string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"))))
            return;

        var bridgePath = ResolveWindowsControllerBridgePath();

        if (bridgePath is null)
        {
            Console.WriteLine(
                "[XSphere] Windows controller bridge script not found.");
            return;
        }

        try
        {
            if (bridgePath.StartsWith("/", StringComparison.Ordinal))
            {
                var conversion = new ProcessStartInfo("wslpath")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                conversion.ArgumentList.Add("-w");
                conversion.ArgumentList.Add(bridgePath);

                using var converter = Process.Start(conversion)
                    ?? throw new InvalidOperationException(
                        "Could not start wslpath.");

                var windowsPath = converter.StandardOutput
                    .ReadToEnd().Trim();

                converter.WaitForExit();

                if (converter.ExitCode != 0 ||
                    string.IsNullOrWhiteSpace(windowsPath))
                    throw new InvalidOperationException(
                        "Could not convert controller bridge path.");

                bridgePath = windowsPath;
            }

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
                "[XSphere] Windows controller bridge launch requested");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[XSphere] Could not start Windows controller bridge: {ex.Message}");
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
                $"[XSphere] Could not stop Windows controller bridge: {ex.Message}");
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
                            $"[XSphere] Controller input error: {ex.Message}");
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
                    $"[XSphere] Controllers detected: {controllers.Count}");

                foreach (var controller in controllers)
                {
                    Console.WriteLine(
                        $"[XSphere] Controller: {controller}");
                }
            }
            catch (Exception ex) when (
                ex is DllNotFoundException ||
                ex is EntryPointNotFoundException ||
                ex is InvalidOperationException ||
                ex is BadImageFormatException)
            {
                Console.WriteLine(
                    $"[XSphere] Controller support unavailable: {ex.Message}");

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
                                $"[XSphere] Controller connected: {connected}");

                            _lastControllerConnected = connected;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[XSphere] Controller monitoring stopped: {ex.Message}");

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


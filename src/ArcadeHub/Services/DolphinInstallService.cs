using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ArcadeHub.Services;

public sealed class DolphinInstallService
{
    public async Task InstallOnWindowsAsync(
        bool userApproved,
        CancellationToken cancellationToken = default)
    {
        if (!userApproved)
            throw new InvalidOperationException(
                "Installation requires user approval.");

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Windows installation requires Windows.");

        var versionCheck = new ProcessStartInfo
        {
            FileName = "winget",
            UseShellExecute = false
        };

        versionCheck.ArgumentList.Add("--version");

        using (var check = Process.Start(versionCheck)
            ?? throw new InvalidOperationException(
                "Could not start Windows Package Manager."))
        {
            await check.WaitForExitAsync(cancellationToken);

            if (check.ExitCode != 0)
                throw new InvalidOperationException(
                    "Windows Package Manager is unavailable.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "winget",
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("--id");
        startInfo.ArgumentList.Add("DolphinEmulator.Dolphin");
        startInfo.ArgumentList.Add("--exact");
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add("winget");
        startInfo.ArgumentList.Add("--accept-source-agreements");
        startInfo.ArgumentList.Add("--accept-package-agreements");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start Dolphin installation.");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Dolphin Windows installation failed with exit code {process.ExitCode}.");
        }
    }

    public async Task InstallOnLinuxAsync(
        bool userApproved,
        CancellationToken cancellationToken = default)
    {
        if (!userApproved)
            throw new InvalidOperationException(
                "Installation requires user approval.");

        if (!OperatingSystem.IsLinux() ||
            EmulatorPlatformService.IsWsl)
        {
            throw new PlatformNotSupportedException(
                "Flatpak installation requires native Linux.");
        }

        if (!await FlatpakService.IsAvailableAsync(cancellationToken))
            throw new InvalidOperationException("Flatpak is not installed.");

        if (await FlatpakService.IsAppInstalledAsync(
            FlatpakService.DolphinAppId, cancellationToken))
            return;

        string executable = FlatpakService.FindExecutable()
            ?? throw new InvalidOperationException("Flatpak executable not found.");

        async Task<(int ExitCode, string Error)> TryInstallAsync(string scope)
        {
            var info = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            info.ArgumentList.Add("install");
            info.ArgumentList.Add(scope);
            info.ArgumentList.Add("--noninteractive");
            info.ArgumentList.Add("-y");
            info.ArgumentList.Add("flathub");
            info.ArgumentList.Add(FlatpakService.DolphinAppId);

            using var process = Process.Start(info)
                ?? throw new InvalidOperationException(
                    "Could not start Flatpak installation.");

            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            return (process.ExitCode,
                (await errorTask + "\\n" + await outputTask).Trim());
        }

        var userResult = await TryInstallAsync("--user");

        if (userResult.ExitCode == 0 &&
            await FlatpakService.IsAppInstalledAsync(
                FlatpakService.DolphinAppId, cancellationToken))
            return;

        var systemResult = await TryInstallAsync("--system");

        if (systemResult.ExitCode == 0 &&
            await FlatpakService.IsAppInstalledAsync(
                FlatpakService.DolphinAppId, cancellationToken))
            return;

        throw new InvalidOperationException(
            "Dolphin installation failed. " +
            "User installation: " + userResult.Error +
            " System installation: " + systemResult.Error);
    }
}

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

        if (!await FlatpakService.IsAvailableAsync(
            cancellationToken))
        {
            throw new InvalidOperationException(
                "Flatpak is not installed.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak",
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("--user");
        startInfo.ArgumentList.Add("--noninteractive");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("flathub");
        startInfo.ArgumentList.Add(
            FlatpakService.DolphinAppId);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start Flatpak installation.");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Dolphin installation failed with exit code {process.ExitCode}.");
        }
    }
}

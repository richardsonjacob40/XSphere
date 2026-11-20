using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArcadeHub.Services;

public sealed class EmulatorFlatpakInstallService
{
    public async Task InstallAsync(
        string emulatorId,
        bool userApproved,
        CancellationToken cancellationToken = default)
    {
        if (!userApproved)
            throw new InvalidOperationException(
                "Installation requires explicit user approval.");

        if (!OperatingSystem.IsLinux() ||
            EmulatorPlatformService.IsWsl)
        {
            throw new PlatformNotSupportedException(
                "Flatpak installation requires native Linux.");
        }

        var emulator = EmulatorDownloadCatalog.Emulators
            .FirstOrDefault(item =>
                string.Equals(
                    item.Id,
                    emulatorId,
                    StringComparison.OrdinalIgnoreCase));

        if (emulator is null)
            throw new ArgumentException(
                "Emulator is not in the approved catalog.",
                nameof(emulatorId));

        if (emulator.FlatpakPublisherVerified != true)
            throw new InvalidOperationException(
                "This emulator's Flatpak package has not been verified. Use its official website instead.");

        if (string.IsNullOrWhiteSpace(emulator.FlatpakAppId))
            throw new InvalidOperationException(
                "No Flatpak application ID is configured.");

        string executable =
            FlatpakService.FindExecutable()
            ?? throw new InvalidOperationException(
                "Flatpak executable was not found.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("--user");
        startInfo.ArgumentList.Add("--noninteractive");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("flathub");
        startInfo.ArgumentList.Add(emulator.FlatpakAppId);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start Flatpak installation.");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{emulator.Name} installation failed with exit code {process.ExitCode}.");
        }
    }
}

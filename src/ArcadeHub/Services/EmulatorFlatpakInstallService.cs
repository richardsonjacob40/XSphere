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

        if (await FlatpakService.IsAppInstalledAsync(
            emulator.FlatpakAppId, cancellationToken))
            return;

        async Task<(int Code, string Message)> InstallInScopeAsync(string scope)
        {
            var info = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            info.ArgumentList.Add("install");
            info.ArgumentList.Add(scope);
            info.ArgumentList.Add("--noninteractive");
            info.ArgumentList.Add("-y");
            info.ArgumentList.Add("flathub");
            info.ArgumentList.Add(emulator.FlatpakAppId);

            using var process = Process.Start(info)
                ?? throw new InvalidOperationException(
                    "Could not start Flatpak installation.");

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            string output = await outputTask;
            string error = await errorTask;

            return (process.ExitCode, (error + "\\n" + output).Trim());
        }

        var user = await InstallInScopeAsync("--user");

        if (user.Code == 0 &&
            await FlatpakService.IsAppInstalledAsync(
                emulator.FlatpakAppId, cancellationToken))
            return;

        var system = await InstallInScopeAsync("--system");

        if (system.Code == 0 &&
            await FlatpakService.IsAppInstalledAsync(
                emulator.FlatpakAppId, cancellationToken))
            return;

        throw new InvalidOperationException(
            $"{emulator.Name} installation failed. " +
            $"User: {user.Message} System: {system.Message}");
    }
}

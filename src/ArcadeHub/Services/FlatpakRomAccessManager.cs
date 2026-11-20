using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace ArcadeHub.Services;

public static class FlatpakRomAccessManager
{
    public static async Task<bool> RequestAndGrantAsync(
        Window owner,
        string romDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux() ||
            EmulatorPlatformService.IsWsl)
        {
            throw new PlatformNotSupportedException(
                "Flatpak ROM access requires native Linux.");
        }

        bool approved =
            await FlatpakRomAccessConfirmation.RequestAsync(
                owner, romDirectory);

        if (!approved)
            return false;

        ProcessStartInfo command =
            FlatpakRomAccessService.CreateAccessCommand(
                FlatpakService.DolphinAppId,
                romDirectory);

        command.RedirectStandardOutput = true;
        command.RedirectStandardError = true;

        using var process = Process.Start(command)
            ?? throw new InvalidOperationException(
                "Could not start Flatpak permission command.");

        string errors = await process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Could not grant Dolphin ROM access: " + errors);
        }

        return true;
    }
}

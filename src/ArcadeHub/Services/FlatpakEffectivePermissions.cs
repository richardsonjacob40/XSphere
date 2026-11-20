using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ArcadeHub.Services;

public static class FlatpakEffectivePermissions
{
    public static async Task<string> GetAsync(
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux() ||
            EmulatorPlatformService.IsWsl)
        {
            throw new PlatformNotSupportedException(
                "Flatpak permission inspection requires native Linux.");
        }

        string executable = FlatpakService.FindExecutable()
            ?? throw new InvalidOperationException(
                "Flatpak executable not found.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("info");
        startInfo.ArgumentList.Add("--show-permissions");
        startInfo.ArgumentList.Add(
            FlatpakService.DolphinAppId);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not inspect Dolphin permissions.");

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync(
                cancellationToken);

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync(
                cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string output = await outputTask;
        string errors = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Could not read Dolphin permissions: " + errors);
        }

        return output;
    }
}

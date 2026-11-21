using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ArcadeHub.Services;

public static class FlatpakPermissionInspector
{
    public static async Task<string> GetOverridesAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetOverridesAsync(
            FlatpakService.DolphinAppId,
            cancellationToken);
    }

    public static async Task<string> GetOverridesAsync(
        string appId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException(
                "Flatpak application ID is required.",
                nameof(appId));

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

        startInfo.ArgumentList.Add("override");
        startInfo.ArgumentList.Add("--user");
        startInfo.ArgumentList.Add("--show");
        startInfo.ArgumentList.Add(appId);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not inspect Flatpak permissions.");

        string output = await process.StandardOutput.ReadToEndAsync(
            cancellationToken);

        string errors = await process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Flatpak permission inspection failed: " + errors);
        }

        return output;
    }
}

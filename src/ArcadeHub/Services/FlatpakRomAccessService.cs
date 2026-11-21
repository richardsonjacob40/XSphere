using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ArcadeHub.Services;

public static class FlatpakRomAccessService
{
    public static ProcessStartInfo CreateAccessCommand(
        string appId,
        string romDirectory)
    {
        if (!OperatingSystem.IsLinux() ||
            EmulatorPlatformService.IsWsl)
        {
            throw new PlatformNotSupportedException(
                "Flatpak ROM access requires native Linux.");
        }

        bool supported = EmulatorDownloadCatalog.Emulators
            .Any(emulator =>
                string.Equals(
                    emulator.FlatpakAppId,
                    appId,
                    StringComparison.OrdinalIgnoreCase));

        if (!supported)
            throw new ArgumentException(
                "Unrecognized emulator Flatpak application.",
                nameof(appId));

        string directory = Path.GetFullPath(romDirectory);

        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(
                $"ROM directory not found: {directory}");

        string executable = FlatpakService.FindExecutable()
            ?? throw new InvalidOperationException(
                "Flatpak executable not found.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("override");
        startInfo.ArgumentList.Add("--user");
        startInfo.ArgumentList.Add(
            "--filesystem=" + directory);
        startInfo.ArgumentList.Add(appId);

        return startInfo;
    }
}

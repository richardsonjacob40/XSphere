using System;
using System.Collections.Generic;
using System.IO;

namespace XSphere.Services;

public static class DolphinExecutableLocator
{
    public static async System.Threading.Tasks.Task<string?> FindExecutableAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        var candidates = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            string programFiles =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles);

            string localAppData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            candidates.Add(Path.Combine(
                EmulatorInstallPaths.GetEmulatorDirectory("dolphin"),
                "Dolphin.exe"));

            candidates.Add(Path.Combine(
                programFiles, "Dolphin-x64", "Dolphin.exe"));

            candidates.Add(Path.Combine(
                programFiles, "Dolphin", "Dolphin.exe"));

            candidates.Add(Path.Combine(
                localAppData, "Programs", "Dolphin",
                "Dolphin.exe"));
        }
        else if (OperatingSystem.IsLinux())
        {
            candidates.Add(Path.Combine(
                EmulatorInstallPaths.GetEmulatorDirectory("dolphin"),
                "dolphin-emu"));

            candidates.Add("/usr/bin/dolphin-emu");
            candidates.Add("/usr/bin/dolphin");
            candidates.Add("/usr/local/bin/dolphin-emu");

            if (!EmulatorPlatformService.IsWsl)
            {
                string? flatpak =
                    FlatpakService.FindExecutable();

                if (flatpak is not null &&
                    await FlatpakService.IsAppInstalledAsync(
                        FlatpakService.DolphinAppId,
                        cancellationToken))
                {
                    candidates.Add(flatpak);
                }
            }
        }

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static bool IsFlatpakExecutable(string executable)
    {
        return OperatingSystem.IsLinux() &&
               string.Equals(
                   executable,
                   FlatpakService.FindExecutable(),
                   StringComparison.Ordinal);
    }
}

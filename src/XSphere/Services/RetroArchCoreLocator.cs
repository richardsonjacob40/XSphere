using System;
using System.Collections.Generic;
using System.IO;

namespace XSphere.Services;

public static class RetroArchCoreLocator
{
    public static string? FindCore(
        string system,
        string? customCoreDirectory = null)
    {
        string? coreName =
            RetroArchCoreService.GetDefaultCore(system);

        if (coreName is null)
            return null;

        string extension = OperatingSystem.IsWindows()
            ? ".dll"
            : OperatingSystem.IsMacOS()
                ? ".dylib"
                : ".so";

        string fileName = coreName + "_libretro" + extension;

        foreach (string directory in GetCoreDirectories(
            customCoreDirectory))
        {
            string candidate = Path.Combine(
                directory, fileName);

            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public static IReadOnlyList<string> GetCoreDirectories(
        string? customCoreDirectory = null)
    {
        var directories = new List<string>();

        if (!string.IsNullOrWhiteSpace(customCoreDirectory))
            directories.Add(customCoreDirectory);

        string home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            directories.Add(Path.Combine(
                EmulatorInstallPaths.GetEmulatorDirectory("retroarch"),
                "cores"));

            directories.Add(Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "RetroArch", "cores"));
        }
        else if (OperatingSystem.IsLinux())
        {
            directories.Add(Path.Combine(
                home, ".config", "retroarch", "cores"));

            directories.Add(Path.Combine(
                home, ".var", "app",
                "org.libretro.RetroArch",
                "config", "retroarch", "cores"));

            directories.Add("/usr/lib/libretro");
            directories.Add("/usr/lib64/libretro");
            directories.Add("/usr/lib/x86_64-linux-gnu/libretro");
        }

        return directories;
    }
}

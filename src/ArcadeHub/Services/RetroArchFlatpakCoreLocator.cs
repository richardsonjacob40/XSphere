using System;
using System.IO;

namespace ArcadeHub.Services;

public static class RetroArchFlatpakCoreLocator
{
    public const string AppId = "org.libretro.RetroArch";

    public static string GetCoreDirectory()
    {
        string home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        return Path.Combine(
            home,
            ".var",
            "app",
            AppId,
            "config",
            "retroarch",
            "cores");
    }

    public static string? FindCore(string system)
    {
        if (!OperatingSystem.IsLinux() ||
            EmulatorPlatformService.IsWsl)
        {
            return null;
        }

        string? coreName =
            RetroArchCoreService.GetDefaultCore(system);

        if (coreName is null)
            return null;

        string candidate = Path.Combine(
            GetCoreDirectory(),
            coreName + "_libretro.so");

        return File.Exists(candidate)
            ? Path.GetFullPath(candidate)
            : null;
    }

    public static bool IsCorePresent(string system)
    {
        return FindCore(system) is not null;
    }
}

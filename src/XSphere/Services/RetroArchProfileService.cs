using System;
using XSphere.Models;

namespace XSphere.Services;

public static class RetroArchProfileService
{
    public static EmulatorProfile CreateProfile(
        string system,
        string executablePath,
        bool isFlatpak = false)
    {
        if (!RetroArchCoreService.IsSupportedSystem(system))
            throw new ArgumentException(
                $"No default RetroArch core is configured for '{system}'.",
                nameof(system));

        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException(
                "RetroArch executable path is required.",
                nameof(executablePath));

        if (isFlatpak &&
            (!OperatingSystem.IsLinux() ||
             EmulatorPlatformService.IsWsl))
        {
            throw new PlatformNotSupportedException(
                "Automatic Flatpak profiles require native Linux.");
        }

        return new EmulatorProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "RetroArch",
            System = system.Trim(),
            ExecutablePath = executablePath,
            ArgumentsTemplate = isFlatpak
                ? "run org.libretro.RetroArch -L \"{core}\" \"{rom}\""
                : "-L \"{core}\" \"{rom}\""
        };
    }
}

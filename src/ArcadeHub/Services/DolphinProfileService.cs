using System;
using ArcadeHub.Models;

namespace ArcadeHub.Services;

public static class DolphinProfileService
{
    public static EmulatorProfile CreateProfile(
        string system,
        string executablePath)
    {
        if (system != "GameCube" && system != "Wii")
            throw new ArgumentException(
                "Dolphin supports GameCube and Wii profiles.",
                nameof(system));

        if (!OperatingSystem.IsWindows() &&
            (!OperatingSystem.IsLinux() ||
             EmulatorPlatformService.IsWsl))
        {
            throw new PlatformNotSupportedException(
                "Automatic Dolphin profiles require Windows or native Linux.");
        }

        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException(
                "Dolphin executable path is required.",
                nameof(executablePath));

        bool isFlatpak =
            DolphinExecutableLocator.IsFlatpakExecutable(
                executablePath);

        return new EmulatorProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Dolphin",
            System = system,
            ExecutablePath = executablePath,
            ArgumentsTemplate = isFlatpak
                ? "run org.DolphinEmu.dolphin-emu -b -e \"{rom}\""
                : "-b -e \"{rom}\""
        };
    }
}

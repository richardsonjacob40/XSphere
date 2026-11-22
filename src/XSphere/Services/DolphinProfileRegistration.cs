using System;
using System.Collections.Generic;
using System.Linq;
using XSphere.Models;

namespace XSphere.Services;

public static class DolphinProfileRegistration
{
    public static async System.Threading.Tasks.Task<int> RegisterMissingProfilesAsync(
        ArcadeDatabase database,
        IReadOnlyCollection<EmulatorProfile> existingProfiles,
        string? actingProfileId,
        System.Threading.CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(existingProfiles);

        if ((!OperatingSystem.IsWindows() &&
             !OperatingSystem.IsLinux()) ||
            EmulatorPlatformService.IsWsl)
        {
            throw new PlatformNotSupportedException(
                "Automatic Dolphin profiles require Windows or native Linux.");
        }

        var installed = new List<EmulatorProfile>(
            existingProfiles);

        string[] systems = ["GameCube", "Wii"];

        var missingSystems = systems
            .Where(system => !installed.Any(profile =>
                string.Equals(
                    profile.System,
                    system,
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (missingSystems.Length == 0)
            return 0;

        string executable =
            await DolphinExecutableLocator.FindExecutableAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Dolphin executable was not found. Install Dolphin before registering profiles.");

        int created = 0;

        foreach (string system in missingSystems)
        {
            EmulatorProfile profile =
                DolphinProfileService.CreateProfile(
                    system, executable);

            database.SaveEmulator(
                profile, actingProfileId);

            installed.Add(profile);
            created++;
        }

        return created;
    }
}

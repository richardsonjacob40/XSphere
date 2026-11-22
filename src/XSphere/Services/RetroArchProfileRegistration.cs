using System;
using System.Collections.Generic;
using System.Linq;
using XSphere.Models;

namespace XSphere.Services;

public static class RetroArchProfileRegistration
{
    public static int RegisterMissingProfiles(
        ArcadeDatabase database,
        IReadOnlyCollection<EmulatorProfile> existingProfiles,
        string? actingProfileId,
        string executablePath,
        bool isFlatpak = false)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(existingProfiles);

        if (string.IsNullOrWhiteSpace(executablePath) ||
            !System.IO.File.Exists(executablePath))
        {
            throw new System.IO.FileNotFoundException(
                "RetroArch executable was not found.",
                executablePath);
        }

        // Avoid replacing user-configured profiles.
        var configuredSystems = new HashSet<string>(
            existingProfiles.Select(profile => profile.System),
            StringComparer.OrdinalIgnoreCase);

        int created = 0;

        foreach (string system in RetroArchCoreService
            .GetMappings().Keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (configuredSystems.Contains(system))
                continue;

            // Register only systems with a detected core.
            // Flatpak core paths still require launch-time validation.
            bool corePresent = isFlatpak
                ? RetroArchFlatpakCoreLocator.IsCorePresent(system)
                : RetroArchCoreLocator.FindCore(system) is not null;

            if (!corePresent)
                continue;

            var profile = RetroArchProfileService.CreateProfile(
                system, executablePath, isFlatpak);

            database.SaveEmulator(profile, actingProfileId);

            configuredSystems.Add(system);
            created++;
        }

        return created;
    }
}

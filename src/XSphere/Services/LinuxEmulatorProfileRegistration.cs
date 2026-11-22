using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XSphere.Models;

namespace XSphere.Services;

public static class LinuxEmulatorProfileRegistration
{
    public static async Task<int> RegisterAsync(
        ArcadeDatabase database,
        string? actingProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!OperatingSystem.IsLinux() ||
            EmulatorPlatformService.IsWsl)
            return 0;

        var discovered =
            await LinuxEmulatorDiscoveryService.DiscoverAsync(
                cancellationToken);

        var existing = database.GetEmulators();
        int created = 0;

        foreach (var installation in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (installation.Id == "retroarch")
                continue;

            string[] systems = installation.Id == "dolphin"
                ? ["GameCube", "Wii"]
                : [installation.System];

            foreach (string system in systems)
            {
                bool alreadyConfigured = existing.Any(profile =>
                    string.Equals(
                        profile.System,
                        system,
                        StringComparison.OrdinalIgnoreCase));

                if (alreadyConfigured)
                    continue;

                var profile = new EmulatorProfile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = installation.Name,
                    System = system,
                    ExecutablePath = installation.ExecutablePath,
                    ArgumentsTemplate = installation.ArgumentsTemplate
                };

                database.SaveEmulator(profile, actingProfileId);
                existing.Add(profile);
                created++;
            }
        }

        return created;
    }
}

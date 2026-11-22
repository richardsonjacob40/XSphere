using System;
using System.IO;

namespace XSphere.Services;

public static class RetroArchArgumentResolver
{
    public static string Resolve(
        string template,
        string system,
        string? customCoreDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException(
                "Launch arguments cannot be empty.",
                nameof(template));

        if (!template.Contains(
            "{core}", StringComparison.OrdinalIgnoreCase))
        {
            return template;
        }

        bool isFlatpak =
            template.Contains(
                "run org.libretro.RetroArch",
                StringComparison.OrdinalIgnoreCase);

        string? corePath = isFlatpak
            ? RetroArchFlatpakCoreLocator.FindCore(system)
            : RetroArchCoreLocator.FindCore(
                system, customCoreDirectory);

        if (isFlatpak && corePath is not null)
        {
            // Flatpak exposes its persistent application configuration
            // under ~/.config inside the sandbox. The host-side
            // ~/.var/app/<app-id>/config directory maps to that location.
            corePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".config",
                "retroarch",
                "cores",
                Path.GetFileName(corePath));
        }

        if (corePath is null)
        {
            string? suggestedCore =
                RetroArchCoreService.GetDefaultCore(system);

            if (suggestedCore is null)
            {
                throw new InvalidOperationException(
                    $"No default RetroArch core is configured for '{system}'. Edit the emulator's launch arguments to specify a core.");
            }

            throw new FileNotFoundException(
                $"RetroArch core '{suggestedCore}' was not found for '{system}'. Install the core in RetroArch or configure its core directory.");
        }

        return template.Replace(
            "{core}",
            corePath,
            StringComparison.OrdinalIgnoreCase);
    }
}

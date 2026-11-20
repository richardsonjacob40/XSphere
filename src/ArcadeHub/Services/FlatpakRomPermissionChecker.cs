using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArcadeHub.Services;

public static class FlatpakRomPermissionChecker
{
    public static async Task<bool> HasUserOverrideAsync(
        string romDirectory,
        CancellationToken cancellationToken = default)
    {
        string directory = Path.GetFullPath(romDirectory);

        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(
                $"ROM directory not found: {directory}");

        string overrides =
            await FlatpakPermissionInspector.GetOverridesAsync(
                cancellationToken);

        string? filesystemsLine = overrides
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line =>
                line.StartsWith(
                    "filesystems=",
                    StringComparison.OrdinalIgnoreCase));

        if (filesystemsLine is null)
            return false;

        string[] permissions = filesystemsLine
            ["filesystems=".Length..]
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (string permission in permissions)
        {
            string entry = permission.Trim();

            if (entry.StartsWith("!", StringComparison.Ordinal))
                continue;

            string grantedPath = entry.Split(':')[0];

            if (!Path.IsPathFullyQualified(grantedPath))
                continue;

            string normalized = Path.GetFullPath(grantedPath);

            if (string.Equals(
                    directory,
                    normalized,
                    StringComparison.Ordinal) ||
                directory.StartsWith(
                    Path.TrimEndingDirectorySeparator(normalized) +
                    Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

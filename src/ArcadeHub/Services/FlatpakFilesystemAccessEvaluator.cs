using System;
using System.Collections.Generic;
using System.IO;
using ArcadeHub.Models;

namespace ArcadeHub.Services;

public static class FlatpakFilesystemAccessEvaluator
{
    public static bool HasReadAccess(
        string romDirectory,
        IReadOnlyList<FlatpakFilesystemPermission> permissions)
    {
        return HasAccess(romDirectory, permissions, "read");
    }

    public static bool HasWriteAccess(
        string directory,
        IReadOnlyList<FlatpakFilesystemPermission> permissions)
    {
        return HasAccess(directory, permissions, "write");
    }

    public static bool HasCreateAccess(
        string directory,
        IReadOnlyList<FlatpakFilesystemPermission> permissions)
    {
        return HasAccess(directory, permissions, "create");
    }

    private static bool HasAccess(
        string directory,
        IReadOnlyList<FlatpakFilesystemPermission> permissions,
        string requestedAccess)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        directory = Path.GetFullPath(directory);

        string home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        int bestSpecificity = -1;
        bool granted = false;

        foreach (var permission in permissions)
        {
            string location = permission.Location;
            int specificity = -1;

            if (location == "host")
            {
                specificity = 0;
            }
            else if (location == "home" &&
                     !string.IsNullOrEmpty(home) &&
                     IsWithin(directory, home))
            {
                specificity = Path.GetFullPath(home).Length;
            }
            else if (location.StartsWith(
                         "xdg-",
                         StringComparison.Ordinal))
            {
                string? resolved =
                    FlatpakXdgDirectoryResolver.Resolve(location);

                if (resolved is not null &&
                    IsWithin(directory, resolved))
                {
                    specificity = resolved.Length;
                }
            }
            else if (Path.IsPathFullyQualified(location) &&
                     IsWithin(directory, location))
            {
                specificity = Path.GetFullPath(location).Length;
            }

            if (specificity < 0)
                continue;

            bool allowsAccess = !permission.IsDenied &&
                requestedAccess switch
                {
                    "read" => permission.AccessMode != "create",
                    "write" => permission.AccessMode == "rw",
                    "create" => permission.AccessMode is "rw" or "create",
                    _ => false
                };

            if (specificity > bestSpecificity)
            {
                bestSpecificity = specificity;
                granted = allowsAccess;
            }
            else if (specificity == bestSpecificity &&
                     !allowsAccess)
            {
                granted = false;
            }
        }

        return granted;
    }

    private static bool IsWithin(
        string directory,
        string parent)
    {
        string normalizedParent =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(parent));

        return string.Equals(
                   directory,
                   normalizedParent,
                   StringComparison.Ordinal) ||
               directory.StartsWith(
                   normalizedParent +
                   Path.DirectorySeparatorChar,
                   StringComparison.Ordinal);
    }
}

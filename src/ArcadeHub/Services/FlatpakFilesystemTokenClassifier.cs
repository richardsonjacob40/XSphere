using System;
using System.IO;

namespace ArcadeHub.Services;

public static class FlatpakFilesystemTokenClassifier
{
    public static bool IsAbsolutePath(string location)
    {
        return Path.IsPathFullyQualified(location);
    }

    public static bool IsBroadHostToken(string location)
    {
        return location is "host" or "host-os" or "host-etc";
    }

    public static bool IsHomeToken(string location)
    {
        return string.Equals(
            location,
            "home",
            StringComparison.Ordinal);
    }

    public static bool IsXdgToken(string location)
    {
        return location.StartsWith(
            "xdg-",
            StringComparison.Ordinal);
    }

    public static bool IsRecognizedToken(string location)
    {
        return IsAbsolutePath(location) ||
               IsBroadHostToken(location) ||
               IsHomeToken(location) ||
               IsXdgToken(location);
    }
}

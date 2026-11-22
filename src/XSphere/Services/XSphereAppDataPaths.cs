using System;
using System.IO;

namespace XSphere.Services;

public static class XSphereAppDataPaths
{
    public static string LocalApplicationData
    {
        get
        {
            var resolved = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

            if (!string.IsNullOrWhiteSpace(resolved) &&
                Path.IsPathFullyQualified(resolved))
                return resolved;

            if (OperatingSystem.IsLinux())
            {
                var xdgDataHome = Environment.GetEnvironmentVariable(
                    "XDG_DATA_HOME");

                if (!string.IsNullOrWhiteSpace(xdgDataHome) &&
                    Path.IsPathFullyQualified(xdgDataHome))
                    return xdgDataHome;

                var home = Environment.GetEnvironmentVariable("HOME");

                if (!string.IsNullOrWhiteSpace(home) &&
                    Path.IsPathFullyQualified(home))
                    return Path.Combine(home, ".local", "share");
            }

            throw new InvalidOperationException(
                "Could not resolve a safe, absolute application data directory.");
        }
    }

    public static string XSphereDirectory =>
        Path.Combine(LocalApplicationData, "XSphere");
}

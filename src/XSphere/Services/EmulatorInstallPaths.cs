using System;
using System.IO;

namespace XSphere.Services;

public static class EmulatorInstallPaths
{
    public static string RootDirectory =>
        Path.Combine(
            XSphereAppDataPaths.LocalApplicationData,
            "XSphere",
            "Emulators",
            EmulatorPlatformService.PlatformId);

    public static string DownloadsDirectory =>
        Path.Combine(
            XSphereAppDataPaths.LocalApplicationData,
            "XSphere",
            "Downloads",
            EmulatorPlatformService.PlatformId);

    public static string EnsureDownloadsDirectory()
    {
        Directory.CreateDirectory(DownloadsDirectory);
        return DownloadsDirectory;
    }

    public static string GetEmulatorDirectory(string emulatorId)
    {
        if (string.IsNullOrWhiteSpace(emulatorId) ||
            emulatorId is "." or ".." ||
            emulatorId.IndexOfAny(
                Path.GetInvalidFileNameChars()) >= 0 ||
            emulatorId.Contains('/') ||
            emulatorId.Contains('\\'))
        {
            throw new ArgumentException(
                "Invalid emulator identifier.",
                nameof(emulatorId));
        }

        return Path.Combine(RootDirectory, emulatorId);
    }

    public static string EnsureEmulatorDirectory(string emulatorId)
    {
        string directory = GetEmulatorDirectory(emulatorId);
        Directory.CreateDirectory(directory);
        return directory;
    }
}

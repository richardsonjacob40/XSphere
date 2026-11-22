using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XSphere.Services;

public sealed record LinuxEmulatorInstallation(
    string Id,
    string Name,
    string System,
    string InstallationType,
    string ExecutablePath,
    string ArgumentsTemplate);

public static class LinuxEmulatorDiscoveryService
{
    private static readonly Dictionary<string, string[]> NativeNames = new()
    {
        ["retroarch"] = ["retroarch"],
        ["dolphin"] = ["dolphin-emu"],
        ["pcsx2"] = ["pcsx2", "PCSX2"],
        ["rpcs3"] = ["rpcs3", "RPCS3"],
        ["ppsspp"] = ["PPSSPPSDL", "ppsspp"],
        ["mgba"] = ["mgba", "mGBA"],
        ["melonds"] = ["melonDS", "melonds"],
        ["azahar"] = ["azahar", "Azahar"]
    };

    public static async Task<IReadOnlyList<LinuxEmulatorInstallation>>
        DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<LinuxEmulatorInstallation>();

        if (!OperatingSystem.IsLinux() ||
            EmulatorPlatformService.IsWsl)
            return results;

        var installed = await FlatpakService.GetInstalledAppIdsAsync(
            cancellationToken);

        string? flatpak = FlatpakService.FindExecutable();

        string home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        string[] nativeDirectories =
        [
            "/usr/bin",
            "/usr/local/bin",
            Path.Combine(home, ".local", "bin")
        ];

        string[] appImageDirectories =
        [
            Path.Combine(home, "Applications"),
            Path.Combine(home, "Downloads"),
            Path.Combine(home, ".local", "bin")
        ];

        foreach (var emulator in EmulatorDownloadCatalog.Emulators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (flatpak is not null &&
                !string.IsNullOrWhiteSpace(emulator.FlatpakAppId) &&
                installed.Contains(emulator.FlatpakAppId))
            {
                results.Add(new LinuxEmulatorInstallation(
                    emulator.Id,
                    emulator.Name,
                    emulator.System,
                    "Flatpak",
                    flatpak,
                    $"run {emulator.FlatpakAppId} {emulator.DefaultArguments}"
                ));
            }

            if (!NativeNames.TryGetValue(emulator.Id, out var names))
                continue;

            foreach (string directory in nativeDirectories)
            {
                foreach (string name in names)
                {
                    string executable = Path.Combine(directory, name);

                    if (!File.Exists(executable))
                        continue;

                    if (results.Any(r =>
                        r.Id == emulator.Id &&
                        r.ExecutablePath == executable))
                        continue;

                    results.Add(new LinuxEmulatorInstallation(
                        emulator.Id,
                        emulator.Name,
                        emulator.System,
                        "Native",
                        executable,
                        emulator.DefaultArguments
                    ));
                }
            }

            foreach (string directory in appImageDirectories)
            {
                if (!Directory.Exists(directory))
                    continue;

                foreach (string file in Directory.EnumerateFiles(
                    directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string filename = Path.GetFileName(file);

                    if (!filename.EndsWith(
                        ".AppImage", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!names.Any(name =>
                        filename.Contains(
                            name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    if (results.Any(r =>
                        r.Id == emulator.Id &&
                        r.ExecutablePath == file))
                        continue;

                    results.Add(new LinuxEmulatorInstallation(
                        emulator.Id,
                        emulator.Name,
                        emulator.System,
                        "AppImage",
                        file,
                        emulator.DefaultArguments
                    ));
                }
            }
        }

        return results;
    }
}

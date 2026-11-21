using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ArcadeHub.Services;

public static class FlatpakService
{
    public const string DolphinAppId =
        "org.DolphinEmu.dolphin-emu";

    public static string? FindExecutable()
    {
        if (!OperatingSystem.IsLinux())
            return null;

        string[] candidates =
        [
            "/usr/bin/flatpak",
            "/usr/local/bin/flatpak"
        ];

        foreach (string candidate in candidates)
        {
            if (System.IO.File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static async Task<System.Collections.Generic.HashSet<string>>
        GetInstalledAppIdsAsync(
            CancellationToken cancellationToken = default)
    {
        var installed = new System.Collections.Generic.HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        string? executable = FindExecutable();

        if (executable is null)
            return installed;

        foreach (string scope in new[] { "--user", "--system" })
        {
            var info = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            info.ArgumentList.Add("list");
            info.ArgumentList.Add(scope);
            info.ArgumentList.Add("--app");
            info.ArgumentList.Add("--columns=application");

            try
            {
                using var process = Process.Start(info);

                if (process is null)
                    continue;

                Task<string> outputTask =
                    process.StandardOutput.ReadToEndAsync();

                Task<string> errorTask =
                    process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                string output = await outputTask;
                await errorTask;

                if (process.ExitCode != 0)
                    continue;

                foreach (string line in output.Split(
                    '\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    string appId = line.Trim();

                    if (!string.IsNullOrWhiteSpace(appId))
                        installed.Add(appId);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                continue;
            }
        }

        return installed;
    }

    public static async Task<bool> IsAppInstalledAsync(
        string appId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException(
                "Flatpak application ID is required.",
                nameof(appId));

        string? executable = FindExecutable();

        if (executable is null)
            return false;

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("info");
        startInfo.ArgumentList.Add(appId);

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
                return false;

            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static async Task<bool> IsAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
            return false;

        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("--version");

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
                return false;

            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

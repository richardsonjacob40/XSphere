using System.Diagnostics;
using XSphere.Models;

namespace XSphere.Services;

public sealed class GameLauncher
{
    public Process Launch(Game game, IReadOnlyCollection<EmulatorProfile> profiles)
    {
        var emulator = profiles.FirstOrDefault(p =>
            string.Equals(p.System, game.System, StringComparison.OrdinalIgnoreCase));

        emulator ??= profiles.FirstOrDefault(p =>
            string.Equals(p.System, "Any / Default", StringComparison.OrdinalIgnoreCase));

        if (emulator is null)
            throw new InvalidOperationException($"No emulator profile is configured for {game.System}. Add one in the Emulators section.");

        if (!File.Exists(emulator.ExecutablePath))
            throw new FileNotFoundException("The configured emulator executable could not be found. Check its path in Emulators.", emulator.ExecutablePath);

        if (!File.Exists(game.FilePath))
            throw new FileNotFoundException("The ROM file could not be found. It may have been moved or renamed.", game.FilePath);

        var template = string.IsNullOrWhiteSpace(emulator.ArgumentsTemplate)
    ? "\"{rom}\""
    : emulator.ArgumentsTemplate;

var includesRom = template.Contains(
    "{rom}", StringComparison.OrdinalIgnoreCase);

// Windows emulators require Windows paths, not WSL paths.
var romPath = game.FilePath;

if (OperatingSystem.IsLinux() &&
    (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_INTEROP")) ||
     !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"))) &&
    emulator.ExecutablePath.EndsWith(
        ".exe", StringComparison.OrdinalIgnoreCase))
{
    var conversion = new ProcessStartInfo
    {
        FileName = "wslpath",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    conversion.ArgumentList.Add("-w");
    conversion.ArgumentList.Add(game.FilePath);

    using var process = Process.Start(conversion)
        ?? throw new InvalidOperationException(
            "Could not start WSL path conversion.");

    romPath = process.StandardOutput.ReadToEnd().Trim();
    process.WaitForExit();

    if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(romPath))
    {
        throw new InvalidOperationException(
            "Could not convert the ROM path to a Windows path.");
    }
}

var resolvedTemplate = RetroArchArgumentResolver.Resolve(
    template, game.System);

var rendered = resolvedTemplate.Replace(
    "{rom}", romPath, StringComparison.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo
        {
            FileName = emulator.ExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(emulator.ExecutablePath) ?? Environment.CurrentDirectory
        };

        foreach (var argument in SplitArguments(rendered)) startInfo.ArgumentList.Add(argument);
        if (!includesRom) startInfo.ArgumentList.Add(romPath);

        var launchedProcess = Process.Start(startInfo);
Console.WriteLine(
    $"[XSphere] Started process: {startInfo.FileName}");

Console.WriteLine(
    $"[XSphere] Process ID: {launchedProcess?.Id}");

if (launchedProcess is null)
{
    throw new InvalidOperationException(
        "The emulator process could not be started.");
}

return launchedProcess;
    }

    // Splits a command-line template while preserving text inside double quotes.
    private static IEnumerable<string> SplitArguments(string commandLine)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];

            // Only treat a backslash as an escape before a quote or another backslash.
            // Ordinary Windows path separators must be preserved.
            if (character == '\\' && index + 1 < commandLine.Length
                && (commandLine[index + 1] == '"' || commandLine[index + 1] == '\\'))
            {
                current.Append(commandLine[++index]);
                continue;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }
}

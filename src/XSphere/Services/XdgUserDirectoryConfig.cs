using System;
using System.Collections.Generic;
using System.IO;

namespace XSphere.Services;

public static class XdgUserDirectoryConfig
{
    public static IReadOnlyDictionary<string, string> Read()
    {
        var directories = new Dictionary<string, string>(
            StringComparer.Ordinal);

        string home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(home))
            return directories;

        string configHome = Environment.GetEnvironmentVariable(
            "XDG_CONFIG_HOME") ?? Path.Combine(home, ".config");

        string configPath = Path.Combine(
            configHome, "user-dirs.dirs");

        if (!File.Exists(configPath))
            return directories;

        foreach (string rawLine in File.ReadLines(configPath))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 ||
                line.StartsWith('#'))
                continue;

            int separator = line.IndexOf('=');

            if (separator <= 0)
                continue;

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();

            if (!key.StartsWith("XDG_", StringComparison.Ordinal) ||
                !key.EndsWith("_DIR", StringComparison.Ordinal))
                continue;

            if (value.Length >= 2 &&
                value[0] == '"' &&
                value[^1] == '"')
            {
                value = value[1..^1];
            }

            value = value.Replace("$HOME", home);

            if (!Path.IsPathFullyQualified(value))
                continue;

            directories[key] = Path.GetFullPath(value);
        }

        return directories;
    }
}

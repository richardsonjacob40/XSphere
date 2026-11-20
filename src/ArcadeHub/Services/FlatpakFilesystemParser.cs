using System;
using System.Collections.Generic;

namespace ArcadeHub.Services;

public static class FlatpakFilesystemParser
{
    public static IReadOnlyList<string> Parse(string permissions)
    {
        var result = new List<string>();
        bool inContext = false;

        foreach (string rawLine in permissions.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inContext = string.Equals(
                    line,
                    "[Context]",
                    StringComparison.OrdinalIgnoreCase);

                continue;
            }

            if (!inContext ||
                !line.StartsWith(
                    "filesystems=",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = line["filesystems=".Length..];

            foreach (string entry in value.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            {
                result.Add(entry);
            }
        }

        return result;
    }
}

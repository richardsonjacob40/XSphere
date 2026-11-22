
using XSphere.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace XSphere.Services;

public static class GameGrouping
{
    public static List<Game> SelectPreferredGames(IEnumerable<Game> games)
    {
        return games
           .Where(g => File.Exists(g.FilePath))
           .GroupBy(g => (
                System: ConsoleDetector.Detect(g.FilePath),
                Title: NormalizeTitle(g.Title)
            ))
            .Select(group =>
            {
                var preferred = group
                    .OrderBy(g => FilePriority(g.FilePath))
                    .ThenBy(g => PathDepth(g.FilePath))
                    .ThenBy(g => g.FilePath,
                        StringComparer.OrdinalIgnoreCase)
                    .First();

                // Update the displayed console using its ROM folder.
                preferred.System = group.Key.System;

                return preferred;
            })
            .OrderBy(g => g.Title,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Trim();

        // Remove the format suffix from Xbox XISO filenames.
        normalized = Regex.Replace(
            normalized,
            @"\s+xiso$",
            "",
            RegexOptions.IgnoreCase);

        return normalized.ToLowerInvariant();
    }

    private static int FilePriority(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".iso" or ".rvz" or ".gcm" or ".chd" => 0,
            ".gcz" or ".cso" or ".ciso" => 1,
            ".zip" or ".7z" => 3,
            _ => 2
        };
    }

    private static int PathDepth(string path)
    {
        return path.Replace('\\', '/')
            .Count(c => c == '/');
    }
}
   

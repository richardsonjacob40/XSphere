using XSphere.Models;

namespace XSphere.Services;

public sealed class RomScanner
{
    private static readonly HashSet<string> SupportedExtensions =
    new(StringComparer.OrdinalIgnoreCase)
    {
        // Nintendo
        ".nes", ".sfc", ".smc",
        ".gb", ".gbc", ".gba",
        ".n64", ".z64", ".v64",
        ".nds", ".3ds", ".cia",

        // Disc-based systems
        ".iso", ".chd", ".cue",
        ".cso", ".pbp", ".wbfs",
        ".rvz", ".gcm", ".gcz",
        ".wia", ".ciso"
    };

    public List<Game> ScanFolder(string rootFolder)
    {
        var found = new List<Game>();
        if (!Directory.Exists(rootFolder)) return found;

        var pending = new Stack<string>();
        pending.Push(rootFolder);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    try { pending.Push(directory); }
                    catch (UnauthorizedAccessException) { /* Skip folders the user cannot access. */ }
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    var extension = Path.GetExtension(file);
                    if (!SupportedExtensions.Contains(extension)) continue;

                    var fullPath = Path.GetFullPath(file);
                    var title = CleanGameTitle(file);

                    found.Add(new Game
                    {
                        Title = title,
                        System = ConsoleDetector.Detect(fullPath),
                        FilePath = fullPath
                    });
                }
            }
            catch (UnauthorizedAccessException) { /* Continue scanning other folders. */ }
            catch (IOException) { /* A removable/network drive may disappear during a scan. */ }
        }
        return found;
    }


    // GAME TITLE CLEANER
    private static string CleanGameTitle(string filePath)
    {
        var title = Path.GetFileNameWithoutExtension(filePath);

        // Replace common filename separators.
        title = title.Replace('_', ' ').Replace('.', ' ');

        // Remove standard ROM metadata in parentheses.
        title = System.Text.RegularExpressions.Regex.Replace(
            title,
            @"\s*\((?:USA|Europe|Japan|World|Germany|France|Spain|Italy|Australia|Korea|Rev\s*\d+|Revision\s*\d+|En|Fr|De|Es|It|En,Fr,De|En,Fr,De,Es,It)[^)]*\)",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove common dump codes and metadata in brackets.
        title = System.Text.RegularExpressions.Regex.Replace(
            title,
            @"\s*\[(?:[A-Z0-9]{4,12}|!|b\d*|a\d*|h\d*|f\d*|t\d*)\]",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

  // Remove trailing ISO-related filename labels.
title = System.Text.RegularExpressions.Regex.Replace(
    title,
    @"(?:\s|[-_.])+(?:ISOX|ISO\s+X|ISO)(?=\s*$)",
    "",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

// Remove ISOX tags, including surrounding parentheses.
title = System.Text.RegularExpressions.Regex.Replace(
    title,
    @"\s*(?:\(\s*ISOX\s*\)|\[\s*ISOX\s*\]|\bISOX\b)",
    "",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
// Remove Xbox ISO labels, including bracketed versions.
title = System.Text.RegularExpressions.Regex.Replace(
    title,
    @"\s*(?:\(\s*(?:XISO|ISOX)\s*\)|\[\s*(?:XISO|ISOX)\s*\]|\b(?:XISO|ISOX)\b)",
    "",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

// Remove version tags such as (0v2 00).
title = System.Text.RegularExpressions.Regex.Replace(
    title,
    @"\s*\(\s*\d+v\d+\s+\d+\s*\)",
    "",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove excessive whitespace.
        title = System.Text.RegularExpressions.Regex.Replace(
            title,
            @"\s+",
            " ").Trim();

        return string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(filePath)
            : title;
    }

    private static string GuessSystem(string filePath, string extension)
    {
        var normalizedPath = filePath.Replace('_', ' ').Replace('-', ' ').ToLowerInvariant();
        string[] hints =
        [
            "playstation 3", "ps3", "playstation 2", "ps2", "playstation 1", "ps1", "psx",
            "playstation portable", "psp", "gamecube", "game cube", "wii", "xbox", "switch",
            "nintendo 64", "n64", "super nintendo", "snes", "nes", "game boy advance", "gba",
            "game boy color", "gbc", "game boy", "gb", "nintendo ds", "nds", "3ds"
        ];

        foreach (var hint in hints)
        {
            if (!normalizedPath.Contains(hint, StringComparison.Ordinal)) continue;
            return hint switch
            {
                "playstation 3" or "ps3" => "PlayStation 3",
                "playstation 2" or "ps2" => "PlayStation 2",
                "playstation 1" or "ps1" or "psx" => "PlayStation",
                "playstation portable" or "psp" => "PlayStation Portable",
                "gamecube" or "game cube" => "GameCube",
                "wii" => "Wii",
                "xbox" => "Xbox",
                "switch" => "Nintendo Switch",
                "nintendo 64" or "n64" => "Nintendo 64",
                "super nintendo" or "snes" => "Super Nintendo",
                "nes" => "NES",
                "game boy advance" or "gba" => "Game Boy Advance",
                "game boy color" or "gbc" => "Game Boy Color",
                "game boy" or "gb" => "Game Boy",
                "nintendo ds" or "nds" => "Nintendo DS",
                "3ds" => "Nintendo 3DS",
                _ => "Unknown"
            };
        }

        return extension.ToLowerInvariant() switch
        {
            ".nes" => "NES",
            ".sfc" or ".smc" => "Super Nintendo",
            ".gb" => "Game Boy",
            ".gbc" => "Game Boy Color",
            ".gba" => "Game Boy Advance",
            ".n64" or ".z64" or ".v64" => "Nintendo 64",
            ".nds" => "Nintendo DS",
            ".3ds" or ".cia" => "Nintendo 3DS",
            ".cso" or ".pbp" => "PlayStation Portable",
            _ => "Unknown"
        };
    }
}


using System;
using System.IO;
using System.Collections.Generic;

namespace XSphere.Services;

public static class ConsoleDetector
{
    private static readonly Dictionary<string, string> FolderSystems =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["NES Games"] = "NES",
            ["SNES Games"] = "Super Nintendo",
            ["N64 Games"] = "Nintendo 64",
            ["GB Games"] = "Game Boy",
            ["GBC Games"] = "Game Boy Color",
            ["GBA Games"] = "Game Boy Advance",
            ["NDS Games"] = "Nintendo DS",
            ["DS Games"] = "Nintendo DS",
            ["3DS Games"] = "Nintendo 3DS",
            ["GameCube Games"] = "GameCube",
            ["Wii Games"] = "Wii",
            ["Switch Games"] = "Nintendo Switch",

            ["PS1 Games"] = "PlayStation",
            ["PSX Games"] = "PlayStation",
            ["PS2 Games"] = "PlayStation 2",
            ["PS3 Games"] = "PlayStation 3",
            ["PSP Games"] = "PlayStation Portable",
            ["PS Vita Games"] = "PlayStation Vita",

            ["Xbox Games"] = "Xbox",
            ["Xbox360 Games"] = "Xbox 360",
            ["Xbox 360 Games"] = "Xbox 360",

            ["Sega Genesis Games"] = "Sega Genesis",
            ["Genesis Games"] = "Sega Genesis",
            ["Dreamcast Games"] = "Dreamcast",
            ["Saturn Games"] = "Sega Saturn",
            ["Game Gear Games"] = "Game Gear"
        };

    public static string Detect(string romPath)
    {
        var directory = Path.GetDirectoryName(
            Path.GetFullPath(romPath));

        while (!string.IsNullOrEmpty(directory))
        {
            var folderName = Path.GetFileName(
                Path.TrimEndingDirectorySeparator(directory));

            if (FolderSystems.TryGetValue(
                folderName, out var system))
            {
                return system;
            }

            var parent = Path.GetDirectoryName(directory);
            if (parent == directory) break;

            directory = parent;
        }

        return "Unknown";
    }
}

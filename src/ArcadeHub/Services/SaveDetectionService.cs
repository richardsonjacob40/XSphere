using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArcadeHub.Services;

public sealed class SaveDetectionService
{
    public sealed record SaveLocation(
        string Emulator,
        string Category,
        string Directory,
        int FileCount
    );

    public List<SaveLocation> Detect()
    {
        var results = new List<SaveLocation>();

        var windowsUser = "/mnt/c/Users/Jacob";

        var dolphin = Path.Combine(
            windowsUser,
            "AppData/Roaming/Dolphin Emulator");

        var retroarch = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".config/retroarch");

        var rpcs3 = "/mnt/e/Rpcs3/"
            + "rpcs3-v0.0.43-20559-a2c2ca2f_win64_msvc";

        Add(results, "Dolphin", "GameCube",
            Path.Combine(dolphin, "GC"));

        Add(results, "Dolphin", "Wii",
            Path.Combine(dolphin, "Wii/title"));

        Add(results, "Dolphin", "Save States",
            Path.Combine(dolphin, "StateSaves"));

        Add(results, "RetroArch", "Game Saves",
            Path.Combine(retroarch, "saves"));

        Add(results, "RetroArch", "Save States",
            Path.Combine(retroarch, "states"));

        Add(results, "RPCS3", "PS3 Saves",
            Path.Combine(rpcs3,
                "dev_hdd0/home/00000001/savedata"));

        return results;
    }

    private static void Add(
        List<SaveLocation> results,
        string emulator,
        string category,
        string directory)
    {
        if (!System.IO.Directory.Exists(directory))
            return;

        try
        {
            var count = System.IO.Directory
                .EnumerateFiles(
                    directory,
                    "*",
                    SearchOption.AllDirectories)
                .Count();

            results.Add(new SaveLocation(
                emulator,
                category,
                directory,
                count));
        }
        catch (IOException)
        {
            // Skip inaccessible or unavailable directories.
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories without permission.
        }
    }
}

using System;
using System.Collections.Generic;

namespace XSphere.Services;

public static class RetroArchCoreService
{
    private static readonly Dictionary<string, string> CoreMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["NES"] = "fceumm",
            ["Super Nintendo"] = "snes9x",
            ["SNES"] = "snes9x",
            ["Nintendo 64"] = "mupen64plus_next",
            ["Game Boy"] = "gambatte",
            ["Game Boy Color"] = "gambatte",
            ["Game Boy Advance"] = "mgba",
            ["Nintendo DS"] = "melonds",
            ["PlayStation"] = "beetle_psx",
            ["PlayStation Portable"] = "ppsspp",
            ["Sega Genesis"] = "genesis_plus_gx",
            ["Genesis"] = "genesis_plus_gx",
            ["Arcade"] = "fbneo"
        };

    public static string? GetDefaultCore(string system)
    {
        if (string.IsNullOrWhiteSpace(system))
            return null;

        return CoreMappings.TryGetValue(system.Trim(), out var core)
            ? core
            : null;
    }

    public static IReadOnlyDictionary<string, string> GetMappings()
    {
        return CoreMappings;
    }

    public static bool IsSupportedSystem(string system)
    {
        return GetDefaultCore(system) is not null;
    }
}

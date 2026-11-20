using ArcadeHub.Models;
using System.Collections.Generic;

namespace ArcadeHub.Services;

public static class EmulatorDownloadCatalog
{
    public static IReadOnlyList<EmulatorDownloadInfo> Emulators { get; } =
    [
        new()
        {
            Id = "retroarch",
            Name = "RetroArch",
            System = "Multi-system",
            OfficialWebsite = "https://www.retroarch.com/",
            FlatpakAppId = "org.libretro.RetroArch",
            FlatpakPublisherVerified = true,
            DefaultArguments = "-L \"{core}\" \"{rom}\""
        },
        new()
        {
            Id = "dolphin",
            Name = "Dolphin",
            System = "GameCube / Wii",
            OfficialWebsite = "https://dolphin-emu.org/download/",
            FlatpakAppId = "org.DolphinEmu.dolphin-emu",
            FlatpakPublisherVerified = false,
            DefaultArguments = "-b -e \"{rom}\""
        },
        new()
        {
            Id = "pcsx2",
            Name = "PCSX2",
            System = "PlayStation 2",
            OfficialWebsite = "https://pcsx2.net/downloads/",
            FlatpakAppId = "net.pcsx2.PCSX2",
            DefaultArguments = "-batch \"{rom}\""
        },
        new()
        {
            Id = "rpcs3",
            Name = "RPCS3",
            System = "PlayStation 3",
            OfficialWebsite = "https://rpcs3.net/download",
            FlatpakAppId = "net.rpcs3.RPCS3",
            FlatpakPublisherVerified = false,
            DefaultArguments = "\"{rom}\""
        },
        new()
        {
            Id = "ppsspp",
            Name = "PPSSPP",
            System = "PSP",
            OfficialWebsite = "https://www.ppsspp.org/download/",
            FlatpakAppId = "org.ppsspp.PPSSPP",
            DefaultArguments = "\"{rom}\""
        },
        new()
        {
            Id = "mgba",
            Name = "mGBA",
            System = "Game Boy Advance",
            OfficialWebsite = "https://mgba.io/downloads.html",
            FlatpakAppId = "io.mgba.mGBA",
            FlatpakPublisherVerified = false,
            DefaultArguments = "\"{rom}\""
        },
        new()
        {
            Id = "melonds",
            Name = "melonDS",
            System = "Nintendo DS",
            OfficialWebsite = "https://melonds.kuribo64.net/downloads.php",
            FlatpakAppId = "net.kuribo64.melonDS",
            DefaultArguments = "\"{rom}\""
        }
    ];
}

using System;
using System.Collections.Generic;
using System.IO;

namespace ArcadeHub.Services;

public static class FlatpakXdgDirectoryResolver
{
    public static string? Resolve(string token)
    {
        string home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(home))
            return null;

        var directories = new Dictionary<string, (string Key, string Folder)>(
            StringComparer.Ordinal)
        {
            ["xdg-desktop"] = ("XDG_DESKTOP_DIR", "Desktop"),
            ["xdg-documents"] = ("XDG_DOCUMENTS_DIR", "Documents"),
            ["xdg-download"] = ("XDG_DOWNLOAD_DIR", "Downloads"),
            ["xdg-music"] = ("XDG_MUSIC_DIR", "Music"),
            ["xdg-pictures"] = ("XDG_PICTURES_DIR", "Pictures"),
            ["xdg-public-share"] = ("XDG_PUBLICSHARE_DIR", "Public"),
            ["xdg-templates"] = ("XDG_TEMPLATES_DIR", "Templates"),
            ["xdg-videos"] = ("XDG_VIDEOS_DIR", "Videos")
        };

        if (!directories.TryGetValue(token, out var mapping))
            return null;

        var configured = XdgUserDirectoryConfig.Read();

        string path = configured.TryGetValue(
            mapping.Key, out string? configuredPath)
            ? configuredPath
            : Path.Combine(home, mapping.Folder);

        return Directory.Exists(path)
            ? Path.GetFullPath(path)
            : null;
    }
}

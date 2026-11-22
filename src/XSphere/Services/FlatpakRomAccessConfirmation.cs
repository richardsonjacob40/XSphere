using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace XSphere.Services;

public static class FlatpakRomAccessConfirmation
{
    public static async Task<bool> RequestAsync(
        Window owner,
        string romDirectory)
    {
        if (string.IsNullOrWhiteSpace(romDirectory))
            throw new ArgumentException(
                "ROM directory cannot be empty.",
                nameof(romDirectory));

        string directory = Path.GetFullPath(romDirectory);

        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(
                $"ROM directory not found: {directory}");

        return await EmulatorInstallConfirmation.ShowAsync(
            owner,
            "Dolphin ROM Access",
            "Flatpak",
            confirmButtonText: "Allow Access",
            customMessage:
                "Dolphin needs permission to read ROM files in:\n\n" +
                directory +
                "\n\nAllowing access will grant the Dolphin Flatpak " +
                "filesystem access to this directory and its contents. " +
                "The permission persists until revoked.\n\n" +
                "Cancel will leave permissions unchanged.");
    }
}

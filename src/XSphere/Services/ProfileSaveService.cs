using System;
using System.IO;
using XSphere.Models;

namespace XSphere.Services;

public sealed class ProfileSaveService
{
    private readonly string _saveRoot;

    public ProfileSaveService()
    {
        _saveRoot = Path.Combine(
            XSphereDataMigration.XSphereDirectory,
            "Saves");
    }

    public string GetProfileSaveDirectory(UserProfile profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        // Use the profile ID to prevent conflicts
        // between profiles with identical names.
        var directory = Path.Combine(
            _saveRoot,
            profile.Id);

        Directory.CreateDirectory(directory);

        return directory;
    }

    public string GetGameSaveDirectory(
        UserProfile profile,
        Game game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        var profileDirectory =
            GetProfileSaveDirectory(profile);

        // Use stable identifiers instead of game titles
        // to avoid invalid folder characters.
        var directory = Path.Combine(
            profileDirectory,
            game.Id);

        Directory.CreateDirectory(directory);

        return directory;
    }
}

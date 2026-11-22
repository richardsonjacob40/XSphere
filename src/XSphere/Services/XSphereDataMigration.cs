using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace XSphere.Services;

public static class XSphereDataMigration
{
    public static string AppDataRoot =>
        XSphereAppDataPaths.LocalApplicationData;

    // Explicit override for isolated migration testing.
    // Normal application startup uses AppDataRoot.
    public static void MigrateFromRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException(
                "A test data directory is required.", nameof(root));

        var fullRoot = Path.GetFullPath(root);
        MigrateDirectories(
            Path.Combine(fullRoot, "ArcadeHub"),
            Path.Combine(fullRoot, "XSphere"));
    }

    public static string LegacyDirectory =>
        Path.Combine(AppDataRoot, "ArcadeHub");

    public static string XSphereDirectory =>
        Path.Combine(AppDataRoot, "XSphere");

    public static void Migrate() =>
        MigrateDirectories(LegacyDirectory, XSphereDirectory);

    private static void MigrateDirectories(
        string legacyDirectory,
        string xsphereDirectory)
    {
        if (!Directory.Exists(legacyDirectory))
            return;

        Directory.CreateDirectory(xsphereDirectory);

        var legacyDatabase =
            Path.Combine(legacyDirectory, "arcadehub.db");

        var newDatabase =
            Path.Combine(xsphereDirectory, "xsphere.db");

        if (File.Exists(legacyDatabase) &&
            !File.Exists(newDatabase))
        {
            var temporaryDatabase =
                Path.Combine(xsphereDirectory,
                    "xsphere-migration-" + Guid.NewGuid().ToString("N") + ".db");

            try
            {
                using (var source = new SqliteConnection(
                    new SqliteConnectionStringBuilder
                    {
                        DataSource = legacyDatabase,
                        Mode = SqliteOpenMode.ReadOnly
                    }.ToString()))
                using (var destination = new SqliteConnection(
                    new SqliteConnectionStringBuilder
                    {
                        DataSource = temporaryDatabase
                    }.ToString()))
                {
                    source.Open();
                    destination.Open();
                    source.BackupDatabase(destination);
                }

                File.Move(temporaryDatabase, newDatabase);
            }
            finally
            {
                // Remove only temporary migration files.
                // Never delete the original ArcadeHub database.
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                {
                    var temporaryFile = temporaryDatabase + suffix;

                    if (File.Exists(temporaryFile))
                        File.Delete(temporaryFile);
                }
            }
        }

        CopyIfMissing(legacyDirectory, xsphereDirectory, "settings.json");
        CopyDirectoryIfMissing(legacyDirectory, xsphereDirectory, "covers");
        CopyDirectoryIfMissing(legacyDirectory, xsphereDirectory, "Saves");
    }

    private static void CopyIfMissing(string legacyDirectory, string xsphereDirectory, string filename)
    {
        var source = Path.Combine(legacyDirectory, filename);
        var destination = Path.Combine(xsphereDirectory, filename);

        if (File.Exists(source) && !File.Exists(destination))
            File.Copy(source, destination);
    }

    private static void CopyDirectoryIfMissing(string legacyDirectory, string xsphereDirectory, string directoryName)
    {
        var source = Path.Combine(legacyDirectory, directoryName);
        var destination = Path.Combine(xsphereDirectory, directoryName);

        if (!Directory.Exists(source))
            return;

        CopyDirectory(source, destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var destinationFile =
                Path.Combine(destination, Path.GetFileName(file));

            if (!File.Exists(destinationFile))
                File.Copy(file, destinationFile);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}

using XSphere.Models;
using Microsoft.Data.Sqlite;

namespace XSphere.Services;

/// <summary>Small SQLite data layer. The database lives in the current user's app-data folder.</summary>
public sealed class ArcadeDatabase
{
    private readonly string _connectionString;

    public ArcadeDatabase(string? databasePath = null)
    {
        if (databasePath is null)
        {
            var folder = XSphereDataMigration.XSphereDirectory;
            Directory.CreateDirectory(folder);
            databasePath = Path.Combine(folder, "xsphere.db");
        }
        else
        {
            databasePath = Path.GetFullPath(databasePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(databasePath)!);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();

        Initialize();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS Games (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                System TEXT NOT NULL,
                FilePath TEXT NOT NULL COLLATE NOCASE UNIQUE,
                IsFavorite INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS Emulators (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                System TEXT NOT NULL,
                ExecutablePath TEXT NOT NULL,
                ArgumentsTemplate TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS GameMetadata (
                GameId TEXT PRIMARY KEY,
                Description TEXT NOT NULL,
                ReleaseDate TEXT NOT NULL,
                Developer TEXT NOT NULL,
                Publisher TEXT NOT NULL,
                Genre TEXT NOT NULL,
                SourceUrl TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS RomFolders (
                Path TEXT PRIMARY KEY COLLATE NOCASE
            );
            """;
        command.ExecuteNonQuery();

        using var profilesCommand = connection.CreateCommand();
        profilesCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS UserProfiles (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Avatar TEXT NOT NULL DEFAULT '🎮'
            );
            """;
        profilesCommand.ExecuteNonQuery();

        InitializeProfilePermissions(connection);

        // Migrate older databases without deleting existing profiles.
        var existingColumns = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText =
                "PRAGMA table_info(UserProfiles);";

            using var reader = schemaCommand.ExecuteReader();
            while (reader.Read())
                existingColumns.Add(reader.GetString(1));
        }

        var requiredColumns = new Dictionary<string, string>
        {
            ["IsAdmin"] = "INTEGER NOT NULL DEFAULT 0",
            ["PinHash"] = "TEXT",
            ["PinSalt"] = "TEXT",
            ["PinIterations"] = "INTEGER NOT NULL DEFAULT 0",
            ["FailedPinAttempts"] = "INTEGER NOT NULL DEFAULT 0",
            ["PinLockedUntilUtc"] = "TEXT"
        };

        foreach (var column in requiredColumns)
        {
            if (existingColumns.Contains(column.Key))
                continue;

            using var migration = connection.CreateCommand();
            migration.CommandText =
                $"ALTER TABLE UserProfiles ADD COLUMN {column.Key} {column.Value};";
            migration.ExecuteNonQuery();
        }
    }


    private void InitializeProfilePermissions(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ProfilePermissions (
                ProfileId TEXT PRIMARY KEY,
                CanLaunchGames INTEGER NOT NULL DEFAULT 1,
                CanManageEmulators INTEGER NOT NULL DEFAULT 1,
                CanModifySettings INTEGER NOT NULL DEFAULT 1,
                CanManageProfiles INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ProfileId) REFERENCES UserProfiles(Id)
            );
            """;
        command.ExecuteNonQuery();
    }

    public ProfilePermissions GetProfilePermissions(string profileId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT p.IsAdmin,
                   COALESCE(r.CanLaunchGames, 1),
                   COALESCE(r.CanManageEmulators, 1),
                   COALESCE(r.CanModifySettings, 1),
                   COALESCE(r.CanManageProfiles, 0)
            FROM UserProfiles p
            LEFT JOIN ProfilePermissions r ON r.ProfileId = p.Id
            WHERE p.Id = $id;
            """;

        command.Parameters.AddWithValue("$id", profileId);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
            throw new InvalidOperationException("Profile not found.");

        bool isAdmin = reader.GetInt64(0) != 0;

        return new ProfilePermissions
        {
            ProfileId = profileId,
            CanLaunchGames = isAdmin || reader.GetInt64(1) != 0,
            CanManageEmulators = isAdmin || reader.GetInt64(2) != 0,
            CanModifySettings = isAdmin || reader.GetInt64(3) != 0,
            CanManageProfiles = isAdmin || reader.GetInt64(4) != 0
        };
    }

    // Authenticates the administrator and saves permissions atomically.
    public bool ChangeProfilePermissions(
        string actingAdminId,
        string actingAdminPin,
        ProfilePermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        if (!ProfileSecurityService.IsValidPin(actingAdminPin))
            return false;

        if (string.IsNullOrWhiteSpace(actingAdminId) ||
            string.IsNullOrWhiteSpace(permissions.ProfileId) ||
            actingAdminId == permissions.ProfileId)
            return false;

        using var connection = OpenConnection();

        using (var begin = connection.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE;";
            begin.ExecuteNonQuery();
        }

        bool committed = false;

        try
        {
            string hash;
            string salt;
            int iterations;
            int attempts;
            string? lockedUntil;

            using (var lookup = connection.CreateCommand())
            {
                lookup.CommandText = """
                    SELECT PinHash, PinSalt, PinIterations,
                           FailedPinAttempts, PinLockedUntilUtc
                    FROM UserProfiles
                    WHERE Id = $id AND IsAdmin = 1;
                    """;

                lookup.Parameters.AddWithValue("$id", actingAdminId);

                using var reader = lookup.ExecuteReader();

                if (!reader.Read() ||
                    reader.IsDBNull(0) ||
                    reader.IsDBNull(1))
                    return false;

                hash = reader.GetString(0);
                salt = reader.GetString(1);
                iterations = reader.GetInt32(2);
                attempts = reader.GetInt32(3);
                lockedUntil = reader.IsDBNull(4)
                    ? null : reader.GetString(4);
            }

            var now = DateTimeOffset.UtcNow;

            if (lockedUntil is not null &&
                DateTimeOffset.TryParse(
                    lockedUntil, out var expiration) &&
                now < expiration)
                return false;

            if (lockedUntil is not null)
                attempts = 0;

            bool valid = ProfileSecurityService.VerifyPin(
                actingAdminPin, hash, salt, iterations);

            if (!valid)
            {
                attempts++;

                string? nextLockout = null;

                if (attempts >= 5)
                {
                    attempts = 0;
                    nextLockout = now.AddMinutes(5).ToString("O");
                }

                using (var failure = connection.CreateCommand())
                {
                    failure.CommandText = """
                        UPDATE UserProfiles
                        SET FailedPinAttempts = $attempts,
                            PinLockedUntilUtc = $locked
                        WHERE Id = $id AND IsAdmin = 1;
                        """;

                    failure.Parameters.AddWithValue(
                        "$attempts", attempts);
                    failure.Parameters.AddWithValue(
                        "$locked",
                        (object?)nextLockout ?? DBNull.Value);
                    failure.Parameters.AddWithValue(
                        "$id", actingAdminId);

                    failure.ExecuteNonQuery();
                }

                using (var commitFailure = connection.CreateCommand())
                {
                    commitFailure.CommandText = "COMMIT;";
                    commitFailure.ExecuteNonQuery();
                }

                committed = true;
                return false;
            }

            // Clear previous failures after successful verification.
            using (var reset = connection.CreateCommand())
            {
                reset.CommandText = """
                    UPDATE UserProfiles
                    SET FailedPinAttempts = 0,
                        PinLockedUntilUtc = NULL
                    WHERE Id = $id AND IsAdmin = 1;
                    """;

                reset.Parameters.AddWithValue("$id", actingAdminId);
                reset.ExecuteNonQuery();
            }

            // Administrator permissions cannot be modified here.
            using (var save = connection.CreateCommand())
            {
                save.CommandText = """
                    INSERT INTO ProfilePermissions
                        (ProfileId, CanLaunchGames,
                         CanManageEmulators, CanModifySettings,
                         CanManageProfiles)
                    SELECT Id, $games, $emulators, $settings, $profiles
                    FROM UserProfiles
                    WHERE Id = $target AND IsAdmin = 0
                    ON CONFLICT(ProfileId) DO UPDATE SET
                        CanLaunchGames = excluded.CanLaunchGames,
                        CanManageEmulators = excluded.CanManageEmulators,
                        CanModifySettings = excluded.CanModifySettings,
                        CanManageProfiles = excluded.CanManageProfiles;
                    """;

                save.Parameters.AddWithValue(
                    "$target", permissions.ProfileId);
                save.Parameters.AddWithValue(
                    "$games", permissions.CanLaunchGames ? 1 : 0);
                save.Parameters.AddWithValue(
                    "$emulators", permissions.CanManageEmulators ? 1 : 0);
                save.Parameters.AddWithValue(
                    "$settings", permissions.CanModifySettings ? 1 : 0);
                save.Parameters.AddWithValue(
                    "$profiles", permissions.CanManageProfiles ? 1 : 0);

                if (save.ExecuteNonQuery() != 1)
                    return false;
            }

            using (var commit = connection.CreateCommand())
            {
                commit.CommandText = "COMMIT;";
                commit.ExecuteNonQuery();
            }

            committed = true;
            return true;
        }
        finally
        {
            if (!committed)
            {
                using var rollback = connection.CreateCommand();
                rollback.CommandText = "ROLLBACK;";
                rollback.ExecuteNonQuery();
            }
        }
    }

    public List<UserProfile> GetUserProfiles()
    {
        var profiles = new List<UserProfile>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Name, Avatar, IsAdmin, PinHash FROM UserProfiles ORDER BY rowid;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            profiles.Add(new UserProfile
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Avatar = reader.GetString(2),
                IsAdmin = reader.GetInt64(3) != 0,
                HasPin = !reader.IsDBNull(4) &&
                    !string.IsNullOrEmpty(reader.GetString(4))
            });
        }
        return profiles;
    }

    public void SaveUserProfile(UserProfile profile)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO UserProfiles (Id, Name, Avatar)
            VALUES ($id, $name, $avatar)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Avatar = excluded.Avatar;
            """;
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$avatar", profile.Avatar);
        command.ExecuteNonQuery();
    }


    // Internal security storage operation.
    // Authorization must be checked before calling this method.
    // Create a standard profile and optional PIN in one transaction.
    // Administrator creation will use a separate authorized operation.
    public void CreateUserProfileWithPin(
        UserProfile profile,
        string? pin)
    {
        if (string.IsNullOrWhiteSpace(profile.Name) ||
            profile.Name.Length > 32)
        {
            throw new ArgumentException("Invalid profile name.");
        }

        if (profile.IsAdmin)
            throw new InvalidOperationException(
                "Administrator creation requires authorization.");

        if (!string.IsNullOrEmpty(pin) &&
            !ProfileSecurityService.IsValidPin(pin))
        {
            throw new ArgumentException(
                "PIN must contain 4 to 8 digits.");
        }

        var hashed = string.IsNullOrEmpty(pin)
            ? ((string Hash, string Salt, int Iterations)?)null
            : ProfileSecurityService.HashPin(pin);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO UserProfiles
                (Id, Name, Avatar, IsAdmin,
                 PinHash, PinSalt, PinIterations)
            VALUES
                ($id, $name, $avatar, 0,
                 $hash, $salt, $iterations);
            """;

        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$avatar", profile.Avatar);

        command.Parameters.AddWithValue(
            "$hash", (object?)hashed?.Hash ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$salt", (object?)hashed?.Salt ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$iterations", hashed?.Iterations ?? 0);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    // One-time first-administrator setup.
    // The selected profile must already have a valid PIN.
    // This method cannot run once any administrator exists.
    public bool BootstrapFirstAdministrator(
        string profileId,
        string pin)
    {
        if (!ProfileSecurityService.IsValidPin(pin))
            return false;

        using var connection = OpenConnection();

        using (var begin = connection.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE;";
            begin.ExecuteNonQuery();
        }

        bool committed = false;

        try
        {
            using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText =
                    "SELECT COUNT(*) FROM UserProfiles WHERE IsAdmin = 1;";

                if (Convert.ToInt64(countCommand.ExecuteScalar()) != 0)
                    return false;
            }

            string hash;
            string salt;
            int iterations;
            int attempts;
            string? lockedUntil;

            using (var lookup = connection.CreateCommand())
            {
                lookup.CommandText = """
                    SELECT PinHash, PinSalt, PinIterations,
                           FailedPinAttempts, PinLockedUntilUtc
                    FROM UserProfiles
                    WHERE Id = $id AND IsAdmin = 0;
                    """;

                lookup.Parameters.AddWithValue("$id", profileId);

                using var reader = lookup.ExecuteReader();

                if (!reader.Read() ||
                    reader.IsDBNull(0) ||
                    reader.IsDBNull(1))
                    return false;

                hash = reader.GetString(0);
                salt = reader.GetString(1);
                iterations = reader.GetInt32(2);
                attempts = reader.GetInt32(3);
                lockedUntil = reader.IsDBNull(4)
                    ? null : reader.GetString(4);
            }

            var now = DateTimeOffset.UtcNow;

            if (lockedUntil is not null &&
                DateTimeOffset.TryParse(
                    lockedUntil, out var expiration) &&
                now < expiration)
            {
                return false;
            }

            if (lockedUntil is not null)
                attempts = 0;

            bool valid = ProfileSecurityService.VerifyPin(
                pin, hash, salt, iterations);

            if (!valid)
            {
                attempts++;

                string? nextLockout = null;

                if (attempts >= 5)
                {
                    attempts = 0;
                    nextLockout = now.AddMinutes(5)
                        .ToString("O");
                }

                using (var failure = connection.CreateCommand())
                {
                    failure.CommandText = """
                        UPDATE UserProfiles
                        SET FailedPinAttempts = $attempts,
                            PinLockedUntilUtc = $locked
                        WHERE Id = $id AND IsAdmin = 0;
                        """;

                    failure.Parameters.AddWithValue(
                        "$attempts", attempts);

                    failure.Parameters.AddWithValue(
                        "$locked",
                        (object?)nextLockout ?? DBNull.Value);

                    failure.Parameters.AddWithValue("$id", profileId);
                    failure.ExecuteNonQuery();
                }

                using (var commit = connection.CreateCommand())
                {
                    commit.CommandText = "COMMIT;";
                    commit.ExecuteNonQuery();
                }

                committed = true;
                return false;
            }

            using (var update = connection.CreateCommand())
            {
                update.CommandText = """
                    UPDATE UserProfiles
                    SET IsAdmin = 1,
                        FailedPinAttempts = 0,
                        PinLockedUntilUtc = NULL
                    WHERE Id = $id AND IsAdmin = 0
                      AND NOT EXISTS (
                          SELECT 1 FROM UserProfiles
                          WHERE IsAdmin = 1
                      );
                    """;

                update.Parameters.AddWithValue("$id", profileId);

                if (update.ExecuteNonQuery() != 1)
                    return false;
            }

            using (var commit = connection.CreateCommand())
            {
                commit.CommandText = "COMMIT;";
                commit.ExecuteNonQuery();
            }

            committed = true;
            return true;
        }
        finally
        {
            if (!committed)
            {
                using var rollback = connection.CreateCommand();
                rollback.CommandText = "ROLLBACK;";
                rollback.ExecuteNonQuery();
            }
        }
    }

    // Administrator-authorized promotion and demotion.
    public bool ChangeAdministratorStatus(
        string actingAdminId,
        string actingAdminPin,
        string targetProfileId,
        bool makeAdmin)
    {
        if (!ProfileSecurityService.IsValidPin(actingAdminPin))
            return false;

        if (actingAdminId == targetProfileId)
            return false;

        using var connection = OpenConnection();

        using (var begin = connection.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE;";
            begin.ExecuteNonQuery();
        }

        bool committed = false;

        try
        {
            string? adminHash;
            string? adminSalt;
            int adminIterations;
            int failedAttempts;
            string? lockedUntil;

            using (var lookup = connection.CreateCommand())
            {
                lookup.CommandText = """
                    SELECT PinHash, PinSalt, PinIterations,
                           FailedPinAttempts, PinLockedUntilUtc
                    FROM UserProfiles
                    WHERE Id = $id AND IsAdmin = 1;
                    """;

                lookup.Parameters.AddWithValue(
                    "$id", actingAdminId);

                using var reader = lookup.ExecuteReader();

                if (!reader.Read() ||
                    reader.IsDBNull(0) ||
                    reader.IsDBNull(1))
                    return false;

                adminHash = reader.GetString(0);
                adminSalt = reader.GetString(1);
                adminIterations = reader.GetInt32(2);
                failedAttempts = reader.GetInt32(3);
                lockedUntil = reader.IsDBNull(4)
                    ? null : reader.GetString(4);
            }

            var now = DateTimeOffset.UtcNow;

            if (lockedUntil is not null &&
                DateTimeOffset.TryParse(
                    lockedUntil, out var expiration) &&
                now < expiration)
            {
                return false;
            }

            if (lockedUntil is not null)
                failedAttempts = 0;

            bool pinValid = ProfileSecurityService.VerifyPin(
                actingAdminPin,
                adminHash,
                adminSalt,
                adminIterations);

            if (!pinValid)
            {
                failedAttempts++;

                string? nextLockout = null;

                if (failedAttempts >= 5)
                {
                    failedAttempts = 0;
                    nextLockout = now.AddMinutes(5)
                        .ToString("O");
                }

                using (var failure = connection.CreateCommand())
                {
                    failure.CommandText = """
                        UPDATE UserProfiles
                        SET FailedPinAttempts = $attempts,
                            PinLockedUntilUtc = $locked
                        WHERE Id = $id AND IsAdmin = 1;
                        """;

                    failure.Parameters.AddWithValue(
                        "$attempts", failedAttempts);
                    failure.Parameters.AddWithValue(
                        "$locked",
                        (object?)nextLockout ?? DBNull.Value);
                    failure.Parameters.AddWithValue(
                        "$id", actingAdminId);

                    failure.ExecuteNonQuery();
                }

                using (var commit = connection.CreateCommand())
                {
                    commit.CommandText = "COMMIT;";
                    commit.ExecuteNonQuery();
                }

                committed = true;
                return false;
            }

            // A correct PIN clears previous failed attempts,
            // even if the requested administrator action is invalid.
            using (var reset = connection.CreateCommand())
            {
                reset.CommandText = """
                    UPDATE UserProfiles
                    SET FailedPinAttempts = 0,
                        PinLockedUntilUtc = NULL
                    WHERE Id = $id AND IsAdmin = 1;
                    """;

                reset.Parameters.AddWithValue(
                    "$id", actingAdminId);
                reset.ExecuteNonQuery();
            }

            // Keep authentication and the administrator change
            // in the same write transaction. The final COMMIT
            // saves both operations atomically.

            bool targetIsAdmin;
            bool targetHasPin;

            using (var targetLookup = connection.CreateCommand())
            {
                targetLookup.CommandText = """
                    SELECT IsAdmin, PinHash
                    FROM UserProfiles
                    WHERE Id = $id;
                    """;

                targetLookup.Parameters.AddWithValue(
                    "$id", targetProfileId);

                using var reader = targetLookup.ExecuteReader();

                if (!reader.Read())
                    return false;

                targetIsAdmin = reader.GetInt64(0) != 0;
                targetHasPin =
                    !reader.IsDBNull(1) &&
                    !string.IsNullOrEmpty(reader.GetString(1));
            }

            if (targetIsAdmin == makeAdmin)
                return false;

            if (makeAdmin && !targetHasPin)
                return false;

            if (!makeAdmin)
            {
                using var count = connection.CreateCommand();
                count.CommandText = """
                    SELECT COUNT(*)
                    FROM UserProfiles
                    WHERE IsAdmin = 1;
                    """;

                var administratorCount =
                    Convert.ToInt64(count.ExecuteScalar());

                if (administratorCount <= 1)
                    return false;
            }

            using (var update = connection.CreateCommand())
            {
                update.CommandText = """
                    UPDATE UserProfiles
                    SET IsAdmin = $isAdmin
                    WHERE Id = $id
                      AND IsAdmin = $expected;
                    """;

                update.Parameters.AddWithValue(
                    "$isAdmin", makeAdmin ? 1 : 0);

                update.Parameters.AddWithValue(
                    "$expected", makeAdmin ? 0 : 1);

                update.Parameters.AddWithValue(
                    "$id", targetProfileId);

                if (update.ExecuteNonQuery() != 1)
                    return false;
            }

            using (var commit = connection.CreateCommand())
            {
                commit.CommandText = "COMMIT;";
                commit.ExecuteNonQuery();
            }

            committed = true;
            return true;
        }
        finally
        {
            if (!committed)
            {
                using var rollback = connection.CreateCommand();
                rollback.CommandText = "ROLLBACK;";
                rollback.ExecuteNonQuery();
            }
        }
    }

    // Initial PIN setup only.
    // Existing PINs must be changed through ChangeProfilePin.
    public void SetProfilePin(string profileId, string pin)
    {
        var result = ProfileSecurityService.HashPin(pin);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE UserProfiles
            SET PinHash = $hash,
                PinSalt = $salt,
                PinIterations = $iterations
            WHERE Id = $id
              AND IsAdmin = 0
              AND PinHash IS NULL;
            """;

        command.Parameters.AddWithValue("$hash", result.Hash);
        command.Parameters.AddWithValue("$salt", result.Salt);
        command.Parameters.AddWithValue(
            "$iterations", result.Iterations);
        command.Parameters.AddWithValue("$id", profileId);

        if (command.ExecuteNonQuery() != 1)
            throw new InvalidOperationException(
                "Profile not found or PIN already exists.");
    }

    // Read-only lockout status for Settings feedback.
    public bool IsProfilePinLocked(string profileId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT PinLockedUntilUtc
            FROM UserProfiles
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", profileId);

        var value = command.ExecuteScalar();

        if (value is not string lockedUntil)
            return false;

        return DateTimeOffset.TryParse(
            lockedUntil,
            out var expiration)
            && DateTimeOffset.UtcNow < expiration;
    }

    public PinVerificationResult VerifyProfilePinWithLockout(
        string profileId,
        string pin)
    {
        using var connection = OpenConnection();

        using (var begin = connection.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE;";
            begin.ExecuteNonQuery();
        }

        bool committed = false;

        try
        {
            string hash;
            string salt;
            int iterations;
            int attempts;
            string? lockedUntil;

            using (var lookup = connection.CreateCommand())
            {
                lookup.CommandText = """
                    SELECT PinHash, PinSalt, PinIterations,
                           FailedPinAttempts, PinLockedUntilUtc
                    FROM UserProfiles
                    WHERE Id = $id;
                    """;

                lookup.Parameters.AddWithValue("$id", profileId);

                using var reader = lookup.ExecuteReader();

                if (!reader.Read() ||
                    reader.IsDBNull(0) ||
                    reader.IsDBNull(1))
                    return PinVerificationResult.NotFound;

                hash = reader.GetString(0);
                salt = reader.GetString(1);
                iterations = reader.GetInt32(2);
                attempts = reader.GetInt32(3);
                lockedUntil = reader.IsDBNull(4)
                    ? null
                    : reader.GetString(4);
            }

            var now = DateTimeOffset.UtcNow;

            if (lockedUntil is not null &&
                DateTimeOffset.TryParse(
                    lockedUntil, out var expiration) &&
                now < expiration)
            {
                return PinVerificationResult.Locked;
            }

            // An expired lockout starts a fresh attempt window.
            if (lockedUntil is not null)
                attempts = 0;

            bool valid = ProfileSecurityService.VerifyPin(
                pin, hash, salt, iterations);

            if (valid)
            {
                using var reset = connection.CreateCommand();

                reset.CommandText = """
                    UPDATE UserProfiles
                    SET FailedPinAttempts = 0,
                        PinLockedUntilUtc = NULL
                    WHERE Id = $id;
                    """;

                reset.Parameters.AddWithValue("$id", profileId);
                reset.ExecuteNonQuery();

                using var commit = connection.CreateCommand();
                commit.CommandText = "COMMIT;";
                commit.ExecuteNonQuery();

                committed = true;
                return PinVerificationResult.Success;
            }

            attempts++;

            string? nextLockout = null;

            if (attempts >= 5)
            {
                nextLockout = now.AddMinutes(5)
                    .ToString("O");

                attempts = 0;
            }

            using (var update = connection.CreateCommand())
            {
                update.CommandText = """
                    UPDATE UserProfiles
                    SET FailedPinAttempts = $attempts,
                        PinLockedUntilUtc = $locked
                    WHERE Id = $id;
                    """;

                update.Parameters.AddWithValue(
                    "$attempts", attempts);

                update.Parameters.AddWithValue(
                    "$locked",
                    (object?)nextLockout ?? DBNull.Value);

                update.Parameters.AddWithValue("$id", profileId);

                update.ExecuteNonQuery();
            }

            using (var commit = connection.CreateCommand())
            {
                commit.CommandText = "COMMIT;";
                commit.ExecuteNonQuery();
            }

            committed = true;

            return nextLockout is not null
                ? PinVerificationResult.Locked
                : PinVerificationResult.Incorrect;
        }
        finally
        {
            if (!committed)
            {
                using var rollback = connection.CreateCommand();
                rollback.CommandText = "ROLLBACK;";
                rollback.ExecuteNonQuery();
            }
        }
    }

    // Legacy verification entry point.
    // All callers must respect the persistent lockout.
    public bool VerifyProfilePin(string profileId, string pin)
    {
        return VerifyProfilePinWithLockout(profileId, pin)
            == PinVerificationResult.Success;
    }

    // PIN removal must use ChangeProfilePin with authentication.
    // The old unauthenticated removal method is intentionally disabled.
    [Obsolete("Use ChangeProfilePin with current PIN verification.")]
    public void RemoveProfilePin(string profileId)
    {
        throw new InvalidOperationException(
            "Unauthenticated PIN removal is disabled.");
    }

    // Change or remove a PIN only after verifying the current PIN.
    // For an unlocked standard profile, currentPin may be blank.
    public bool ChangeProfilePin(
        string profileId,
        string currentPin,
        string? newPin)
    {
        if (newPin is not null &&
            !ProfileSecurityService.IsValidPin(newPin))
            throw new ArgumentException(
                "New PIN must contain 4 to 8 digits.");

        using var connection = OpenConnection();

        using (var begin = connection.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE;";
            begin.ExecuteNonQuery();
        }

        bool committed = false;

        try
        {
            bool isAdmin;
            string? hash;
            string? salt;
            int iterations;
            int attempts;
            string? lockedUntil;

            using (var lookup = connection.CreateCommand())
            {
                lookup.CommandText = """
                    SELECT IsAdmin, PinHash, PinSalt,
                           PinIterations, FailedPinAttempts,
                           PinLockedUntilUtc
                    FROM UserProfiles
                    WHERE Id = $id;
                    """;

                lookup.Parameters.AddWithValue("$id", profileId);

                using var reader = lookup.ExecuteReader();

                if (!reader.Read())
                    return false;

                isAdmin = reader.GetInt64(0) != 0;
                hash = reader.IsDBNull(1)
                    ? null : reader.GetString(1);
                salt = reader.IsDBNull(2)
                    ? null : reader.GetString(2);
                iterations = reader.GetInt32(3);
                attempts = reader.GetInt32(4);
                lockedUntil = reader.IsDBNull(5)
                    ? null : reader.GetString(5);
            }

            var now = DateTimeOffset.UtcNow;

            if (lockedUntil is not null &&
                DateTimeOffset.TryParse(
                    lockedUntil, out var expiration) &&
                now < expiration)
            {
                return false;
            }

            if (lockedUntil is not null)
                attempts = 0;

            bool verified;

            if (hash is null)
            {
                verified = !isAdmin && currentPin.Length == 0;
            }
            else
            {
                verified = salt is not null &&
                    ProfileSecurityService.VerifyPin(
                        currentPin, hash, salt, iterations);
            }

            if (!verified)
            {
                if (hash is null)
                    return false;

                attempts++;

                string? nextLockout = null;

                if (attempts >= 5)
                {
                    attempts = 0;
                    nextLockout = now.AddMinutes(5)
                        .ToString("O");
                }

                using (var failure = connection.CreateCommand())
                {
                    failure.CommandText = """
                        UPDATE UserProfiles
                        SET FailedPinAttempts = $attempts,
                            PinLockedUntilUtc = $locked
                        WHERE Id = $id;
                        """;

                    failure.Parameters.AddWithValue(
                        "$attempts", attempts);
                    failure.Parameters.AddWithValue(
                        "$locked",
                        (object?)nextLockout ?? DBNull.Value);
                    failure.Parameters.AddWithValue("$id", profileId);
                    failure.ExecuteNonQuery();
                }

                using (var commit = connection.CreateCommand())
                {
                    commit.CommandText = "COMMIT;";
                    commit.ExecuteNonQuery();
                }

                committed = true;
                return false;
            }

            if (isAdmin && newPin is null)
                return false;

            var hashed = newPin is null
                ? ((string Hash, string Salt, int Iterations)?)null
                : ProfileSecurityService.HashPin(newPin);

            using (var update = connection.CreateCommand())
            {
                update.CommandText = """
                    UPDATE UserProfiles
                    SET PinHash = $hash,
                        PinSalt = $salt,
                        PinIterations = $iterations,
                        FailedPinAttempts = 0,
                        PinLockedUntilUtc = NULL
                    WHERE Id = $id;
                    """;

                update.Parameters.AddWithValue("$id", profileId);
                update.Parameters.AddWithValue(
                    "$hash",
                    (object?)hashed?.Hash ?? DBNull.Value);
                update.Parameters.AddWithValue(
                    "$salt",
                    (object?)hashed?.Salt ?? DBNull.Value);
                update.Parameters.AddWithValue(
                    "$iterations", hashed?.Iterations ?? 0);
                update.ExecuteNonQuery();
            }

            using (var commit = connection.CreateCommand())
            {
                commit.CommandText = "COMMIT;";
                commit.ExecuteNonQuery();
            }

            committed = true;
            return true;
        }
        finally
        {
            if (!committed)
            {
                using var rollback = connection.CreateCommand();
                rollback.CommandText = "ROLLBACK;";
                rollback.ExecuteNonQuery();
            }
        }
    }

    public GameMetadata? GetGameMetadata(string gameId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Description, ReleaseDate, Developer, Publisher, Genre, SourceUrl FROM GameMetadata WHERE GameId=$id";
        command.Parameters.AddWithValue("$id", gameId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new GameMetadata(
            reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5)) : null;
    }

    public void SaveGameMetadata(string gameId, GameMetadata metadata)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GameMetadata (GameId, Description, ReleaseDate, Developer, Publisher, Genre, SourceUrl)
            VALUES ($id,$desc,$date,$dev,$pub,$genre,$url)
            ON CONFLICT(GameId) DO UPDATE SET
                Description=excluded.Description, ReleaseDate=excluded.ReleaseDate,
                Developer=excluded.Developer, Publisher=excluded.Publisher,
                Genre=excluded.Genre, SourceUrl=excluded.SourceUrl;
            """;
        command.Parameters.AddWithValue("$id", gameId);
        command.Parameters.AddWithValue("$desc", metadata.Description);
        command.Parameters.AddWithValue("$date", metadata.ReleaseDate);
        command.Parameters.AddWithValue("$dev", metadata.Developer);
        command.Parameters.AddWithValue("$pub", metadata.Publisher);
        command.Parameters.AddWithValue("$genre", metadata.Genre);
        command.Parameters.AddWithValue("$url", metadata.SourceUrl);
        command.ExecuteNonQuery();
    }

    public List<Game> GetGames()
    {
        var games = new List<Game>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, System, FilePath, IsFavorite FROM Games ORDER BY Title COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            games.Add(new Game
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                System = reader.GetString(2),
                FilePath = reader.GetString(3),
                IsFavorite = reader.GetInt64(4) != 0
            });
        }
        return games;
    }

    public void SaveGame(Game game)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Games (Id, Title, System, FilePath, IsFavorite)
            VALUES ($id, $title, $system, $path, $favorite)
            ON CONFLICT(FilePath) DO UPDATE SET
                Title = excluded.Title,
                System = excluded.System;
            """;
        command.Parameters.AddWithValue("$id", game.Id);
        command.Parameters.AddWithValue("$title", game.Title);
        command.Parameters.AddWithValue("$system", game.System);
        command.Parameters.AddWithValue("$path", game.FilePath);
        command.Parameters.AddWithValue("$favorite", game.IsFavorite ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public int RemoveInvalidGameEntries()

    {
         using var connection = OpenConnection();
         using var command = connection.CreateCommand();

         command.CommandText = """
             DELETE FROM Games
             WHERE lower(FilePath) LIKE '%.sav'
                OR lower(FilePath) LIKE '%.srm'
                OR lower(FilePath) LIKE '%.gci'
                OR lower(FilePath) LIKE '%.state'
                OR lower(FilePath) LIKE '%.raw';
             """;

       return command.ExecuteNonQuery();
  }

    public void SetFavorite(Game game)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Games SET IsFavorite = $favorite WHERE Id = $id;";
        command.Parameters.AddWithValue("$favorite", game.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$id", game.Id);
        command.ExecuteNonQuery();
    }

    public List<EmulatorProfile> GetEmulators()
    {
        var profiles = new List<EmulatorProfile>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, System, ExecutablePath, ArgumentsTemplate FROM Emulators ORDER BY Name COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            profiles.Add(new EmulatorProfile
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                System = reader.GetString(2),
                ExecutablePath = reader.GetString(3),
                ArgumentsTemplate = reader.GetString(4)
            });
        }
        return profiles;
    }

    public bool CanProfileModifySettings(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return false;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT CASE
                WHEN p.IsAdmin = 1 THEN 1
                ELSE COALESCE(r.CanModifySettings, 1)
            END
            FROM UserProfiles p
            LEFT JOIN ProfilePermissions r
                ON r.ProfileId = p.Id
            WHERE p.Id = $id;
            """;

        command.Parameters.AddWithValue("$id", profileId);

        return Convert.ToInt64(
            command.ExecuteScalar() ?? 0L) != 0;
    }

    public bool CanProfileManageEmulators(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return false;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT CASE
                WHEN p.IsAdmin = 1 THEN 1
                ELSE COALESCE(r.CanManageEmulators, 1)
            END
            FROM UserProfiles p
            LEFT JOIN ProfilePermissions r
                ON r.ProfileId = p.Id
            WHERE p.Id = $id;
            """;

        command.Parameters.AddWithValue("$id", profileId);

        return Convert.ToInt64(
            command.ExecuteScalar() ?? 0L) != 0;
    }

    private static void RequireEmulatorManagementPermission(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string? actingProfileId)
    {
        if (string.IsNullOrWhiteSpace(actingProfileId))
            throw new UnauthorizedAccessException(
                "Emulator management denied: No active profile.");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = """
            SELECT CASE
                WHEN p.IsAdmin = 1 THEN 1
                ELSE COALESCE(r.CanManageEmulators, 1)
            END
            FROM UserProfiles p
            LEFT JOIN ProfilePermissions r
                ON r.ProfileId = p.Id
            WHERE p.Id = $id;
            """;

        command.Parameters.AddWithValue("$id", actingProfileId);

        if (Convert.ToInt64(command.ExecuteScalar() ?? 0L) != 1)
            throw new UnauthorizedAccessException(
                "Emulator management denied: Insufficient permissions.");
    }

    public void SaveEmulator(
        EmulatorProfile profile,
        string? actingProfileId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        RequireEmulatorManagementPermission(
            connection, transaction, actingProfileId);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Emulators (Id, Name, System, ExecutablePath, ArgumentsTemplate)
            VALUES ($id, $name, $system, $executable, $arguments)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                System = excluded.System,
                ExecutablePath = excluded.ExecutablePath,
                ArgumentsTemplate = excluded.ArgumentsTemplate;
            """;
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$system", profile.System);
        command.Parameters.AddWithValue("$executable", profile.ExecutablePath);
        command.Parameters.AddWithValue("$arguments", profile.ArgumentsTemplate);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void DeleteEmulator(
        string id,
        string? actingProfileId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        RequireEmulatorManagementPermission(
            connection, transaction, actingProfileId);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Emulators WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<string> GetRomFolders()
    {
        var folders = new List<string>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Path FROM RomFolders ORDER BY Path COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        while (reader.Read()) folders.Add(reader.GetString(0));
        return folders;
    }

    public void AddRomFolder(string path)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO RomFolders (Path) VALUES ($path);";
        command.Parameters.AddWithValue("$path", Path.GetFullPath(path));
        command.ExecuteNonQuery();
    }

    public void DeleteRomFolder(string path)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM RomFolders WHERE Path = $path;";
        command.Parameters.AddWithValue("$path", path);
        command.ExecuteNonQuery();
    }
}

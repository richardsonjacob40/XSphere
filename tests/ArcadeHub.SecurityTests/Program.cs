using ArcadeHub.Models;
using ArcadeHub.Services;

var testFolder = Path.Combine(
    Path.GetTempPath(),
    "ArcadeHubSecurityTests",
    Guid.NewGuid().ToString("N"));

Directory.CreateDirectory(testFolder);

var databasePath = Path.Combine(testFolder, "test.db");

try
{
    var database = new ArcadeDatabase(databasePath);

    var admin = new UserProfile
    {
        Name = "Test Administrator"
    };

    var standard = new UserProfile
    {
        Name = "Test Standard User"
    };

    database.CreateUserProfileWithPin(admin, "123456");
    database.CreateUserProfileWithPin(standard, "5678");

    Check(
        "Administrator bootstrap",
        database.BootstrapFirstAdministrator(admin.Id, "123456"));

    var permissions = new ProfilePermissions
    {
        ProfileId = standard.Id,
        CanLaunchGames = false,
        CanManageEmulators = false,
        CanModifySettings = false,
        CanManageProfiles = false
    };

    Check(
        "Incorrect administrator PIN rejected",
        !database.ChangeProfilePermissions(
            admin.Id, "999999", permissions));

    Check(
        "Correct administrator PIN accepted",
        database.ChangeProfilePermissions(
            admin.Id, "123456", permissions));

    var saved = database.GetProfilePermissions(standard.Id);

    Check(
        "Game-launch restriction persisted",
        !saved.CanLaunchGames);

    Check(
        "Emulator-management restriction persisted",
        !saved.CanManageEmulators);

    Check(
        "Settings restriction persisted",
        !saved.CanModifySettings);

    Check(
        "Standard profile cannot modify permissions",
        !database.ChangeProfilePermissions(
            standard.Id, "5678", permissions));

    var adminPermissions = new ProfilePermissions
    {
        ProfileId = admin.Id,
        CanLaunchGames = false,
        CanManageEmulators = false,
        CanModifySettings = false,
        CanManageProfiles = false
    };

    Check(
        "Administrator permissions protected",
        !database.ChangeProfilePermissions(
            admin.Id, "123456", adminPermissions));

    Check(
        "Administrator retains full access",
        database.GetProfilePermissions(admin.Id).CanLaunchGames);

    // Test administrator PIN lockout using a separate profile.
    var lockedAdmin = new UserProfile
    {
        Name = "Lockout Test Administrator"
    };

    database.CreateUserProfileWithPin(lockedAdmin, "87654321");

    Check(
        "Second administrator promoted",
        database.ChangeAdministratorStatus(
            admin.Id, "123456", lockedAdmin.Id, true));

    var lockoutPermissions = new ProfilePermissions
    {
        ProfileId = standard.Id,
        CanLaunchGames = true,
        CanManageEmulators = true,
        CanModifySettings = true,
        CanManageProfiles = false
    };

    for (int attempt = 1; attempt <= 5; attempt++)
    {
        Check(
            $"Incorrect PIN attempt {attempt} rejected",
            !database.ChangeProfilePermissions(
                lockedAdmin.Id, "000000", lockoutPermissions));
    }

    Check(
        "Administrator locked after five failures",
        database.IsProfilePinLocked(lockedAdmin.Id));

    Check(
        "Correct PIN rejected during lockout",
        !database.ChangeProfilePermissions(
            lockedAdmin.Id, "87654321", lockoutPermissions));

    Check(
        "Permissions unchanged during lockout",
        !database.GetProfilePermissions(standard.Id).CanLaunchGames);

    // Phase 7F.4C: Emulator management authorization tests.

    var adminEmulator = new EmulatorProfile
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = "Administrator Test Emulator",
        System = "Test System",
        ExecutablePath = "/test/admin-emulator",
        ArgumentsTemplate = "\"{rom}\""
    };

    database.SaveEmulator(adminEmulator, admin.Id);

    Check(
        "Administrator can save emulator",
        database.GetEmulators().Any(e => e.Id == adminEmulator.Id));

    database.DeleteEmulator(adminEmulator.Id, admin.Id);

    Check(
        "Administrator can delete emulator",
        !database.GetEmulators().Any(e => e.Id == adminEmulator.Id));

    var authorizedPermissions = new ProfilePermissions
    {
        ProfileId = standard.Id,
        CanLaunchGames = true,
        CanManageEmulators = true,
        CanModifySettings = true,
        CanManageProfiles = false
    };

    Check(
        "Administrator grants emulator-management permission",
        database.ChangeProfilePermissions(
            admin.Id, "123456", authorizedPermissions));

    var standardEmulator = new EmulatorProfile
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = "Standard Test Emulator",
        System = "Test System",
        ExecutablePath = "/test/standard-emulator",
        ArgumentsTemplate = "\"{rom}\""
    };

    database.SaveEmulator(standardEmulator, standard.Id);

    Check(
        "Authorized standard profile can save emulator",
        database.GetEmulators().Any(e => e.Id == standardEmulator.Id));

    database.DeleteEmulator(standardEmulator.Id, standard.Id);

    Check(
        "Authorized standard profile can delete emulator",
        !database.GetEmulators().Any(e => e.Id == standardEmulator.Id));

    // Seed an emulator to verify denied deletion leaves it unchanged.
    var protectedEmulator = new EmulatorProfile
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = "Protected Test Emulator",
        System = "Test System",
        ExecutablePath = "/test/protected-emulator",
        ArgumentsTemplate = "\"{rom}\""
    };

    database.SaveEmulator(protectedEmulator, admin.Id);

    var restrictedPermissions = new ProfilePermissions
    {
        ProfileId = standard.Id,
        CanLaunchGames = true,
        CanManageEmulators = false,
        CanModifySettings = true,
        CanManageProfiles = false
    };

    Check(
        "Administrator revokes emulator-management permission",
        database.ChangeProfilePermissions(
            admin.Id, "123456", restrictedPermissions));

    Check(
        "Restricted standard profile cannot save emulator",
        ExpectUnauthorized(() =>
            database.SaveEmulator(
                new EmulatorProfile
                {
                    Name = "Unauthorized Emulator",
                    System = "Test System",
                    ExecutablePath = "/test/unauthorized",
                    ArgumentsTemplate = "\"{rom}\""
                },
                standard.Id)));

    Check(
        "Restricted standard profile cannot delete emulator",
        ExpectUnauthorized(() =>
            database.DeleteEmulator(
                protectedEmulator.Id, standard.Id)));

    Check(
        "Denied operations leave emulator database unchanged",
        database.GetEmulators().Any(
            e => e.Id == protectedEmulator.Id &&
                 e.Name == protectedEmulator.Name));

    Check(
        "Missing profile ID is denied",
        ExpectUnauthorized(() =>
            database.SaveEmulator(
                new EmulatorProfile
                {
                    Name = "Missing Profile Test",
                    System = "Test System",
                    ExecutablePath = "/test/missing",
                    ArgumentsTemplate = "\"{rom}\""
                },
                null)));

    Check(
        "Unknown profile ID is denied",
        ExpectUnauthorized(() =>
            database.DeleteEmulator(
                protectedEmulator.Id, "unknown-profile-id")));

    // Regrant, then revoke permission to verify changes take effect
    // immediately without restarting the application.
    Check(
        "Emulator permission can be restored",
        database.ChangeProfilePermissions(
            admin.Id, "123456", authorizedPermissions));

    Check(
        "Authorized profile can save after permission restoration",
        database.CanProfileManageEmulators(standard.Id));

    Check(
        "Permission revocation takes effect immediately",
        database.ChangeProfilePermissions(
            admin.Id, "123456", restrictedPermissions) &&
            !database.CanProfileManageEmulators(standard.Id));

    Check(
        "Revoked profile cannot modify emulator afterward",
        ExpectUnauthorized(() =>
            database.SaveEmulator(
                new EmulatorProfile
                {
                    Name = "Post Revocation Test",
                    System = "Test System",
                    ExecutablePath = "/test/revoked",
                    ArgumentsTemplate = "\"{rom}\""
                },
                standard.Id)));
}
finally
{
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    Directory.Delete(testFolder, recursive: true);
}


static bool ExpectUnauthorized(Action action)
{
    try
    {
        action();
        return false;
    }
    catch (UnauthorizedAccessException)
    {
        return true;
    }
}

static void Check(string name, bool condition)
{
    if (!condition)
        throw new Exception($"FAIL: {name}");

    Console.WriteLine($"PASS: {name}");
}

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
}
finally
{
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    Directory.Delete(testFolder, recursive: true);
}

static void Check(string name, bool condition)
{
    if (!condition)
        throw new Exception($"FAIL: {name}");

    Console.WriteLine($"PASS: {name}");
}

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




    // RetroArch Flatpak argument resolver tests.
    Console.WriteLine("\nRetroArch Flatpak argument resolver tests:");

    const string flatpakTemplate =
        "run org.libretro.RetroArch -L \"{core}\" \"{rom}\"";

    Check("Flatpak arguments preserve ROM placeholder",
        RetroArchArgumentResolver.Resolve(
            "run org.libretro.RetroArch \"{rom}\"",
            "NES").Contains("{rom}"));

    Check("Native arguments without core remain unchanged",
        RetroArchArgumentResolver.Resolve(
            "--fullscreen \"{rom}\"",
            "NES") == "--fullscreen \"{rom}\"");

    Check("Flatpak core template contains required placeholders",
        flatpakTemplate.Contains("{core}") &&
        flatpakTemplate.Contains("{rom}"));

    bool missingFlatpakCoreRejected = false;
    try
    {
        RetroArchArgumentResolver.Resolve(
            flatpakTemplate,
            "Unsupported Test System");
    }
    catch (InvalidOperationException)
    {
        missingFlatpakCoreRejected = true;
    }

    Check("Unsupported Flatpak core is rejected",
        missingFlatpakCoreRejected);

    // RetroArch Flatpak core locator tests.
    Console.WriteLine("\nRetroArch Flatpak core locator tests:");

    Check("Flatpak core directory uses RetroArch application ID",
        RetroArchFlatpakCoreLocator.GetCoreDirectory()
            .Contains("org.libretro.RetroArch"));

    Check("Unsupported system has no Flatpak core",
        RetroArchFlatpakCoreLocator.FindCore(
            "Unsupported Test System") is null);

    Check("Unsupported system is not marked installed",
        !RetroArchFlatpakCoreLocator.IsCorePresent(
            "Unsupported Test System"));

    if (EmulatorPlatformService.IsWsl)
    {
        Check("WSL does not detect native Flatpak cores",
            RetroArchFlatpakCoreLocator.FindCore("NES") is null);
    }

    // RetroArch registration safety tests.
    Console.WriteLine("\nRetroArch registration safety tests:");

    bool missingRetroArchRejected = false;
    try
    {
        RetroArchProfileRegistration.RegisterMissingProfiles(
            database,
            database.GetEmulators(),
            admin.Id,
            Path.Combine(testFolder, "missing-retroarch"));
    }
    catch (FileNotFoundException)
    {
        missingRetroArchRejected = true;
    }

    Check("Missing RetroArch executable is rejected",
        missingRetroArchRejected);

    var existingRetroArchProfile = new EmulatorProfile
    {
        Name = "Custom NES Emulator",
        System = "NES",
        ExecutablePath = "/custom/nes-emulator",
        ArgumentsTemplate = "--custom \"{rom}\""
    };

    database.SaveEmulator(existingRetroArchProfile, admin.Id);

    var fakeRetroArchExecutable = Path.Combine(
        testFolder, "retroarch-test-executable");

    File.WriteAllText(fakeRetroArchExecutable, "test executable placeholder");

    var registeredRetroArchCount =
        RetroArchProfileRegistration.RegisterMissingProfiles(
            database,
            database.GetEmulators(),
            admin.Id,
            fakeRetroArchExecutable);

    var preservedRetroArchProfile = database.GetEmulators()
        .Single(profile => profile.Id == existingRetroArchProfile.Id);

    Check("Existing RetroArch system profile is preserved",
        preservedRetroArchProfile.Name == "Custom NES Emulator" &&
        preservedRetroArchProfile.ExecutablePath == "/custom/nes-emulator" &&
        preservedRetroArchProfile.ArgumentsTemplate == "--custom \"{rom}\"");

    Check("Existing RetroArch profile is not duplicated",
        database.GetEmulators().Count(profile =>
            profile.System.Equals("NES",
                StringComparison.OrdinalIgnoreCase)) == 1);

    Console.WriteLine(
        $"RetroArch profiles registered with available cores: {registeredRetroArchCount}");

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

    // Flatpak ROM permission tests.
    Console.WriteLine("\nFlatpak ROM permission tests:");

    string romParent = Path.Combine(testFolder, "roms");
    string romDirectory = Path.Combine(romParent, "GameCube");
    string childDirectory = Path.Combine(romDirectory, "Special");

    Directory.CreateDirectory(childDirectory);

    var readOnly = new[]
    {
        new FlatpakFilesystemPermission(romDirectory + ":ro")
    };

    Check("Read-only permission allows reading",
        FlatpakFilesystemAccessEvaluator.HasReadAccess(
            romDirectory, readOnly));

    Check("Read-only permission denies writing",
        !FlatpakFilesystemAccessEvaluator.HasWriteAccess(
            romDirectory, readOnly));

    Check("Read-only permission denies creation",
        !FlatpakFilesystemAccessEvaluator.HasCreateAccess(
            romDirectory, readOnly));

    var readWrite = new[]
    {
        new FlatpakFilesystemPermission(romDirectory + ":rw")
    };

    Check("Read/write permission allows reading",
        FlatpakFilesystemAccessEvaluator.HasReadAccess(
            romDirectory, readWrite));

    Check("Read/write permission allows writing",
        FlatpakFilesystemAccessEvaluator.HasWriteAccess(
            romDirectory, readWrite));

    Check("Read/write permission allows creation",
        FlatpakFilesystemAccessEvaluator.HasCreateAccess(
            romDirectory, readWrite));

    var createOnly = new[]
    {
        new FlatpakFilesystemPermission(romDirectory + ":create")
    };

    Check("Create-only permission denies reading",
        !FlatpakFilesystemAccessEvaluator.HasReadAccess(
            romDirectory, createOnly));

    Check("Create-only permission allows creation",
        FlatpakFilesystemAccessEvaluator.HasCreateAccess(
            romDirectory, createOnly));

    var denied = new[]
    {
        new FlatpakFilesystemPermission("!" + romDirectory)
    };

    Check("Explicit denial blocks reading",
        !FlatpakFilesystemAccessEvaluator.HasReadAccess(
            romDirectory, denied));

    Check("Explicit denial blocks writing",
        !FlatpakFilesystemAccessEvaluator.HasWriteAccess(
            romDirectory, denied));

    var parentGrantChildRestriction = new[]
    {
        new FlatpakFilesystemPermission(romParent + ":rw"),
        new FlatpakFilesystemPermission("!" + romDirectory)
    };

    Check("Specific child denial overrides parent grant",
        !FlatpakFilesystemAccessEvaluator.HasReadAccess(
            romDirectory, parentGrantChildRestriction));

    var parentReadOnlyChildGrant = new[]
    {
        new FlatpakFilesystemPermission(romParent + ":ro"),
        new FlatpakFilesystemPermission(romDirectory + ":rw")
    };

    Check("Specific child grant overrides parent restriction",
        FlatpakFilesystemAccessEvaluator.HasWriteAccess(
            romDirectory, parentReadOnlyChildGrant));

    var parentGrantChildReadOnly = new[]
    {
        new FlatpakFilesystemPermission(romParent + ":rw"),
        new FlatpakFilesystemPermission(romDirectory + ":ro")
    };

    Check("Specific child read-only permission overrides parent write",
        !FlatpakFilesystemAccessEvaluator.HasWriteAccess(
            romDirectory, parentGrantChildReadOnly));


    // RetroArch core mapping and argument-resolution tests.
    Console.WriteLine("\nRetroArch core tests:");

    Check("NES maps to FCEUmm",
        RetroArchCoreService.GetDefaultCore("NES") == "fceumm");

    Check("Super Nintendo maps to Snes9x",
        RetroArchCoreService.GetDefaultCore("Super Nintendo") == "snes9x");

    Check("Game Boy Advance maps to mGBA",
        RetroArchCoreService.GetDefaultCore("Game Boy Advance") == "mgba");

    Check("Core mapping ignores case",
        RetroArchCoreService.GetDefaultCore("nes") == "fceumm");

    Check("Unknown system has no default core",
        RetroArchCoreService.GetDefaultCore("Unknown System") is null);

    string unchangedArguments =
        RetroArchArgumentResolver.Resolve(
            "--fullscreen \"{rom}\"", "NES");

    Check("Custom arguments without core placeholder remain unchanged",
        unchangedArguments == "--fullscreen \"{rom}\"");

    string testCoreDirectory = Path.Combine(testFolder, "cores");
    Directory.CreateDirectory(testCoreDirectory);

    string coreExtension = OperatingSystem.IsWindows()
        ? ".dll"
        : OperatingSystem.IsMacOS() ? ".dylib" : ".so";

    string testCorePath = Path.Combine(
        testCoreDirectory, "fceumm_libretro" + coreExtension);

    File.WriteAllText(testCorePath, "test core placeholder");

    string resolvedArguments =
        RetroArchArgumentResolver.Resolve(
            "-L \"{core}\" \"{rom}\"",
            "NES",
            testCoreDirectory);

    Check("Installed core path replaces placeholder",
        resolvedArguments.Contains(
            testCorePath, StringComparison.Ordinal) &&
        !resolvedArguments.Contains(
            "{core}", StringComparison.OrdinalIgnoreCase));

    Check("ROM placeholder remains available",
        resolvedArguments.Contains(
            "{rom}", StringComparison.OrdinalIgnoreCase));

    bool missingCoreRejected = false;
    try
    {
        RetroArchArgumentResolver.Resolve(
            "-L \"{core}\" \"{rom}\"",
            "Unknown System",
            testCoreDirectory);
    }
    catch (InvalidOperationException)
    {
        missingCoreRejected = true;
    }

    Check("Unknown core mapping produces a useful error",
        missingCoreRejected);


    // Flatpak installation detection regression test.
    Console.WriteLine("\nFlatpak installation detection tests:");

    Check("Missing Flatpak application is not reported as installed",
        !await FlatpakService.IsAppInstalledAsync(
            "org.xsphere.NonexistentTestApplication"));


    // RetroArch profile factory tests.
    Console.WriteLine("\nRetroArch profile factory tests:");

    var nesProfile = RetroArchProfileService.CreateProfile(
        "NES", "/test/retroarch");

    Check("RetroArch profile uses requested system",
        nesProfile.System == "NES");

    Check("RetroArch profile uses requested executable",
        nesProfile.ExecutablePath == "/test/retroarch");

    Check("RetroArch profile includes core and ROM placeholders",
        nesProfile.ArgumentsTemplate.Contains("{core}") &&
        nesProfile.ArgumentsTemplate.Contains("{rom}"));

    var snesProfile = RetroArchProfileService.CreateProfile(
        "Super Nintendo", "/test/retroarch");

    Check("RetroArch system profiles have unique IDs",
        nesProfile.Id != snesProfile.Id);

    bool unsupportedProfileRejected = false;
    try
    {
        RetroArchProfileService.CreateProfile(
            "Unknown System", "/test/retroarch");
    }
    catch (ArgumentException)
    {
        unsupportedProfileRejected = true;
    }

    Check("Unsupported RetroArch system is rejected",
        unsupportedProfileRejected);

    bool missingExecutableRejected = false;
    try
    {
        RetroArchProfileService.CreateProfile("NES", "");
    }
    catch (ArgumentException)
    {
        missingExecutableRejected = true;
    }

    Check("Missing RetroArch executable is rejected",
        missingExecutableRejected);

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

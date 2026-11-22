# XSphere — cross-platform emulation library (starter build)

XSphere is a starter desktop application for bringing your emulators and your own ROM/game files into one library. It is built with C#/.NET 10, Avalonia UI, and SQLite. The same desktop project targets Windows, macOS, and Linux.

## What this first build can do

- Maintain a local SQLite library of scanned games.
- Scan ROM folders you select and remember those folders between sessions.
- Recognize common cartridge and disc/container file extensions.
- Search the library and mark games as favorites.
- Register emulator executables against a system name.
- Launch a game by passing its file path to the selected emulator.
- Open official emulator project pages from the emulator manager.
- Switch between a normal desktop window and a full-screen presentation mode.

This is an early foundation, not a finished replacement for LaunchBox/Playnite yet. Artwork/metadata lookup, robust emulator auto-detection, controller navigation, installer workflows, per-game overrides, and import cleanup are planned follow-on features.

## Important ROM note

XSphere indexes files already on your computer. It does not provide ROM downloads. Only scan or use game files you are legally entitled to use in your location.

## 1. Install the tools

1. Install the **.NET 10 SDK** (not just the runtime): https://dotnet.microsoft.com/download/dotnet/10.0
2. Install an editor. For a beginner, Visual Studio Code with the C# Dev Kit extension is a straightforward free option: https://code.visualstudio.com/ and https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit
3. Extract this `XSphere` folder somewhere convenient, such as `Documents/Projects/XSphere`.

Avalonia's current platform documentation lists Windows, macOS, and desktop Linux as supported desktop targets: https://docs.avaloniaui.net/docs/supported-platforms

## 2. Run the application

For this diagnostic build, the Windows console remains attached so startup errors are visible in PowerShell. Once startup is confirmed, release packaging can switch back to a window-only executable.

Open a terminal in the `XSphere` folder and run:

```bash
dotnet restore src/XSphere/XSphere.csproj
dotnet run --project src/XSphere/XSphere.csproj
```

The first run downloads the Avalonia and SQLite NuGet packages. An internet connection is needed for that first restore.

### Windows shortcut

You can also double-click `run-windows.bat` after installing the .NET 10 SDK.

### macOS / Linux shortcut

From the extracted folder, run:

```bash
chmod +x run-unix.sh
./run-unix.sh
```

## 3. Add your games

1. Start XSphere.
2. Select **Library** or **ROM Folders**.
3. Choose **Add ROM Folder** and select the directory containing your game files.
4. XSphere scans the selected folder and its subfolders.
5. Search for a title or system, and use the star button to add a favorite.

The scanner currently recognizes `.nes`, `.sfc`, `.smc`, `.gb`, `.gbc`, `.gba`, `.n64`, `.z64`, `.v64`, `.nds`, `.3ds`, `.cia`, `.iso`, `.chd`, `.cue`, `.cso`, `.pbp`, `.wbfs`, `.rvz`, `.gcm`, `.zip`, and `.7z` files. Some disc formats are shared by several systems. For those, include a system name in the folder path, for example `ROMs/PlayStation 2/` or `ROMs/GameCube/`, to help XSphere classify them.

## 4. Connect an emulator

1. Install an emulator using its official project instructions.
2. In XSphere, open **Emulators**.
3. Enter a display name, the matching system name, and the path to the emulator executable.
4. Keep the argument template as `"{rom}"` unless the emulator requires different launch arguments.
5. Save the profile. The system text must match the system shown on the game card (for example, `NES`, `Super Nintendo`, `GameCube`, or `PlayStation 2`).
6. Return to the library and press **Play**.

If your emulator needs extra options, use arguments such as `--fullscreen "{rom}"`. XSphere substitutes `{rom}` with the selected game's full file path. If there is no `{rom}` placeholder, XSphere appends the ROM path as the final argument.

## 5. Where your data is saved

XSphere stores its SQLite database at `XSphere/xsphere.db` inside your operating system's per-user application data folder. On first launch, existing ArcadeHub data from `ArcadeHub/arcadehub.db` is migrated to `XSphere/xsphere.db` without deleting the original database. Existing settings, cover artwork, and saves are also copied when available, without overwriting existing XSphere files. Your ROM files are not copied, renamed, or moved.

## 6. Publish a self-contained build

When the app is ready to distribute, publish separately for each operating system and CPU architecture. Examples:

```bash
# Windows 64-bit
dotnet publish src/XSphere/XSphere.csproj -c Release -r win-x64 --self-contained true

# macOS Apple Silicon
dotnet publish src/XSphere/XSphere.csproj -c Release -r osx-arm64 --self-contained true

# macOS Intel
dotnet publish src/XSphere/XSphere.csproj -c Release -r osx-x64 --self-contained true

# Linux 64-bit
dotnet publish src/XSphere/XSphere.csproj -c Release -r linux-x64 --self-contained true
```

Build each operating system package on that OS when preparing a release and test it on a clean machine.

## Known limitations in this starter build

- The first version uses generic placeholder cover cards; it does not yet download metadata or artwork.
- System detection is based on file extensions and names in the folder path. Ambiguous disc images may show as `Unknown` until classified.
- The app does not automatically detect all emulator installations or install emulator packages. It includes official download-page shortcuts and lets you register the installed executable.
- Full-screen mode is present, but gamepad/controller navigation is not wired up yet.
- Some emulators use special launch syntax. Configure their argument template to match the emulator's own documentation.
- This starter has not been built in this environment because the .NET SDK is not installed here. Run the build commands above on your computer; if you paste any build error, it can be fixed against your environment.

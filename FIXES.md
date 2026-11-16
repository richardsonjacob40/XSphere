# ArcadeHub build fixes

## Fix 1: CS1736 (optional parameter default)

`TextTrimming.None` is a property value, not a compile-time constant. The text helper uses a nullable optional parameter and applies `TextTrimming.None` inside the object initializer when no value is supplied.

## Fix 2: CS0201 (GameLauncher)

`Process.Start(startInfo) ?? throw ...` was a null-coalescing expression used as a stand-alone statement. It is now tested with an `if` statement, which is a valid statement and still reports failure if the process cannot be started.

## Fix 3: CS0103 (ScrollBarVisibility)

Added `using Avalonia.Controls.Primitives;`, which is the namespace containing Avalonia's `ScrollBarVisibility` enum.

## Fix 4: CS0618 (Watermark obsolete warnings)

Replaced `TextBox.Watermark` with `TextBox.PlaceholderText`, the current Avalonia 12 property. These were warnings, not build-stopping errors.

## Rebuild

From the folder containing `ArcadeHub.sln`, run:

```powershell
dotnet clean src/ArcadeHub/ArcadeHub.csproj
dotnet restore src/ArcadeHub/ArcadeHub.csproj
dotnet build src/ArcadeHub/ArcadeHub.csproj
dotnet run --project src/ArcadeHub/ArcadeHub.csproj
```


## Startup diagnostics update

- Changed the project output type from `WinExe` to `Exe` temporarily so startup exceptions are visible in the terminal during testing.
- Added exception reporting to the console and `%LOCALAPPDATA%\ArcadeHub\startup.log` if startup fails.
- This is a diagnostic build; the console can be hidden again after the app has been verified.

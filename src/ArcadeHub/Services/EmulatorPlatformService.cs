using System;
using System.Runtime.InteropServices;

namespace ArcadeHub.Services;

public static class EmulatorPlatformService
{
    public static string OperatingSystemName =>
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsLinux() ? "linux" :
        OperatingSystem.IsMacOS() ? "macos" :
        "unsupported";

    public static string Architecture =>
        RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            _ => "unsupported"
        };

    public static bool IsWsl =>
        OperatingSystem.IsLinux() &&
        (!string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("WSL_INTEROP")) ||
         !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("WSL_DISTRO_NAME")));

    public static string PlatformId =>
        $"{OperatingSystemName}-{Architecture}";
}

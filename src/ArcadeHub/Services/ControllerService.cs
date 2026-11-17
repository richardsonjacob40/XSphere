
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ArcadeHub.Services;

public sealed class ControllerService : IDisposable
{
    private const uint SDL_INIT_GAMEPAD = 0x00002000;
    private bool _initialized;

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_InitSubSystem(uint flags);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_QuitSubSystem(uint flags);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetGamepads(out int count);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetGamepadNameForID(uint instanceId);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_free(IntPtr memory);

    public void Initialize()
    {
        if (_initialized)
            return;

        if (!SDL_InitSubSystem(SDL_INIT_GAMEPAD))
            throw new InvalidOperationException(
                "SDL3 controller initialization failed.");

        _initialized = true;
    }

    public IReadOnlyList<string> GetConnectedControllers()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "Controller service has not been initialized.");

        var controllers = new List<string>();
        IntPtr ids = SDL_GetGamepads(out int count);

        if (ids == IntPtr.Zero)
            return controllers;

        try
        {
            for (int i = 0; i < count; i++)
            {
                uint id = unchecked(
                    (uint)Marshal.ReadInt32(ids, i * sizeof(uint)));

                IntPtr namePtr = SDL_GetGamepadNameForID(id);

                string name =
                    Marshal.PtrToStringUTF8(namePtr)
                    ?? $"Controller {i + 1}";

                controllers.Add(name);
            }
        }
        finally
        {
            SDL_free(ids);
        }

        return controllers;
    }

    public void Dispose()
    {
        if (!_initialized)
            return;

        SDL_QuitSubSystem(SDL_INIT_GAMEPAD);
        _initialized = false;
    }
}

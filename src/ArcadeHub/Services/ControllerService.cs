using ArcadeHub.Models;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ArcadeHub.Services;

public sealed class ControllerService : IDisposable
{
    private const uint SDL_INIT_GAMEPAD = 0x00002000;
    private bool _initialized;
    private IntPtr _gamepad = IntPtr.Zero;

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
        SDL_PumpEvents();
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


    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_OpenGamepad(uint instanceId);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_CloseGamepad(IntPtr gamepad);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_UpdateGamepads();

[DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
private static extern void SDL_PumpEvents();

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_GetGamepadButton(
        IntPtr gamepad, ControllerButton button);

    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern short SDL_GetGamepadAxis(
        IntPtr gamepad, ControllerAxis axis);

    public bool OpenController(int index = 0)
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "Controller service has not been initialized.");

        CloseController();

        IntPtr ids = SDL_GetGamepads(out int count);

        if (ids == IntPtr.Zero)
            return false;

        try
        {
            if (index < 0 || index >= count)
                return false;

            uint id = unchecked(
                (uint)Marshal.ReadInt32(ids, index * sizeof(uint)));

            _gamepad = SDL_OpenGamepad(id);
            return _gamepad != IntPtr.Zero;
        }
        finally
        {
            SDL_free(ids);
        }
    }
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_GamepadConnected(IntPtr gamepad);

    public bool IsControllerConnected()
    {
        if (_gamepad == IntPtr.Zero)
            return false;

        SDL_UpdateGamepads();
        return SDL_GamepadConnected(_gamepad);
    }

    public bool RefreshControllerConnection()
    {
    if (!_initialized)
        return false;

    // Process pending USB/Bluetooth device events.
    SDL_PumpEvents();

    // Keep using the current controller if it remains connected.
    if (_gamepad != IntPtr.Zero)
    {
        SDL_UpdateGamepads();

        if (SDL_GamepadConnected(_gamepad))
            return true;

        // Release a disconnected controller.
        CloseController();
    }

    // Attempt to open the first available controller.
        return OpenController();
    }

    public void UpdateInput()
    {
        if (_gamepad != IntPtr.Zero)
            SDL_UpdateGamepads();
    }

    public bool IsButtonPressed(ControllerButton button)
    {
        return _gamepad != IntPtr.Zero &&
               SDL_GetGamepadButton(_gamepad, button);
    }

    public short GetAxisValue(ControllerAxis axis)
    {
        return _gamepad == IntPtr.Zero
            ? (short)0
            : SDL_GetGamepadAxis(_gamepad, axis);
    }

    public void CloseController()
    {
        if (_gamepad == IntPtr.Zero)
            return;

        SDL_CloseGamepad(_gamepad);
        _gamepad = IntPtr.Zero;
    }

    public void Dispose()
    {
        CloseController();

        if (!_initialized)
            return;

        SDL_QuitSubSystem(SDL_INIT_GAMEPAD);
        _initialized = false;
    }
}

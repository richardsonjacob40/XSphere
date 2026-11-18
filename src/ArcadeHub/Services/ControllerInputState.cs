using System;

namespace ArcadeHub.Services;

public sealed class ControllerInputState
{
    public uint Buttons { get; init; }
    public uint Pov { get; init; }
    public uint X { get; init; }
    public uint Y { get; init; }
    public uint Z { get; init; }
    public uint R { get; init; }
    public uint U { get; init; }
    public uint V { get; init; }

    public static ControllerInputState FromValues(uint[] values)
    {
        if (values.Length != 8)
            throw new ArgumentException("Expected eight controller values.");

        return new ControllerInputState
        {
            Buttons = values[0],
            Pov = values[1],
            X = values[2],
            Y = values[3],
            Z = values[4],
            R = values[5],
            U = values[6],
            V = values[7]
        };
    }

    public bool IsPressed(uint mask) => (Buttons & mask) != 0;

    public bool WasPressed(ControllerInputState previous, uint mask) =>
        IsPressed(mask) && !previous.IsPressed(mask);

    public int DpadVertical =>
        Pov == 0 ? -1 : Pov == 18000 ? 1 : 0;

    public int DpadHorizontal =>
        Pov == 27000 ? -1 : Pov == 9000 ? 1 : 0;

    public static int AxisDirection(uint value, uint deadZone = 10000)
    {
        const uint center = 32767;

        if (value < center - deadZone)
            return -1;

        if (value > center + deadZone)
            return 1;

        return 0;
    }
}

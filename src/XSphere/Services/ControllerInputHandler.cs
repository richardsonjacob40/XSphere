using System;

namespace XSphere.Services;

public sealed class ControllerInputHandler
{
    private ControllerInputState _previous = new()
    {
        Pov = 65535,
        X = 32767,
        Y = 32767,
        Z = 32767,
        R = 32767,
        U = 32767
    };

    private static readonly (uint Mask, string Name)[] Buttons =
    {
        (1, "A"),
        (2, "B"),
        (4, "X"),
        (8, "Y"),
        (16, "LB"),
        (32, "RB"),
        (64, "Back"),
        (128, "Start"),
        (256, "L3"),
        (512, "R3")
    };

    public event Action<string>? InputPressed;

    public void Process(ControllerInputState current)
    {
        foreach (var (mask, name) in Buttons)
        {
            if (current.WasPressed(_previous, mask))
                InputPressed?.Invoke(name);
        }

        if (current.DpadVertical != 0 &&
            current.DpadVertical != _previous.DpadVertical)
        {
            InputPressed?.Invoke(
                current.DpadVertical < 0 ? "DpadUp" : "DpadDown");
        }

        if (current.DpadHorizontal != 0 &&
            current.DpadHorizontal != _previous.DpadHorizontal)
        {
            InputPressed?.Invoke(
                current.DpadHorizontal < 0 ? "DpadLeft" : "DpadRight");
        }

        CheckAxis("X", current.X, _previous.X);
        CheckAxis("Y", current.Y, _previous.Y);
        CheckAxis("Z", current.Z, _previous.Z);
        CheckAxis("R", current.R, _previous.R);
        CheckAxis("U", current.U, _previous.U);
        CheckAxis("V", current.V, _previous.V);

        _previous = current;
    }

    private void CheckAxis(string name, uint current, uint previous)
    {
        int direction = ControllerInputState.AxisDirection(current);
        int oldDirection = ControllerInputState.AxisDirection(previous);

        if (direction != 0 && direction != oldDirection)
        {
            InputPressed?.Invoke(
                name + (direction < 0 ? "Negative" : "Positive"));
        }
    }
}

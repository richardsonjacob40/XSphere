using ArcadeHub.Models;

namespace ArcadeHub.Services;

public static class SdlControllerStateAdapter
{
    public static ControllerInputState Read(ControllerService controller)
    {
        uint buttons = 0;

        if (controller.IsButtonPressed(ControllerButton.South)) buttons |= 1;
        if (controller.IsButtonPressed(ControllerButton.East)) buttons |= 2;
        if (controller.IsButtonPressed(ControllerButton.West)) buttons |= 4;
        if (controller.IsButtonPressed(ControllerButton.North)) buttons |= 8;
        if (controller.IsButtonPressed(ControllerButton.LeftShoulder)) buttons |= 16;
        if (controller.IsButtonPressed(ControllerButton.RightShoulder)) buttons |= 32;
        if (controller.IsButtonPressed(ControllerButton.Back)) buttons |= 64;
        if (controller.IsButtonPressed(ControllerButton.Start)) buttons |= 128;
        if (controller.IsButtonPressed(ControllerButton.LeftStick)) buttons |= 256;
        if (controller.IsButtonPressed(ControllerButton.RightStick)) buttons |= 512;

        bool up = controller.IsButtonPressed(ControllerButton.DpadUp);
        bool down = controller.IsButtonPressed(ControllerButton.DpadDown);
        bool left = controller.IsButtonPressed(ControllerButton.DpadLeft);
        bool right = controller.IsButtonPressed(ControllerButton.DpadRight);

        uint pov = up ? 0u :
                   right ? 9000u :
                   down ? 18000u :
                   left ? 27000u : 65535u;

        static uint Stick(short value) => (uint)((int)value + 32768);

        static uint Trigger(short value) =>
            (uint)((int)value < 0 ? 0 : value);

        return new ControllerInputState
        {
            Buttons = buttons,
            Pov = pov,
            X = Stick(controller.GetAxisValue(ControllerAxis.LeftX)),
            Y = Stick(controller.GetAxisValue(ControllerAxis.LeftY)),
            Z = Stick(controller.GetAxisValue(ControllerAxis.RightX)),
            R = Stick(controller.GetAxisValue(ControllerAxis.RightY)),
            U = Trigger(controller.GetAxisValue(ControllerAxis.LeftTrigger)),
            V = Trigger(controller.GetAxisValue(ControllerAxis.RightTrigger))
        };
    }
}

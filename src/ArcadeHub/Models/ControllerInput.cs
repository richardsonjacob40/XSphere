
namespace ArcadeHub.Models;

// SDL3 standard gamepad button values.
public enum ControllerButton
{
    South = 1,        // Xbox A
    East = 2,         // Xbox B
    West = 3,         // Xbox X
    North = 4,        // Xbox Y

    Back = 5,
    Guide = 6,
    Start = 7,

    LeftStick = 8,
    RightStick = 9,

    LeftShoulder = 10,
    RightShoulder = 11,

    DpadUp = 12,
    DpadDown = 13,
    DpadLeft = 14,
    DpadRight = 15
}

// SDL3 standard gamepad axis values.
public enum ControllerAxis
{
    LeftX = 0,
    LeftY = 1,

    RightX = 2,
    RightY = 3,

    LeftTrigger = 4,
    RightTrigger = 5
}


namespace XSphere.Models;

// SDL3 standard gamepad button values.
public enum ControllerButton
{
    South = 0,        // Xbox A
    East = 1,         // Xbox B
    West = 2,         // Xbox X
    North = 3,        // Xbox Y

    Back = 4,
    Guide = 5,
    Start = 6,

    LeftStick = 7,
    RightStick = 8,

    LeftShoulder = 9,
    RightShoulder = 10,

    DpadUp = 11,
    DpadDown = 12,
    DpadLeft = 13,
    DpadRight = 14
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

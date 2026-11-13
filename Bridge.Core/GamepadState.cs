namespace Bridge.Core;

/// <summary>
/// Buttons named by position, so the Switch → Xbox mapping is positional
/// (Switch B, the bottom button, becomes Xbox A, also the bottom button).
/// </summary>
[Flags]
public enum Buttons : uint
{
    None = 0,
    South = 1 << 0,      // Switch B  / Xbox A
    East = 1 << 1,       // Switch A  / Xbox B
    West = 1 << 2,       // Switch Y  / Xbox X
    North = 1 << 3,      // Switch X  / Xbox Y
    LeftShoulder = 1 << 4,
    RightShoulder = 1 << 5,
    LeftTrigger = 1 << 6,  // ZL (digital on this controller)
    RightTrigger = 1 << 7, // ZR
    Back = 1 << 8,       // Minus / View
    Start = 1 << 9,      // Plus  / Menu
    Guide = 1 << 10,     // Home  / Guide
    LeftStick = 1 << 11,
    RightStick = 1 << 12,
    DpadUp = 1 << 13,
    DpadDown = 1 << 14,
    DpadLeft = 1 << 15,
    DpadRight = 1 << 16,
    // Switch-only buttons with no Xbox equivalent
    Capture = 1 << 17,
    C = 1 << 18,
    GL = 1 << 19,
    GR = 1 << 20,
}

/// <param name="LeftX">Stick axes are -1..1, with +Y pointing up.</param>
public readonly record struct GamepadState(Buttons Buttons, float LeftX, float LeftY, float RightX, float RightY, int Battery)
{
    public bool Has(Buttons b) => (Buttons & b) != 0;
}

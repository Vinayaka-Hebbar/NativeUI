namespace NativeUI.Core;

/// <summary>
/// Identifies the modifier keys held during a pointer or keyboard event.
/// </summary>
[Flags]
public enum InputModifiers
{
    /// <summary>
    /// No modifier is held.
    /// </summary>
    None = 0,

    /// <summary>
    /// The Shift key is held.
    /// </summary>
    Shift = 1,

    /// <summary>
    /// The Control key is held.
    /// </summary>
    Control = 2,

    /// <summary>
    /// The Alt or Option key is held.
    /// </summary>
    Alt = 4,

    /// <summary>
    /// The Command or Windows key is held.
    /// </summary>
    Meta = 8
}

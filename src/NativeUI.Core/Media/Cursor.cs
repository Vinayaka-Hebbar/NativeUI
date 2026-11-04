namespace NativeUI.Media;

/// <summary>
/// Specifies the appearance of the mouse pointer cursor.
/// </summary>
public enum Cursor
{
    /// <summary>
    /// The platform determines which cursor to display based on context.
    /// Corresponds to CSS cursor: auto.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// The platform's default cursor, usually an arrow, whatever the element contains.
    /// Corresponds to CSS cursor: default.
    /// </summary>
    Default = 1,

    /// <summary>An arrow pointer.</summary>
    Arrow = 2,

    /// <summary>A pointing hand indicating a clickable link or control.</summary>
    Pointer = 3,

    /// <summary>An I-beam indicating selectable text or editable input.</summary>
    IBeam = 4,

    /// <summary>A circle with a line indicating an action that is not allowed.</summary>
    NotAllowed = 5,

    /// <summary>A crosshair for precise targeting or selection.</summary>
    Crosshair = 6,

    /// <summary>A wait cursor or hourglass indicating background activity.</summary>
    Wait = 7,

    /// <summary>Alias for <see cref="Pointer"/>.</summary>
    Hand = Pointer
}

namespace NativeUI.Media;

/// <summary>
/// Specifies the appearance of the mouse pointer cursor.
/// </summary>
public enum Cursor
{
    /// <summary>The default platform cursor (usually an arrow).</summary>
    Default = 0,

    /// <summary>An arrow pointer.</summary>
    Arrow = 1,

    /// <summary>A pointing hand indicating a clickable link or control.</summary>
    Pointer = 2,

    /// <summary>An I-beam indicating selectable text or editable input.</summary>
    IBeam = 3,

    /// <summary>A circle with a line indicating an action that is not allowed.</summary>
    NotAllowed = 4,

    /// <summary>A crosshair for precise targeting or selection.</summary>
    Crosshair = 5,

    /// <summary>A wait cursor or hourglass indicating background activity.</summary>
    Wait = 6,

    /// <summary>Alias for <see cref="Pointer"/>.</summary>
    Hand = Pointer
}

namespace NativeUI.Media;

/// <summary>
/// Specifies how text breaks into lines, as the CSS <c>white-space</c> property does.
/// </summary>
public enum WhiteSpace
{
    /// <summary>
    /// Text wraps at the available width.
    /// </summary>
    Normal,

    /// <summary>
    /// Text breaks only at line feeds and never wraps.
    /// </summary>
    NoWrap
}

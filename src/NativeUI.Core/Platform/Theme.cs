namespace NativeUI.Core;

/// <summary>
/// Specifies the appearance theme of an <see cref="Application"/>.
/// </summary>
public enum Theme
{
    /// <summary>
    /// Follows the theme reported by the operating system or browser.
    /// </summary>
    Default,

    /// <summary>
    /// Explicitly requests the light appearance theme.
    /// </summary>
    Light,

    /// <summary>
    /// Explicitly requests the dark appearance theme.
    /// </summary>
    Dark
}

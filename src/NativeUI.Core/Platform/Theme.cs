namespace NativeUI.Core;

/// <summary>
/// Specifies the appearance theme for an application, window, or visual subtree.
/// </summary>
public enum Theme
{
    /// <summary>
    /// Inherits the theme from the parent element or system platform settings.
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

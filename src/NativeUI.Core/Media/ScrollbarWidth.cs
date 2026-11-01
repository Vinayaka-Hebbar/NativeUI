namespace NativeUI.Media;

/// <summary>
/// Specifies the thickness of a view's scrollbar, matching the CSS <c>scrollbar-width</c> keywords.
/// </summary>
public enum ScrollbarWidth
{
    /// <summary>
    /// Uses the platform's default thickness.
    /// </summary>
    Auto,

    /// <summary>
    /// Uses a thinner scrollbar than the default.
    /// </summary>
    Thin,

    /// <summary>
    /// Hides the scrollbar while the view can still scroll.
    /// </summary>
    None
}

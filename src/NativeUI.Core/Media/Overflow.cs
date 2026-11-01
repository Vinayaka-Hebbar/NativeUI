namespace NativeUI.Media;

/// <summary>
/// Specifies how a view treats content that exceeds its bounds, matching the CSS <c>overflow</c> keywords.
/// </summary>
public enum Overflow
{
    /// <summary>
    /// Content draws outside the view's bounds.
    /// </summary>
    Visible,

    /// <summary>
    /// Content is clipped and can scroll only programmatically.
    /// </summary>
    Hidden,

    /// <summary>
    /// Content is clipped and the user can scroll it when it exceeds the view's bounds.
    /// </summary>
    Auto,

    /// <summary>
    /// Content is clipped and the user can scroll it. Browsers may always show a scrollbar.
    /// </summary>
    Scroll
}

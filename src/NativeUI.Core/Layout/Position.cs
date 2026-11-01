namespace NativeUI.Layout;

/// <summary>
/// Specifies how an element is positioned in the layout.
/// </summary>
public enum Position
{
    /// <summary>
    /// The element participates in normal layout flow according to its container.
    /// </summary>
    Static,

    /// <summary>
    /// The element participates in normal layout flow, but is offset relative to its normal position without affecting siblings.
    /// </summary>
    Relative,

    /// <summary>
    /// The element is removed from normal flow and positioned relative to its nearest positioned ancestor.
    /// </summary>
    Absolute,

    /// <summary>
    /// The element is removed from normal flow and positioned relative to the viewport.
    /// </summary>
    Fixed
}

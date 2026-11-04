namespace NativeUI.Controls;

/// <summary>
/// Specifies where scrolling places an item within the visible area.
/// </summary>
public enum ScrollAlignment
{
    /// <summary>
    /// The item's start edge aligns with the start of the visible area.
    /// </summary>
    Start,

    /// <summary>
    /// The item is centered in the visible area.
    /// </summary>
    Center,

    /// <summary>
    /// The item's end edge aligns with the end of the visible area.
    /// </summary>
    End,

    /// <summary>
    /// The visible area scrolls the least distance that shows the item, and does not scroll when the item is visible.
    /// </summary>
    Nearest
}

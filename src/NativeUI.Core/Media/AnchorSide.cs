namespace NativeUI.Media;

/// <summary>
/// Specifies an anchor edge used by an inset reference.
/// </summary>
public enum AnchorSide
{
    /// <summary>
    /// The top edge.
    /// </summary>
    Top,

    /// <summary>
    /// The right edge.
    /// </summary>
    Right,

    /// <summary>
    /// The bottom edge.
    /// </summary>
    Bottom,

    /// <summary>
    /// The left edge.
    /// </summary>
    Left,

    /// <summary>
    /// The start edge on the inset axis.
    /// </summary>
    Start,

    /// <summary>
    /// The end edge on the inset axis.
    /// </summary>
    End,

    /// <summary>
    /// The start edge in the positioned element’s writing direction.
    /// </summary>
    SelfStart,

    /// <summary>
    /// The end edge in the positioned element’s writing direction.
    /// </summary>
    SelfEnd,

    /// <summary>
    /// The center on the inset axis.
    /// </summary>
    Center,

    /// <summary>
    /// The edge corresponding to the inset property.
    /// </summary>
    Inside,

    /// <summary>
    /// The edge opposite the inset property.
    /// </summary>
    Outside
}

namespace NativeUI.Media;

/// <summary>
/// Specifies the occupied tracks on one axis of the anchor positioning grid.
/// </summary>
public enum PositionAreaTrack
{
    /// <summary>
    /// The track before the anchor.
    /// </summary>
    Start,

    /// <summary>
    /// The track occupied by the anchor.
    /// </summary>
    Center,

    /// <summary>
    /// The track after the anchor.
    /// </summary>
    End,

    /// <summary>
    /// The start and center tracks.
    /// </summary>
    SpanStart,

    /// <summary>
    /// The center and end tracks.
    /// </summary>
    SpanEnd,

    /// <summary>
    /// All three tracks.
    /// </summary>
    SpanAll
}

namespace NativeUI.Media;

/// <summary>
/// Specifies how a line of text that does not fit its width is shown, as the CSS <c>text-overflow</c> property does.
/// </summary>
public enum TextOverflow
{
    /// <summary>
    /// The text overflows and an ancestor that clips overflow cuts it off.
    /// </summary>
    Clip,

    /// <summary>
    /// The text is shortened and ends with an ellipsis. It applies to text that does not wrap, which is a single line.
    /// </summary>
    Ellipsis
}

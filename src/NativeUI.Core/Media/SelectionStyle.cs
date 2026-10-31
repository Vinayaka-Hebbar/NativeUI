namespace NativeUI.Media;

/// <summary>
/// Represents an immutable snapshot of the colors applied to selected text, resolved separately from <see cref="ComputedStyle"/>.
/// </summary>
public sealed record SelectionStyle
{
    /// <summary>
    /// Gets the selection style that leaves both colors unspecified.
    /// </summary>
    public static SelectionStyle Default { get; } = new();

    /// <summary>
    /// Gets the selected text color, or <see langword="null"/> to keep the element's text color.
    /// </summary>
    public Color? TextColor { get; init; }

    /// <summary>
    /// Gets the selection background color, or <see langword="null"/> to use the renderer's default.
    /// </summary>
    public Color? BackgroundColor { get; init; }
}

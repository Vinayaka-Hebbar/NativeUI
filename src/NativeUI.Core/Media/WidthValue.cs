namespace NativeUI.Media;

/// <summary>
/// Represents a specified width: a length in logical pixels, or the CSS <c>fit-content</c> keyword.
/// </summary>
public readonly record struct WidthValue
{
    private WidthValue(double? pixels)
    {
        Pixels = pixels;
    }

    /// <summary>
    /// Gets the width that fits the element's content, limited to the available width.
    /// </summary>
    public static WidthValue FitContent { get; } = new(null);

    /// <summary>
    /// Gets the length in logical pixels, or <see langword="null"/> for <see cref="FitContent"/>.
    /// </summary>
    public double? Pixels { get; }

    /// <summary>
    /// Gets a value indicating whether this value is <see cref="FitContent"/>.
    /// </summary>
    public bool IsFitContent => Pixels is null;

    /// <summary>
    /// Converts a length in logical pixels to a <see cref="WidthValue"/>.
    /// </summary>
    public static implicit operator WidthValue(double pixels)
    {
        return new WidthValue(pixels);
    }
}

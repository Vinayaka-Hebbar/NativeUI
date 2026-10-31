namespace NativeUI.Layout;

/// <summary>
/// Represents a 2D width and height in logical pixels.
/// </summary>
/// <param name="Width">The horizontal dimension in logical pixels.</param>
/// <param name="Height">The vertical dimension in logical pixels.</param>
public readonly record struct Size(double Width, double Height)
{
    /// <summary>
    /// Gets an empty <see cref="Size"/> (0, 0).
    /// </summary>
    public static Size Zero => new(0, 0);
}

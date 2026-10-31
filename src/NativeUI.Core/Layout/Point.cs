namespace NativeUI.Layout;

/// <summary>
/// Represents a 2D coordinate in logical pixels.
/// </summary>
/// <param name="X">The horizontal coordinate in logical pixels.</param>
/// <param name="Y">The vertical coordinate in logical pixels.</param>
public readonly record struct Point(double X, double Y)
{
    /// <summary>
    /// Gets the origin <see cref="Point"/> (0, 0).
    /// </summary>
    public static Point Zero => new(0, 0);
}

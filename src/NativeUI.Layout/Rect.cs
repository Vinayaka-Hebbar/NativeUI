namespace NativeUI.Layout;

/// <summary>
/// Represents a rectangle in 2D logical pixel coordinates.
/// </summary>
/// <param name="X">The horizontal coordinate of the top-left corner.</param>
/// <param name="Y">The vertical coordinate of the top-left corner.</param>
/// <param name="Width">The width of the rectangle.</param>
/// <param name="Height">The height of the rectangle.</param>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    /// <summary>
    /// Gets the right edge coordinate (<see cref="X"/> + <see cref="Width"/>).
    /// </summary>
    public double Right => X + Width;

    /// <summary>
    /// Gets the bottom edge coordinate (<see cref="Y"/> + <see cref="Height"/>).
    /// </summary>
    public double Bottom => Y + Height;
}

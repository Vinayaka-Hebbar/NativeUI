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
    /// Gets an empty rectangle with coordinates and dimensions set to zero.
    /// </summary>
    public static Rect Empty => new(0, 0, 0, 0);

    /// <summary>
    /// Gets the right edge coordinate (<see cref="X"/> + <see cref="Width"/>).
    /// </summary>
    public double Right => X + Width;

    /// <summary>
    /// Gets the bottom edge coordinate (<see cref="Y"/> + <see cref="Height"/>).
    /// </summary>
    public double Bottom => Y + Height;

    /// <summary>
    /// Gets the size dimensions of the rectangle.
    /// </summary>
    public Size Size => new(Width, Height);

    /// <summary>
    /// Returns a value indicating whether <paramref name="point"/> lies within or on the edges of this rectangle.
    /// </summary>
    public bool Contains(Point point)
    {
        return point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
    }

    /// <summary>
    /// Computes the intersection of this rectangle and <paramref name="other"/>.
    /// Returns an empty rectangle if there is no intersection.
    /// </summary>
    public Rect Intersect(Rect other)
    {
        double newX = Math.Max(X, other.X);
        double newY = Math.Max(Y, other.Y);
        double newRight = Math.Min(Right, other.Right);
        double newBottom = Math.Min(Bottom, other.Bottom);

        if (newRight > newX && newBottom > newY)
        {
            return new Rect(newX, newY, newRight - newX, newBottom - newY);
        }

        return new Rect(0, 0, 0, 0);
    }
}

namespace NativeUI.Media;

/// <summary>
/// Represents the radii of the four corners of a box in logical pixels, in the order of the CSS <c>border-radius</c>
/// property. Radii whose sum exceeds a side of the box are scaled down together when the box is drawn.
/// </summary>
/// <param name="TopLeft">The radius of the top-left corner.</param>
/// <param name="TopRight">The radius of the top-right corner.</param>
/// <param name="BottomRight">The radius of the bottom-right corner.</param>
/// <param name="BottomLeft">The radius of the bottom-left corner.</param>
public readonly record struct CornerRadius(double TopLeft, double TopRight, double BottomRight, double BottomLeft)
{
    /// <summary>
    /// Initializes a <see cref="CornerRadius"/> with the same radius at every corner.
    /// </summary>
    /// <param name="uniform">The radius of every corner.</param>
    public CornerRadius(double uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    /// Gets a <see cref="CornerRadius"/> with square corners.
    /// </summary>
    public static CornerRadius Zero => new(0, 0, 0, 0);

    /// <summary>
    /// Gets a value indicating whether every corner has the same radius.
    /// </summary>
    public bool IsUniform => TopLeft == TopRight && TopLeft == BottomRight && TopLeft == BottomLeft;

    /// <summary>
    /// Gets a value indicating whether every corner is square.
    /// </summary>
    public bool IsZero => TopLeft <= 0 && TopRight <= 0 && BottomRight <= 0 && BottomLeft <= 0;

    /// <summary>
    /// Converts a single radius to a <see cref="CornerRadius"/> with that radius at every corner.
    /// </summary>
    /// <param name="uniform">The radius of every corner.</param>
    public static implicit operator CornerRadius(double uniform) => new(uniform);

    /// <summary>
    /// Returns the radii reduced by <paramref name="amount"/>, each no smaller than zero, such as the inner edge of a border.
    /// </summary>
    /// <param name="amount">The distance to reduce every radius by.</param>
    public CornerRadius Deflate(double amount)
    {
        return new CornerRadius(
            Math.Max(0, TopLeft - amount),
            Math.Max(0, TopRight - amount),
            Math.Max(0, BottomRight - amount),
            Math.Max(0, BottomLeft - amount));
    }
}

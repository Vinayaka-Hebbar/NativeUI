namespace NativeUI.Media;

/// <summary>
/// Represents the thickness of a box edge (such as margin, padding, or border width) in logical pixels.
/// </summary>
/// <param name="Top">The top edge thickness.</param>
/// <param name="Right">The right edge thickness.</param>
/// <param name="Bottom">The bottom edge thickness.</param>
/// <param name="Left">The left edge thickness.</param>
public readonly record struct Thickness(double Top, double Right, double Bottom, double Left)
{
    /// <summary>
    /// Gets a uniform zero thickness.
    /// </summary>
    public static Thickness Zero => new(0, 0, 0, 0);

    /// <summary>
    /// Initializes a uniform <see cref="Thickness"/> across all four edges.
    /// </summary>
    public Thickness(double uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    /// Initializes a <see cref="Thickness"/> with vertical (top/bottom) and horizontal (left/right) values.
    /// </summary>
    public Thickness(double vertical, double horizontal)
        : this(vertical, horizontal, vertical, horizontal)
    {
    }

    /// <summary>
    /// Gets the combined horizontal thickness (<see cref="Left"/> + <see cref="Right"/>).
    /// </summary>
    public double Horizontal => Left + Right;

    /// <summary>
    /// Gets the combined vertical thickness (<see cref="Top"/> + <see cref="Bottom"/>).
    /// </summary>
    public double Vertical => Top + Bottom;
}

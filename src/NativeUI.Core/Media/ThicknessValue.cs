namespace NativeUI.Media;

/// <summary>
/// Represents the specified thickness of the four edges of a box, such as a margin or padding, each a length or a
/// percentage of the width of the containing block. <see cref="Resolve"/> converts the edges to a <see cref="Thickness"/>
/// once the width of the containing block is known.
/// </summary>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Left">The left edge.</param>
public readonly record struct ThicknessValue(EdgeValue Top, EdgeValue Right, EdgeValue Bottom, EdgeValue Left)
{
    /// <summary>
    /// Initializes a <see cref="ThicknessValue"/> with the same edge on all four sides.
    /// </summary>
    /// <param name="uniform">The edge on every side.</param>
    public ThicknessValue(EdgeValue uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    /// Initializes a <see cref="ThicknessValue"/> with a vertical (top and bottom) and a horizontal (left and right) edge.
    /// </summary>
    /// <param name="vertical">The top and bottom edge.</param>
    /// <param name="horizontal">The left and right edge.</param>
    public ThicknessValue(EdgeValue vertical, EdgeValue horizontal)
        : this(vertical, horizontal, vertical, horizontal)
    {
    }

    /// <summary>
    /// Gets a <see cref="ThicknessValue"/> whose edges are all zero.
    /// </summary>
    public static ThicknessValue Zero => default;

    /// <summary>
    /// Converts the thickness in logical pixels of a <see cref="Thickness"/>.
    /// </summary>
    public static implicit operator ThicknessValue(Thickness thickness)
    {
        return new ThicknessValue(thickness.Top, thickness.Right, thickness.Bottom, thickness.Left);
    }

    /// <summary>
    /// Returns the thickness in logical pixels. A percentage resolves against <paramref name="containingWidth"/> and is
    /// zero when that width is not finite.
    /// </summary>
    /// <param name="containingWidth">The width of the containing block.</param>
    public Thickness Resolve(double containingWidth)
    {
        return new Thickness(
            Top.Resolve(containingWidth),
            Right.Resolve(containingWidth),
            Bottom.Resolve(containingWidth),
            Left.Resolve(containingWidth));
    }
}

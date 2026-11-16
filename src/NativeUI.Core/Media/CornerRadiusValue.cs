using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>
/// Represents the specified radii of the four corners of a box, each a length or a percentage, in the order of the CSS
/// <c>border-radius</c> property. <see cref="Resolve"/> converts the radii to a <see cref="CornerRadius"/> once the size
/// of the box is known.
/// </summary>
/// <param name="TopLeft">The radius of the top-left corner.</param>
/// <param name="TopRight">The radius of the top-right corner.</param>
/// <param name="BottomRight">The radius of the bottom-right corner.</param>
/// <param name="BottomLeft">The radius of the bottom-left corner.</param>
public readonly record struct CornerRadiusValue(
    RadiusValue TopLeft,
    RadiusValue TopRight,
    RadiusValue BottomRight,
    RadiusValue BottomLeft)
{
    /// <summary>
    /// Initializes a <see cref="CornerRadiusValue"/> with the same radius at every corner.
    /// </summary>
    /// <param name="uniform">The radius of every corner.</param>
    public CornerRadiusValue(RadiusValue uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    /// Gets a value indicating whether every corner has the same radius.
    /// </summary>
    public bool IsUniform => TopLeft == TopRight && TopLeft == BottomRight && TopLeft == BottomLeft;

    /// <summary>
    /// Converts a radius in logical pixels to a <see cref="CornerRadiusValue"/> with that radius at every corner.
    /// </summary>
    public static implicit operator CornerRadiusValue(double uniform)
    {
        return new CornerRadiusValue(uniform);
    }

    /// <summary>
    /// Converts the radii in logical pixels of a <see cref="CornerRadius"/>.
    /// </summary>
    public static implicit operator CornerRadiusValue(CornerRadius radius)
    {
        return new CornerRadiusValue(radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
    }

    /// <summary>
    /// Returns the radii in logical pixels. A percentage resolves against the shorter side of
    /// <paramref name="borderBox"/>, because a <see cref="CornerRadius"/> is circular.
    /// </summary>
    /// <param name="borderBox">The size of the border box of the element.</param>
    public CornerRadius Resolve(Size borderBox)
    {
        double reference = Math.Min(borderBox.Width, borderBox.Height);
        return new CornerRadius(
            TopLeft.Resolve(reference),
            TopRight.Resolve(reference),
            BottomRight.Resolve(reference),
            BottomLeft.Resolve(reference));
    }
}

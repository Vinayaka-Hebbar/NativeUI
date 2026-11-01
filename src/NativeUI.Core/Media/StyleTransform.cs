using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>
/// Represents a CSS <c>transform</c>: a translation followed by a scale about the center of the element's border box.
/// A transform does not affect layout.
/// </summary>
/// <param name="TranslateX">The horizontal translation in logical pixels.</param>
/// <param name="TranslateY">The vertical translation in logical pixels.</param>
/// <param name="TranslateXPercent">An additional horizontal translation, as a percentage of the element's width.</param>
/// <param name="TranslateYPercent">An additional vertical translation, as a percentage of the element's height.</param>
/// <param name="ScaleX">The horizontal scale factor.</param>
/// <param name="ScaleY">The vertical scale factor.</param>
public readonly record struct StyleTransform(
    double TranslateX = 0,
    double TranslateY = 0,
    double TranslateXPercent = 0,
    double TranslateYPercent = 0,
    double ScaleX = 1,
    double ScaleY = 1)
{
    /// <summary>
    /// Initializes a transform that leaves an element unchanged. Without this constructor, <c>new()</c> would zero every
    /// field instead of using the parameter defaults, which scales an element to nothing.
    /// </summary>
    public StyleTransform()
        : this(0, 0, 0, 0, 1, 1)
    {
    }

    /// <summary>
    /// Gets the transform that leaves an element unchanged.
    /// </summary>
    public static StyleTransform Identity { get; } = new();

    /// <summary>
    /// Gets a value indicating whether this transform leaves an element unchanged.
    /// </summary>
    public bool IsIdentity => this == Identity;

    /// <summary>
    /// Gets the translation in logical pixels for an element with the given <paramref name="size"/>.
    /// </summary>
    public Point GetTranslation(Size size)
    {
        return new Point(
            TranslateX + (TranslateXPercent / 100.0 * size.Width),
            TranslateY + (TranslateYPercent / 100.0 * size.Height));
    }

    /// <summary>
    /// Maps a point on the screen to the untransformed coordinates of an element with the given <paramref name="bounds"/>.
    /// An axis scaled to zero covers no points, so it maps to the point outside the bounds.
    /// </summary>
    public Point ToUntransformed(Point point, Rect bounds)
    {
        Point translation = GetTranslation(bounds.Size);
        double centerX = bounds.X + (bounds.Width / 2.0);
        double centerY = bounds.Y + (bounds.Height / 2.0);
        return new Point(
            ScaleX == 0 ? bounds.X - 1 : centerX + ((point.X - translation.X - centerX) / ScaleX),
            ScaleY == 0 ? bounds.Y - 1 : centerY + ((point.Y - translation.Y - centerY) / ScaleY));
    }
}

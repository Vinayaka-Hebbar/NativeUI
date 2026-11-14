using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>
/// Represents a CSS <c>transform</c>: a translation followed by a rotation and a scale about the center of the element's
/// border box. A transform does not affect layout.
/// </summary>
/// <param name="TranslateX">The horizontal translation in logical pixels.</param>
/// <param name="TranslateY">The vertical translation in logical pixels.</param>
/// <param name="TranslateXPercent">An additional horizontal translation, as a percentage of the element's width.</param>
/// <param name="TranslateYPercent">An additional vertical translation, as a percentage of the element's height.</param>
/// <param name="ScaleX">The horizontal scale factor.</param>
/// <param name="ScaleY">The vertical scale factor.</param>
/// <param name="Rotation">The clockwise rotation angle in degrees.</param>
public readonly record struct StyleTransform(
    double TranslateX = 0,
    double TranslateY = 0,
    double TranslateXPercent = 0,
    double TranslateYPercent = 0,
    double ScaleX = 1,
    double ScaleY = 1,
    double Rotation = 0)
{
    /// <summary>
    /// Initializes a transform that leaves an element unchanged. Without this constructor, <c>new()</c> would zero every
    /// field instead of using the parameter defaults, which scales an element to nothing.
    /// </summary>
    public StyleTransform()
        : this(0, 0, 0, 0, 1, 1, 0)
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
        double dx = point.X - translation.X - centerX;
        double dy = point.Y - translation.Y - centerY;
        if (Rotation != 0)
        {
            double radians = -Rotation * (Math.PI / 180.0);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            (dx, dy) = ((dx * cos) - (dy * sin), (dx * sin) + (dy * cos));
        }

        return new Point(
            ScaleX == 0 ? bounds.X - 1 : centerX + (dx / ScaleX),
            ScaleY == 0 ? bounds.Y - 1 : centerY + (dy / ScaleY));
    }

    /// <summary>
    /// Applies this transform about the center of <paramref name="bounds"/> to <paramref name="point"/>.
    /// </summary>
    public Point TransformPoint(Point point, Rect bounds)
    {
        Point translation = GetTranslation(bounds.Size);
        double centerX = bounds.X + (bounds.Width / 2.0);
        double centerY = bounds.Y + (bounds.Height / 2.0);
        double dx = (point.X - centerX) * ScaleX;
        double dy = (point.Y - centerY) * ScaleY;
        if (Rotation != 0)
        {
            double radians = Rotation * (Math.PI / 180.0);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            (dx, dy) = ((dx * cos) - (dy * sin), (dx * sin) + (dy * cos));
        }

        return new Point(centerX + translation.X + dx, centerY + translation.Y + dy);
    }

    /// <summary>
    /// Returns the axis-aligned bounding box of <paramref name="rect"/> after applying this transform about the center of
    /// <paramref name="bounds"/>.
    /// </summary>
    public Rect TransformBounds(Rect rect, Rect bounds)
    {
        if (IsIdentity)
        {
            return rect;
        }

        Point topLeft = TransformPoint(new Point(rect.X, rect.Y), bounds);
        Point topRight = TransformPoint(new Point(rect.Right, rect.Y), bounds);
        Point bottomRight = TransformPoint(new Point(rect.Right, rect.Bottom), bounds);
        Point bottomLeft = TransformPoint(new Point(rect.X, rect.Bottom), bounds);

        double left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomRight.X, bottomLeft.X));
        double top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomRight.Y, bottomLeft.Y));
        double right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomRight.X, bottomLeft.X));
        double bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomRight.Y, bottomLeft.Y));
        return new Rect(left, top, right - left, bottom - top);
    }
}

using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Provides shared immutable icon geometry for controls.
/// </summary>
internal static class Icons
{
    /// <summary>
    /// Gets the filled checkmark geometry.
    /// </summary>
    internal static readonly PathGeometry Checkmark = new(
    [
        new PathFigure
        {
            StartPoint = new Point(6.2, 11.6),
            IsClosed = true,
            Segments =
            [
                new LineSegment(new Point(2.6, 8)),
                new LineSegment(new Point(3.8, 6.8)),
                new LineSegment(new Point(6.2, 9.2)),
                new LineSegment(new Point(12.2, 3.2)),
                new LineSegment(new Point(13.4, 4.4))
            ]
        }
    ]);

    /// <summary>
    /// Gets the filled dash geometry used by indeterminate check boxes.
    /// </summary>
    internal static readonly PathGeometry Dash = new(
    [
        new PathFigure
        {
            StartPoint = new Point(3, 7),
            IsClosed = true,
            Segments =
            [
                new LineSegment(new Point(13, 7)),
                new LineSegment(new Point(13, 9)),
                new LineSegment(new Point(3, 9))
            ]
        }
    ]);

    /// <summary>
    /// Gets the right-pointing chevron geometry used by submenu indicators.
    /// </summary>
    internal static readonly PathGeometry ChevronDown = new(
    [
        new PathFigure
        {
            StartPoint = new Point(13, 6),
            IsClosed = true,
            Segments =
            [
                new LineSegment(new Point(8, 11)),
                new LineSegment(new Point(3, 6)),
                new LineSegment(new Point(4.25, 4.75)),
                new LineSegment(new Point(8, 8.5)),
                new LineSegment(new Point(11.75, 4.75))
            ]
        }
    ]);

    internal static readonly PathGeometry ChevronRight = new(
    [
        new PathFigure
        {
            StartPoint = new Point(6, 3),
            IsClosed = true,
            Segments =
            [
                new LineSegment(new Point(11, 8)),
                new LineSegment(new Point(6, 13)),
                new LineSegment(new Point(4.75, 11.75)),
                new LineSegment(new Point(8.5, 8)),
                new LineSegment(new Point(4.75, 4.25))
            ]
        }
    ]);

}

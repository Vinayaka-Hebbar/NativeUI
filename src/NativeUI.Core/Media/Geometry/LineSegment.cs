using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>A straight line to an endpoint.</summary>
public sealed record LineSegment : PathSegment
{
    /// <summary>Creates a line ending at the given point.</summary>
    public LineSegment(Point point)
    {
        Point = point;
    }

    /// <summary>The endpoint.</summary>
    public Point Point { get; init; }

}

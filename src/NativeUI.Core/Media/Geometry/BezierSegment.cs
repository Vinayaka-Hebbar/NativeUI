using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>A cubic curve with two control points.</summary>
public sealed record BezierSegment : PathSegment
{
    /// <summary>The first control point.</summary>
    public Point ControlPoint1 { get; init; }

    /// <summary>The second control point.</summary>
    public Point ControlPoint2 { get; init; }

    /// <summary>The endpoint.</summary>
    public Point Point { get; init; }

}

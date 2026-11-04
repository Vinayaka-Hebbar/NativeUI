using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>A quadratic curve with one control point.</summary>
public sealed record QuadraticBezierSegment : PathSegment
{
    /// <summary>The control point.</summary>
    public Point ControlPoint { get; init; }

    /// <summary>The endpoint.</summary>
    public Point Point { get; init; }

}

using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>An SVG endpoint elliptical arc.</summary>
public sealed record ArcSegment : PathSegment
{
    /// <summary>The endpoint.</summary>
    public Point Point { get; init; }

    /// <summary>The ellipse radii.</summary>
    public Size Size { get; init; }

    /// <summary>The clockwise axis rotation in degrees.</summary>
    public double RotationAngle { get; init; }

    /// <summary>Whether to select the larger arc.</summary>
    public bool IsLargeArc { get; init; }

    /// <summary>The sweep orientation.</summary>
    public SweepDirection SweepDirection { get; init; }

}

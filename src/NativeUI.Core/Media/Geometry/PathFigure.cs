using System.Collections.Immutable;
using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>An immutable connected contour of a path.</summary>
public sealed record PathFigure
{
    /// <summary>Gets the contour starting point.</summary>
    public Point StartPoint { get; init; }

    /// <summary>Gets whether the contour closes to its starting point.</summary>
    public bool IsClosed { get; init; }

    /// <summary>Gets the immutable contour segments.</summary>
    public ImmutableArray<PathSegment> Segments { get; init; } = [];
}

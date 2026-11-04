using System.Collections.Immutable;
using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>An immutable set of connected vector contours.</summary>
public sealed class PathGeometry : Geometry
{
    /// <summary>Creates geometry by copying the supplied figures.</summary>
    public PathGeometry(IEnumerable<PathFigure> figures, FillRule fillRule = FillRule.Nonzero)
    {
        ArgumentNullException.ThrowIfNull(figures);
        Figures = [.. figures];
        FillRule = fillRule;
    }

    /// <summary>Gets the immutable figures.</summary>
    public ImmutableArray<PathFigure> Figures { get; }

    /// <summary>Gets the rule used to determine the filled interior.</summary>
    public FillRule FillRule { get; }

    internal static PathGeometry FromCommands(IReadOnlyList<PathCommand> commands)
    {
        List<PathFigure> figures = [];
        List<PathSegment> segments = [];
        Point start = default;
        bool active = false;
        foreach (PathCommand command in commands)
        {
            double[] v = command.Values;
            if (command.Kind == 'M')
            {
                if (active) figures.Add(new PathFigure { StartPoint = start, Segments = [.. segments] });
                start = new Point(v[0], v[1]);
                segments.Clear();
                active = true;
            }
            else if (command.Kind == 'Z')
            {
                figures.Add(new PathFigure { StartPoint = start, IsClosed = true, Segments = [.. segments] });
                segments.Clear();
                active = false;
            }
            else
            {
                active = true;
                segments.Add(command.Kind switch
                {
                    'L' => new LineSegment(new Point(v[0], v[1])),
                    'Q' => new QuadraticBezierSegment { ControlPoint = new Point(v[0], v[1]), Point = new Point(v[2], v[3]) },
                    'C' => new BezierSegment { ControlPoint1 = new Point(v[0], v[1]), ControlPoint2 = new Point(v[2], v[3]), Point = new Point(v[4], v[5]) },
                    'A' => new ArcSegment { Size = new Size(v[0], v[1]), RotationAngle = v[2], IsLargeArc = v[3] == 1, SweepDirection = v[4] == 1 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise, Point = new Point(v[5], v[6]) },
                    _ => throw new FormatException("Invalid normalized command.")
                });
            }
        }
        if (active) figures.Add(new PathFigure { StartPoint = start, Segments = [.. segments] });
        return new PathGeometry(figures);
    }
}

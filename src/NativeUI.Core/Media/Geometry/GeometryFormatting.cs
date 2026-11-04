using System.Globalization;
using System.Text;
using NativeUI.Layout;

namespace NativeUI.Media;

/// <summary>Converts immutable geometry to browser SVG path data.</summary>
public static class GeometryFormatting
{
    /// <summary>Serializes normalized figures and segments without reparsing source text.</summary>
    public static string PathData(PathGeometry geometry)
    {
        StringBuilder result = new();
        string N(double n) => n.ToString("R", CultureInfo.InvariantCulture);
        string P(Point p) => N(p.X) + "," + N(p.Y);
        foreach (PathFigure figure in geometry.Figures)
        {
            result.Append('M').Append(P(figure.StartPoint));
            foreach (PathSegment segment in figure.Segments)
            {
                result.Append(segment switch
                {
                    LineSegment line => "L" + P(line.Point),
                    QuadraticBezierSegment q => "Q" + P(q.ControlPoint) + " " + P(q.Point),
                    BezierSegment c => "C" + P(c.ControlPoint1) + " " + P(c.ControlPoint2) + " " + P(c.Point),
                    ArcSegment a => "A" + N(a.Size.Width) + "," + N(a.Size.Height) + " " + N(a.RotationAngle) + " " + (a.IsLargeArc ? "1" : "0") + " " + (a.SweepDirection == SweepDirection.Clockwise ? "1" : "0") + " " + P(a.Point),
                    _ => throw new NotSupportedException("Unknown path segment.")
                });
            }
            if (figure.IsClosed) result.Append('Z');
        }
        return result.ToString();
    }

    /// <summary>Returns exact extrema bounds for lines, curves, and endpoint arcs.</summary>
    public static Rect Bounds(PathGeometry geometry) => GeometryBounds.Calculate(geometry);
}

using NativeUI.Layout;

namespace NativeUI.Media;

internal static class GeometryBounds
{
    public static Rect Calculate(PathGeometry geometry)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        void Add(Point p)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }
        foreach (PathFigure figure in geometry.Figures)
        {
            Point current = figure.StartPoint;
            Add(current);
            foreach (PathSegment segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line:
                        Add(line.Point);
                        current = line.Point;
                        break;
                    case QuadraticBezierSegment q:
                        Add(q.Point);
                        foreach (double t in new[] { QuadraticExtremum(current.X, q.ControlPoint.X, q.Point.X), QuadraticExtremum(current.Y, q.ControlPoint.Y, q.Point.Y) })
                        {
                            if (t > 0 && t < 1) Add(Quadratic(current, q.ControlPoint, q.Point, t));
                        }
                        current = q.Point;
                        break;
                    case BezierSegment c:
                        Add(c.Point);
                        foreach (double t in CubicExtrema(current.X, c.ControlPoint1.X, c.ControlPoint2.X, c.Point.X).Concat(CubicExtrema(current.Y, c.ControlPoint1.Y, c.ControlPoint2.Y, c.Point.Y)))
                        {
                            if (t > 0 && t < 1) Add(Cubic(current, c.ControlPoint1, c.ControlPoint2, c.Point, t));
                        }
                        current = c.Point;
                        break;
                    case ArcSegment arc:
                        Add(arc.Point);
                        ArcBounds(current, arc, Add);
                        current = arc.Point;
                        break;
                }
            }
        }
        return double.IsPositiveInfinity(minX) ? Rect.Empty : new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static double QuadraticExtremum(double p0, double p1, double p2) => (p0 - p1) / (p0 - 2 * p1 + p2);
    private static Point Quadratic(Point a, Point b, Point c, double t) => new((1-t)*(1-t)*a.X + 2*(1-t)*t*b.X + t*t*c.X, (1-t)*(1-t)*a.Y + 2*(1-t)*t*b.Y + t*t*c.Y);
    private static Point Cubic(Point a, Point b, Point c, Point d, double t)
    {
        double u = 1-t;
        return new Point(u*u*u*a.X + 3*u*u*t*b.X + 3*u*t*t*c.X + t*t*t*d.X, u*u*u*a.Y + 3*u*u*t*b.Y + 3*u*t*t*c.Y + t*t*t*d.Y);
    }
    private static IEnumerable<double> CubicExtrema(double p0, double p1, double p2, double p3)
    {
        double a = -p0 + 3*p1 - 3*p2 + p3, b = 2*(p0 - 2*p1 + p2), c = p1 - p0;
        if (Math.Abs(a) < 1e-14)
        {
            if (Math.Abs(b) > 1e-14) yield return -c / b;
            yield break;
        }
        double discriminant = b*b - 4*a*c;
        if (discriminant < 0) yield break;
        double root = Math.Sqrt(discriminant);
        yield return (-b + root)/(2*a);
        yield return (-b - root)/(2*a);
    }
    private static void ArcBounds(Point start, ArcSegment arc, Action<Point> add)
    {
        if (start == arc.Point || arc.Size.Width == 0 || arc.Size.Height == 0) return;
        double phi = arc.RotationAngle * Math.PI/180, cos = Math.Cos(phi), sin = Math.Sin(phi);
        double rx = Math.Abs(arc.Size.Width), ry = Math.Abs(arc.Size.Height);
        double dx = (start.X-arc.Point.X)/2, dy = (start.Y-arc.Point.Y)/2;
        double xp = cos*dx + sin*dy, yp = -sin*dx + cos*dy;
        double lambda = xp*xp/(rx*rx) + yp*yp/(ry*ry);
        if (lambda > 1) { double scale = Math.Sqrt(lambda); rx *= scale; ry *= scale; }
        bool clockwise = arc.SweepDirection == SweepDirection.Clockwise;
        double factor = Math.Sqrt(Math.Max(0, (rx*rx*ry*ry - rx*rx*yp*yp - ry*ry*xp*xp)/(rx*rx*yp*yp + ry*ry*xp*xp)));
        if (arc.IsLargeArc == clockwise) factor = -factor;
        double cxp = factor * rx*yp/ry, cyp = -factor * ry*xp/rx;
        double cx = cos*cxp - sin*cyp + (start.X+arc.Point.X)/2;
        double cy = sin*cxp + cos*cyp + (start.Y+arc.Point.Y)/2;
        double theta = Math.Atan2((yp-cyp)/ry, (xp-cxp)/rx);
        double end = Math.Atan2((-yp-cyp)/ry, (-xp-cxp)/rx);
        double sweep = end-theta;
        if (clockwise && sweep < 0) sweep += 2*Math.PI;
        if (!clockwise && sweep > 0) sweep -= 2*Math.PI;
        double Normalize(double a) => (a % (2*Math.PI) + 2*Math.PI) % (2*Math.PI);
        double tx = Math.Atan2(-ry*sin, rx*cos), ty = Math.Atan2(ry*cos, rx*sin);
        foreach (double angle in new[] { tx, tx+Math.PI, ty, ty+Math.PI })
        {
            double distance = clockwise ? Normalize(angle-theta) : Normalize(theta-angle);
            if (distance <= Math.Abs(sweep) + 1e-12) add(new Point(cx+rx*cos*Math.Cos(angle)-ry*sin*Math.Sin(angle), cy+rx*sin*Math.Cos(angle)+ry*cos*Math.Sin(angle)));
        }
    }
}

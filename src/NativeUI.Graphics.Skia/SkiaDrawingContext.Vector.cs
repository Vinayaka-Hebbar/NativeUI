using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Graphics.Skia;

public partial class SkiaDrawingContext
{
    /// <inheritdoc />
    public override IImageDecoder ImageDecoder => SkiaImageDecoder.Shared;

    /// <inheritdoc />
    public override void DrawImage(IDecodedImage image, Rect destination, double opacity = 1)
    {
        if (image is not SkiaDecodedImage decoded) throw new ArgumentException("The image belongs to another backend.", nameof(image));
        decoded.Draw(_canvas, ToSKRect(destination), opacity);
    }

    /// <summary>Creates an owned Skia path with explicit SVG arc direction mapping.</summary>
    public static SKPath CreatePath(PathGeometry geometry)
    {
        SKPath result = new() { FillType = geometry.FillRule == FillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding };
        foreach (PathFigure figure in geometry.Figures)
        {
            Point current = figure.StartPoint;
            result.MoveTo((float)current.X, (float)current.Y);
            foreach (PathSegment segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line:
                        result.LineTo((float)line.Point.X, (float)line.Point.Y);
                        current = line.Point;
                        break;
                    case QuadraticBezierSegment q:
                        result.QuadTo((float)q.ControlPoint.X, (float)q.ControlPoint.Y, (float)q.Point.X, (float)q.Point.Y);
                        current = q.Point;
                        break;
                    case BezierSegment c:
                        result.CubicTo((float)c.ControlPoint1.X, (float)c.ControlPoint1.Y, (float)c.ControlPoint2.X, (float)c.ControlPoint2.Y, (float)c.Point.X, (float)c.Point.Y);
                        current = c.Point;
                        break;
                    case ArcSegment a:
                        if (a.Point == current) break;
                        if (a.Size.Width == 0 || a.Size.Height == 0) result.LineTo((float)a.Point.X, (float)a.Point.Y);
                        else result.ArcTo((float)a.Size.Width, (float)a.Size.Height, (float)a.RotationAngle,
                            a.IsLargeArc ? SKPathArcSize.Large : SKPathArcSize.Small,
                            a.SweepDirection == SweepDirection.Clockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise,
                            (float)a.Point.X, (float)a.Point.Y);
                        current = a.Point;
                        break;
                }
            }
            if (figure.IsClosed) result.Close();
        }
        return result;
    }

    /// <inheritdoc />
    public override void DrawGeometry(PathGeometry geometry, Brush? fill, Brush? stroke, double strokeThickness, double opacity = 1)
    {
        SKPath path = Resources.GetOrCreate(geometry, () => CreatePath(geometry));
        if (fill is not null)
        {
            using SKPaint paint = BrushPaint(fill, opacity);
            _canvas.DrawPath(path, paint);
        }
        if (stroke is not null && strokeThickness > 0)
        {
            using SKPaint paint = BrushPaint(stroke, opacity);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = (float)strokeThickness;
            _canvas.DrawPath(path, paint);
        }
    }

    private static SKPaint BrushPaint(Brush brush, double opacity)
    {
        SKPaint result = new() { IsAntialias = true };
        if (brush is SolidColorBrush solid) result.Color = solid.Color.ToSKColor(opacity);
        else if (brush is GradientBrush gradient)
        {
            SKColor[] colors = gradient.Stops.Select(static stop => stop.Color.ToSKColor()).ToArray();
            float[] positions = gradient.Stops.Select(static stop => (float)stop.Offset).ToArray();
            result.Color = SKColors.White.WithAlpha((byte)Math.Clamp(opacity * 255, 0, 255));
            result.Shader = gradient is LinearGradientBrush linear
                ? SKShader.CreateLinearGradient(new SKPoint((float)linear.Start.X, (float)linear.Start.Y), new SKPoint((float)linear.End.X, (float)linear.End.Y), colors, positions, SKShaderTileMode.Clamp)
                : gradient is RadialGradientBrush radial ? SKShader.CreateRadialGradient(new SKPoint((float)radial.Center.X, (float)radial.Center.Y), (float)radial.Radius, colors, positions, SKShaderTileMode.Clamp) : null;
        }
        return result;
    }

    /// <inheritdoc />
    public override void DrawSvg(SvgNode definition, Rect viewport, double opacity = 1)
    {
        SkiaSvgDrawing.Draw(this, definition, viewport, opacity);
    }
}

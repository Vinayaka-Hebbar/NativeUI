using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Implements <see cref="DrawingContext"/> backed by a SkiaSharp <see cref="SKCanvas"/> and <see cref="SkiaTextShaper"/>.
/// </summary>
public partial class SkiaDrawingContext : DrawingContext
{
    private readonly SKCanvas _canvas;
    private readonly SkiaTextShaper _textShaper;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaDrawingContext"/> class.
    /// </summary>
    public SkiaDrawingContext(SKCanvas canvas, SkiaTextShaper? textShaper = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        _canvas = canvas;
        _textShaper = textShaper ?? SkiaTextShaper.Shared;
    }

    /// <summary>
    /// Gets the underlying SkiaSharp <see cref="SKCanvas"/>.
    /// </summary>
    public SKCanvas Canvas => _canvas;

    /// <inheritdoc />
    public override void Save()
    {
        _canvas.Save();
    }

    /// <inheritdoc />
    public override void Restore()
    {
        _canvas.Restore();
    }

    /// <inheritdoc />
    public override void Scale(float scaleX, float scaleY)
    {
        if (float.IsNaN(scaleX) || float.IsInfinity(scaleX))
        {
            throw new ArgumentException("Scale X must be a finite number.", nameof(scaleX));
        }

        if (float.IsNaN(scaleY) || float.IsInfinity(scaleY))
        {
            throw new ArgumentException("Scale Y must be a finite number.", nameof(scaleY));
        }

        _canvas.Scale(scaleX, scaleY);
    }

    /// <inheritdoc />
    public override void Rotate(float degrees)
    {
        if (float.IsNaN(degrees) || float.IsInfinity(degrees))
        {
            throw new ArgumentException("Rotation angle must be a finite number.", nameof(degrees));
        }

        _canvas.RotateDegrees(degrees);
    }

    /// <inheritdoc />
    public override void Translate(double dx, double dy)
    {
        if (double.IsNaN(dx) || double.IsInfinity(dx))
        {
            throw new ArgumentException("Translate dx must be a finite number.", nameof(dx));
        }

        if (double.IsNaN(dy) || double.IsInfinity(dy))
        {
            throw new ArgumentException("Translate dy must be a finite number.", nameof(dy));
        }

        _canvas.Translate((float)dx, (float)dy);
    }

    /// <inheritdoc />
    public override void ClipRegion(IReadOnlyList<Rect> region, ClipMode mode)
    {
        ArgumentNullException.ThrowIfNull(region);

        using SKPath path = new() { FillType = SKPathFillType.Winding };
        foreach (Rect rect in region)
        {
            path.AddRect(ToSKRect(rect));
        }

        // Aliased edges keep complementary clips from double-painting boundary pixels.
        SKClipOperation operation = mode == ClipMode.Intersect ? SKClipOperation.Intersect : SKClipOperation.Difference;
        _canvas.ClipPath(path, operation, antialias: false);
    }

    /// <inheritdoc />
    public override void ClipRect(Rect rect)
    {
        _canvas.ClipRect(ToSKRect(rect), SKClipOperation.Intersect, antialias: true);
    }

    /// <inheritdoc />
    public override void ClipRoundedRectangle(Rect rect, double radius)
    {
        float r = (float)radius;
        using SKRoundRect roundRect = new(ToSKRect(rect), r, r);
        _canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, antialias: true);
    }

    /// <inheritdoc />
    public override void ClipRoundedRectangle(Rect rect, CornerRadius radius)
    {
        using SKRoundRect roundRect = ToSKRoundRect(rect, radius);
        _canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, antialias: true);
    }

    /// <inheritdoc />
    public override void SaveLayer(Rect bounds, double opacity)
    {
        if (double.IsNaN(opacity))
        {
            throw new ArgumentException("Opacity must not be NaN.", nameof(opacity));
        }

        byte alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255.0), 0, 255);
        using SKPaint paint = new()
        {
            Color = new SKColor(255, 255, 255, alpha)
        };
        _canvas.SaveLayer(ToSKRect(bounds), paint);
    }

    /// <inheritdoc />
    public override void Clear(Color color)
    {
        _canvas.Clear(color.ToSKColor());
    }

    /// <inheritdoc />
    public override void DrawRectangle(Rect rect, Paint paint)
    {
        using SKPaint skPaint = new()
        {
            Color = paint.Color.ToSKColor(paint.Opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _canvas.DrawRect(ToSKRect(rect), skPaint);
    }

    /// <inheritdoc />
    public override void DrawRoundedRectangle(Rect rect, double radius, Paint paint)
    {
        using SKPaint skPaint = new()
        {
            Color = paint.Color.ToSKColor(paint.Opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        float r = (float)radius;
        _canvas.DrawRoundRect(ToSKRect(rect), r, r, skPaint);
    }

    /// <inheritdoc />
    public override void DrawRoundedRectangle(Rect rect, CornerRadius radius, Paint paint)
    {
        using SKPaint skPaint = new()
        {
            Color = paint.Color.ToSKColor(paint.Opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using SKRoundRect roundRect = ToSKRoundRect(rect, radius);
        _canvas.DrawRoundRect(roundRect, skPaint);
    }

    /// <inheritdoc />
    public override void FillLinearGradient(Rect rect, Point start, Point end, IReadOnlyList<GradientStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);

        SKColor[] colors = new SKColor[stops.Count];
        float[] offsets = new float[stops.Count];
        for (int i = 0; i < stops.Count; i++)
        {
            colors[i] = stops[i].Color.ToSKColor();
            offsets[i] = (float)stops[i].Offset;
        }

        using SKShader shader = SKShader.CreateLinearGradient(
            new SKPoint((float)start.X, (float)start.Y),
            new SKPoint((float)end.X, (float)end.Y),
            colors,
            offsets,
            SKShaderTileMode.Clamp);
        using SKPaint skPaint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = shader
        };

        _canvas.DrawRect(ToSKRect(rect), skPaint);
    }

    /// <inheritdoc />
    public override void FillPolygon(IReadOnlyList<Point> points, Paint paint)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
        {
            return;
        }

        using SKPath path = new();
        path.MoveTo((float)points[0].X, (float)points[0].Y);
        for (int i = 1; i < points.Count; i++)
        {
            path.LineTo((float)points[i].X, (float)points[i].Y);
        }

        path.Close();

        using SKPaint skPaint = new()
        {
            Color = paint.Color.ToSKColor(paint.Opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _canvas.DrawPath(path, skPaint);
    }

    /// <inheritdoc />
    public override void StrokeRoundedRectangle(Rect rect, double radius, double strokeWidth, Paint paint)
    {
        using SKPaint skPaint = new()
        {
            Color = paint.Color.ToSKColor(paint.Opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)strokeWidth
        };

        float r = (float)radius;
        _canvas.DrawRoundRect(ToSKRect(rect), r, r, skPaint);
    }

    /// <inheritdoc />
    public override void StrokeRoundedRectangle(Rect rect, CornerRadius radius, double strokeWidth, Paint paint)
    {
        using SKPaint skPaint = new()
        {
            Color = paint.Color.ToSKColor(paint.Opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)strokeWidth
        };

        using SKRoundRect roundRect = ToSKRoundRect(rect, radius);
        _canvas.DrawRoundRect(roundRect, skPaint);
    }

    /// <inheritdoc />
    public override void DrawBoxShadow(Rect rect, CornerRadius radius, BoxShadow shadow, double opacity = 1)
    {
        Color color = shadow.Color ?? Colors.Black;
        using SKPaint skPaint = new()
        {
            Color = color.ToSKColor(opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Fill,

            // CSS defines the blur as a Gaussian whose standard deviation is half the blur radius.
            MaskFilter = shadow.BlurRadius > 0 ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)(shadow.BlurRadius / 2.0)) : null
        };

        using SKRoundRect box = ToSKRoundRect(rect, radius);

        // An outer shadow is the offset box grown by the spread. An inset shadow surrounds the offset box shrunk by the spread.
        double grow = shadow.IsInset ? -shadow.SpreadRadius : shadow.SpreadRadius;
        Rect shape = new(
            rect.X + shadow.OffsetX - grow,
            rect.Y + shadow.OffsetY - grow,
            Math.Max(0, rect.Width + (grow * 2)),
            Math.Max(0, rect.Height + (grow * 2)));
        using SKRoundRect shapeRect = ToSKRoundRect(shape, SpreadCorners(radius, grow));

        _canvas.Save();
        try
        {
            if (shadow.IsInset)
            {
                _canvas.ClipRoundRect(box, SKClipOperation.Intersect, antialias: true);
                double margin = Math.Abs(shadow.OffsetX) + Math.Abs(shadow.OffsetY) + Math.Abs(shadow.SpreadRadius) + (shadow.BlurRadius * 2);
                using SKPath path = new() { FillType = SKPathFillType.EvenOdd };
                path.AddRect(ToSKRect(new Rect(rect.X - margin, rect.Y - margin, rect.Width + (margin * 2), rect.Height + (margin * 2))));
                path.AddRoundRect(shapeRect);
                _canvas.DrawPath(path, skPaint);
            }
            else
            {
                _canvas.ClipRoundRect(box, SKClipOperation.Difference, antialias: true);
                _canvas.DrawRoundRect(shapeRect, skPaint);
            }
        }
        finally
        {
            _canvas.Restore();
        }
    }

    /// <inheritdoc />
    public override void DrawTextLayout(
        ITextLayout layout,
        Point origin,
        Paint paint)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout is SkiaTextLayout skiaLayout)
        {
            skiaLayout.Draw(_canvas, origin, paint.Color, paint.Opacity);
        }
    }

    /// <inheritdoc />
    public override void DrawTextLayout(ITextLayout layout, Point origin, Color foreground, double opacity)
    {
        if (layout is SkiaTextLayout skiaLayout)
        {
            skiaLayout.Draw(_canvas, origin, foreground, opacity, foreground);
        }
    }

    /// <summary>
    /// Creates a rounded rectangle with circular corners. Skia scales radii whose sum exceeds a side down together, as CSS does.
    /// </summary>
    private static SKRoundRect ToSKRoundRect(Rect rect, CornerRadius radius)
    {
        SKRoundRect roundRect = new();
        roundRect.SetRectRadii(
            ToSKRect(rect),
            [
                new SKPoint((float)radius.TopLeft, (float)radius.TopLeft),
                new SKPoint((float)radius.TopRight, (float)radius.TopRight),
                new SKPoint((float)radius.BottomRight, (float)radius.BottomRight),
                new SKPoint((float)radius.BottomLeft, (float)radius.BottomLeft)
            ]);
        return roundRect;
    }

    /// <summary>
    /// Grows each rounded corner by <paramref name="spread"/>, or shrinks it for a negative spread. A square corner stays square.
    /// </summary>
    private static CornerRadius SpreadCorners(CornerRadius radius, double spread)
    {
        return new CornerRadius(
            radius.TopLeft > 0 ? Math.Max(0, radius.TopLeft + spread) : 0,
            radius.TopRight > 0 ? Math.Max(0, radius.TopRight + spread) : 0,
            radius.BottomRight > 0 ? Math.Max(0, radius.BottomRight + spread) : 0,
            radius.BottomLeft > 0 ? Math.Max(0, radius.BottomLeft + spread) : 0);
    }

    private static SKRect ToSKRect(Rect rect)
    {
        return new SKRect((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);
    }
}

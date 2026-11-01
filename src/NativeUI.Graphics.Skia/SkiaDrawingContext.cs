using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Implements <see cref="DrawingContext"/> backed by a SkiaSharp <see cref="SKCanvas"/> and <see cref="SkiaTextShaper"/>.
/// </summary>
public sealed class SkiaDrawingContext : DrawingContext
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

    private static SKRect ToSKRect(Rect rect)
    {
        return new SKRect((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);
    }
}

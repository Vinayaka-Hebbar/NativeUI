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
        _canvas.Scale(scaleX, scaleY);
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
    public override void Clear(Color color)
    {
        _canvas.Clear(color.ToSKColor());
    }

    /// <inheritdoc />
    public override void DrawRectangle(Rect rect, Color color, double opacity = 1.0)
    {
        using SKPaint paint = new()
        {
            Color = color.ToSKColor(opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _canvas.DrawRect(ToSKRect(rect), paint);
    }

    /// <inheritdoc />
    public override void DrawRoundedRectangle(Rect rect, double borderRadius, Color color, double opacity = 1.0)
    {
        using SKPaint paint = new()
        {
            Color = color.ToSKColor(opacity),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        float r = (float)borderRadius;
        _canvas.DrawRoundRect(ToSKRect(rect), r, r, paint);
    }

    /// <inheritdoc />
    public override void DrawTextLayout(
        ITextLayout textLayout,
        Point origin,
        Color color,
        double opacity = 1.0)
    {
        ArgumentNullException.ThrowIfNull(textLayout);
        if (textLayout is SkiaTextLayout skiaLayout)
        {
            skiaLayout.Draw(_canvas, origin, color, opacity);
        }
    }

    /// <inheritdoc />
    public override void DrawText(
        string text,
        Rect contentRect,
        Color color,
        double fontSize,
        FontWeight fontWeight,
        string? fontFamily,
        TextAlign textAlign,
        double opacity = 1.0)
    {
        ComputedStyle style = new()
        {
            FontSize = fontSize,
            FontWeight = fontWeight,
            FontFamily = fontFamily,
            TextAlign = textAlign
        };

        using SkiaTextLayout layout = _textShaper.ShapeText(
            text,
            new Size(contentRect.Width, contentRect.Height),
            style);
        layout.Draw(_canvas, new Point(contentRect.X, contentRect.Y), color, opacity);
    }

    private static SKRect ToSKRect(Rect rect)
    {
        return new SKRect(
            (float)rect.X,
            (float)rect.Y,
            (float)rect.Right,
            (float)rect.Bottom);
    }
}

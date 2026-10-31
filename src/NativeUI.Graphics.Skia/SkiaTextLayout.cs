using NativeUI.Layout;
using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Measures and draws text runs using SkiaSharp's <see cref="SKFont"/> and <see cref="SkiaResourceCache"/>.
/// </summary>
public sealed class SkiaTextLayout : ITextMeasurer
{
    private readonly SkiaResourceCache _resourceCache;

    /// <summary>
    /// Gets the shared singleton instance of <see cref="SkiaTextLayout"/>.
    /// </summary>
    public static SkiaTextLayout Shared { get; } = new(SkiaResourceCache.Shared);

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaTextLayout"/> class.
    /// </summary>
    public SkiaTextLayout(SkiaResourceCache resourceCache)
    {
        ArgumentNullException.ThrowIfNull(resourceCache);
        _resourceCache = resourceCache;
    }

    /// <inheritdoc />
    public Size MeasureText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        string? fontFamily,
        double maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new Size(0, fontSize * 1.25);
        }

        SKTypeface typeface = _resourceCache.GetTypeface(fontFamily, fontWeight);
        using SKFont font = CreateFont(typeface, fontSize);

        float measuredWidth = font.MeasureText(text);
        font.GetFontMetrics(out SKFontMetrics metrics);
        float lineHeight = Math.Max((float)(fontSize * 1.25), (-metrics.Ascent) + metrics.Descent + metrics.Leading);

        return new Size(measuredWidth, lineHeight);
    }

    /// <summary>
    /// Draws <paramref name="text"/> onto <paramref name="canvas"/> aligned within <paramref name="contentRect"/>.
    /// </summary>
    public void DrawText(
        SKCanvas canvas,
        string text,
        Rect contentRect,
        Color color,
        double fontSize,
        FontWeight fontWeight,
        string? fontFamily,
        TextAlign textAlign,
        double opacity = 1.0)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        SKTypeface typeface = _resourceCache.GetTypeface(fontFamily, fontWeight);
        using SKFont font = CreateFont(typeface, fontSize);
        using SKPaint paint = new()
        {
            Color = color.ToSKColor(opacity),
            IsAntialias = true
        };

        font.GetFontMetrics(out SKFontMetrics metrics);
        float baselineY = (float)contentRect.Y - metrics.Ascent;
        float textWidth = font.MeasureText(text);

        float drawX = textAlign switch
        {
            TextAlign.Center => (float)contentRect.X + Math.Max(0, ((float)contentRect.Width - textWidth) / 2.0f),
            TextAlign.Right => (float)contentRect.Right - textWidth,
            _ => (float)contentRect.X
        };

        canvas.DrawText(text, drawX, baselineY, SKTextAlign.Left, font, paint);
    }

    private static SKFont CreateFont(SKTypeface typeface, double fontSize)
    {
        return new SKFont(typeface, (float)fontSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true
        };
    }
}

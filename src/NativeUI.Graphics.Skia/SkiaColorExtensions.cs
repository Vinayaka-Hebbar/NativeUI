using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Provides conversion methods from <see cref="NativeUI.Media.Color"/> (<c>Srgb</c>, <c>Hsl</c>, <c>Oklab</c>, <c>Oklch</c>)
/// to SkiaSharp's <see cref="SKColor"/>.
/// </summary>
public static class SkiaColorExtensions
{
    /// <summary>
    /// Converts this <see cref="Color"/> to an 8-bit sRGB <see cref="SKColor"/>, optionally multiplied by <paramref name="opacityMultiplier"/>.
    /// </summary>
    public static SKColor ToSKColor(this Color color, double opacityMultiplier = 1.0)
    {
        (byte r, byte g, byte b, byte a) = color.ToSrgbBytes();
        byte scaledAlpha = (byte)Math.Round(Math.Clamp((a / 255.0) * opacityMultiplier, 0.0, 1.0) * 255.0);
        return new SKColor(r, g, b, scaledAlpha);
    }
}

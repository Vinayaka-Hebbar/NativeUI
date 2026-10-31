using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS color value backed by <see cref="NativeUI.Media.Color"/>.
/// </summary>
/// <param name="Color">The color value in its declared color space (sRGB, HSL, Oklab, or Oklch).</param>
public sealed record CssColorValue(Color Color) : CssValue;

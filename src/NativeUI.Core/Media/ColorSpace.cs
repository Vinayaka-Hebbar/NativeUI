namespace NativeUI.Media;

/// <summary>
/// Specifies the color space used to interpret the components of a <see cref="Color"/>.
/// </summary>
public enum ColorSpace
{
    /// <summary>
    /// Standard RGB color space with components in the range [0.0, 1.0].
    /// </summary>
    Srgb,

    /// <summary>
    /// Hue-Saturation-Lightness color space where Hue is in degrees [0, 360] and Saturation/Lightness are in [0.0, 1.0].
    /// </summary>
    Hsl,

    /// <summary>
    /// Perceptual Oklab color space where L is in [0.0, 1.0] and a, b are chromaticity coordinates.
    /// </summary>
    Oklab,

    /// <summary>
    /// Cylindrical Oklch color space where L is in [0.0, 1.0], C is chroma, and h is hue in degrees [0, 360].
    /// </summary>
    Oklch
}

using System.Globalization;

namespace NativeUI.Media;

/// <summary>
/// Represents a color in a specified <see cref="ColorSpace"/> with three channel components and an alpha channel.
/// </summary>
/// <param name="Space">The color space of the color components.</param>
/// <param name="C1">The first channel component (R in sRGB, H in HSL, L in Oklab/Oklch).</param>
/// <param name="C2">The second channel component (G in sRGB, S in HSL, a in Oklab, C in Oklch).</param>
/// <param name="C3">The third channel component (B in sRGB, L in HSL, b in Oklab, h in Oklch).</param>
/// <param name="Alpha">The opacity component in the range [0.0, 1.0].</param>
public readonly record struct Color(
    ColorSpace Space,
    double C1,
    double C2,
    double C3,
    double Alpha = 1.0)
{
    /// <summary>
    /// Creates a color in the <see cref="ColorSpace.Srgb"/> color space from normalized [0.0, 1.0] channels.
    /// </summary>
    public static Color FromSrgb(double r, double g, double b, double alpha = 1.0)
    {
        return new Color(ColorSpace.Srgb, r, g, b, alpha);
    }

    /// <summary>
    /// Creates a color in the <see cref="ColorSpace.Srgb"/> color space from 8-bit [0, 255] channels.
    /// </summary>
    public static Color FromRgbBytes(byte r, byte g, byte b, byte a = 255)
    {
        return new Color(ColorSpace.Srgb, r / 255.0, g / 255.0, b / 255.0, a / 255.0);
    }

    /// <summary>
    /// Creates a color in the <see cref="ColorSpace.Hsl"/> color space.
    /// </summary>
    /// <param name="hue">Hue angle in degrees [0, 360].</param>
    /// <param name="saturation">Saturation in [0.0, 1.0].</param>
    /// <param name="lightness">Lightness in [0.0, 1.0].</param>
    /// <param name="alpha">Opacity in [0.0, 1.0].</param>
    public static Color FromHsl(double hue, double saturation, double lightness, double alpha = 1.0)
    {
        return new Color(ColorSpace.Hsl, hue, saturation, lightness, alpha);
    }

    /// <summary>
    /// Creates a color in the <see cref="ColorSpace.Oklab"/> color space.
    /// </summary>
    public static Color FromOklab(double l, double a, double b, double alpha = 1.0)
    {
        return new Color(ColorSpace.Oklab, l, a, b, alpha);
    }

    /// <summary>
    /// Creates a color in the <see cref="ColorSpace.Oklch"/> color space.
    /// </summary>
    /// <param name="l">Perceived lightness in [0.0, 1.0].</param>
    /// <param name="c">Chroma value (typically [0.0, 0.4]).</param>
    /// <param name="h">Hue angle in degrees [0, 360].</param>
    /// <param name="alpha">Opacity in [0.0, 1.0].</param>
    public static Color FromOklch(double l, double c, double h, double alpha = 1.0)
    {
        return new Color(ColorSpace.Oklch, l, c, h, alpha);
    }

    /// <summary>
    /// Parses a hexadecimal color string (#RGB, #RGBA, #RRGGBB, or #RRGGBBAA) into an sRGB <see cref="Color"/>.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the hex string is not in a supported format.</exception>
    public static Color FromHex(string hex)
    {
        if (!TryFromHex(hex, out Color color))
        {
            throw new FormatException($"Invalid hexadecimal color string '{hex}'.");
        }

        return color;
    }

    /// <summary>
    /// Attempts to parse a hexadecimal color string (#RGB, #RGBA, #RRGGBB, or #RRGGBBAA).
    /// </summary>
    public static bool TryFromHex(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        ReadOnlySpan<char> span = hex.AsSpan().Trim();
        if (span.StartsWith("#"))
        {
            span = span[1..];
        }

        if (span.Length == 3)
        {
            if (TryParseHexNibbles(span[0], span[0], out byte r) &&
                TryParseHexNibbles(span[1], span[1], out byte g) &&
                TryParseHexNibbles(span[2], span[2], out byte b))
            {
                color = FromRgbBytes(r, g, b);
                return true;
            }

            return false;
        }

        if (span.Length == 4)
        {
            if (TryParseHexNibbles(span[0], span[0], out byte r) &&
                TryParseHexNibbles(span[1], span[1], out byte g) &&
                TryParseHexNibbles(span[2], span[2], out byte b) &&
                TryParseHexNibbles(span[3], span[3], out byte a))
            {
                color = FromRgbBytes(r, g, b, a);
                return true;
            }

            return false;
        }

        if (span.Length == 6)
        {
            if (TryParseHexNibbles(span[0], span[1], out byte r) &&
                TryParseHexNibbles(span[2], span[3], out byte g) &&
                TryParseHexNibbles(span[4], span[5], out byte b))
            {
                color = FromRgbBytes(r, g, b);
                return true;
            }

            return false;
        }

        if (span.Length == 8)
        {
            if (TryParseHexNibbles(span[0], span[1], out byte r) &&
                TryParseHexNibbles(span[2], span[3], out byte g) &&
                TryParseHexNibbles(span[4], span[5], out byte b) &&
                TryParseHexNibbles(span[6], span[7], out byte a))
            {
                color = FromRgbBytes(r, g, b, a);
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// Converts this color from its current <see cref="ColorSpace"/> into 8-bit sRGB (R, G, B, A) bytes.
    /// </summary>
    public (byte R, byte G, byte B, byte A) ToSrgbBytes()
    {
        (double r, double g, double b) = Space switch
        {
            ColorSpace.Srgb => (C1, C2, C3),
            ColorSpace.Hsl => HslToSrgb(C1, C2, C3),
            ColorSpace.Oklab => OklabToSrgb(C1, C2, C3),
            ColorSpace.Oklch => OklchToSrgb(C1, C2, C3),
            _ => (C1, C2, C3)
        };

        return (
            ClampToByte(r),
            ClampToByte(g),
            ClampToByte(b),
            ClampToByte(Alpha));
    }

    /// <summary>
    /// Formats this color as a standard CSS color string preserving its original color space.
    /// </summary>
    public string ToCssString()
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        return Space switch
        {
            ColorSpace.Srgb => FormatSrgbCss(),
            ColorSpace.Hsl => Alpha >= 1.0
                ? string.Create(inv, $"hsl({C1:0.##} {C2 * 100.0:0.##}% {C3 * 100.0:0.##}%)")
                : string.Create(inv, $"hsl({C1:0.##} {C2 * 100.0:0.##}% {C3 * 100.0:0.##}% / {Alpha:0.###})"),
            ColorSpace.Oklab => Alpha >= 1.0
                ? string.Create(inv, $"oklab({C1:0.####} {C2:0.####} {C3:0.####})")
                : string.Create(inv, $"oklab({C1:0.####} {C2:0.####} {C3:0.####} / {Alpha:0.###})"),
            ColorSpace.Oklch => Alpha >= 1.0
                ? string.Create(inv, $"oklch({C1:0.####} {C2:0.####} {C3:0.##})")
                : string.Create(inv, $"oklch({C1:0.####} {C2:0.####} {C3:0.##} / {Alpha:0.###})"),
            _ => FormatSrgbCss()
        };
    }

    private string FormatSrgbCss()
    {
        (byte r, byte g, byte b, byte a) = ToSrgbBytes();
        if (a == 255)
        {
            return $"#{r:x2}{g:x2}{b:x2}";
        }

        return string.Create(CultureInfo.InvariantCulture, $"rgba({r}, {g}, {b}, {Alpha:0.###})");
    }

    private static (double R, double G, double B) HslToSrgb(double h, double s, double l)
    {
        double hue = ((h % 360.0) + 360.0) % 360.0;
        double c = (1.0 - Math.Abs((2.0 * l) - 1.0)) * s;
        double x = c * (1.0 - Math.Abs(((hue / 60.0) % 2.0) - 1.0));
        double m = l - (c / 2.0);

        (double r1, double g1, double b1) = hue switch
        {
            < 60.0 => (c, x, 0.0),
            < 120.0 => (x, c, 0.0),
            < 180.0 => (0.0, c, x),
            < 240.0 => (0.0, x, c),
            < 300.0 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return (r1 + m, g1 + m, b1 + m);
    }

    private static (double R, double G, double B) OklchToSrgb(double l, double c, double hDegrees)
    {
        double hRadians = hDegrees * (Math.PI / 180.0);
        double a = c * Math.Cos(hRadians);
        double b = c * Math.Sin(hRadians);
        return OklabToSrgb(l, a, b);
    }

    private static (double R, double G, double B) OklabToSrgb(double l, double a, double b)
    {
        // Convert Oklab to non-linear LMS cone responses, cube to linear LMS, then transform to linear sRGB.
        double lPrime = l + (0.3963377774 * a) + (0.2158037573 * b);
        double mPrime = l - (0.1055613458 * a) - (0.0638541728 * b);
        double sPrime = l - (0.0894841775 * a) - (1.2914855480 * b);

        double lLinear = lPrime * lPrime * lPrime;
        double mLinear = mPrime * mPrime * mPrime;
        double sLinear = sPrime * sPrime * sPrime;

        double rLinear = (+4.0767416621 * lLinear) - (3.3077115913 * mLinear) + (0.2309699292 * sLinear);
        double gLinear = (-1.2684380046 * lLinear) + (2.6097574011 * mLinear) - (0.3413193965 * sLinear);
        double bLinear = (-0.0041960863 * lLinear) - (0.7034186147 * mLinear) + (1.7076147010 * sLinear);

        return (LinearToGammaSrgb(rLinear), LinearToGammaSrgb(gLinear), LinearToGammaSrgb(bLinear));
    }

    private static double LinearToGammaSrgb(double c)
    {
        double clamped = Math.Clamp(c, 0.0, 1.0);
        return clamped <= 0.0031308
            ? 12.92 * clamped
            : (1.055 * Math.Pow(clamped, 1.0 / 2.4)) - 0.055;
    }

    private static byte ClampToByte(double value)
    {
        double clamped = Math.Clamp(value, 0.0, 1.0);
        return (byte)Math.Round(clamped * 255.0);
    }

    private static bool TryParseHexNibbles(char high, char low, out byte result)
    {
        int hi = HexValue(high);
        int lo = HexValue(low);
        if (hi < 0 || lo < 0)
        {
            result = 0;
            return false;
        }

        result = (byte)((hi << 4) | lo);
        return true;
    }

    private static int HexValue(char c)
    {
        return c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1
        };
    }
}

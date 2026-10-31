namespace NativeUI.Css.Compiler;

/// <summary>
/// Identifies the color space of a compile-time parsed CSS color literal.
/// </summary>
public enum ParsedColorSpace
{
    /// <summary>
    /// sRGB color space.
    /// </summary>
    Srgb,

    /// <summary>
    /// HSL color space.
    /// </summary>
    Hsl,

    /// <summary>
    /// Oklab color space.
    /// </summary>
    Oklab,

    /// <summary>
    /// Oklch color space.
    /// </summary>
    Oklch
}

/// <summary>
/// Represents a validated CSS color parsed at compile time from a <see cref="CssValueSyntaxNode"/>.
/// </summary>
public readonly record struct ParsedCssColor(
    ParsedColorSpace Space,
    double C1,
    double C2,
    double C3,
    double Alpha = 1.0);

/// <summary>
/// Evaluates <see cref="CssValueSyntaxNode"/> AST nodes (<see cref="CssHexColorValueNode"/>,
/// <see cref="CssFunctionValueNode"/> for <c>oklch</c>, <c>oklab</c>, <c>hsl</c>, <c>rgb</c>,
/// and <see cref="CssKeywordValueNode"/>) into <see cref="ParsedCssColor"/> values.
/// </summary>
public static class CssColorParser
{
    /// <summary>
    /// Attempts to evaluate a <see cref="CssValueSyntaxNode"/> AST node into a <see cref="ParsedCssColor"/>.
    /// </summary>
    public static bool TryEvaluate(CssValueSyntaxNode node, out ParsedCssColor color)
    {
        color = default;
        return node switch
        {
            CssHexColorValueNode hex => TryEvaluateHex(hex.Hex, out color),
            CssKeywordValueNode keyword => TryEvaluateNamedColor(keyword.Keyword, out color),
            CssFunctionValueNode func => TryEvaluateFunction(func, out color),
            _ => false
        };
    }

    private static bool TryEvaluateFunction(CssFunctionValueNode func, out ParsedCssColor color)
    {
        color = default;
        string name = func.Name.ToLowerInvariant();

        if (!TryExtractChannelsAndAlpha(func, out CssValueSyntaxNode[] channels, out double alpha))
        {
            return false;
        }

        switch (name)
        {
            case "oklch":
                if (TryReadNumberOrPercent(channels[0], 1.0, out double lCh) &&
                    TryReadNumber(channels[1], out double cCh) &&
                    TryReadAngleDegrees(channels[2], out double hCh))
                {
                    color = new ParsedCssColor(ParsedColorSpace.Oklch, lCh, cCh, hCh, alpha);
                    return true;
                }
                return false;

            case "oklab":
                if (TryReadNumberOrPercent(channels[0], 1.0, out double lLab) &&
                    TryReadNumber(channels[1], out double aLab) &&
                    TryReadNumber(channels[2], out double bLab))
                {
                    color = new ParsedCssColor(ParsedColorSpace.Oklab, lLab, aLab, bLab, alpha);
                    return true;
                }
                return false;

            case "hsl" or "hsla":
                if (TryReadAngleDegrees(channels[0], out double hHsl) &&
                    TryReadNormalizedPercentOrUnit(channels[1], out double sHsl) &&
                    TryReadNormalizedPercentOrUnit(channels[2], out double lHsl))
                {
                    color = new ParsedCssColor(ParsedColorSpace.Hsl, hHsl, sHsl, lHsl, alpha);
                    return true;
                }
                return false;

            case "rgb" or "rgba":
                if (TryReadNumberOrPercent(channels[0], 255.0, out double r) &&
                    TryReadNumberOrPercent(channels[1], 255.0, out double g) &&
                    TryReadNumberOrPercent(channels[2], 255.0, out double b))
                {
                    color = new ParsedCssColor(
                        ParsedColorSpace.Srgb,
                        Math.Clamp(r / 255.0, 0.0, 1.0),
                        Math.Clamp(g / 255.0, 0.0, 1.0),
                        Math.Clamp(b / 255.0, 0.0, 1.0),
                        alpha);
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    private static bool TryExtractChannelsAndAlpha(
        CssFunctionValueNode func,
        out CssValueSyntaxNode[] channels,
        out double alpha)
    {
        channels = [];
        alpha = 1.0;

        if (func.SlashArgument is not null)
        {
            if (func.Arguments.Count != 3 || !TryReadNormalizedPercentOrUnit(func.SlashArgument, out alpha))
            {
                return false;
            }

            channels = [func.Arguments[0], func.Arguments[1], func.Arguments[2]];
            return true;
        }

        if (func.Arguments.Count == 4)
        {
            if (!TryReadNormalizedPercentOrUnit(func.Arguments[3], out alpha))
            {
                return false;
            }

            channels = [func.Arguments[0], func.Arguments[1], func.Arguments[2]];
            return true;
        }

        if (func.Arguments.Count == 3)
        {
            channels = [func.Arguments[0], func.Arguments[1], func.Arguments[2]];
            return true;
        }

        return false;
    }

    private static bool TryReadNumber(CssValueSyntaxNode node, out double value)
    {
        if (node is CssNumberValueNode num)
        {
            value = num.Value;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadNumberOrPercent(CssValueSyntaxNode node, double scaleForHundredPercent, out double value)
    {
        switch (node)
        {
            case CssNumberValueNode num:
                value = num.Value;
                return true;
            case CssPercentageValueNode pct:
                value = (pct.Value / 100.0) * scaleForHundredPercent;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static bool TryReadNormalizedPercentOrUnit(CssValueSyntaxNode node, out double value)
    {
        switch (node)
        {
            case CssPercentageValueNode pct:
                value = Math.Clamp(pct.Value / 100.0, 0.0, 1.0);
                return true;
            case CssNumberValueNode num:
                value = Math.Clamp(num.Value, 0.0, 1.0);
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static bool TryReadAngleDegrees(CssValueSyntaxNode node, out double degrees)
    {
        switch (node)
        {
            case CssNumberValueNode num:
                degrees = num.Value;
                return true;
            case CssDimensionValueNode dim when string.Equals(dim.Unit, "deg", StringComparison.OrdinalIgnoreCase):
                degrees = dim.Value;
                return true;
            default:
                degrees = 0;
                return false;
        }
    }

    private static bool TryEvaluateHex(string hex, out ParsedCssColor color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex) || hex[0] != '#')
        {
            return false;
        }

        ReadOnlySpan<char> span = hex.AsSpan()[1..];
        if (span.Length == 3 &&
            TryHexByte(span[0], span[0], out byte r3) &&
            TryHexByte(span[1], span[1], out byte g3) &&
            TryHexByte(span[2], span[2], out byte b3))
        {
            color = new ParsedCssColor(ParsedColorSpace.Srgb, r3 / 255.0, g3 / 255.0, b3 / 255.0, 1.0);
            return true;
        }

        if (span.Length == 6 &&
            TryHexByte(span[0], span[1], out byte r6) &&
            TryHexByte(span[2], span[3], out byte g6) &&
            TryHexByte(span[4], span[5], out byte b6))
        {
            color = new ParsedCssColor(ParsedColorSpace.Srgb, r6 / 255.0, g6 / 255.0, b6 / 255.0, 1.0);
            return true;
        }

        if (span.Length == 8 &&
            TryHexByte(span[0], span[1], out byte r8) &&
            TryHexByte(span[2], span[3], out byte g8) &&
            TryHexByte(span[4], span[5], out byte b8) &&
            TryHexByte(span[6], span[7], out byte a8))
        {
            color = new ParsedCssColor(ParsedColorSpace.Srgb, r8 / 255.0, g8 / 255.0, b8 / 255.0, a8 / 255.0);
            return true;
        }

        return false;
    }

    private static bool TryEvaluateNamedColor(string keyword, out ParsedCssColor color)
    {
        switch (keyword.ToLowerInvariant())
        {
            case "transparent":
                color = new ParsedCssColor(ParsedColorSpace.Srgb, 0, 0, 0, 0);
                return true;
            case "black":
                color = new ParsedCssColor(ParsedColorSpace.Srgb, 0, 0, 0, 1);
                return true;
            case "white":
                color = new ParsedCssColor(ParsedColorSpace.Srgb, 1, 1, 1, 1);
                return true;
            default:
                color = default;
                return false;
        }
    }

    private static bool TryHexByte(char hi, char lo, out byte b)
    {
        int h = HexNibble(hi);
        int l = HexNibble(lo);
        if (h < 0 || l < 0)
        {
            b = 0;
            return false;
        }

        b = (byte)((h << 4) | l);
        return true;
    }

    private static int HexNibble(char c)
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

using System.Globalization;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls.Web;

/// <summary>
/// Converts specified style values to browser CSS declarations for inline styles and generated stylesheets.
/// </summary>
internal static class StyleCssDeclarations
{
    /// <summary>
    /// Returns every supported CSS property with its value, or a null value when <paramref name="style"/> leaves it unspecified.
    /// </summary>
    public static (string Property, string? Value)[] Get(IStyle? style)
    {
        return
        [
            ("color", FormatColor(style?.Color)),
            ("background-color", FormatColor(style?.BackgroundColor)),
            ("font-size", FormatLength(style?.FontSize)),
            ("font-weight", style?.FontWeight is FontWeight weight ? ((int)weight).ToString(CultureInfo.InvariantCulture) : null),
            ("font-family", style?.FontFamily),
            ("text-align", FormatTextAlign(style?.TextAlign)),
            ("padding", FormatThickness(style?.Padding)),
            ("margin", FormatThickness(style?.Margin)),
            ("border-radius", FormatLength(style?.BorderRadius)),
            ("opacity", style?.Opacity is double opacity ? Math.Clamp(opacity, 0, 1).ToString("R", CultureInfo.InvariantCulture) : null),
            ("width", FormatLength(style?.Width)),
            ("height", FormatLength(style?.Height))
        ];
    }

    private static string? FormatColor(ThemeValue<Color>? value)
    {
        if (value is not ThemeValue<Color> color)
        {
            return null;
        }

        if (color.IsThemeKey)
        {
            return color.HasValue
                ? $"var({color.Key}, {color.Value.ToCssString()})"
                : $"var({color.Key})";
        }

        return color.Value.ToCssString();
    }

    private static string? FormatLength(double? value)
    {
        return value is double length ? length.ToString("R", CultureInfo.InvariantCulture) + "px" : null;
    }

    private static string? FormatThickness(Thickness? value)
    {
        if (value is not Thickness thickness)
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"{thickness.Top:R}px {thickness.Right:R}px {thickness.Bottom:R}px {thickness.Left:R}px");
    }

    private static string? FormatTextAlign(TextAlign? value)
    {
        return value switch
        {
            TextAlign.Left => "left",
            TextAlign.Center => "center",
            TextAlign.Right => "right",
            _ => null
        };
    }
}

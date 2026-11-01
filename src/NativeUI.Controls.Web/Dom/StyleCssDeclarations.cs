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
            ("width", FormatWidth(style?.Width)),
            ("height", FormatLength(style?.Height)),
            ("cursor", FormatCursor(style?.Cursor)),
            ("overflow-x", FormatOverflow(style?.OverflowX)),
            ("overflow-y", FormatOverflow(style?.OverflowY)),
            ("scrollbar-color", FormatScrollbarColor(style)),
            ("scrollbar-width", FormatScrollbarWidth(style?.ScrollbarWidth))
        ];
    }

    /// <summary>
    /// Formats the CSS property, which requires both colors. An unspecified track is transparent.
    /// </summary>
    private static string? FormatScrollbarColor(IStyle? style)
    {
        string? thumb = FormatColor(style?.ScrollbarThumbColor);
        return thumb is null ? null : $"{thumb} {FormatColor(style?.ScrollbarTrackColor) ?? "transparent"}";
    }

    private static string? FormatScrollbarWidth(ScrollbarWidth? width)
    {
        return width switch
        {
            ScrollbarWidth.Auto => "auto",
            ScrollbarWidth.Thin => "thin",
            ScrollbarWidth.None => "none",
            _ => null
        };
    }

    private static string? FormatOverflow(Overflow? overflow)
    {
        return overflow switch
        {
            Overflow.Visible => "visible",
            Overflow.Hidden => "hidden",
            Overflow.Auto => "auto",
            Overflow.Scroll => "scroll",
            _ => null
        };
    }

    private static string? FormatCursor(Cursor? cursor)
    {
        return cursor switch
        {
            Cursor.Pointer => "pointer",
            Cursor.IBeam => "text",
            Cursor.NotAllowed => "not-allowed",
            Cursor.Crosshair => "crosshair",
            Cursor.Wait => "wait",
            Cursor.Arrow or Cursor.Default => "default",
            _ => null
        };
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

    private static string? FormatWidth(WidthValue? value)
    {
        return value is { IsFitContent: true } ? "fit-content" : FormatLength(value?.Pixels);
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

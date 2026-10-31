using NativeUI.Styling;

namespace NativeUI.Media;

/// <summary>
/// Represents an immutable, value-comparable snapshot of a UI element's resolved style after applying any active
/// <see cref="IStyleResolver"/> (such as optional CSS rules) and inline C# style overrides.
/// </summary>
public sealed record ComputedStyle
{
    /// <summary>
    /// Gets the default computed style with standard baseline typography and zero spacing.
    /// </summary>
    public static ComputedStyle Default { get; } = new();

    /// <summary>
    /// Gets the resolved foreground text color, or <see langword="null"/> if unspecified.
    /// </summary>
    public Color? TextColor { get; init; }

    /// <summary>
    /// Gets the resolved background color, or <see langword="null"/> if transparent/unspecified.
    /// </summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>
    /// Gets the resolved font size in logical pixels (defaults to 16.0).
    /// </summary>
    public double FontSize { get; init; } = 16.0;

    /// <summary>
    /// Gets the resolved font weight (defaults to <see cref="FontWeight.Normal"/>).
    /// </summary>
    public FontWeight FontWeight { get; init; } = FontWeight.Normal;

    /// <summary>
    /// Gets the resolved font family name, or <see langword="null"/> for the platform system font.
    /// </summary>
    public string? FontFamily { get; init; }

    /// <summary>
    /// Gets the resolved horizontal text alignment.
    /// </summary>
    public TextAlign TextAlign { get; init; } = TextAlign.Left;

    /// <summary>
    /// Gets the resolved inner padding thickness in logical pixels.
    /// </summary>
    public Thickness Padding { get; init; } = Thickness.Zero;

    /// <summary>
    /// Gets the resolved outer margin thickness in logical pixels.
    /// </summary>
    public Thickness Margin { get; init; } = Thickness.Zero;

    /// <summary>
    /// Gets the resolved corner border radius in logical pixels.
    /// </summary>
    public double BorderRadius { get; init; }

    /// <summary>
    /// Gets the resolved element opacity in the range [0.0, 1.0].
    /// </summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// Gets the explicit width in logical pixels, or <see langword="null"/> for auto sizing.
    /// </summary>
    public double? Width { get; init; }

    /// <summary>
    /// Gets the explicit height in logical pixels, or <see langword="null"/> for auto sizing.
    /// </summary>
    public double? Height { get; init; }

    /// <summary>
    /// Creates an element's initial style by inheriting text properties from its parent.
    /// Box geometry, background, and opacity start at their initial values.
    /// </summary>
    /// <param name="parent">The parent's computed style, or <see langword="null"/> for a root element.</param>
    public static ComputedStyle CreateInherited(ComputedStyle? parent)
    {
        if (parent is null)
        {
            return Default;
        }

        return new ComputedStyle
        {
            TextColor = parent.TextColor,
            FontSize = parent.FontSize,
            FontWeight = parent.FontWeight,
            FontFamily = parent.FontFamily,
            TextAlign = parent.TextAlign
        };
    }

    /// <summary>
    /// Applies specified inline values without modifying this computed snapshot.
    /// </summary>
    /// <param name="inlineStyle">The specified inline properties, or <see langword="null"/> to preserve this snapshot.</param>
    /// <param name="resolveColor">An optional scoped theme-token resolver; unresolved tokens use their fallback or the existing value.</param>
    public ComputedStyle ApplyInline(IStyle? inlineStyle, Func<ThemeValue<Color>, Color?>? resolveColor = null)
    {
        if (inlineStyle is null)
        {
            return this;
        }

        return this with
        {
            TextColor = ResolveColor(inlineStyle.Color, TextColor, resolveColor),
            BackgroundColor = ResolveColor(inlineStyle.BackgroundColor, BackgroundColor, resolveColor),
            FontSize = inlineStyle.FontSize ?? FontSize,
            FontWeight = inlineStyle.FontWeight ?? FontWeight,
            FontFamily = inlineStyle.FontFamily ?? FontFamily,
            TextAlign = inlineStyle.TextAlign ?? TextAlign,
            Padding = inlineStyle.Padding ?? Padding,
            Margin = inlineStyle.Margin ?? Margin,
            BorderRadius = inlineStyle.BorderRadius ?? BorderRadius,
            Opacity = inlineStyle.Opacity is double opacity ? Math.Clamp(opacity, 0.0, 1.0) : Opacity,
            Width = inlineStyle.Width ?? Width,
            Height = inlineStyle.Height ?? Height
        };
    }

    private static Color? ResolveColor(ThemeValue<Color>? value, Color? fallback, Func<ThemeValue<Color>, Color?>? resolveColor)
    {
        if (value is not ThemeValue<Color> specified)
        {
            return fallback;
        }

        if (specified.IsThemeKey && resolveColor?.Invoke(specified) is Color resolved)
        {
            return resolved;
        }

        return specified.HasValue ? specified.Value : fallback;
    }
}

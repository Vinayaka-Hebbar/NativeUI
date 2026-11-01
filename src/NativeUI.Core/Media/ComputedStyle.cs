using NativeUI.Core;
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
    /// Gets the specified width, or <see langword="null"/> to fill the available width.
    /// </summary>
    public WidthValue? Width { get; init; }

    /// <summary>
    /// Gets the explicit height in logical pixels, or <see langword="null"/> for auto sizing.
    /// </summary>
    public double? Height { get; init; }

    /// <summary>
    /// Gets the resolved pointer cursor appearance (defaults to <see cref="Cursor.Default"/>).
    /// </summary>
    public Cursor Cursor { get; init; } = Cursor.Default;

    /// <summary>
    /// Gets the specified horizontal overflow, or <see langword="null"/> when none is specified.
    /// </summary>
    public Overflow? OverflowX { get; init; }

    /// <summary>
    /// Gets the specified vertical overflow, or <see langword="null"/> when none is specified.
    /// </summary>
    public Overflow? OverflowY { get; init; }

    /// <summary>
    /// Gets the inherited scrollbar thumb color, or <see langword="null"/> for the platform default.
    /// </summary>
    public Color? ScrollbarThumbColor { get; init; }

    /// <summary>
    /// Gets the inherited scrollbar track color, or <see langword="null"/> for the platform default.
    /// </summary>
    public Color? ScrollbarTrackColor { get; init; }

    /// <summary>
    /// Gets the scrollbar thickness, which is not inherited.
    /// </summary>
    public ScrollbarWidth ScrollbarWidth { get; init; } = ScrollbarWidth.Auto;

    /// <summary>
    /// Gets a value indicating whether content is clipped to the element's bounds.
    /// As in CSS, a non-visible overflow on either axis clips both.
    /// </summary>
    public bool ClipsOverflow => OverflowX is not (null or Overflow.Visible) || OverflowY is not (null or Overflow.Visible);

    /// <summary>
    /// Gets a value indicating whether the user can scroll horizontally. As in CSS, a visible axis computes to
    /// <see cref="Overflow.Auto"/> when the other axis clips.
    /// </summary>
    public bool IsUserScrollableX => ClipsOverflow && OverflowX is not Overflow.Hidden;

    /// <summary>
    /// Gets a value indicating whether the user can scroll vertically. As in CSS, a visible axis computes to
    /// <see cref="Overflow.Auto"/> when the other axis clips.
    /// </summary>
    public bool IsUserScrollableY => ClipsOverflow && OverflowY is not Overflow.Hidden;

    /// <summary>
    /// Creates an element's initial style before stylesheets apply. Text properties inherit from its parent, sizes and
    /// overflow start from the element's own values, and other box geometry, background, and opacity start at their initial values.
    /// </summary>
    /// <param name="element">The element being styled.</param>
    public static ComputedStyle CreateInitial(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        ComputedStyle? parent = element.Parent?.ComputedStyle;
        return new ComputedStyle
        {
            TextColor = parent is null ? Default.TextColor : parent.TextColor,
            FontSize = parent is null ? Default.FontSize : parent.FontSize,
            FontWeight = parent is null ? Default.FontWeight : parent.FontWeight,
            FontFamily = parent is null ? Default.FontFamily : parent.FontFamily,
            TextAlign = parent is null ? Default.TextAlign : parent.TextAlign,
            Cursor = parent is null ? Default.Cursor : parent.Cursor,
            ScrollbarThumbColor = parent is null ? Default.ScrollbarThumbColor : parent.ScrollbarThumbColor,
            ScrollbarTrackColor = parent is null ? Default.ScrollbarTrackColor : parent.ScrollbarTrackColor,
            Width = element.Width,
            Height = element.Height,
            OverflowX = element.OverflowX,
            OverflowY = element.OverflowY
        };
    }

    /// <summary>
    /// Applies specified inline values without modifying this computed snapshot.
    /// </summary>
    /// <param name="inlineStyle">The specified inline properties, or <see langword="null"/> to preserve this snapshot.</param>
    /// <param name="resolver">An optional style resolver used to look up theme color variables.</param>
    /// <param name="element">The element being styled, or <see langword="null"/> if not associated with an element.</param>
    public ComputedStyle ApplyInline(IStyle? inlineStyle, IStyleResolver? resolver = null, IUIElement? element = null)
    {
        if (inlineStyle is null)
        {
            return this;
        }

        return this with
        {
            TextColor = ResolveColor(inlineStyle.Color, TextColor, resolver, element),
            BackgroundColor = ResolveColor(inlineStyle.BackgroundColor, BackgroundColor, resolver, element),
            FontSize = inlineStyle.FontSize ?? FontSize,
            FontWeight = inlineStyle.FontWeight ?? FontWeight,
            FontFamily = inlineStyle.FontFamily ?? FontFamily,
            TextAlign = inlineStyle.TextAlign ?? TextAlign,
            Padding = inlineStyle.Padding ?? Padding,
            Margin = inlineStyle.Margin ?? Margin,
            BorderRadius = inlineStyle.BorderRadius ?? BorderRadius,
            Opacity = inlineStyle.Opacity is double opacity ? Math.Clamp(opacity, 0.0, 1.0) : Opacity,
            Width = inlineStyle.Width ?? Width,
            Height = inlineStyle.Height ?? Height,
            Cursor = inlineStyle.Cursor ?? Cursor,
            OverflowX = inlineStyle.OverflowX ?? OverflowX,
            OverflowY = inlineStyle.OverflowY ?? OverflowY,
            ScrollbarThumbColor = ResolveColor(inlineStyle.ScrollbarThumbColor, ScrollbarThumbColor, resolver, element),
            ScrollbarTrackColor = ResolveColor(inlineStyle.ScrollbarTrackColor, ScrollbarTrackColor, resolver, element),
            ScrollbarWidth = inlineStyle.ScrollbarWidth ?? ScrollbarWidth
        };
    }

    private static Color? ResolveColor(
        ThemeValue<Color>? value,
        Color? fallback,
        IStyleResolver? resolver,
        IUIElement? element)
    {
        if (value is not ThemeValue<Color> specified)
        {
            return fallback;
        }

        if (specified.IsThemeKey && resolver is not null && element is not null)
        {
            Color? resolved = resolver.ResolveColor(specified, element);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }
        }

        return specified.HasValue ? specified.Value : fallback;
    }
}

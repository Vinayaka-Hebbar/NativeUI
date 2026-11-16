using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media.Animation;
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
    /// Gets the inherited height of a line of text. Defaults to 1.25 times the font size, the line height that the Web base
    /// stylesheet sets on the <c>native-ui</c> element.
    /// </summary>
    public LineHeight LineHeight { get; init; } = LineHeight.Multiple(1.25);

    /// <summary>
    /// Gets where the children of a horizontal stack sit on the vertical axis, within the height of the tallest child.
    /// </summary>
    public Alignment AlignItems { get; init; } = Alignment.Start;

    /// <summary>
    /// Gets the resolved inner padding thickness, each edge in logical pixels or as a percentage of the width of the
    /// containing block.
    /// </summary>
    public ThicknessValue Padding { get; init; }

    /// <summary>
    /// Gets the resolved outer margin thickness, each edge in logical pixels or as a percentage of the width of the
    /// containing block.
    /// </summary>
    public ThicknessValue Margin { get; init; }

    /// <summary>
    /// Gets the resolved spacing between adjacent children of a stack container in logical pixels.
    /// </summary>
    public double Gap { get; init; }

    /// <summary>
    /// Gets the resolved radii of the four corners, each in logical pixels or as a percentage of the border box.
    /// </summary>
    public CornerRadiusValue BorderRadius { get; init; }

    /// <summary>
    /// Gets the resolved border width on all four edges in logical pixels.
    /// </summary>
    public double BorderWidth { get; init; }

    /// <summary>
    /// Gets the resolved border color, or <see langword="null"/> to use <see cref="TextColor"/>.
    /// </summary>
    public Color? BorderColor { get; init; }

    /// <summary>
    /// Gets the shadows of the box from top to bottom, which are not inherited.
    /// </summary>
    public ImmutableArray<BoxShadow> BoxShadows { get; init; } = [];

    /// <summary>
    /// Returns the distance from each edge of the border box to the content box, which is the padding plus the border width.
    /// A percentage padding resolves against <paramref name="containingWidth"/>.
    /// </summary>
    /// <param name="containingWidth">The width of the containing block.</param>
    public Thickness GetContentInsets(double containingWidth)
    {
        Thickness padding = Padding.Resolve(containingWidth);
        return new Thickness(
            padding.Top + BorderWidth,
            padding.Right + BorderWidth,
            padding.Bottom + BorderWidth,
            padding.Left + BorderWidth);
    }

    /// <summary>
    /// Gets the resolved element opacity in the range [0.0, 1.0].
    /// </summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// Gets the resolved width, which is <see cref="WidthValue.Auto"/> when nothing specifies one.
    /// </summary>
    public WidthValue Width { get; init; } = WidthValue.Auto;

    /// <summary>
    /// Gets the resolved height, which is <see cref="HeightValue.Auto"/> when nothing specifies one.
    /// </summary>
    public HeightValue Height { get; init; } = HeightValue.Auto;

    /// <summary>
    /// Gets the smallest border-box width in logical pixels or as a percentage of the containing width, or
    /// <see langword="null"/> when the width has no minimum.
    /// </summary>
    public WidthValue? MinWidth { get; init; }

    /// <summary>
    /// Gets the largest border-box width in logical pixels or as a percentage of the containing width, or
    /// <see langword="null"/> when the width has no maximum.
    /// </summary>
    public WidthValue? MaxWidth { get; init; }

    /// <summary>
    /// Gets the smallest border-box height in logical pixels or as a percentage of the containing height, or
    /// <see langword="null"/> when the height has no minimum.
    /// </summary>
    public HeightValue? MinHeight { get; init; }

    /// <summary>
    /// Gets the largest border-box height in logical pixels or as a percentage of the containing height, or
    /// <see langword="null"/> when the height has no maximum.
    /// </summary>
    public HeightValue? MaxHeight { get; init; }

    /// <summary>
    /// Gets the resolved pointer cursor appearance (defaults to <see cref="Cursor.Auto"/>).
    /// </summary>
    public Cursor Cursor { get; init; } = Cursor.Auto;

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
    /// Gets the positioning scheme for the element (defaults to <see cref="Position.Static"/>).
    /// </summary>
    public Position Position { get; init; } = Position.Static;

    /// <summary>
    /// Gets the resolved top inset, which is <see cref="InsetValue.Auto"/> when nothing specifies one.
    /// </summary>
    public InsetValue Top { get; init; } = InsetValue.Auto;

    /// <summary>
    /// Gets the resolved right inset, which is <see cref="InsetValue.Auto"/> when nothing specifies one.
    /// </summary>
    public InsetValue Right { get; init; } = InsetValue.Auto;

    /// <summary>
    /// Gets the resolved bottom inset, which is <see cref="InsetValue.Auto"/> when nothing specifies one.
    /// </summary>
    public InsetValue Bottom { get; init; } = InsetValue.Auto;

    /// <summary>
    /// Gets the resolved left inset, which is <see cref="InsetValue.Auto"/> when nothing specifies one.
    /// </summary>
    public InsetValue Left { get; init; } = InsetValue.Auto;

    /// <summary>
    /// Gets the resolved transform, or <see langword="null"/> for none. A transform does not affect layout.
    /// </summary>
    public StyleTransform? Transform { get; init; }

    /// <summary>
    /// Gets the paint order of a positioned element among its positioned siblings. Zero is the CSS <c>auto</c> value.
    /// Elements with a negative value paint below the siblings in normal flow, and higher values paint above lower ones.
    /// </summary>
    public int ZIndex { get; init; }

    /// <summary>
    /// Gets how changes to this element's properties transition.
    /// </summary>
    public ImmutableArray<TransitionSpec> Transitions { get; init; } = [];

    /// <summary>
    /// Gets the keyframe animation this element plays, or <see langword="null"/> for none.
    /// </summary>
    public AnimationSpec? Animation { get; init; }

    /// <summary>
    /// Gets the computed custom properties, which descendants inherit, or <see langword="null"/> when none are defined.
    /// Theme colors in inline styles resolve against these properties before the <see cref="IStyleResolver"/>.
    /// </summary>
    public ICustomProperties? CustomProperties { get; init; }

    /// <summary>
    /// Gets a value indicating whether this element establishes a positioned context (<c>relative</c>, <c>absolute</c>, or <c>fixed</c>).
    /// </summary>
    public bool IsPositioned => Position is not Position.Static;

    /// <summary>
    /// Gets a value indicating whether this element is taken out of normal layout flow (<c>absolute</c> or <c>fixed</c>).
    /// </summary>
    public bool IsOutOfFlow => Position is Position.Absolute or Position.Fixed;

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
    /// Creates an element's initial style before stylesheets apply. Text properties, including the line height, inherit from its parent, sizes,
    /// overflow, and the cursor start from the element's own values, and other box geometry, background, and opacity start at their initial values.
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
            LineHeight = parent is null ? Default.LineHeight : parent.LineHeight,
            Cursor = element.Cursor ?? (parent is null ? Default.Cursor : parent.Cursor),
            ScrollbarThumbColor = parent is null ? Default.ScrollbarThumbColor : parent.ScrollbarThumbColor,
            ScrollbarTrackColor = parent is null ? Default.ScrollbarTrackColor : parent.ScrollbarTrackColor,
            CustomProperties = parent?.CustomProperties,
            Width = element.Width ?? WidthValue.Auto,
            Height = element.Height ?? HeightValue.Auto,
            OverflowX = element.OverflowX,
            OverflowY = element.OverflowY,
            Position = element.Position ?? Default.Position,
            Top = element.Top ?? InsetValue.Auto,
            Right = element.Right ?? InsetValue.Auto,
            Bottom = element.Bottom ?? InsetValue.Auto,
            Left = element.Left ?? InsetValue.Auto
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
            LineHeight = inlineStyle.LineHeight ?? LineHeight,
            AlignItems = inlineStyle.AlignItems ?? AlignItems,
            Padding = inlineStyle.Padding ?? Padding,
            Margin = inlineStyle.Margin ?? Margin,
            Gap = inlineStyle.Gap ?? Gap,
            BorderRadius = inlineStyle.BorderRadius ?? BorderRadius,
            BorderWidth = inlineStyle.BorderWidth ?? BorderWidth,
            BorderColor = ResolveColor(inlineStyle.BorderColor, BorderColor, resolver, element),
            BoxShadows = inlineStyle.BoxShadows ?? BoxShadows,
            Opacity = inlineStyle.Opacity is double opacity ? Math.Clamp(opacity, 0.0, 1.0) : Opacity,
            Width = inlineStyle.Width ?? Width,
            Height = inlineStyle.Height ?? Height,
            MinWidth = inlineStyle.MinWidth ?? MinWidth,
            MaxWidth = inlineStyle.MaxWidth ?? MaxWidth,
            MinHeight = inlineStyle.MinHeight ?? MinHeight,
            MaxHeight = inlineStyle.MaxHeight ?? MaxHeight,
            Cursor = inlineStyle.Cursor ?? Cursor,
            OverflowX = inlineStyle.OverflowX ?? OverflowX,
            OverflowY = inlineStyle.OverflowY ?? OverflowY,
            ScrollbarThumbColor = ResolveColor(inlineStyle.ScrollbarThumbColor, ScrollbarThumbColor, resolver, element),
            ScrollbarTrackColor = ResolveColor(inlineStyle.ScrollbarTrackColor, ScrollbarTrackColor, resolver, element),
            ScrollbarWidth = inlineStyle.ScrollbarWidth ?? ScrollbarWidth,
            Position = inlineStyle.Position ?? Position,
            Top = inlineStyle.Top ?? Top,
            Right = inlineStyle.Right ?? Right,
            Bottom = inlineStyle.Bottom ?? Bottom,
            Left = inlineStyle.Left ?? Left,
            Transform = inlineStyle.Transform ?? Transform,
            ZIndex = inlineStyle.ZIndex ?? ZIndex
        };
    }

    private Color? ResolveColor(
        ThemeValue<Color>? value,
        Color? fallback,
        IStyleResolver? resolver,
        IUIElement? element)
    {
        if (value is not ThemeValue<Color> specified)
        {
            return fallback;
        }

        if (specified.Key is string key && CustomProperties is not null && CustomProperties.TryGetValue(key, out Color color))
        {
            return color;
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

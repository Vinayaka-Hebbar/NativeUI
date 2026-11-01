using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Represents an inline C# style object that can be assigned to <see cref="UIElement.Style"/>
/// (for example, <c>Style = new() { BackgroundColor = Colors.Blue, Color = Colors.White, Padding = new(12, 8), BorderRadius = 6 }</c>).
/// Overrides properties from CSS classes when present.
/// </summary>
public sealed class Style : BindableObject, IStyle
{
    /// <summary>
    /// Identifies the <see cref="Color"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ThemeValue<Color>?> ColorProperty =
        BindableProperty.Create<Style, ThemeValue<Color>?>(nameof(Color));

    /// <summary>
    /// Identifies the <see cref="BackgroundColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ThemeValue<Color>?> BackgroundColorProperty =
        BindableProperty.Create<Style, ThemeValue<Color>?>(nameof(BackgroundColor));

    /// <summary>
    /// Identifies the <see cref="FontSize"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> FontSizeProperty =
        BindableProperty.Create<Style, double?>(nameof(FontSize));

    /// <summary>
    /// Identifies the <see cref="FontWeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<FontWeight?> FontWeightProperty =
        BindableProperty.Create<Style, FontWeight?>(nameof(FontWeight));

    /// <summary>
    /// Identifies the <see cref="FontFamily"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> FontFamilyProperty =
        BindableProperty.Create<Style, string?>(nameof(FontFamily));

    /// <summary>
    /// Identifies the <see cref="TextAlign"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<TextAlign?> TextAlignProperty =
        BindableProperty.Create<Style, TextAlign?>(nameof(TextAlign));

    /// <summary>
    /// Identifies the <see cref="Padding"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Thickness?> PaddingProperty =
        BindableProperty.Create<Style, Thickness?>(nameof(Padding));

    /// <summary>
    /// Identifies the <see cref="Margin"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Thickness?> MarginProperty =
        BindableProperty.Create<Style, Thickness?>(nameof(Margin));

    /// <summary>
    /// Identifies the <see cref="BorderRadius"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> BorderRadiusProperty =
        BindableProperty.Create<Style, double?>(nameof(BorderRadius));

    /// <summary>
    /// Identifies the <see cref="BorderWidth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> BorderWidthProperty =
        BindableProperty.Create<Style, double?>(nameof(BorderWidth));

    /// <summary>
    /// Identifies the <see cref="BorderColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ThemeValue<Color>?> BorderColorProperty =
        BindableProperty.Create<Style, ThemeValue<Color>?>(nameof(BorderColor));

    /// <summary>
    /// Identifies the <see cref="Opacity"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> OpacityProperty =
        BindableProperty.Create<Style, double?>(nameof(Opacity));

    /// <summary>
    /// Identifies the <see cref="Width"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<WidthValue?> WidthProperty =
        BindableProperty.Create<Style, WidthValue?>(nameof(Width));

    /// <summary>
    /// Identifies the <see cref="Height"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> HeightProperty =
        BindableProperty.Create<Style, double?>(nameof(Height));

    /// <summary>
    /// Identifies the <see cref="Cursor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Cursor?> CursorProperty =
        BindableProperty.Create<Style, Cursor?>(nameof(Cursor));

    /// <summary>
    /// Identifies the <see cref="OverflowX"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Overflow?> OverflowXProperty =
        BindableProperty.Create<Style, Overflow?>(nameof(OverflowX));

    /// <summary>
    /// Identifies the <see cref="OverflowY"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Overflow?> OverflowYProperty =
        BindableProperty.Create<Style, Overflow?>(nameof(OverflowY));

    /// <summary>
    /// Identifies the <see cref="ScrollbarThumbColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ThemeValue<Color>?> ScrollbarThumbColorProperty =
        BindableProperty.Create<Style, ThemeValue<Color>?>(nameof(ScrollbarThumbColor));

    /// <summary>
    /// Identifies the <see cref="ScrollbarTrackColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ThemeValue<Color>?> ScrollbarTrackColorProperty =
        BindableProperty.Create<Style, ThemeValue<Color>?>(nameof(ScrollbarTrackColor));

    /// <summary>
    /// Identifies the <see cref="ScrollbarWidth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ScrollbarWidth?> ScrollbarWidthProperty =
        BindableProperty.Create<Style, ScrollbarWidth?>(nameof(ScrollbarWidth));

    /// <summary>
    /// Identifies the <see cref="Position"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Position?> PositionProperty =
        BindableProperty.Create<Style, Position?>(nameof(Position));

    /// <summary>
    /// Identifies the <see cref="Top"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> TopProperty =
        BindableProperty.Create<Style, double?>(nameof(Top));

    /// <summary>
    /// Identifies the <see cref="Right"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> RightProperty =
        BindableProperty.Create<Style, double?>(nameof(Right));

    /// <summary>
    /// Identifies the <see cref="Bottom"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> BottomProperty =
        BindableProperty.Create<Style, double?>(nameof(Bottom));

    /// <summary>
    /// Identifies the <see cref="Left"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> LeftProperty =
        BindableProperty.Create<Style, double?>(nameof(Left));

    /// <summary>
    /// Gets or sets the foreground text color or theme color token.
    /// </summary>
    public ThemeValue<Color>? Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the background color or theme color token.
    /// </summary>
    public ThemeValue<Color>? BackgroundColor
    {
        get => GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the font size in logical pixels.
    /// </summary>
    public double? FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the typographic font weight.
    /// </summary>
    public FontWeight? FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the font family name.
    /// </summary>
    public string? FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal text alignment.
    /// </summary>
    public TextAlign? TextAlign
    {
        get => GetValue(TextAlignProperty);
        set => SetValue(TextAlignProperty, value);
    }

    /// <summary>
    /// Gets or sets the inner padding thickness in logical pixels.
    /// </summary>
    public Thickness? Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the outer margin thickness in logical pixels.
    /// </summary>
    public Thickness? Margin
    {
        get => GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner border radius in logical pixels.
    /// </summary>
    public double? BorderRadius
    {
        get => GetValue(BorderRadiusProperty);
        set => SetValue(BorderRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the border width on all four edges in logical pixels. The border lies inside the element's width and height.
    /// </summary>
    public double? BorderWidth
    {
        get => GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color or theme token. An unspecified border color uses the text color.
    /// </summary>
    public ThemeValue<Color>? BorderColor
    {
        get => GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the opacity in the range [0.0, 1.0].
    /// </summary>
    public double? Opacity
    {
        get => GetValue(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    /// <summary>
    /// Gets or sets the width, as a length in logical pixels or <see cref="WidthValue.FitContent"/>.
    /// </summary>
    public WidthValue? Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the explicit height in logical pixels.
    /// </summary>
    public double? Height
    {
        get => GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the mouse pointer cursor appearance.
    /// </summary>
    public Cursor? Cursor
    {
        get => GetValue(CursorProperty);
        set => SetValue(CursorProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal overflow behavior.
    /// </summary>
    public Overflow? OverflowX
    {
        get => GetValue(OverflowXProperty);
        set => SetValue(OverflowXProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical overflow behavior.
    /// </summary>
    public Overflow? OverflowY
    {
        get => GetValue(OverflowYProperty);
        set => SetValue(OverflowYProperty, value);
    }

    /// <summary>
    /// Gets or sets the scrollbar thumb color.
    /// </summary>
    public ThemeValue<Color>? ScrollbarThumbColor
    {
        get => GetValue(ScrollbarThumbColorProperty);
        set => SetValue(ScrollbarThumbColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the scrollbar track color, which applies only with <see cref="ScrollbarThumbColor"/>.
    /// </summary>
    public ThemeValue<Color>? ScrollbarTrackColor
    {
        get => GetValue(ScrollbarTrackColorProperty);
        set => SetValue(ScrollbarTrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the scrollbar thickness.
    /// </summary>
    public ScrollbarWidth? ScrollbarWidth
    {
        get => GetValue(ScrollbarWidthProperty);
        set => SetValue(ScrollbarWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the positioning scheme.
    /// </summary>
    public Position? Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the top offset in logical pixels.
    /// </summary>
    public double? Top
    {
        get => GetValue(TopProperty);
        set => SetValue(TopProperty, value);
    }

    /// <summary>
    /// Gets or sets the right offset in logical pixels.
    /// </summary>
    public double? Right
    {
        get => GetValue(RightProperty);
        set => SetValue(RightProperty, value);
    }

    /// <summary>
    /// Gets or sets the bottom offset in logical pixels.
    /// </summary>
    public double? Bottom
    {
        get => GetValue(BottomProperty);
        set => SetValue(BottomProperty, value);
    }

    /// <summary>
    /// Gets or sets the left offset in logical pixels.
    /// </summary>
    public double? Left
    {
        get => GetValue(LeftProperty);
        set => SetValue(LeftProperty, value);
    }
}

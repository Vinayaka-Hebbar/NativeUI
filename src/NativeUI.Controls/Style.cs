using System.Collections.Immutable;
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
    /// Identifies the <see cref="WhiteSpace"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<WhiteSpace?> WhiteSpaceProperty =
        BindableProperty.Create<Style, WhiteSpace?>(nameof(WhiteSpace));

    /// <summary>
    /// Identifies the <see cref="TextOverflow"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<TextOverflow?> TextOverflowProperty =
        BindableProperty.Create<Style, TextOverflow?>(nameof(TextOverflow));

    /// <summary>
    /// Identifies the <see cref="AlignItems"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Alignment?> AlignItemsProperty =
        BindableProperty.Create<Style, Alignment?>(nameof(AlignItems));

    /// <summary>
    /// Identifies the <see cref="Padding"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ThicknessValue?> PaddingProperty =
        BindableProperty.Create<Style, ThicknessValue?>(nameof(Padding));

    /// <summary>
    /// Identifies the <see cref="Margin"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ThicknessValue?> MarginProperty =
        BindableProperty.Create<Style, ThicknessValue?>(nameof(Margin));

    /// <summary>
    /// Identifies the <see cref="Gap"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double?> GapProperty =
        BindableProperty.Create<Style, double?>(nameof(Gap));

    /// <summary>
    /// Identifies the <see cref="BorderRadius"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<CornerRadiusValue?> BorderRadiusProperty =
        BindableProperty.Create<Style, CornerRadiusValue?>(nameof(BorderRadius));

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
    public static readonly BindableProperty<HeightValue?> HeightProperty =
        BindableProperty.Create<Style, HeightValue?>(nameof(Height));

    /// <summary>
    /// Identifies the <see cref="BoxShadows"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ImmutableArray<BoxShadow>?> BoxShadowsProperty =
        BindableProperty.Create<Style, ImmutableArray<BoxShadow>?>(nameof(BoxShadows));

    /// <summary>
    /// Identifies the <see cref="LineHeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<LineHeight?> LineHeightProperty =
        BindableProperty.Create<Style, LineHeight?>(nameof(LineHeight));

    /// <summary>
    /// Identifies the <see cref="MinWidth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<WidthValue?> MinWidthProperty =
        BindableProperty.Create<Style, WidthValue?>(nameof(MinWidth));

    /// <summary>
    /// Identifies the <see cref="MaxWidth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<WidthValue?> MaxWidthProperty =
        BindableProperty.Create<Style, WidthValue?>(nameof(MaxWidth));

    /// <summary>
    /// Identifies the <see cref="MinHeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<HeightValue?> MinHeightProperty =
        BindableProperty.Create<Style, HeightValue?>(nameof(MinHeight));

    /// <summary>
    /// Identifies the <see cref="MaxHeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<HeightValue?> MaxHeightProperty =
        BindableProperty.Create<Style, HeightValue?>(nameof(MaxHeight));

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
    public static readonly BindableProperty<InsetValue?> TopProperty =
        BindableProperty.Create<Style, InsetValue?>(nameof(Top));

    /// <summary>
    /// Identifies the <see cref="Right"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<InsetValue?> RightProperty =
        BindableProperty.Create<Style, InsetValue?>(nameof(Right));

    /// <summary>
    /// Identifies the <see cref="Bottom"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<InsetValue?> BottomProperty =
        BindableProperty.Create<Style, InsetValue?>(nameof(Bottom));

    /// <summary>
    /// Identifies the <see cref="Left"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<InsetValue?> LeftProperty =
        BindableProperty.Create<Style, InsetValue?>(nameof(Left));

    /// <summary>
    /// Identifies the <see cref="Transform"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<StyleTransform?> TransformProperty =
        BindableProperty.Create<Style, StyleTransform?>(nameof(Transform));

    /// <summary>
    /// Identifies the <see cref="ZIndex"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<int?> ZIndexProperty =
        BindableProperty.Create<Style, int?>(nameof(ZIndex));

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
    /// Gets or sets how text breaks into lines. Text inherits the value of its parent.
    /// </summary>
    public WhiteSpace? WhiteSpace
    {
        get => GetValue(WhiteSpaceProperty);
        set => SetValue(WhiteSpaceProperty, value);
    }

    /// <summary>
    /// Gets or sets how a line of text that does not fit its width is shown. An ellipsis applies to text that does not wrap.
    /// </summary>
    public TextOverflow? TextOverflow
    {
        get => GetValue(TextOverflowProperty);
        set => SetValue(TextOverflowProperty, value);
    }

    /// <summary>
    /// Gets or sets where the children of a horizontal stack sit on the vertical axis, within the height of the tallest child.
    /// </summary>
    public Alignment? AlignItems
    {
        get => GetValue(AlignItemsProperty);
        set => SetValue(AlignItemsProperty, value);
    }

    /// <summary>
    /// Gets or sets the inner padding thickness, each edge in logical pixels or as a percentage of the width of the containing
    /// block.
    /// </summary>
    public ThicknessValue? Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the outer margin thickness, each edge in logical pixels or as a percentage of the width of the containing
    /// block.
    /// </summary>
    public ThicknessValue? Margin
    {
        get => GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the spacing between adjacent children of a stack container in logical pixels.
    /// </summary>
    public double? Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>
    /// Gets or sets the radii of the four corners, each in logical pixels or as a percentage of the border box. A
    /// <see cref="double"/> converts to the same radius at every corner.
    /// </summary>
    public CornerRadiusValue? BorderRadius
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
    /// Gets or sets the width, as a length in logical pixels, <see cref="WidthValue.Percent"/>, or <see cref="WidthValue.FitContent"/>.
    /// </summary>
    public WidthValue? Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the height, as a length in logical pixels or <see cref="HeightValue.Percent"/>.
    /// </summary>
    public HeightValue? Height
    {
        get => GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the shadows of the box from top to bottom. An empty list removes the shadows that stylesheets set.
    /// </summary>
    public ImmutableArray<BoxShadow>? BoxShadows
    {
        get => GetValue(BoxShadowsProperty);
        set => SetValue(BoxShadowsProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of a line of text, as <see cref="Media.LineHeight.Normal"/>, a multiple of the font size, or
    /// a length in logical pixels. Descendants inherit it.
    /// </summary>
    public LineHeight? LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the smallest width, as a length in logical pixels or <see cref="WidthValue.Percent"/>. It wins over
    /// <see cref="MaxWidth"/>.
    /// </summary>
    public WidthValue? MinWidth
    {
        get => GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the largest width, as a length in logical pixels or <see cref="WidthValue.Percent"/>.
    /// </summary>
    public WidthValue? MaxWidth
    {
        get => GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the smallest height, as a length in logical pixels or <see cref="HeightValue.Percent"/>. It wins over
    /// <see cref="MaxHeight"/>.
    /// </summary>
    public HeightValue? MinHeight
    {
        get => GetValue(MinHeightProperty);
        set => SetValue(MinHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the largest height, as a length in logical pixels or <see cref="HeightValue.Percent"/>.
    /// </summary>
    public HeightValue? MaxHeight
    {
        get => GetValue(MaxHeightProperty);
        set => SetValue(MaxHeightProperty, value);
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
    /// Gets or sets the top inset, as a length in logical pixels or <see cref="InsetValue.Percent"/>.
    /// </summary>
    public InsetValue? Top
    {
        get => GetValue(TopProperty);
        set => SetValue(TopProperty, value);
    }

    /// <summary>
    /// Gets or sets the right inset, as a length in logical pixels or <see cref="InsetValue.Percent"/>.
    /// </summary>
    public InsetValue? Right
    {
        get => GetValue(RightProperty);
        set => SetValue(RightProperty, value);
    }

    /// <summary>
    /// Gets or sets the bottom inset, as a length in logical pixels or <see cref="InsetValue.Percent"/>.
    /// </summary>
    public InsetValue? Bottom
    {
        get => GetValue(BottomProperty);
        set => SetValue(BottomProperty, value);
    }

    /// <summary>
    /// Gets or sets the left inset, as a length in logical pixels or <see cref="InsetValue.Percent"/>.
    /// </summary>
    public InsetValue? Left
    {
        get => GetValue(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    /// <summary>
    /// Gets or sets the transform, which moves and scales the element without affecting layout.
    /// </summary>
    public StyleTransform? Transform
    {
        get => GetValue(TransformProperty);
        set => SetValue(TransformProperty, value);
    }

    /// <summary>
    /// Gets or sets the paint order of a positioned element among its positioned siblings. Higher values paint later, above
    /// lower ones, and equal values paint in tree order.
    /// </summary>
    public int? ZIndex
    {
        get => GetValue(ZIndexProperty);
        set => SetValue(ZIndexProperty, value);
    }
}

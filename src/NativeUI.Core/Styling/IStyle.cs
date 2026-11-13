using System.Collections.Immutable;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Styling;

/// <summary>
/// Provides specified style values to the cascade. A null property leaves the lower-priority value in effect.
/// </summary>
public interface IStyle
{
    /// <summary>Gets the specified foreground color or theme token.</summary>
    ThemeValue<Color>? Color { get; }

    /// <summary>Gets the specified background color or theme token.</summary>
    ThemeValue<Color>? BackgroundColor { get; }

    /// <summary>Gets the specified font size in logical pixels.</summary>
    double? FontSize { get; }

    /// <summary>Gets the specified font weight.</summary>
    FontWeight? FontWeight { get; }

    /// <summary>Gets the specified font family name.</summary>
    string? FontFamily { get; }

    /// <summary>Gets the specified horizontal text alignment.</summary>
    TextAlign? TextAlign { get; }

    /// <summary>Gets the specified inner box spacing.</summary>
    Thickness? Padding { get; }

    /// <summary>Gets the specified outer box spacing.</summary>
    Thickness? Margin { get; }

    /// <summary>Gets the specified spacing between adjacent children of a stack container.</summary>
    double? Gap { get; }

    /// <summary>Gets the specified radii of the four corners.</summary>
    CornerRadius? BorderRadius { get; }

    /// <summary>Gets the specified border width on all four edges.</summary>
    double? BorderWidth { get; }

    /// <summary>Gets the specified border color or theme token.</summary>
    ThemeValue<Color>? BorderColor { get; }

    /// <summary>Gets the specified element opacity.</summary>
    double? Opacity { get; }

    /// <summary>Gets the specified width.</summary>
    WidthValue? Width { get; }

    /// <summary>Gets the specified height in logical pixels.</summary>
    double? Height { get; }

    /// <summary>Gets the specified shadows of the box from top to bottom.</summary>
    ImmutableArray<BoxShadow>? BoxShadows => null;

    /// <summary>Gets the specified height of a line of text.</summary>
    LineHeight? LineHeight => null;

    /// <summary>Gets the specified smallest width, in logical pixels or as a percentage of the containing width.</summary>
    WidthValue? MinWidth => null;

    /// <summary>Gets the specified largest width, in logical pixels or as a percentage of the containing width.</summary>
    WidthValue? MaxWidth => null;

    /// <summary>Gets the specified smallest height in logical pixels.</summary>
    double? MinHeight => null;

    /// <summary>Gets the specified largest height in logical pixels.</summary>
    double? MaxHeight => null;

    /// <summary>Gets the specified pointer cursor appearance.</summary>
    Cursor? Cursor { get; }

    /// <summary>Gets the specified horizontal overflow behavior.</summary>
    Overflow? OverflowX { get; }

    /// <summary>Gets the specified vertical overflow behavior.</summary>
    Overflow? OverflowY { get; }

    /// <summary>Gets the specified scrollbar thumb color.</summary>
    ThemeValue<Color>? ScrollbarThumbColor { get; }

    /// <summary>Gets the specified scrollbar track color, which applies only with <see cref="ScrollbarThumbColor"/>.</summary>
    ThemeValue<Color>? ScrollbarTrackColor { get; }

    /// <summary>Gets the specified scrollbar thickness.</summary>
    ScrollbarWidth? ScrollbarWidth { get; }

    /// <summary>Gets the specified positioning scheme.</summary>
    Position? Position { get; }

    /// <summary>Gets the specified top offset in logical pixels.</summary>
    double? Top { get; }

    /// <summary>Gets the specified right offset in logical pixels.</summary>
    double? Right { get; }

    /// <summary>Gets the specified bottom offset in logical pixels.</summary>
    double? Bottom { get; }

    /// <summary>Gets the specified left offset in logical pixels.</summary>
    double? Left { get; }

    /// <summary>Gets the specified transform.</summary>
    StyleTransform? Transform { get; }

    /// <summary>Gets the specified paint order of a positioned element among its positioned siblings.</summary>
    int? ZIndex { get; }
}

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

    /// <summary>Gets the specified corner radius.</summary>
    double? BorderRadius { get; }

    /// <summary>Gets the specified element opacity.</summary>
    double? Opacity { get; }

    /// <summary>Gets the specified width.</summary>
    WidthValue? Width { get; }

    /// <summary>Gets the specified height in logical pixels.</summary>
    double? Height { get; }

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
}

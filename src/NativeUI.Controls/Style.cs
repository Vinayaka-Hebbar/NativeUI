using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Represents an inline C# style object that can be assigned to <see cref="UIElement.Style"/>
/// (for example, <c>Style = new() { BackgroundColor = Colors.Blue, Color = Colors.White, Padding = new(12, 8), BorderRadius = 6 }</c>).
/// Overrides properties from CSS classes when present.
/// </summary>
public sealed class Style : IStyle
{
    private ThemeValue<Color>? _color;
    private ThemeValue<Color>? _backgroundColor;
    private double? _fontSize;
    private FontWeight? _fontWeight;
    private string? _fontFamily;
    private TextAlign? _textAlign;
    private Thickness? _padding;
    private Thickness? _margin;
    private double? _borderRadius;
    private double? _opacity;
    private double? _width;
    private double? _height;

    /// <summary>
    /// Occurs whenever any property on this <see cref="Style"/> instance is modified.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets or sets the foreground text color or theme color token.
    /// </summary>
    public ThemeValue<Color>? Color
    {
        get => _color;
        set
        {
            _color = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the background color or theme color token.
    /// </summary>
    public ThemeValue<Color>? BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the font size in logical pixels.
    /// </summary>
    public double? FontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the typographic font weight.
    /// </summary>
    public FontWeight? FontWeight
    {
        get => _fontWeight;
        set
        {
            _fontWeight = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the font family name.
    /// </summary>
    public string? FontFamily
    {
        get => _fontFamily;
        set
        {
            _fontFamily = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the horizontal text alignment.
    /// </summary>
    public TextAlign? TextAlign
    {
        get => _textAlign;
        set
        {
            _textAlign = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the inner padding thickness in logical pixels.
    /// </summary>
    public Thickness? Padding
    {
        get => _padding;
        set
        {
            _padding = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the outer margin thickness in logical pixels.
    /// </summary>
    public Thickness? Margin
    {
        get => _margin;
        set
        {
            _margin = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the corner border radius in logical pixels.
    /// </summary>
    public double? BorderRadius
    {
        get => _borderRadius;
        set
        {
            _borderRadius = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the opacity in the range [0.0, 1.0].
    /// </summary>
    public double? Opacity
    {
        get => _opacity;
        set
        {
            _opacity = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the explicit width in logical pixels.
    /// </summary>
    public double? Width
    {
        get => _width;
        set
        {
            _width = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the explicit height in logical pixels.
    /// </summary>
    public double? Height
    {
        get => _height;
        set
        {
            _height = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

}

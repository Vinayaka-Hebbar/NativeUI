using NativeUI.Xaml;

namespace NativeUI.Media;

/// <summary>
/// Represents the height of a line of text: the CSS <c>normal</c> keyword, a multiple of the font size, or a length in
/// logical pixels. A multiple inherits as the multiple, so a descendant with a larger font gets a taller line. A length
/// inherits as the length.
/// </summary>
public readonly record struct LineHeight
{
    private readonly Unit _unit;

    private LineHeight(Unit unit, double value)
    {
        _unit = unit;
        Value = value;
    }

    private enum Unit
    {
        Normal,
        Multiple,
        Pixels
    }

    /// <summary>
    /// Gets the line height of the font: its ascent, descent, and line gap.
    /// </summary>
    [XamlLiteral("Normal")]
    public static LineHeight Normal { get; } = new(Unit.Normal, 0);

    /// <summary>
    /// Gets the multiple of the font size or the number of logical pixels, and zero for <see cref="Normal"/>.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets a value indicating whether this value is <see cref="Normal"/>.
    /// </summary>
    public bool IsNormal => _unit == Unit.Normal;

    /// <summary>
    /// Gets a value indicating whether this value is a multiple of the font size.
    /// </summary>
    public bool IsMultiple => _unit == Unit.Multiple;

    /// <summary>
    /// Gets a value indicating whether this value is a length in logical pixels.
    /// </summary>
    public bool IsPixels => _unit == Unit.Pixels;

    /// <summary>
    /// Creates a line height of <paramref name="factor"/> times the font size, the CSS form without a unit such as <c>1.5</c>.
    /// </summary>
    [XamlLiteral("{0}")]
    public static LineHeight Multiple(double factor)
    {
        return new LineHeight(Unit.Multiple, factor);
    }

    /// <summary>
    /// Creates a line height of a fixed number of logical pixels.
    /// </summary>
    [XamlLiteral("{0}px")]
    public static LineHeight Pixels(double pixels)
    {
        return new LineHeight(Unit.Pixels, pixels);
    }

    /// <summary>
    /// Returns the line height in logical pixels.
    /// </summary>
    /// <param name="fontSize">The font size in logical pixels.</param>
    /// <param name="normalHeight">The line height of the font in logical pixels, used for <see cref="Normal"/>.</param>
    public double Resolve(double fontSize, double normalHeight)
    {
        return _unit switch
        {
            Unit.Multiple => fontSize * Value,
            Unit.Pixels => Value,
            _ => normalHeight
        };
    }
}

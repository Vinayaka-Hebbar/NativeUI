using NativeUI.Xaml;

namespace NativeUI.Media;

/// <summary>
/// Represents a height: the CSS <c>auto</c> keyword, a length in logical pixels, or a percentage of the height of the
/// containing block. The default value is <see cref="Auto"/>.
/// </summary>
public readonly record struct HeightValue
{
    private readonly Unit _unit;

    private HeightValue(Unit unit, double value)
    {
        _unit = unit;
        Value = value;
    }

    private enum Unit
    {
        Auto,
        Pixels,
        Percent
    }

    /// <summary>
    /// Gets the height that the content of the element determines.
    /// </summary>
    [XamlLiteral("Auto")]
    public static HeightValue Auto { get; } = new(Unit.Auto, 0);

    /// <summary>
    /// Gets the number of logical pixels or the percentage, and zero for <see cref="Auto"/>.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets a value indicating whether this value is <see cref="Auto"/>.
    /// </summary>
    public bool IsAuto => _unit == Unit.Auto;

    /// <summary>
    /// Gets a value indicating whether this value is a length in logical pixels.
    /// </summary>
    public bool IsPixels => _unit == Unit.Pixels;

    /// <summary>
    /// Gets a value indicating whether this value is a percentage of the height of the containing block.
    /// </summary>
    public bool IsPercent => _unit == Unit.Percent;

    /// <summary>
    /// Creates a height of a fixed number of logical pixels.
    /// </summary>
    [XamlLiteral("{0}")]
    public static HeightValue Pixels(double pixels)
    {
        return new HeightValue(Unit.Pixels, pixels);
    }

    /// <summary>
    /// Creates a height that is <paramref name="percent"/> percent of the height of the containing block.
    /// </summary>
    [XamlLiteral("{0}%")]
    public static HeightValue Percent(double percent)
    {
        return new HeightValue(Unit.Percent, percent);
    }

    /// <summary>
    /// Converts a length in logical pixels to a <see cref="HeightValue"/>.
    /// </summary>
    public static implicit operator HeightValue(double pixels)
    {
        return Pixels(pixels);
    }
}

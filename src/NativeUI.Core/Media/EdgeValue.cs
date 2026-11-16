using NativeUI.Xaml;

namespace NativeUI.Media;

/// <summary>
/// Represents the thickness of one edge of a box, such as a margin or padding: a length in logical pixels or a percentage
/// of the width of the containing block. The default value is zero pixels.
/// </summary>
public readonly record struct EdgeValue
{
    private readonly Unit _unit;

    private EdgeValue(Unit unit, double value)
    {
        _unit = unit;
        Value = value;
    }

    private enum Unit
    {
        Pixels,
        Percent
    }

    /// <summary>
    /// Gets the number of logical pixels or the percentage.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets a value indicating whether this value is a length in logical pixels.
    /// </summary>
    public bool IsPixels => _unit == Unit.Pixels;

    /// <summary>
    /// Gets a value indicating whether this value is a percentage of the width of the containing block.
    /// </summary>
    public bool IsPercent => _unit == Unit.Percent;

    /// <summary>
    /// Creates an edge of a fixed number of logical pixels.
    /// </summary>
    [XamlLiteral("{0}")]
    public static EdgeValue Pixels(double pixels)
    {
        return new EdgeValue(Unit.Pixels, pixels);
    }

    /// <summary>
    /// Creates an edge that is <paramref name="percent"/> percent of the width of the containing block.
    /// </summary>
    [XamlLiteral("{0}%")]
    public static EdgeValue Percent(double percent)
    {
        return new EdgeValue(Unit.Percent, percent);
    }

    /// <summary>
    /// Converts a length in logical pixels to an <see cref="EdgeValue"/>.
    /// </summary>
    public static implicit operator EdgeValue(double pixels)
    {
        return Pixels(pixels);
    }

    /// <summary>
    /// Returns the thickness in logical pixels. A percentage resolves against <paramref name="containingWidth"/> and is
    /// zero when that width is not finite.
    /// </summary>
    /// <param name="containingWidth">The width of the containing block.</param>
    public double Resolve(double containingWidth)
    {
        if (!IsPercent)
        {
            return Value;
        }

        return double.IsFinite(containingWidth) ? Value / 100.0 * containingWidth : 0;
    }
}

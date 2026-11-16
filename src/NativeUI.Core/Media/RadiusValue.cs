using NativeUI.Xaml;

namespace NativeUI.Media;

/// <summary>
/// Represents the radius of one corner of a box: a length in logical pixels or a percentage of the border box. The
/// default value is zero pixels.
/// </summary>
public readonly record struct RadiusValue
{
    private readonly Unit _unit;

    private RadiusValue(Unit unit, double value)
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
    /// Gets a value indicating whether this value is a percentage of the border box.
    /// </summary>
    public bool IsPercent => _unit == Unit.Percent;

    /// <summary>
    /// Creates a radius of a fixed number of logical pixels.
    /// </summary>
    [XamlLiteral("{0}")]
    public static RadiusValue Pixels(double pixels)
    {
        return new RadiusValue(Unit.Pixels, pixels);
    }

    /// <summary>
    /// Creates a radius that is <paramref name="percent"/> percent of the border box.
    /// </summary>
    [XamlLiteral("{0}%")]
    public static RadiusValue Percent(double percent)
    {
        return new RadiusValue(Unit.Percent, percent);
    }

    /// <summary>
    /// Converts a length in logical pixels to a <see cref="RadiusValue"/>.
    /// </summary>
    public static implicit operator RadiusValue(double pixels)
    {
        return Pixels(pixels);
    }

    /// <summary>
    /// Returns the radius in logical pixels, resolving a percentage against <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The length that 100 percent stands for.</param>
    public double Resolve(double reference)
    {
        return IsPercent ? Value / 100.0 * reference : Value;
    }
}

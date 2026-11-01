using NativeUI.Xaml;

namespace NativeUI.Media;

/// <summary>
/// Represents a width: the CSS <c>auto</c> or <c>fit-content</c> keyword, a length in logical pixels, or a percentage of
/// the containing width. The default value is <see cref="Auto"/>.
/// </summary>
public readonly record struct WidthValue
{
    private readonly Unit _unit;

    private WidthValue(Unit unit, double value)
    {
        _unit = unit;
        Value = value;
    }

    private enum Unit
    {
        Auto,
        FitContent,
        Pixels,
        Percent
    }

    /// <summary>
    /// Gets the element's natural width: the default of its control type when it has one. Otherwise an element in normal
    /// flow fills the available width, and an out-of-flow element fits its content.
    /// </summary>
    [XamlLiteral("Auto")]
    public static WidthValue Auto { get; } = new(Unit.Auto, 0);

    /// <summary>
    /// Gets the width that fits the element's content, limited to the available width.
    /// </summary>
    [XamlLiteral("FitContent")]
    public static WidthValue FitContent { get; } = new(Unit.FitContent, 0);

    /// <summary>
    /// Gets the number of logical pixels or the percentage, and zero for <see cref="Auto"/> and <see cref="FitContent"/>.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets a value indicating whether this value is <see cref="Auto"/>.
    /// </summary>
    public bool IsAuto => _unit == Unit.Auto;

    /// <summary>
    /// Gets a value indicating whether this value is <see cref="FitContent"/>.
    /// </summary>
    public bool IsFitContent => _unit == Unit.FitContent;

    /// <summary>
    /// Gets a value indicating whether this value is a length in logical pixels.
    /// </summary>
    public bool IsPixels => _unit == Unit.Pixels;

    /// <summary>
    /// Gets a value indicating whether this value is a percentage of the containing width.
    /// </summary>
    public bool IsPercent => _unit == Unit.Percent;

    /// <summary>
    /// Creates a width of a fixed number of logical pixels.
    /// </summary>
    [XamlLiteral("{0}")]
    public static WidthValue Pixels(double pixels)
    {
        return new WidthValue(Unit.Pixels, pixels);
    }

    /// <summary>
    /// Creates a width that is <paramref name="percent"/> percent of the containing width.
    /// </summary>
    [XamlLiteral("{0}%")]
    public static WidthValue Percent(double percent)
    {
        return new WidthValue(Unit.Percent, percent);
    }

    /// <summary>
    /// Converts a length in logical pixels to a <see cref="WidthValue"/>.
    /// </summary>
    public static implicit operator WidthValue(double pixels)
    {
        return Pixels(pixels);
    }
}

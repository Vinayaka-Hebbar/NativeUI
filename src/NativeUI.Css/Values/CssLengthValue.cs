namespace NativeUI.Css;

/// <summary>
/// Specifies the unit of a <see cref="CssLengthValue"/>.
/// </summary>
public enum CssLengthUnit
{
    /// <summary>
    /// Logical device-independent pixels (<c>px</c>).
    /// </summary>
    Px,

    /// <summary>
    /// Percentage (<c>%</c>) of the containing block or reference dimension.
    /// </summary>
    Percent,

    /// <summary>
    /// Relative to the element's font size (<c>em</c>).
    /// </summary>
    Em,

    /// <summary>
    /// Relative to the root element's font size (<c>rem</c>).
    /// </summary>
    Rem
}

/// <summary>
/// Represents a compiled CSS length value with a numeric magnitude and unit.
/// </summary>
/// <param name="Value">The numeric length value.</param>
/// <param name="Unit">The unit of measurement.</param>
public sealed record CssLengthValue(
    double Value,
    CssLengthUnit Unit = CssLengthUnit.Px
) : CssValue
{
    /// <summary>
    /// Resolves this length into logical pixels given a reference font size and container length.
    /// </summary>
    public double ToLogicalPixels(double referenceFontSize = 16.0, double containerLength = 0.0)
    {
        return Unit switch
        {
            CssLengthUnit.Px => Value,
            CssLengthUnit.Em => Value * referenceFontSize,
            CssLengthUnit.Rem => Value * 16.0,
            CssLengthUnit.Percent => (Value / 100.0) * containerLength,
            _ => Value
        };
    }
}

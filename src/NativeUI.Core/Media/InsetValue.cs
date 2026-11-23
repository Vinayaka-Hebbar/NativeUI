using NativeUI.Xaml;

namespace NativeUI.Media;

/// <summary>
/// Represents an inset, which is the offset of a positioned element from an edge of its containing block: the CSS
/// <c>auto</c> keyword, a length in logical pixels, a percentage of the width or height of the containing block, or an anchor edge. The
/// default value is <see cref="Auto"/>.
/// </summary>
public readonly record struct InsetValue
{
    private readonly Unit _unit;

    private InsetValue(Unit unit, double value, AnchorSide anchorSide = default, string? anchorName = null)
    {
        _unit = unit;
        Value = value;
        AnchorSide = anchorSide;
        AnchorName = anchorName;
    }

    private enum Unit
    {
        Auto,
        Pixels,
        Percent,
        Anchor
    }

    /// <summary>
    /// Gets the inset that does not offset the element from the edge.
    /// </summary>
    [XamlLiteral("Auto")]
    public static InsetValue Auto { get; } = new(Unit.Auto, 0);

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
    /// Gets a value indicating whether this value is a percentage of the containing block.
    /// </summary>
    public bool IsPercent => _unit == Unit.Percent;

    /// <summary>Gets whether this inset refers to an anchor edge.</summary>
    public bool IsAnchor => _unit == Unit.Anchor;

    /// <summary>Gets the edge of an anchor reference.</summary>
    public AnchorSide AnchorSide { get; }

    /// <summary>Gets the explicit anchor name, or null to use the default anchor.</summary>
    public string? AnchorName { get; }

    /// <summary>Creates an inset referring to an anchor edge.</summary>
    /// <param name="side">The edge on the inset axis.</param>
    /// <param name="anchorName">An explicit CSS dashed identifier, or null for the default anchor.</param>
    public static InsetValue Anchor(AnchorSide side, string? anchorName = null)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side));
        }

        return new InsetValue(Unit.Anchor, 0, side, anchorName);
    }

    /// <summary>
    /// Creates an inset of a fixed number of logical pixels.
    /// </summary>
    [XamlLiteral("{0}")]
    public static InsetValue Pixels(double pixels)
    {
        return new InsetValue(Unit.Pixels, pixels);
    }

    /// <summary>
    /// Creates an inset that is <paramref name="percent"/> percent of the width or height of the containing block.
    /// </summary>
    [XamlLiteral("{0}%")]
    public static InsetValue Percent(double percent)
    {
        return new InsetValue(Unit.Percent, percent);
    }

    /// <summary>
    /// Converts a length in logical pixels to an <see cref="InsetValue"/>.
    /// </summary>
    public static implicit operator InsetValue(double pixels)
    {
        return Pixels(pixels);
    }
}

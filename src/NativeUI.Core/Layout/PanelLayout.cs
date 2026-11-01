using System.Globalization;

namespace NativeUI.Layout;

/// <summary>
/// Specifies the direction in which a stack arranges its children.
/// </summary>
public enum Orientation
{
    /// <summary>
    /// Children are arranged from top to bottom.
    /// </summary>
    Vertical,

    /// <summary>
    /// Children are arranged from left to right.
    /// </summary>
    Horizontal
}

/// <summary>
/// Represents the size of a grid row or column: fixed logical pixels, its content (<c>Auto</c>), or a share of the
/// remaining space (<c>*</c> or <c>2*</c>). A star row without a definite grid height sizes to its content, as in CSS.
/// </summary>
public readonly record struct GridLength
{
    private enum Unit
    {
        Pixels,
        Auto,
        Star
    }

    private readonly Unit _unit;

    private GridLength(Unit unit, double value)
    {
        _unit = unit;
        Value = value;
    }

    /// <summary>
    /// Gets a length that sizes to its content.
    /// </summary>
    public static GridLength Auto { get; } = new(Unit.Auto, 0);

    /// <summary>
    /// Gets the number of pixels, or the star weight, and zero for <see cref="Auto"/>.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets a value indicating whether this length sizes to its content.
    /// </summary>
    public bool IsAuto => _unit == Unit.Auto;

    /// <summary>
    /// Gets a value indicating whether this length takes a share of the remaining space.
    /// </summary>
    public bool IsStar => _unit == Unit.Star;

    /// <summary>
    /// Gets a value indicating whether this length is a fixed number of logical pixels.
    /// </summary>
    public bool IsPixels => _unit == Unit.Pixels;

    /// <summary>
    /// Creates a length that takes <paramref name="weight"/> shares of the remaining space.
    /// </summary>
    public static GridLength Star(double weight = 1)
    {
        return new GridLength(Unit.Star, weight);
    }

    /// <summary>
    /// Creates a length of a fixed number of logical pixels.
    /// </summary>
    public static GridLength Pixels(double pixels)
    {
        return new GridLength(Unit.Pixels, pixels);
    }

    /// <summary>
    /// Parses <c>Auto</c>, <c>*</c>, <c>2*</c>, or a number of logical pixels, using the invariant culture.
    /// </summary>
    /// <exception cref="FormatException"><paramref name="text"/> is not a grid length.</exception>
    public static GridLength Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string trimmed = text.Trim();
        if (string.Equals(trimmed, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return Auto;
        }

        if (trimmed == "*")
        {
            return Star();
        }

        bool isStar = trimmed.EndsWith('*');
        ReadOnlySpan<char> number = isStar ? trimmed.AsSpan(0, trimmed.Length - 1) : trimmed;
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !double.IsFinite(value) || value < 0)
        {
            throw new FormatException($"'{text}' is not a grid length. Use Auto, *, 2*, or a number of pixels.");
        }

        return isStar ? Star(value) : Pixels(value);
    }

    /// <summary>
    /// Converts <c>Auto</c>, <c>*</c>, <c>2*</c>, or a number of pixels, as <see cref="Parse"/> does.
    /// </summary>
    public static implicit operator GridLength(string text)
    {
        return Parse(text);
    }

    /// <summary>
    /// Converts a number of logical pixels.
    /// </summary>
    public static implicit operator GridLength(double pixels)
    {
        return Pixels(pixels);
    }
}

/// <summary>
/// Describes the cells a grid child occupies.
/// </summary>
/// <param name="Row">The zero-based first row.</param>
/// <param name="Column">The zero-based first column.</param>
/// <param name="RowSpan">The number of rows occupied.</param>
/// <param name="ColumnSpan">The number of columns occupied.</param>
public readonly record struct GridPlacement(int Row, int Column, int RowSpan, int ColumnSpan);

/// <summary>
/// Implemented by layout nodes that arrange their children in a line with fixed spacing.
/// </summary>
public interface IStackLayoutNode : ILayoutNode
{
    /// <summary>
    /// Gets the direction in which children are arranged.
    /// </summary>
    Orientation Orientation { get; }

    /// <summary>
    /// Gets the logical pixels between adjacent children.
    /// </summary>
    double Spacing { get; }
}

/// <summary>
/// Implemented by layout nodes that arrange their children in rows and columns.
/// </summary>
public interface IGridLayoutNode : ILayoutNode
{
    /// <summary>
    /// Gets the row sizes. No rows means one star row.
    /// </summary>
    IReadOnlyList<GridLength> Rows { get; }

    /// <summary>
    /// Gets the column sizes. No columns means one star column.
    /// </summary>
    IReadOnlyList<GridLength> Columns { get; }

    /// <summary>
    /// Gets the logical pixels between adjacent rows.
    /// </summary>
    double RowSpacing { get; }

    /// <summary>
    /// Gets the logical pixels between adjacent columns.
    /// </summary>
    double ColumnSpacing { get; }

    /// <summary>
    /// Returns the cells that <paramref name="child"/> occupies, which may lie outside the defined rows and columns.
    /// </summary>
    GridPlacement GetPlacement(ILayoutNode child);
}

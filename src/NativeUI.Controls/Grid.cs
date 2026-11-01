using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Represents a view that arranges its children in rows and columns. Children choose their cells with the attached
/// <see cref="RowProperty"/>, <see cref="ColumnProperty"/>, <see cref="RowSpanProperty"/>, and <see cref="ColumnSpanProperty"/>.
/// </summary>
public class Grid : View, IGridLayoutNode
{
    /// <summary>
    /// Identifies the <see cref="Rows"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<IReadOnlyList<GridLength>> RowsProperty =
        BindableProperty.Create<Grid, IReadOnlyList<GridLength>>(
            nameof(Rows),
            defaultValue: [],
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="Columns"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<IReadOnlyList<GridLength>> ColumnsProperty =
        BindableProperty.Create<Grid, IReadOnlyList<GridLength>>(
            nameof(Columns),
            defaultValue: [],
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="RowSpacing"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> RowSpacingProperty =
        BindableProperty.Create<Grid, double>(
            nameof(RowSpacing),
            defaultValue: 0,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> ColumnSpacingProperty =
        BindableProperty.Create<Grid, double>(
            nameof(ColumnSpacing),
            defaultValue: 0,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the attached zero-based row of a child.
    /// </summary>
    public static readonly BindableProperty<int> RowProperty = CreatePlacementProperty("Row", 0);

    /// <summary>
    /// Identifies the attached zero-based column of a child.
    /// </summary>
    public static readonly BindableProperty<int> ColumnProperty = CreatePlacementProperty("Column", 0);

    /// <summary>
    /// Identifies the attached number of rows a child occupies.
    /// </summary>
    public static readonly BindableProperty<int> RowSpanProperty = CreatePlacementProperty("RowSpan", 1);

    /// <summary>
    /// Identifies the attached number of columns a child occupies.
    /// </summary>
    public static readonly BindableProperty<int> ColumnSpanProperty = CreatePlacementProperty("ColumnSpan", 1);

    /// <summary>
    /// Gets or sets the row sizes, for example <c>["Auto", "*"]</c>. No rows means one star row.
    /// </summary>
    public IReadOnlyList<GridLength> Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>
    /// Gets or sets the column sizes, for example <c>[240, "*"]</c>. No columns means one star column.
    /// </summary>
    public IReadOnlyList<GridLength> Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets the logical pixels between adjacent rows.
    /// </summary>
    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the logical pixels between adjacent columns.
    /// </summary>
    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Gets the zero-based row of <paramref name="element"/>.
    /// </summary>
    public static int GetRow(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(RowProperty);
    }

    /// <summary>
    /// Sets the zero-based row of <paramref name="element"/>.
    /// </summary>
    public static void SetRow(UIElement element, int row)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        element.SetValue(RowProperty, row);
    }

    /// <summary>
    /// Gets the zero-based column of <paramref name="element"/>.
    /// </summary>
    public static int GetColumn(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(ColumnProperty);
    }

    /// <summary>
    /// Sets the zero-based column of <paramref name="element"/>.
    /// </summary>
    public static void SetColumn(UIElement element, int column)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        element.SetValue(ColumnProperty, column);
    }

    /// <summary>
    /// Gets the number of rows <paramref name="element"/> occupies.
    /// </summary>
    public static int GetRowSpan(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(RowSpanProperty);
    }

    /// <summary>
    /// Sets the number of rows <paramref name="element"/> occupies.
    /// </summary>
    public static void SetRowSpan(UIElement element, int rowSpan)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowSpan);
        element.SetValue(RowSpanProperty, rowSpan);
    }

    /// <summary>
    /// Gets the number of columns <paramref name="element"/> occupies.
    /// </summary>
    public static int GetColumnSpan(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(ColumnSpanProperty);
    }

    /// <summary>
    /// Sets the number of columns <paramref name="element"/> occupies.
    /// </summary>
    public static void SetColumnSpan(UIElement element, int columnSpan)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columnSpan);
        element.SetValue(ColumnSpanProperty, columnSpan);
    }

    /// <inheritdoc />
    GridPlacement IGridLayoutNode.GetPlacement(ILayoutNode child)
    {
        UIElement element = (UIElement)child;
        return new GridPlacement(GetRow(element), GetColumn(element), GetRowSpan(element), GetColumnSpan(element));
    }

    private static BindableProperty<int> CreatePlacementProperty(string name, int defaultValue)
    {
        return BindableProperty.CreateAttached<Grid, int>(
            name,
            defaultValue,
            static (target, _, _) => (target as UIElement)?.InvalidateVisual());
    }
}

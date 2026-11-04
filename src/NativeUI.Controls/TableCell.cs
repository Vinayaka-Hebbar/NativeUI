using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Represents one cell of a <see cref="TableRow"/>. A table presents its cells as the <c>cell</c> template part, and the
/// cells of its column header row as the <c>header-cell</c> part.
/// </summary>
public class TableCell : ContentControl
{
    /// <summary>
    /// Identifies the <see cref="ColumnSpan"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<int> ColumnSpanProperty =
        BindableProperty.Create<TableCell, int>(
            nameof(ColumnSpan),
            defaultValue: 1,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the sort direction that a column header cell shows.
    /// </summary>
    internal static readonly BindableProperty<SortDirection> SortDirectionProperty =
        BindableProperty.Create<TableCell, SortDirection>(
            nameof(SortDirection),
            defaultValue: SortDirection.None,
            propertyChanged: static (owner, _, _) => owner.OnSortDirectionChanged());

    /// <inheritdoc />
    public override string TagName => "tablecell";

    /// <summary>
    /// Gets or sets the visible column that the cell starts in, or <see langword="null"/> to follow the cells before it in
    /// its row. A table that creates cells only for visible columns sets it.
    /// </summary>
    internal int? ColumnIndex { get; set; }

    /// <summary>
    /// Gets or sets the sort direction that this cell shows as a column header.
    /// </summary>
    internal SortDirection SortDirection
    {
        get => GetValue(SortDirectionProperty);
        set => SetValue(SortDirectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of columns the cell spans. Defaults to 1.
    /// </summary>
    public int ColumnSpan
    {
        get => GetValue(ColumnSpanProperty);
        set => SetValue(ColumnSpanProperty, value);
    }

    /// <summary>
    /// Returns <c>ascending</c> or <c>descending</c> for <c>data-sort</c> while the cell is a sorted column header, which
    /// <c>tablecell[data-sort="ascending"]</c> selects.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public override string? GetAttribute(string name)
    {
        if (string.Equals(name, "data-sort", StringComparison.Ordinal))
        {
            return SortDirection switch
            {
                SortDirection.Ascending => "ascending",
                SortDirection.Descending => "descending",
                _ => null
            };
        }

        return base.GetAttribute(name);
    }

    private void OnSortDirectionChanged()
    {
        InvalidateStyle();
    }
}

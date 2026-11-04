using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Describes one column of a <see cref="Table"/> or <see cref="VirtualizedTable"/>: its header, the template of its
/// cells, and its width. Changing a property updates every row of the tables that show the column.
/// </summary>
public class TableColumn : BindableObject
{
    /// <summary>
    /// Identifies the <see cref="Header"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> HeaderProperty =
        BindableProperty.Create<TableColumn, object?>(nameof(Header), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="HeaderTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<DataTemplate?> HeaderTemplateProperty =
        BindableProperty.Create<TableColumn, DataTemplate?>(nameof(HeaderTemplate), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="CellTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<DataTemplate?> CellTemplateProperty =
        BindableProperty.Create<TableColumn, DataTemplate?>(nameof(CellTemplate), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="Width"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<GridLength> WidthProperty =
        BindableProperty.Create<TableColumn, GridLength>(nameof(Width), defaultValue: GridLength.Star());

    /// <summary>
    /// Identifies the <see cref="MinWidth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> MinWidthProperty =
        BindableProperty.Create<TableColumn, double>(nameof(MinWidth), defaultValue: 40);

    /// <summary>
    /// Identifies the <see cref="MaxWidth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> MaxWidthProperty =
        BindableProperty.Create<TableColumn, double>(nameof(MaxWidth), defaultValue: double.PositiveInfinity);

    /// <summary>
    /// Identifies the <see cref="IsVisible"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsVisibleProperty =
        BindableProperty.Create<TableColumn, bool>(nameof(IsVisible), defaultValue: true);

    /// <summary>
    /// Identifies the <see cref="CanSort"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> CanSortProperty =
        BindableProperty.Create<TableColumn, bool>(nameof(CanSort), defaultValue: true);

    /// <summary>
    /// Identifies the <see cref="SortDirection"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<SortDirection> SortDirectionProperty =
        BindableProperty.Create<TableColumn, SortDirection>(nameof(SortDirection), defaultValue: SortDirection.None);

    /// <summary>
    /// Identifies the <see cref="SortMemberPath"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> SortMemberPathProperty =
        BindableProperty.Create<TableColumn, string?>(nameof(SortMemberPath), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="SortComparer"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<IComparer<object?>?> SortComparerProperty =
        BindableProperty.Create<TableColumn, IComparer<object?>?>(nameof(SortComparer), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="IsResizable"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsResizableProperty =
        BindableProperty.Create<TableColumn, bool>(nameof(IsResizable), defaultValue: true);

    /// <summary>
    /// Gets or sets the header content. <see cref="HeaderTemplate"/> presents it when set. Otherwise a
    /// <see cref="UIElement"/> is presented as it is, and any other value as text.
    /// </summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the template that presents <see cref="Header"/>.
    /// </summary>
    public DataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the template that builds the cell of each data item in this column. Without a template, cells of data
    /// items are empty.
    /// </summary>
    public DataTemplate? CellTemplate
    {
        get => GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the column. Defaults to one share of the remaining width (<c>*</c>). Pixel and star
    /// widths line up across rows. An <c>Auto</c> width sizes to the content of each row separately.
    /// </summary>
    public GridLength Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the smallest width, in logical pixels, that resizing the column can produce. Defaults to 40.
    /// </summary>
    public double MinWidth
    {
        get => GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the largest width, in logical pixels, that resizing the column can produce. Defaults to no limit.
    /// </summary>
    public double MaxWidth
    {
        get => GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the column is shown.
    /// </summary>
    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can sort by the column while the table allows sorting.
    /// </summary>
    public bool CanSort
    {
        get => GetValue(CanSortProperty);
        set => SetValue(CanSortProperty, value);
    }

    /// <summary>
    /// Gets or sets the order in which the column sorts the rows. Setting it from code sorts the table without raising
    /// <see cref="Table.SortingEvent"/>.
    /// </summary>
    public SortDirection SortDirection
    {
        get => GetValue(SortDirectionProperty);
        set => SetValue(SortDirectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the name of the public property of each data item that the column sorts by. Values that implement
    /// <see cref="IComparable"/> compare by it, and other values by their string form. The property is read by
    /// reflection, so trimming must keep it. <see cref="SortComparer"/> takes precedence.
    /// </summary>
    public string? SortMemberPath
    {
        get => GetValue(SortMemberPathProperty);
        set => SetValue(SortMemberPathProperty, value);
    }

    /// <summary>
    /// Gets or sets the comparer of two data items in ascending order. A column with neither a comparer nor a
    /// <see cref="SortMemberPath"/> keeps its sort direction but does not reorder rows.
    /// </summary>
    public IComparer<object?>? SortComparer
    {
        get => GetValue(SortComparerProperty);
        set => SetValue(SortComparerProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can resize the column while the table allows resizing.
    /// </summary>
    public bool IsResizable
    {
        get => GetValue(IsResizableProperty);
        set => SetValue(IsResizableProperty, value);
    }
}

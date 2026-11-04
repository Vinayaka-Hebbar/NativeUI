using System.Collections.ObjectModel;
using System.Collections.Specialized;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Represents one row of a <see cref="Table"/>: a grid of <see cref="Cells"/> whose columns are the visible columns of the
/// table. Every row of a table has the same column widths, so pixel and star columns line up across rows. A table presents
/// its rows as the <c>row</c> template part. The row has <see cref="ElementStates.Selected"/>, selected by <c>:selected</c>,
/// while the table has it selected.
/// </summary>
[ContentProperty(nameof(Cells))]
public class TableRow : View, IGridLayoutNode
{
    /// <summary>
    /// Identifies the <see cref="IsSelected"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsSelectedProperty =
        BindableProperty.Create<TableRow, bool>(
            nameof(IsSelected),
            defaultValue: false,
            propertyChanged: static (owner, _, isSelected) => owner.OnIsSelectedChanged(isSelected));

    /// <summary>
    /// Identifies the column widths that the owning table assigns to the row.
    /// </summary>
    internal static readonly BindableProperty<IReadOnlyList<GridLength>> ColumnWidthsProperty =
        BindableProperty.Create<TableRow, IReadOnlyList<GridLength>>(
            nameof(ColumnWidths),
            defaultValue: [],
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    private readonly ObservableCollection<TableCell> _cells = [];
    private Control? _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableRow"/> class.
    /// </summary>
    public TableRow()
    {
        _cells.CollectionChanged += OnCellsChanged;
    }

    /// <inheritdoc />
    public override string TagName => "tablerow";

    /// <summary>
    /// Gets the cells of the row, from the first column. A cell occupies the next <see cref="TableCell.ColumnSpan"/> columns.
    /// </summary>
    public IList<TableCell> Cells => _cells;

    /// <summary>
    /// Gets a value indicating whether the table has this row selected.
    /// </summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        internal set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Gets or sets the widths of the table's visible columns.
    /// </summary>
    internal IReadOnlyList<GridLength> ColumnWidths
    {
        get => GetValue(ColumnWidthsProperty);
        set => SetValue(ColumnWidthsProperty, value);
    }

    /// <summary>
    /// Gets or sets the table that presents this row, which presents its cells as the part named <see cref="CellPartName"/>.
    /// </summary>
    internal Control? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            foreach (TableCell cell in _cells)
            {
                MarkCell(cell);
            }
        }
    }

    /// <summary>
    /// Gets or sets the part name of the cells, <c>cell</c> or <c>header-cell</c>.
    /// </summary>
    internal string CellPartName { get; set; } = "cell";

    IReadOnlyList<GridLength> IGridLayoutNode.Rows => [];

    IReadOnlyList<GridLength> IGridLayoutNode.Columns => ColumnWidths;

    double IGridLayoutNode.RowSpacing => 0;

    double IGridLayoutNode.ColumnSpacing => 0;

    /// <inheritdoc />
    GridPlacement IGridLayoutNode.GetPlacement(ILayoutNode child)
    {
        return GetPlacement(child);
    }

    /// <summary>
    /// Returns the columns that <paramref name="child"/> occupies: from its <see cref="TableCell.ColumnIndex"/> when set,
    /// and otherwise after the columns of the cells before it.
    /// </summary>
    internal GridPlacement GetPlacement(ILayoutNode child)
    {
        int column = 0;
        foreach (TableCell cell in _cells)
        {
            column = cell.ColumnIndex ?? column;
            int span = Math.Max(1, cell.ColumnSpan);
            if (ReferenceEquals(cell, child))
            {
                return new GridPlacement(0, column, 1, span);
            }

            column += span;
        }

        return new GridPlacement(0, column, 1, 1);
    }

    private void MarkCell(TableCell cell)
    {
        if (_owner is not null && cell.TemplatedParent is null)
        {
            cell.SetTemplatePart(_owner, CellPartName);
        }
    }

    private void OnCellsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            Children.Clear();
            foreach (TableCell cell in _cells)
            {
                MarkCell(cell);
                Children.Add(cell);
            }

            return;
        }

        if (args.OldItems is not null)
        {
            foreach (TableCell cell in args.OldItems)
            {
                Children.Remove(cell);
                cell.ClearTemplatePart();
            }
        }

        if (args.NewItems is not null)
        {
            for (int i = 0; i < args.NewItems.Count; i++)
            {
                TableCell cell = (TableCell)args.NewItems[i]!;
                MarkCell(cell);
                Children.Insert(Math.Min(args.NewStartingIndex + i, Children.Count), cell);
            }
        }

        InvalidateVisual();
    }

    private void OnIsSelectedChanged(bool isSelected)
    {
        SetState(ElementStates.Selected, isSelected);
        InvalidateStyle();
        InvalidateVisual();
    }
}

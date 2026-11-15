using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Provides the columns, column headers, sorting, and column resizing and reordering that <see cref="Table"/> and
/// <see cref="VirtualizedTable"/> share. Every row is a <see cref="TableRow"/> whose cells follow the visible
/// <see cref="Columns"/>. The table scrolls its own content.
/// </summary>
/// <remarks>
/// <para>
/// The template has these parts, in order: <c>header</c> hosts <see cref="Header"/>, <c>column-header</c> is the row of
/// <c>header-cell</c> parts, <c>items</c> hosts the <c>row</c> parts and their <c>cell</c> parts, and <c>footer</c> hosts
/// <see cref="Footer"/>. CSS <c>position: sticky</c> on the <c>column-header</c> or <c>footer</c> part keeps it visible while
/// the rows scroll.
/// </para>
/// <para>
/// While <see cref="CanSortColumns"/> is <see langword="true"/>, a click on a header cell cycles the
/// <see cref="TableColumn.SortDirection"/> of its column through ascending, descending, and none, and clears the other
/// columns. Shift+click keeps the other columns, for a multi-column sort. The table then raises
/// <see cref="SortingEvent"/>. A handler that sets <see cref="RoutedEventArgs.Handled"/> sorts the source itself, and the
/// table shows the items in source order. Otherwise the table shows the items sorted by <see cref="SortColumns"/> without
/// changing <see cref="ItemsControl.ItemsSource"/>. A sorted header cell has <c>data-sort="ascending"</c> or
/// <c>data-sort="descending"</c>.
/// </para>
/// <para>
/// While <see cref="CanResizeColumns"/> is <see langword="true"/>, each header cell of a resizable column has a
/// <c>column-resizer</c> part along its right edge, with the <see cref="Cursor.ColumnResize"/> cursor. Dragging it sets the
/// column's <see cref="TableColumn.Width"/> to pixels within its minimum and maximum. While <see cref="CanReorderColumns"/>
/// is <see langword="true"/>, dragging a header cell moves its column. During the drag the column header row has a
/// <c>column-drag</c> part that shows the column's header and follows the pointer, and a <c>drop-indicator</c> part at the
/// boundary where the column goes. These parts have no appearance until a stylesheet gives them one.
/// </para>
/// </remarks>
public abstract class TableBase : Selector
{
    private const string HeaderPartName = "header";
    private const string ColumnHeaderPartName = "column-header";
    private const string FooterPartName = "footer";
    private const string RowPartName = "row";
    private const string CellPartName = "cell";
    private const string HeaderCellPartName = "header-cell";
    private const string ColumnResizerPartName = "column-resizer";
    private const string ColumnDragPartName = "column-drag";
    private const string DropIndicatorPartName = "drop-indicator";
    private const double ColumnResizerWidth = 6;
    private const double DropIndicatorWidth = 2;
    private const double DragThreshold = 4;

    static TableBase()
    {
        OverflowXProperty.OverrideMetadata<TableBase>(Overflow.Auto);
    }

    /// <summary>
    /// Identifies the <see cref="Sorting"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<TableSortingEventArgs> SortingEvent =
        RoutedEvent.Register<TableBase, TableSortingEventArgs>(nameof(Sorting), RoutingStrategy.Bubble);

    /// <summary>
    /// Identifies the <see cref="Header"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> HeaderProperty =
        BindableProperty.Create<TableBase, UIElement?>(
            nameof(Header),
            defaultValue: null,
            propertyChanged: static (owner, oldHeader, newHeader) => ReplaceContent(owner._header, oldHeader, newHeader));

    /// <summary>
    /// Identifies the <see cref="Footer"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> FooterProperty =
        BindableProperty.Create<TableBase, UIElement?>(
            nameof(Footer),
            defaultValue: null,
            propertyChanged: static (owner, oldFooter, newFooter) => ReplaceContent(owner._footer, oldFooter, newFooter));

    /// <summary>
    /// Identifies the <see cref="RowHeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> RowHeightProperty =
        BindableProperty.Create<TableBase, double>(
            nameof(RowHeight),
            defaultValue: double.NaN,
            propertyChanged: static (owner, _, _) => owner.OnRowHeightChanged());

    /// <summary>
    /// Identifies the <see cref="HeaderHeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> HeaderHeightProperty =
        BindableProperty.Create<TableBase, double>(
            nameof(HeaderHeight),
            defaultValue: double.NaN,
            propertyChanged: static (owner, _, height) => owner._columnHeader.Height = double.IsFinite(height) ? height : null);

    /// <summary>
    /// Identifies the <see cref="FooterHeight"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> FooterHeightProperty =
        BindableProperty.Create<TableBase, double>(
            nameof(FooterHeight),
            defaultValue: double.NaN,
            propertyChanged: static (owner, _, height) => owner._footer.Height = double.IsFinite(height) ? height : null);

    /// <summary>
    /// Identifies the <see cref="ShowColumnHeaders"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> ShowColumnHeadersProperty =
        BindableProperty.Create<TableBase, bool>(
            nameof(ShowColumnHeaders),
            defaultValue: true,
            propertyChanged: static (owner, _, isShown) => owner._columnHeader.IsVisible = isShown);

    /// <summary>
    /// Identifies the <see cref="CanResizeColumns"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> CanResizeColumnsProperty =
        BindableProperty.Create<TableBase, bool>(
            nameof(CanResizeColumns),
            defaultValue: true,
            propertyChanged: static (owner, _, _) => owner.UpdateColumnResizers());

    /// <summary>
    /// Identifies the <see cref="CanReorderColumns"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> CanReorderColumnsProperty =
        BindableProperty.Create<TableBase, bool>(nameof(CanReorderColumns), defaultValue: false);

    /// <summary>
    /// Identifies the <see cref="CanSortColumns"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> CanSortColumnsProperty =
        BindableProperty.Create<TableBase, bool>(nameof(CanSortColumns), defaultValue: false);

    private readonly ObservableCollection<TableColumn> _columns = [];
    private readonly List<TableColumn> _sortColumns = [];
    private readonly Dictionary<TableCell, TableColumn> _headerCells = [];
    private readonly Dictionary<View, TableColumn> _columnResizers = [];
    private readonly List<TableColumn> _visibleColumns = [];
    private readonly Dictionary<(Type Type, string Path), PropertyInfo?> _sortProperties = [];
    private View _header = null!;
    private TableRow _columnHeader = null!;
    private View _footer = null!;
    private IReadOnlyList<GridLength> _columnWidths = [];
    private WidthValue? _contentWidth;
    private int[] _displayOrder = [];
    private int[] _displayPositions = [];
    private bool _isSortedBySource;
    private bool _isUpdatingSort;
    private HeaderDrag? _drag;
    private View? _columnDrag;
    private View? _dropIndicator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableBase"/> class.
    /// </summary>
    protected TableBase()
    {
        _columnHeader.Owner = this;
        _columns.CollectionChanged += OnColumnsChanged;
        _columnHeader.PointerPressed += OnHeaderPointerPressed;
        PointerMoved += OnTablePointerMoved;
        PointerReleased += OnTablePointerReleased;
    }

    /// <summary>
    /// Occurs after the user changes the sort direction of a column.
    /// </summary>
    public event EventHandler<TableSortingEventArgs> Sorting
    {
        add => AddHandler(SortingEvent, value);
        remove => RemoveHandler(SortingEvent, value);
    }

    /// <summary>
    /// Gets the columns of the table, in display order.
    /// </summary>
    public IList<TableColumn> Columns => _columns;

    /// <summary>
    /// Gets the columns that sort the rows, from the highest priority, all with a <see cref="TableColumn.SortDirection"/>
    /// other than <see cref="SortDirection.None"/>.
    /// </summary>
    public IReadOnlyList<TableColumn> SortColumns => _sortColumns.AsReadOnly();

    /// <summary>
    /// Gets or sets the element above the column headers, hosted by the <c>header</c> part.
    /// </summary>
    public UIElement? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the element below the rows, hosted by the <c>footer</c> part.
    /// </summary>
    public UIElement? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of every row in logical pixels, or <see cref="double.NaN"/> to size each row to its cells.
    /// </summary>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of the column header row in logical pixels, or <see cref="double.NaN"/> to size it to its cells.
    /// </summary>
    public double HeaderHeight
    {
        get => GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of the <c>footer</c> part in logical pixels, or <see cref="double.NaN"/> to size it to
    /// <see cref="Footer"/>.
    /// </summary>
    public double FooterHeight
    {
        get => GetValue(FooterHeightProperty);
        set => SetValue(FooterHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the column header row is shown. Defaults to <see langword="true"/>.
    /// </summary>
    public bool ShowColumnHeaders
    {
        get => GetValue(ShowColumnHeadersProperty);
        set => SetValue(ShowColumnHeadersProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can resize columns. Defaults to <see langword="true"/>.
    /// </summary>
    public bool CanResizeColumns
    {
        get => GetValue(CanResizeColumnsProperty);
        set => SetValue(CanResizeColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can reorder columns. Defaults to <see langword="false"/>.
    /// </summary>
    public bool CanReorderColumns
    {
        get => GetValue(CanReorderColumnsProperty);
        set => SetValue(CanReorderColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can sort by columns. Defaults to <see langword="false"/>.
    /// </summary>
    public bool CanSortColumns
    {
        get => GetValue(CanSortColumnsProperty);
        set => SetValue(CanSortColumnsProperty, value);
    }

    /// <summary>
    /// Gets the widths of the visible columns, which every row uses.
    /// </summary>
    protected IReadOnlyList<GridLength> ColumnWidths => _columnWidths;

    /// <summary>
    /// Scrolls so that the row of the item at <paramref name="index"/> is visible.
    /// </summary>
    /// <param name="index">The index of the item in <see cref="ItemsControl.Items"/>.</param>
    /// <param name="alignment">Where the row is placed in the visible area.</param>
    public void ScrollToRow(int index, ScrollAlignment alignment = ScrollAlignment.Nearest)
    {
        ScrollToIndex(index, alignment);
    }

    /// <summary>
    /// Scrolls so that the column at <paramref name="index"/> in <see cref="Columns"/> is visible. Nothing scrolls while the
    /// column is hidden or before the column headers have been laid out.
    /// </summary>
    /// <param name="index">The index of the column.</param>
    /// <param name="alignment">Where the column is placed in the visible area.</param>
    public void ScrollToColumn(int index, ScrollAlignment alignment = ScrollAlignment.Nearest)
    {
        if (TryGetColumnTarget(index, alignment, out double x))
        {
            ScrollTo(x, VerticalOffset);
        }
    }

    /// <summary>
    /// Scrolls so that the cell in the row of the item at <paramref name="rowIndex"/> and the column at
    /// <paramref name="columnIndex"/> is visible.
    /// </summary>
    /// <param name="rowIndex">The index of the item in <see cref="ItemsControl.Items"/>.</param>
    /// <param name="columnIndex">The index of the column in <see cref="Columns"/>.</param>
    /// <param name="vertical">Where the row is placed in the visible area.</param>
    /// <param name="horizontal">Where the column is placed in the visible area.</param>
    public void ScrollToCell(
        int rowIndex,
        int columnIndex,
        ScrollAlignment vertical = ScrollAlignment.Nearest,
        ScrollAlignment horizontal = ScrollAlignment.Nearest)
    {
        double x = TryGetColumnTarget(columnIndex, horizontal, out double columnX) ? columnX : HorizontalOffset;
        double y = rowIndex >= 0 && rowIndex < Items.Count && TryGetItemExtent(rowIndex, out double top, out double height)
            ? GetAlignedOffset(top, height, ViewportHeight, VerticalOffset, vertical)
            : VerticalOffset;
        ScrollTo(x, y);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _header = DefinePart(new View(), HeaderPartName);
        _columnHeader = DefinePart(new TableRow { CellPartName = HeaderCellPartName }, ColumnHeaderPartName);
        View items = DefinePart(CreateItemsHost(), ItemsPartName);
        _footer = DefinePart(new View(), FooterPartName);
        Children.Add(_header);
        Children.Add(_columnHeader);
        Children.Add(items);
        Children.Add(_footer);
    }

    /// <summary>
    /// Creates the view that becomes the <c>items</c> part and hosts the rows. <see cref="BuildTemplate"/> calls this
    /// before a derived constructor runs.
    /// </summary>
    protected virtual View CreateItemsHost()
    {
        return new View();
    }

    /// <summary>
    /// Returns the index of the item shown at <paramref name="position"/> in display order.
    /// </summary>
    protected int GetItemIndexAt(int position)
    {
        return position >= 0 && position < _displayOrder.Length ? _displayOrder[position] : position;
    }

    /// <summary>
    /// Returns the position in display order of the item at <paramref name="index"/>.
    /// </summary>
    protected int GetDisplayPosition(int index)
    {
        return index >= 0 && index < _displayPositions.Length ? _displayPositions[index] : index;
    }

    /// <summary>
    /// Computes the display order from <see cref="SortColumns"/>. The derived table then shows the rows in that order.
    /// </summary>
    protected void UpdateDisplayOrder()
    {
        int count = Items.Count;
        int[] order = new int[count];
        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        List<TableColumn> sortColumns = [];
        foreach (TableColumn column in _sortColumns)
        {
            if (column.SortComparer is not null || !string.IsNullOrEmpty(column.SortMemberPath))
            {
                sortColumns.Add(column);
            }
        }

        if (!_isSortedBySource && sortColumns.Count > 0)
        {
            Array.Sort(order, (left, right) => CompareItems(sortColumns, left, right));
        }

        _displayOrder = order;
        _displayPositions = new int[count];
        for (int position = 0; position < count; position++)
        {
            _displayPositions[order[position]] = position;
        }
    }

    /// <summary>
    /// Shows the rows in the order that <see cref="UpdateDisplayOrder"/> computed.
    /// </summary>
    protected abstract void ApplyDisplayOrder();

    /// <summary>
    /// Returns the row of <paramref name="item"/>: the item itself when it is a <see cref="TableRow"/>, or a new row with
    /// one cell per visible column that the column's <see cref="TableColumn.CellTemplate"/> fills. The row is the
    /// <c>row</c> part, with the table's column widths and <see cref="RowHeight"/>.
    /// </summary>
    protected TableRow CreateRow(object? item)
    {
        TableRow row;
        if (item is TableRow explicitRow)
        {
            row = explicitRow;
        }
        else
        {
            row = CreateEmptyRow(item);
            for (int column = 0; column < _visibleColumns.Count; column++)
            {
                row.Cells.Add(CreateCell(item, column));
            }
        }

        return PrepareRow(row);
    }

    /// <summary>
    /// Returns a new row for the data item <paramref name="item"/> without cells. The row is the <c>row</c> part, with
    /// the table's column widths and <see cref="RowHeight"/>.
    /// </summary>
    protected TableRow CreateEmptyRow(object? item)
    {
        return PrepareRow(new TableRow { DataContext = item });
    }

    /// <summary>
    /// Returns the cell of the data item <paramref name="item"/> in the visible column at <paramref name="columnIndex"/>,
    /// filled by the column's <see cref="TableColumn.CellTemplate"/>.
    /// </summary>
    protected TableCell CreateCell(object? item, int columnIndex)
    {
        return new TableCell
        {
            ColumnIndex = columnIndex,
            Content = _visibleColumns[columnIndex].CellTemplate?.Build(item)
        };
    }

    /// <summary>
    /// Invoked after the width of a column changes. The base implementation applies the widths to every row element.
    /// </summary>
    protected virtual void OnColumnWidthsChanged()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (GetItemElement(i) is TableRow row)
            {
                row.ColumnWidths = _columnWidths;
                row.Width = _contentWidth;
            }
        }
    }

    private TableRow PrepareRow(TableRow row)
    {
        if (!ReferenceEquals(row.TemplatedParent, this))
        {
            row.SetTemplatePart(this, RowPartName);
        }

        row.CellPartName = CellPartName;
        row.Owner = this;
        row.ColumnWidths = _columnWidths;
        row.Width = _contentWidth;
        row.Height = double.IsFinite(RowHeight) ? RowHeight : null;
        return row;
    }

    /// <inheritdoc />
    protected override UIElement CreateItemElement(object? item)
    {
        return CreateRow(item);
    }

    /// <inheritdoc />
    protected override void ApplySelectedState(UIElement element, bool isSelected)
    {
        if (element is TableRow row)
        {
            row.IsSelected = isSelected;
        }
        else
        {
            base.ApplySelectedState(element, isSelected);
        }
    }

    /// <inheritdoc />
    protected override int GetNavigationIndex(Key key, int currentIndex)
    {
        int position = base.GetNavigationIndex(key, currentIndex < 0 ? -1 : GetDisplayPosition(currentIndex));
        return position < 0 ? -1 : GetItemIndexAt(position);
    }

    /// <inheritdoc />
    protected override IEnumerable<int> GetRangeIndices(int startIndex, int endIndex)
    {
        int start = GetDisplayPosition(startIndex);
        int end = GetDisplayPosition(endIndex);
        for (int position = Math.Min(start, end); position <= Math.Max(start, end); position++)
        {
            yield return GetItemIndexAt(position);
        }
    }

    /// <summary>
    /// Creates every row element again after the columns change.
    /// </summary>
    protected virtual void OnColumnsStructureChanged()
    {
        UpdateItemElements(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        RefreshItemStates();
    }

    private static void ReplaceContent(View host, UIElement? oldContent, UIElement? newContent)
    {
        if (oldContent is not null)
        {
            host.Children.Remove(oldContent);
        }

        if (newContent is not null)
        {
            host.Children.Add(newContent);
        }
    }

    /// <summary>
    /// Applies <see cref="RowHeight"/> to every row element.
    /// </summary>
    protected virtual void OnRowHeightChanged()
    {
        double? height = double.IsFinite(RowHeight) ? RowHeight : null;
        for (int i = 0; i < Items.Count; i++)
        {
            if (GetItemElement(i) is TableRow row)
            {
                row.Height = height;
            }
        }
    }

    private bool TryGetColumnTarget(int index, ScrollAlignment alignment, out double x)
    {
        x = HorizontalOffset;
        if (index < 0 || index >= _columns.Count)
        {
            return false;
        }

        foreach ((TableCell cell, TableColumn column) in _headerCells)
        {
            if (ReferenceEquals(column, _columns[index]) && cell.ActualWidth > 0)
            {
                double left = cell.Bounds.X - Bounds.X - ComputedStyle.BorderWidth + HorizontalOffset;
                x = GetAlignedOffset(left, cell.ActualWidth, ViewportWidth, HorizontalOffset, alignment);
                return true;
            }
        }

        return false;
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is not null)
        {
            foreach (TableColumn column in args.OldItems)
            {
                column.PropertyChanged -= OnColumnPropertyChanged;
                _sortColumns.Remove(column);
            }
        }

        if (args.NewItems is not null)
        {
            foreach (TableColumn column in args.NewItems)
            {
                column.PropertyChanged += OnColumnPropertyChanged;
                if (column.SortDirection != SortDirection.None && !_sortColumns.Contains(column))
                {
                    _sortColumns.Add(column);
                }
            }
        }

        UpdateColumns();
        OnColumnsStructureChanged();
    }

    private void OnColumnPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        TableColumn column = (TableColumn)sender!;
        if (ReferenceEquals(args.Property, TableColumn.WidthProperty))
        {
            UpdateColumns();
            OnColumnWidthsChanged();
        }
        else if (ReferenceEquals(args.Property, TableColumn.IsVisibleProperty) ||
            ReferenceEquals(args.Property, TableColumn.CellTemplateProperty))
        {
            UpdateColumns();
            OnColumnsStructureChanged();
        }
        else if (ReferenceEquals(args.Property, TableColumn.HeaderProperty) ||
            ReferenceEquals(args.Property, TableColumn.HeaderTemplateProperty))
        {
            UpdateColumns();
        }
        else if (ReferenceEquals(args.Property, TableColumn.IsResizableProperty))
        {
            UpdateColumnResizers();
        }
        else if (ReferenceEquals(args.Property, TableColumn.SortDirectionProperty) && !_isUpdatingSort)
        {
            UpdateSortColumn(column);
            _isSortedBySource = false;
            OnSortChanged();
        }
        else if ((ReferenceEquals(args.Property, TableColumn.SortComparerProperty) ||
            ReferenceEquals(args.Property, TableColumn.SortMemberPathProperty)) && _sortColumns.Contains(column))
        {
            OnSortChanged();
        }
    }

    /// <summary>
    /// Rebuilds the column header cells and the column widths from the visible columns.
    /// </summary>
    private void UpdateColumns()
    {
        List<GridLength> widths = [];
        _headerCells.Clear();
        _columnResizers.Clear();
        _visibleColumns.Clear();
        _columnHeader.Cells.Clear();
        foreach (TableColumn column in _columns)
        {
            if (!column.IsVisible)
            {
                continue;
            }

            _visibleColumns.Add(column);
            widths.Add(column.Width);
            TableCell cell = new()
            {
                Content = column.HeaderTemplate is DataTemplate template ? template.Build(column.Header) : column.Header,
                SortDirection = column.SortDirection,
                Position = NativeUI.Layout.Position.Relative
            };
            View resizer = CreateOverlayPart(ColumnResizerPartName);
            resizer.Top = 0;
            resizer.Bottom = 0;
            resizer.Right = 0;
            resizer.Width = ColumnResizerWidth;
            resizer.SetValue(CursorProperty, Cursor.ColumnResize);
            cell.Children.Add(resizer);
            _columnResizers[resizer] = column;
            _headerCells[cell] = column;
            _columnHeader.Cells.Add(cell);
        }

        UpdateColumnResizers();

        _columnWidths = widths;
        _columnHeader.ColumnWidths = widths;

        // Pixel columns can be wider than the table. Their total width widens the rows and lets the table scroll sideways.
        double total = 0;
        bool isPixels = widths.Count > 0;
        foreach (GridLength width in widths)
        {
            isPixels &= width.IsPixels;
            total += width.Value;
        }

        _contentWidth = isPixels ? WidthValue.Pixels(total) : null;
        _columnHeader.Width = _contentWidth;
        ItemsHost.Width = _contentWidth;
    }

    private void UpdateSortColumn(TableColumn column)
    {
        if (column.SortDirection == SortDirection.None)
        {
            _sortColumns.Remove(column);
        }
        else if (!_sortColumns.Contains(column))
        {
            _sortColumns.Add(column);
        }

        foreach ((TableCell cell, TableColumn cellColumn) in _headerCells)
        {
            cell.SortDirection = cellColumn.SortDirection;
        }
    }

    private void OnSortChanged()
    {
        UpdateDisplayOrder();
        ApplyDisplayOrder();
    }

    /// <summary>
    /// Cycles the sort direction of <paramref name="column"/>, clearing the other columns unless
    /// <paramref name="isMultiColumn"/>, and raises <see cref="SortingEvent"/>.
    /// </summary>
    private void SortByUser(TableColumn column, bool isMultiColumn)
    {
        _isUpdatingSort = true;
        try
        {
            if (!isMultiColumn)
            {
                foreach (TableColumn other in _sortColumns.ToArray())
                {
                    if (!ReferenceEquals(other, column))
                    {
                        other.SortDirection = SortDirection.None;
                        _sortColumns.Remove(other);
                    }
                }
            }

            column.SortDirection = column.SortDirection switch
            {
                SortDirection.None => SortDirection.Ascending,
                SortDirection.Ascending => SortDirection.Descending,
                _ => SortDirection.None
            };
            UpdateSortColumn(column);
        }
        finally
        {
            _isUpdatingSort = false;
        }

        TableSortingEventArgs args = new(column);
        RaiseEvent(SortingEvent, args);
        _isSortedBySource = args.Handled;
        OnSortChanged();
    }

    private int CompareItems(List<TableColumn> sortColumns, int left, int right)
    {
        object? leftItem = Items[left];
        object? rightItem = Items[right];
        foreach (TableColumn column in sortColumns)
        {
            int result = column.SortComparer is IComparer<object?> comparer
                ? comparer.Compare(leftItem, rightItem)
                : CompareValues(ReadSortValue(leftItem, column.SortMemberPath!), ReadSortValue(rightItem, column.SortMemberPath!));
            if (result != 0)
            {
                return column.SortDirection == SortDirection.Descending ? -result : result;
            }
        }

        // Equal items keep their source order, which makes the sort stable.
        return left.CompareTo(right);
    }

    private object? ReadSortValue(object? item, string path)
    {
        if (item is null)
        {
            return null;
        }

        Type type = item.GetType();
        if (!_sortProperties.TryGetValue((type, path), out PropertyInfo? property))
        {
            property = type.GetProperty(path, BindingFlags.Public | BindingFlags.Instance);
            _sortProperties[(type, path)] = property;
        }

        return property?.GetValue(item);
    }

    /// <summary>
    /// Compares two values with null first, then by <see cref="IComparable"/> when the types match, then by their string form.
    /// </summary>
    private static int CompareValues(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null ? (right is null ? 0 : -1) : 1;
        }

        if (left is IComparable comparable && left.GetType() == right.GetType())
        {
            return comparable.CompareTo(right);
        }

        return string.Compare(
            Convert.ToString(left, CultureInfo.CurrentCulture),
            Convert.ToString(right, CultureInfo.CurrentCulture),
            StringComparison.CurrentCulture);
    }

    private TableCell? FindHeaderCell(UIElement? element)
    {
        for (UIElement? current = element; current is not null && !ReferenceEquals(current, _columnHeader); current = current.Parent)
        {
            if (current is TableCell cell && _headerCells.ContainsKey(cell))
            {
                return cell;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an absolutely positioned view that is the template part <paramref name="name"/> and paints above the
    /// column header cells.
    /// </summary>
    private View CreateOverlayPart(string name)
    {
        View part = DefinePart(new View { Style = new Style { ZIndex = 1 } }, name);
        part.Position = NativeUI.Layout.Position.Absolute;
        return part;
    }

    private void UpdateColumnResizers()
    {
        foreach ((View resizer, TableColumn column) in _columnResizers)
        {
            resizer.IsVisible = CanResizeColumns && column.IsResizable;
        }
    }

    private void OnHeaderPointerPressed(object? sender, PointerEventArgs args)
    {
        if (!IsEffectivelyEnabled || args.Source is not UIElement source)
        {
            return;
        }

        if (source is View resizer && _columnResizers.TryGetValue(resizer, out TableColumn? resizedColumn) &&
            FindHeaderCell(resizer) is TableCell resizedCell)
        {
            _drag = new HeaderDrag(resizedColumn, resizedCell, args.Position.X, resizedCell.ActualWidth, IsResizing: true, args.Modifiers);
            args.Handled = true;
            return;
        }

        if (FindHeaderCell(source) is TableCell cell)
        {
            _drag = new HeaderDrag(_headerCells[cell], cell, args.Position.X, cell.ActualWidth, IsResizing: false, args.Modifiers);
            args.Handled = true;
        }
    }

    private void OnTablePointerMoved(object? sender, PointerEventArgs args)
    {
        if (_drag is not HeaderDrag drag)
        {
            return;
        }

        // The release ends the drag only when it reaches the table. A move without the button ends a drag whose release
        // happened elsewhere.
        if (!args.Buttons.HasFlag(PointerButtons.Primary))
        {
            _drag = null;
            HideColumnDrag();
            return;
        }

        double delta = args.Position.X - drag.StartX;
        if (drag.IsResizing)
        {
            double width = Math.Clamp(drag.StartWidth + delta, drag.Column.MinWidth, drag.Column.MaxWidth);
            drag.Column.Width = GridLength.Pixels(width);
            return;
        }

        if (!drag.IsMoving && Math.Abs(delta) > DragThreshold && CanReorderColumns)
        {
            drag = drag with { IsMoving = true };
            _drag = drag;
            ShowColumnDrag(drag);
        }

        if (drag.IsMoving)
        {
            UpdateColumnDrag(drag, args.Position.X);
        }
    }

    private void OnTablePointerReleased(object? sender, PointerEventArgs args)
    {
        if (_drag is not HeaderDrag drag)
        {
            return;
        }

        _drag = null;
        if (drag.IsResizing)
        {
            return;
        }

        if (drag.IsMoving)
        {
            HideColumnDrag();
            MoveColumn(drag.Column, GetInsertionIndex(args.Position.X));
        }
        else if (CanSortColumns && drag.Column.CanSort)
        {
            SortByUser(drag.Column, drag.Modifiers.HasFlag(InputModifiers.Shift));
        }
    }

    /// <summary>
    /// Adds the <c>column-drag</c> part, which shows the dragged column's header and follows the pointer, and the
    /// <c>drop-indicator</c> part, which marks where the column moves on release, to the column header row.
    /// </summary>
    private void ShowColumnDrag(HeaderDrag drag)
    {
        _columnDrag = CreateOverlayPart(ColumnDragPartName);
        _columnDrag.Top = 0;
        _columnDrag.Bottom = 0;
        _columnDrag.Width = drag.StartWidth;
        if (drag.Column.Header is not UIElement)
        {
            _columnDrag.Children.Add(new Text
            {
                Value = Convert.ToString(drag.Column.Header, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            });
        }

        _dropIndicator = CreateOverlayPart(DropIndicatorPartName);
        _dropIndicator.Top = 0;
        _dropIndicator.Bottom = 0;
        _dropIndicator.Width = DropIndicatorWidth;
        _columnHeader.Children.Add(_columnDrag);
        _columnHeader.Children.Add(_dropIndicator);
    }

    private void UpdateColumnDrag(HeaderDrag drag, double pointerX)
    {
        if (_columnDrag is null || _dropIndicator is null)
        {
            return;
        }

        double headerX = _columnHeader.Bounds.X;
        _columnDrag.Left = drag.Cell.Bounds.X - headerX + (pointerX - drag.StartX);

        int index = GetInsertionIndex(pointerX);
        IList<TableCell> cells = _columnHeader.Cells;
        double boundary = index < cells.Count ? cells[index].Bounds.X : cells[^1].Bounds.Right;
        _dropIndicator.Left = boundary - headerX - (DropIndicatorWidth / 2);
    }

    private void HideColumnDrag()
    {
        if (_columnDrag is not null)
        {
            _columnHeader.Children.Remove(_columnDrag);
            _columnDrag = null;
        }

        if (_dropIndicator is not null)
        {
            _columnHeader.Children.Remove(_dropIndicator);
            _dropIndicator = null;
        }
    }

    /// <summary>
    /// Returns the position among the visible columns where a column dropped at <paramref name="x"/> goes: before the first
    /// header cell whose center lies right of <paramref name="x"/>, or after the last cell.
    /// </summary>
    private int GetInsertionIndex(double x)
    {
        IList<TableCell> cells = _columnHeader.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            if (x < cells[i].Bounds.X + (cells[i].ActualWidth / 2))
            {
                return i;
            }
        }

        return cells.Count;
    }

    /// <summary>
    /// Moves <paramref name="column"/> before the visible column at <paramref name="visibleIndex"/>, or after the last
    /// column when <paramref name="visibleIndex"/> is past the end.
    /// </summary>
    private void MoveColumn(TableColumn column, int visibleIndex)
    {
        int oldIndex = _columns.IndexOf(column);
        int newIndex = visibleIndex < _visibleColumns.Count ? _columns.IndexOf(_visibleColumns[visibleIndex]) : _columns.Count;
        if (newIndex > oldIndex)
        {
            newIndex--;
        }

        if (newIndex != oldIndex)
        {
            _columns.Move(oldIndex, newIndex);
        }
    }

    /// <summary>
    /// Holds a pointer gesture that began on a column header cell.
    /// </summary>
    private readonly record struct HeaderDrag(
        TableColumn Column,
        TableCell Cell,
        double StartX,
        double StartWidth,
        bool IsResizing,
        InputModifiers Modifiers,
        bool IsMoving = false);
}

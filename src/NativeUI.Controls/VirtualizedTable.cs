using System.Collections.Specialized;
using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Represents a table that creates row elements only for the visible rows plus <see cref="RowOverscan"/> rows on each
/// side. While every visible column has a pixel width, each row also creates cells only for the visible columns plus
/// <see cref="ColumnOverscan"/> columns on each side, and the table scrolls sideways across all columns. Rows come from
/// <see cref="ItemsControl.ItemsSource"/> with each column's <see cref="TableColumn.CellTemplate"/>. Every row has the
/// height <see cref="TableBase.RowHeight"/>, which defaults to 40. Sorting reorders the rows without creating an element
/// for every item. See <see cref="TableBase"/> for columns, sorting, and template parts.
/// </summary>
public class VirtualizedTable : TableBase
{
    private const double DefaultRowHeight = 40;

    static VirtualizedTable()
    {
        RowHeightProperty.OverrideMetadata<VirtualizedTable>(DefaultRowHeight);
    }

    /// <summary>
    /// Identifies the <see cref="RowOverscan"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<int> RowOverscanProperty =
        BindableProperty.Create<VirtualizedTable, int>(
            nameof(RowOverscan),
            defaultValue: 3,
            propertyChanged: static (owner, _, _) => owner.UpdatePanel());

    /// <summary>
    /// Identifies the <see cref="ColumnOverscan"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<int> ColumnOverscanProperty =
        BindableProperty.Create<VirtualizedTable, int>(
            nameof(ColumnOverscan),
            defaultValue: 2,
            propertyChanged: static (owner, _, _) => owner.UpdatePanel());

    private readonly VirtualizingGridPanel _panel;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizedTable"/> class.
    /// </summary>
    public VirtualizedTable()
    {
        _panel = (VirtualizingGridPanel)ItemsHost;
        _panel.Attach(this, CreateRealizedRow);
        _panel.AttachCells(CreateRealizedCell);
        UpdatePanel();
    }

    /// <inheritdoc />
    public override string TagName => "virtualizedtable";

    /// <summary>
    /// Gets or sets the number of rows created beyond each end of the visible range. Defaults to 3.
    /// </summary>
    public int RowOverscan
    {
        get => GetValue(RowOverscanProperty);
        set => SetValue(RowOverscanProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of columns created beyond each end of the visible horizontal range while every visible column
    /// has a pixel width. Defaults to 2.
    /// </summary>
    public int ColumnOverscan
    {
        get => GetValue(ColumnOverscanProperty);
        set => SetValue(ColumnOverscanProperty, value);
    }

    /// <inheritdoc />
    public override UIElement? GetItemElement(int index)
    {
        return _panel?.GetElement(GetDisplayPosition(index));
    }

    /// <inheritdoc />
    public override int IndexOfItemElement(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        int position = _panel.IndexOf(element);
        return position < 0 ? -1 : GetItemIndexAt(position);
    }

    /// <inheritdoc />
    protected override View CreateItemsHost()
    {
        return new VirtualizingGridPanel();
    }

    /// <inheritdoc />
    protected override void UpdateItemElements(NotifyCollectionChangedEventArgs args)
    {
        UpdateDisplayOrder();
        _panel?.Reset();
    }

    /// <inheritdoc />
    protected override void ApplyDisplayOrder()
    {
        _panel?.Reset();
    }

    /// <inheritdoc />
    protected override void OnColumnsStructureChanged()
    {
        if (_panel is not null)
        {
            _panel.ColumnWidths = ColumnWidths;
        }

        base.OnColumnsStructureChanged();
    }

    /// <inheritdoc />
    protected override void OnColumnWidthsChanged()
    {
        base.OnColumnWidthsChanged();
        if (_panel is not null)
        {
            _panel.ColumnWidths = ColumnWidths;
            _panel.Update();
        }
    }

    /// <inheritdoc />
    protected override void OnRowHeightChanged()
    {
        UpdatePanel();
    }

    /// <inheritdoc />
    protected override bool TryGetItemExtent(int index, out double top, out double height)
    {
        top = _panel.GetItemTop(GetDisplayPosition(index));
        height = _panel.ItemHeight;
        return true;
    }

    private UIElement CreateRealizedRow(int position)
    {
        int index = GetItemIndexAt(position);
        TableRow row = CreateEmptyRow(Items[index]);
        ApplySelectedState(row, IsSelectedIndex(index));
        return row;
    }

    private TableCell CreateRealizedCell(int position, int columnIndex)
    {
        return CreateCell(Items[GetItemIndexAt(position)], columnIndex);
    }

    private void UpdatePanel()
    {
        // The panel is null while the base constructors set property defaults.
        if (_panel is null)
        {
            return;
        }

        _panel.ItemHeight = double.IsFinite(RowHeight) && RowHeight > 0 ? RowHeight : DefaultRowHeight;
        _panel.Overscan = Math.Max(0, RowOverscan);
        _panel.ColumnOverscan = Math.Max(0, ColumnOverscan);
        _panel.ColumnWidths = ColumnWidths;
        _panel.Reset();
    }
}

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NativeUI.Controls;

/// <summary>
/// Represents a table that creates a row element for every item. Rows come from <see cref="Rows"/>, or from
/// <see cref="ItemsControl.ItemsSource"/> with each column's <see cref="TableColumn.CellTemplate"/>, but not from both.
/// See <see cref="TableBase"/> for columns, sorting, and template parts.
/// </summary>
public class Table : TableBase
{
    private readonly ObservableCollection<TableRow> _rows = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Table"/> class.
    /// </summary>
    public Table()
    {
        _rows.CollectionChanged += OnRowsChanged;
    }

    /// <inheritdoc />
    public override string TagName => "table";

    /// <summary>
    /// Gets the rows that the table shows when <see cref="ItemsControl.ItemsSource"/> is not set.
    /// </summary>
    /// <exception cref="InvalidOperationException">A row is added while <see cref="ItemsControl.ItemsSource"/> is set.</exception>
    public IList<TableRow> Rows => _rows;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException"><paramref name="newSource"/> is set while <see cref="Rows"/> has rows.</exception>
    protected override void OnItemsSourceChanged(IEnumerable? oldSource, IEnumerable? newSource)
    {
        if (newSource is not null && _rows.Count > 0)
        {
            throw new InvalidOperationException("ItemsSource cannot be set while Rows has rows.");
        }

        base.OnItemsSourceChanged(oldSource, newSource);
    }

    /// <inheritdoc />
    protected override void UpdateItemElements(NotifyCollectionChangedEventArgs args)
    {
        UpdateDisplayOrder();
        base.UpdateItemElements(args);
        ApplyDisplayOrder();
    }

    /// <summary>
    /// Moves each row element to its position in display order.
    /// </summary>
    protected override void ApplyDisplayOrder()
    {
        for (int position = 0; position < Items.Count; position++)
        {
            if (GetItemElement(GetItemIndexAt(position)) is not UIElement row)
            {
                continue;
            }

            int current = ItemsHost.Children.IndexOf(row);
            int target = ItemsStart + position;
            if (current >= 0 && current != target)
            {
                ItemsHost.MoveChild(current, target);
            }
        }
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is not null)
        {
            foreach (TableRow row in args.OldItems)
            {
                Items.Remove(row);
                row.Owner = null;
                row.ClearTemplatePart();
            }
        }

        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            Items.Clear();
            foreach (TableRow row in _rows)
            {
                Items.Add(row);
            }

            return;
        }

        if (args.NewItems is not null)
        {
            for (int i = 0; i < args.NewItems.Count; i++)
            {
                Items.Insert(args.NewStartingIndex + i, args.NewItems[i]);
            }
        }
    }
}

using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Hosts the rows of a virtualized table as a vertical stack and creates each row's cells. While every visible column has
/// a pixel width, the panel creates cells only for the columns in the visible horizontal range of the table, plus
/// <see cref="ColumnOverscan"/> columns beyond each end. Otherwise every row has a cell for every visible column.
/// </summary>
internal sealed class VirtualizingGridPanel : VirtualizingPanel
{
    private Func<int, int, TableCell>? _createCell;

    /// <summary>
    /// Gets or sets the widths of the table's visible columns.
    /// </summary>
    public IReadOnlyList<GridLength> ColumnWidths { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of columns created beyond each end of the visible horizontal range.
    /// </summary>
    public int ColumnOverscan { get; set; } = 2;

    /// <summary>
    /// Sets the function that creates the cell of the row at a display position in a visible column.
    /// </summary>
    public void AttachCells(Func<int, int, TableCell> createCell)
    {
        _createCell = createCell;
    }

    /// <inheritdoc />
    protected override void OnElementsUpdated()
    {
        if (_createCell is null || Owner is not Selector owner)
        {
            return;
        }

        int count = ColumnWidths.Count;
        bool isVirtualizingColumns = count > 0 && ColumnWidths.All(static width => width.IsPixels);
        int first = 0;
        int last = count - 1;
        if (isVirtualizingColumns)
        {
            (first, last) = GetVisibleColumns(owner);
        }

        foreach ((int position, UIElement element) in Elements)
        {
            if (element is not TableRow row)
            {
                continue;
            }

            // A row with pixel columns has the total column width, which the table sets on it.
            row.Right = isVirtualizingColumns ? null : 0;
            UpdateCells(row, position, first, last);
        }
    }

    /// <summary>
    /// Returns the first and last visible columns, extended by <see cref="ColumnOverscan"/> and limited to the columns.
    /// </summary>
    private (int First, int Last) GetVisibleColumns(Selector owner)
    {
        double viewport = owner.ViewportWidth > 0 ? owner.ViewportWidth : owner.ActualWidth;
        double start = owner.HorizontalOffset;
        double end = start + viewport;
        int first = ColumnWidths.Count - 1;
        int last = 0;
        double left = 0;
        for (int column = 0; column < ColumnWidths.Count; column++)
        {
            double right = left + ColumnWidths[column].Value;
            if (right > start && left < end)
            {
                first = Math.Min(first, column);
                last = Math.Max(last, column);
            }

            left = right;
        }

        if (first > last)
        {
            (first, last) = (0, -1);
        }

        int overscan = Math.Max(0, ColumnOverscan);
        return (Math.Max(0, first - overscan), Math.Min(ColumnWidths.Count - 1, last + overscan));
    }

    /// <summary>
    /// Removes the cells of <paramref name="row"/> outside the columns from <paramref name="first"/> to
    /// <paramref name="last"/> and creates the missing cells inside them.
    /// </summary>
    private void UpdateCells(TableRow row, int position, int first, int last)
    {
        HashSet<int> present = [];
        for (int i = row.Cells.Count - 1; i >= 0; i--)
        {
            int column = row.Cells[i].ColumnIndex ?? i;
            if (column < first || column > last)
            {
                row.Cells.RemoveAt(i);
            }
            else
            {
                present.Add(column);
            }
        }

        for (int column = first; column <= last; column++)
        {
            if (!present.Contains(column))
            {
                row.Cells.Add(_createCell!(position, column));
            }
        }
    }
}

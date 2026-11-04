using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Describes a sort change that the user made on a table column. A handler that sets <see cref="RoutedEventArgs.Handled"/>
/// sorts the table's source itself, and the table then shows the items in source order.
/// </summary>
/// <param name="column">The column whose sort direction changed.</param>
public sealed class TableSortingEventArgs(TableColumn column) : RoutedEventArgs
{
    /// <summary>
    /// Gets the column whose sort direction changed. Its <see cref="TableColumn.SortDirection"/> holds the new direction.
    /// </summary>
    public TableColumn Column { get; } = column;
}

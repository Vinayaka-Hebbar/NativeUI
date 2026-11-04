using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Describes a change to the selection of a <see cref="Selector"/>.
/// </summary>
/// <param name="removedItems">The items that were deselected.</param>
/// <param name="addedItems">The items that were selected.</param>
public sealed class SelectionChangedEventArgs(IReadOnlyList<object?> removedItems, IReadOnlyList<object?> addedItems) : RoutedEventArgs
{
    /// <summary>
    /// Gets the items that were deselected.
    /// </summary>
    public IReadOnlyList<object?> RemovedItems { get; } = removedItems;

    /// <summary>
    /// Gets the items that were selected.
    /// </summary>
    public IReadOnlyList<object?> AddedItems { get; } = addedItems;
}

using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Describes a click on an item of an items control.
/// </summary>
/// <param name="clickedItem">The item that was clicked.</param>
/// <param name="index">The index of the item in <see cref="ItemsControl.Items"/>.</param>
public sealed class ItemClickEventArgs(object? clickedItem, int index) : RoutedEventArgs
{
    /// <summary>
    /// Gets the item that was clicked.
    /// </summary>
    public object? ClickedItem { get; } = clickedItem;

    /// <summary>
    /// Gets the index of the item in <see cref="ItemsControl.Items"/>.
    /// </summary>
    public int Index { get; } = index;
}

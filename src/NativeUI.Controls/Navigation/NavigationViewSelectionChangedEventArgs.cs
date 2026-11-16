namespace NativeUI.Controls;

/// <summary>
/// Provides data for the <see cref="NavigationView.SelectionChanged"/> event.
/// </summary>
public sealed class NavigationViewSelectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewSelectionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="selectedItem">The newly selected data item or navigation item.</param>
    /// <param name="selectedItemContainer">The container for the newly selected item.</param>
    /// <param name="isSettingsSelected">Whether the built-in settings item was selected.</param>
    public NavigationViewSelectionChangedEventArgs(
        object? selectedItem,
        NavigationViewItemBase? selectedItemContainer,
        bool isSettingsSelected)
    {
        SelectedItem = selectedItem;
        SelectedItemContainer = selectedItemContainer;
        IsSettingsSelected = isSettingsSelected;
    }

    /// <summary>
    /// Gets the newly selected item, or <see langword="null"/> if no item is selected.
    /// </summary>
    public object? SelectedItem { get; }

    /// <summary>
    /// Gets the visual container of the newly selected item.
    /// </summary>
    public NavigationViewItemBase? SelectedItemContainer { get; }

    /// <summary>
    /// Gets whether the selection represents the built-in settings item.
    /// </summary>
    public bool IsSettingsSelected { get; }
}

namespace NativeUI.Controls;

/// <summary>
/// Provides data for the <see cref="NavigationView.ItemInvoked"/> event.
/// </summary>
public sealed class NavigationViewItemInvokedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItemInvokedEventArgs"/> class.
    /// </summary>
    /// <param name="invokedItem">The data item or navigation item that was invoked.</param>
    /// <param name="invokedItemContainer">The container that was invoked.</param>
    /// <param name="isSettingsInvoked">Whether the built-in settings item was invoked.</param>
    public NavigationViewItemInvokedEventArgs(
        object? invokedItem,
        NavigationViewItemBase? invokedItemContainer,
        bool isSettingsInvoked)
    {
        InvokedItem = invokedItem;
        InvokedItemContainer = invokedItemContainer;
        IsSettingsInvoked = isSettingsInvoked;
    }

    /// <summary>
    /// Gets the invoked item.
    /// </summary>
    public object? InvokedItem { get; }

    /// <summary>
    /// Gets the visual container of the invoked item.
    /// </summary>
    public NavigationViewItemBase? InvokedItemContainer { get; }

    /// <summary>
    /// Gets whether the invocation target is the built-in settings item.
    /// </summary>
    public bool IsSettingsInvoked { get; }
}

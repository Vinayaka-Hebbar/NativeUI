namespace NativeUI.Core;

using NativeUI.Layout;

/// <summary>
/// Provides event data for a size changed notification.
/// </summary>
/// <param name="previousSize">The previous rendered size of the element.</param>
/// <param name="newSize">The new rendered size of the element.</param>
public sealed class SizeChangedEventArgs(Size previousSize, Size newSize) : RoutedEventArgs
{
    /// <summary>
    /// Gets the size of the element prior to this change.
    /// </summary>
    public Size PreviousSize { get; } = previousSize;

    /// <summary>
    /// Gets the new size of the element.
    /// </summary>
    public Size NewSize { get; } = newSize;
}

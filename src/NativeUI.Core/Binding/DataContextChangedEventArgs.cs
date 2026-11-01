namespace NativeUI.Core;

/// <summary>
/// Provides the previous and new effective <see cref="BindableObject.DataContext"/> of a <see cref="BindableObject"/>.
/// </summary>
/// <param name="oldValue">The previous effective data context.</param>
/// <param name="newValue">The new effective data context.</param>
public sealed class DataContextChangedEventArgs(
    object? oldValue,
    object? newValue) : EventArgs
{
    /// <summary>
    /// Gets the previous effective data context.
    /// </summary>
    public object? OldValue { get; } = oldValue;

    /// <summary>
    /// Gets the new effective data context.
    /// </summary>
    public object? NewValue { get; } = newValue;
}

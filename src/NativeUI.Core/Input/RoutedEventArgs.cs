namespace NativeUI.Core;

/// <summary>
/// Carries the routing state shared by every handler along a <see cref="Core.RoutedEvent"/> route.
/// </summary>
public class RoutedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the event being dispatched, or <see langword="null"/> before the event is raised.
    /// </summary>
    public RoutedEvent? RoutedEvent { get; private set; }

    /// <summary>
    /// Gets the element that originally raised the event, or <see langword="null"/> before the event is raised.
    /// </summary>
    public object? Source { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether ordinary handlers are skipped for the rest of the route.
    /// Handlers registered with <c>handledEventsToo</c> still run.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Records the event identity and preserves the first source when these arguments are raised again.
    /// </summary>
    internal void BeginRoute(RoutedEvent routedEvent, object source)
    {
        RoutedEvent = routedEvent;
        Source ??= source;
    }
}

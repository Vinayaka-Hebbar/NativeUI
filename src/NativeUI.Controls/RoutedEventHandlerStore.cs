using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Stores routed event handlers for a <see cref="UIElement"/> and manages the lifecycle of on-demand event observers.
/// </summary>
internal sealed class RoutedEventHandlerStore(UIElement owner) : IDisposable
{
    private readonly UIElement _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    private readonly Dictionary<RoutedEvent, List<RoutedHandler>> _handlers = [];
    private readonly Dictionary<RoutedEvent, IDisposable> _observers = [];

    /// <summary>
    /// Adds a handler for <paramref name="routedEvent"/> and activates the registered observer when the first handler is added.
    /// </summary>
    public void Add<TArgs>(
        RoutedEvent<TArgs> routedEvent,
        EventHandler<TArgs> handler,
        bool handledEventsToo = false)
        where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryGetValue(routedEvent, out List<RoutedHandler>? handlers))
        {
            handlers = [];
            _handlers.Add(routedEvent, handlers);
        }

        handlers.Add(new RoutedHandler(handler, handledEventsToo));

        if (handlers.Count == 1 &&
            EventManager.CreateObserver(_owner, routedEvent) is IDisposable observer)
        {
            _observers.Add(routedEvent, observer);
        }
    }

    /// <summary>
    /// Removes the specified handler from <paramref name="routedEvent"/> and disposes the active observer when the last handler is removed.
    /// </summary>
    public void Remove<TArgs>(
        RoutedEvent<TArgs> routedEvent,
        EventHandler<TArgs> handler)
        where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryGetValue(routedEvent, out List<RoutedHandler>? handlers))
        {
            return;
        }

        int index = handlers.FindLastIndex(item => Equals(item.Handler, handler));
        if (index < 0)
        {
            return;
        }

        handlers.RemoveAt(index);

        if (handlers.Count == 0)
        {
            _handlers.Remove(routedEvent);

            if (_observers.Remove(routedEvent, out IDisposable? observer))
            {
                observer.Dispose();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether any handlers are registered for the specified routed event.
    /// </summary>
    public bool HasHandler(RoutedEvent routedEvent)
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        return _handlers.ContainsKey(routedEvent);
    }

    /// <summary>
    /// Gets a value indicating whether an active observer is registered for the specified routed event.
    /// </summary>
    public bool HasObserver(RoutedEvent routedEvent)
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        return _observers.ContainsKey(routedEvent);
    }

    /// <summary>
    /// Returns the snapshot array of handlers registered for <paramref name="routedEvent"/>.
    /// </summary>
    public RoutedHandler[] GetHandlers(RoutedEvent routedEvent)
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        return _handlers.TryGetValue(routedEvent, out List<RoutedHandler>? handlers)
            ? [.. handlers]
            : [];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (IDisposable observer in _observers.Values)
        {
            observer.Dispose();
        }

        _observers.Clear();
        _handlers.Clear();
    }
}

/// <summary>
/// Encapsulates a registered routed event handler and whether it should be invoked for already handled events.
/// </summary>
internal readonly record struct RoutedHandler(Delegate Handler, bool HandledEventsToo);

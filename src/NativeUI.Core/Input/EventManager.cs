using System.Collections.Concurrent;

namespace NativeUI.Core;

/// <summary>
/// Defines a factory that starts observation for a routed event on a target owner instance.
/// </summary>
/// <param name="owner">The owner instance that triggered the observation demand.</param>
/// <returns>A disposable observation token, or <see langword="null"/> if observation could not be started.</returns>
public delegate IDisposable? EventObserverFactory(object owner);

/// <summary>
/// Provides routed event registration and class-level event observer factory lookup.
/// </summary>
public static class EventManager
{
    private static readonly ConcurrentDictionary<(Type OwnerType, RoutedEvent Event), EventObserverFactory> s_observerFactories = new();

    /// <summary>
    /// Registers a new routed event with the specified name, owner type, and routing strategy.
    /// </summary>
    /// <typeparam name="TOwner">The type of the element registering the routed event.</typeparam>
    /// <typeparam name="TArgs">The routed event arguments type.</typeparam>
    /// <param name="name">The name of the routed event.</param>
    /// <param name="routingStrategy">The routing strategy used when the event is raised.</param>
    /// <returns>A registered <see cref="RoutedEvent{TArgs}"/> instance.</returns>
    public static RoutedEvent<TArgs> RegisterRoutedEvent<TOwner, TArgs>(string name, RoutingStrategy routingStrategy)
        where TArgs : RoutedEventArgs
    {
        return RoutedEvent.Register<TOwner, TArgs>(name, routingStrategy);
    }

    /// <summary>
    /// Registers an observer factory that is invoked when the first instance handler for <paramref name="routedEvent"/>
    /// is added to an element of type <typeparamref name="TOwner"/> (or a derived type), and disposed when the last handler is removed.
    /// </summary>
    /// <typeparam name="TOwner">The element type that responds to the observation demand.</typeparam>
    /// <param name="routedEvent">The routed event that triggers observation.</param>
    /// <param name="observe">A factory that starts observation on the target instance and returns an <see cref="IDisposable"/> token.</param>
    public static void RegisterEventObserver<TOwner>(
        RoutedEvent routedEvent,
        Func<TOwner, IDisposable?> observe)
        where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(observe);

        if (!s_observerFactories.TryAdd((typeof(TOwner), routedEvent), owner => observe((TOwner)owner)))
        {
            throw new InvalidOperationException($"An event observer is already registered for {typeof(TOwner).Name}.{routedEvent.Name}.");
        }
    }

    /// <summary>
    /// Creates an observation token for <paramref name="owner"/> and <paramref name="routedEvent"/> using the registered factory,
    /// traversing the inheritance hierarchy of <paramref name="owner"/> if necessary.
    /// </summary>
    /// <param name="owner">The element instance requesting observation.</param>
    /// <param name="routedEvent">The routed event that triggered the observation demand.</param>
    /// <returns>A disposable observation token, or <see langword="null"/> if no observer factory is registered.</returns>
    public static IDisposable? CreateObserver(object owner, RoutedEvent routedEvent)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(routedEvent);

        for (Type? type = owner.GetType(); type is not null; type = type.BaseType)
        {
            if (s_observerFactories.TryGetValue((type, routedEvent), out EventObserverFactory? factory))
            {
                return factory(owner);
            }
        }

        return null;
    }
}

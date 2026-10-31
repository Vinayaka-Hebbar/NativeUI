namespace NativeUI.Core;

/// <summary>
/// Specifies the elements visited when a <see cref="RoutedEvent"/> is raised.
/// </summary>
public enum RoutingStrategy
{
    /// <summary>
    /// Invokes handlers only on the element that raised the event.
    /// </summary>
    Direct,

    /// <summary>
    /// Invokes handlers from the root of the tree down to the element that raised the event.
    /// </summary>
    Tunnel,

    /// <summary>
    /// Invokes handlers from the element that raised the event up to the root of the tree.
    /// </summary>
    Bubble
}

/// <summary>
/// Identifies an event that is dispatched along a route through the element tree.
/// </summary>
public abstract class RoutedEvent
{
    private protected RoutedEvent(string name, Type ownerType, RoutingStrategy routingStrategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(ownerType);

        Name = name;
        OwnerType = ownerType;
        RoutingStrategy = routingStrategy;
    }

    /// <summary>
    /// Gets the event name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type that registered the event.
    /// </summary>
    public Type OwnerType { get; }

    /// <summary>
    /// Gets the route used when the event is raised.
    /// </summary>
    public RoutingStrategy RoutingStrategy { get; }

    /// <summary>
    /// Registers a routed event owned by <typeparamref name="TOwner"/>.
    /// </summary>
    /// <param name="name">The event name.</param>
    /// <param name="routingStrategy">The route used when the event is raised.</param>
    public static RoutedEvent<TArgs> Register<TOwner, TArgs>(string name, RoutingStrategy routingStrategy)
        where TArgs : RoutedEventArgs
    {
        return new RoutedEvent<TArgs>(name, typeof(TOwner), routingStrategy);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{OwnerType.Name}.{Name}";
    }
}

/// <summary>
/// Identifies a routed event whose handlers receive <typeparamref name="TArgs"/>.
/// </summary>
public sealed class RoutedEvent<TArgs> : RoutedEvent
    where TArgs : RoutedEventArgs
{
    internal RoutedEvent(string name, Type ownerType, RoutingStrategy routingStrategy)
        : base(name, ownerType, routingStrategy)
    {
    }
}

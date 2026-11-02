using System.Runtime.CompilerServices;

namespace NativeUI.Core;

/// <summary>
/// Represents a read-only bindable property whose value is computed on demand rather than stored in a property store.
/// </summary>
/// <typeparam name="TValue">The value type computed by the property.</typeparam>
public class ComputedBindableProperty<TValue> : BindableProperty<TValue>
{
    private readonly Func<BindableObject, TValue> _compute;
    private readonly Func<BindableObject, IDisposable?>? _observe;
    private readonly ConditionalWeakTable<BindableObject, ObservationState> _observationStates = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputedBindableProperty{TValue}"/> class.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="ownerType">The CLR type of the owning object.</param>
    /// <param name="compute">The function to compute the value for an owner instance.</param>
    /// <param name="observe">An optional factory callback called when the first subscriber is registered on an owner.</param>
    public ComputedBindableProperty(
        string name,
        Type ownerType,
        Func<BindableObject, TValue> compute,
        Func<BindableObject, IDisposable?>? observe = null)
        : base(name, ownerType, default!)
    {
        ArgumentNullException.ThrowIfNull(compute);
        _compute = compute;
        _observe = observe;
    }

    /// <summary>
    /// Computes the current value of this property for the specified <paramref name="owner"/>.
    /// </summary>
    /// <param name="owner">The bindable object to compute the property for.</param>
    /// <returns>The computed value.</returns>
    public TValue Compute(BindableObject owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _compute(owner);
    }

    /// <inheritdoc />
    public override TValue GetDefaultValue(BindableObject owner)
    {
        return Compute(owner);
    }

    /// <summary>
    /// Registers a subscriber for the specified <paramref name="owner"/>, starting observation on the first subscriber.
    /// Disposing the returned <see cref="IDisposable"/> decrements the subscriber count and stops observation when all subscribers are removed.
    /// </summary>
    /// <param name="owner">The bindable object instance.</param>
    /// <returns>A disposable subscription token.</returns>
    public IDisposable Subscribe(BindableObject owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        lock (_observationStates)
        {
            ObservationState state = _observationStates.GetOrCreateValue(owner);
            state.SubscriberCount++;
            if (state.SubscriberCount == 1 && _observe is not null)
            {
                state.Observation = _observe(owner);
            }

            return new SubscriptionToken(this, owner);
        }
    }

    /// <summary>
    /// Gets a value indicating whether any subscribers are currently registered for the specified <paramref name="owner"/>.
    /// </summary>
    /// <param name="owner">The bindable object instance.</param>
    /// <returns><see langword="true"/> if at least one subscriber is registered; otherwise, <see langword="false"/>.</returns>
    public bool HasSubscribers(BindableObject owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        lock (_observationStates)
        {
            return _observationStates.TryGetValue(owner, out ObservationState? state) && state.SubscriberCount > 0;
        }
    }

    /// <summary>
    /// Starts observation for existing subscribers when the owner's observation source becomes available.
    /// </summary>
    internal void StartObservation(BindableObject owner)
    {
        lock (_observationStates)
        {
            if (!_observationStates.TryGetValue(owner, out ObservationState? state))
            {
                return;
            }

            state.Observation ??= _observe?.Invoke(owner);
        }
    }

    /// <summary>
    /// Stops observation while preserving subscriptions until the observation source becomes available again.
    /// </summary>
    internal void StopObservation(BindableObject owner)
    {
        lock (_observationStates)
        {
            if (_observationStates.TryGetValue(owner, out ObservationState? state))
            {
                IDisposable? observation = state.Observation;
                state.Observation = null;
                observation?.Dispose();
            }
        }
    }

    private void Unsubscribe(BindableObject owner)
    {
        lock (_observationStates)
        {
            if (!_observationStates.TryGetValue(owner, out ObservationState? state))
            {
                return;
            }

            state.SubscriberCount--;
            if (state.SubscriberCount <= 0)
            {
                state.Observation?.Dispose();
                state.Observation = null;
                _observationStates.Remove(owner);
            }
        }
    }

    private sealed class ObservationState
    {
        public int SubscriberCount;
        public IDisposable? Observation;
    }

    private sealed class SubscriptionToken(
        ComputedBindableProperty<TValue> property,
        BindableObject owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            property.Unsubscribe(owner);
        }
    }
}

/// <summary>
/// Represents an owner-constrained <see cref="ComputedBindableProperty{TValue}"/> whose compute function is strongly typed to <typeparamref name="TOwner"/>.
/// </summary>
/// <typeparam name="TOwner">The owning <see cref="BindableObject"/> type.</typeparam>
/// <typeparam name="TValue">The value type computed by the property.</typeparam>
public sealed class ComputedBindableProperty<TOwner, TValue> : ComputedBindableProperty<TValue>
    where TOwner : BindableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComputedBindableProperty{TOwner, TValue}"/> class.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="compute">The function to compute the value for a strongly typed owner.</param>
    /// <param name="observe">An optional factory callback called when the first subscriber is registered on an owner.</param>
    public ComputedBindableProperty(
        string name,
        Func<TOwner, TValue> compute,
        Func<TOwner, IDisposable?>? observe = null)
        : base(
            name,
            typeof(TOwner),
            owner => compute((TOwner)owner),
            observe is not null ? owner => observe((TOwner)owner) : null)
    {
    }

    /// <summary>
    /// Registers a subscriber for the specified <paramref name="owner"/>, starting observation on the first subscriber.
    /// Disposing the returned <see cref="IDisposable"/> decrements the subscriber count and stops observation when all subscribers are removed.
    /// </summary>
    /// <param name="owner">The strongly typed owner instance.</param>
    /// <returns>A disposable subscription token.</returns>
    public IDisposable Subscribe(TOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return ((ComputedBindableProperty<TValue>)this).Subscribe(owner);
    }

    /// <summary>
    /// Gets a value indicating whether any subscribers are currently registered for the specified <paramref name="owner"/>.
    /// </summary>
    /// <param name="owner">The strongly typed owner instance.</param>
    /// <returns><see langword="true"/> if at least one subscriber is registered; otherwise, <see langword="false"/>.</returns>
    public bool HasSubscribers(TOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return ((ComputedBindableProperty<TValue>)this).HasSubscribers(owner);
    }
}

namespace NativeUI.Core;

/// <summary>
/// Provides data for the <see cref="BindableObject.PropertyChanged"/> event.
/// </summary>
/// <param name="Property">The bindable property whose effective value changed.</param>
/// <param name="OldValue">The previous effective value.</param>
/// <param name="NewValue">The new effective value.</param>
public readonly record struct BindablePropertyChangedEventArgs(
    BindableProperty Property,
    object? OldValue,
    object? NewValue);

/// <summary>
/// Base class for objects that support <see cref="BindableProperty{TValue}"/> storage, <see cref="DataContext"/> inheritance,
/// and incremental property bindings.
/// </summary>
public abstract class BindableObject : IDisposable
{
    private readonly Dictionary<BindableProperty, object?> _values = [];
    private readonly Dictionary<BindableProperty, IBindingSubscription> _bindings = [];
    private object? _localDataContext;
    private bool _hasLocalDataContext;
    private object? _inheritedDataContext;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the UI thread dispatcher used to marshal bound property changes.
    /// </summary>
    public IDispatcher Dispatcher { get; set; } = ImmediateDispatcher.Instance;

    /// <summary>
    /// Gets or sets the data context used as the default source for bindings on this object and its descendants.
    /// </summary>
    public object? DataContext
    {
        get => _hasLocalDataContext ? _localDataContext : _inheritedDataContext;
        set
        {
            object? previous = DataContext;
            _localDataContext = value;
            _hasLocalDataContext = true;
            if (!ReferenceEquals(previous, value))
            {
                NotifyDataContextChanged(previous, value);
            }
        }
    }

    /// <summary>
    /// Occurs whenever a <see cref="BindableProperty"/> on this instance changes its effective value.
    /// Listeners run in subscription order, and the value is already stored when they run. A listener that changes the same
    /// property starts a nested notification, so listeners after it see the newer value first; read the current value instead
    /// of relying on <see cref="BindablePropertyChangedEventArgs.NewValue"/> in that case. An exception from a listener
    /// propagates to the caller of <see cref="SetValue"/> and skips the remaining listeners, but the value stays stored.
    /// </summary>
    public event EventHandler<BindablePropertyChangedEventArgs>? PropertyChanged;

    /// <summary>
    /// Gets the current value of the specified strongly typed <paramref name="property"/>.
    /// </summary>
    public TValue GetValue<TValue>(BindableProperty<TValue> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        BindableObject? store = GetPropertyStore(property, createIfMissing: false);
        if (store is not null && store._values.TryGetValue(property, out object? rawValue))
        {
            // A null in a forwarding store such as Style means unspecified, so the reader's default applies.
            if (rawValue is not null || ReferenceEquals(store, this))
            {
                return (TValue)rawValue!;
            }
        }

        return property.GetDefaultValue(this);
    }

    /// <summary>
    /// Sets the value of the specified strongly typed <paramref name="property"/> and notifies listeners only if the value changed.
    /// </summary>
    /// <returns><see langword="true"/> when the value changed.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool SetValue<TValue>(BindableProperty<TValue> property, TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);
        ObjectDisposedException.ThrowIf(_disposed, this);
        BindableObject store = GetPropertyStore(property, createIfMissing: true)!;
        if (!ReferenceEquals(store, this))
        {
            return store.SetValue(property, value);
        }

        TValue oldValue = GetValue(property);
        if (EqualityComparer<TValue>.Default.Equals(oldValue, value))
        {
            _values[property] = value;
            return false;
        }

        _values[property] = value;
        NotifyPropertyChanged(property, oldValue, value);
        return true;
    }

    /// <summary>
    /// Removes the set value of <paramref name="property"/>, so it reads as its default for this object's type again.
    /// A binding on the property stays attached and sets the value on its next update.
    /// </summary>
    /// <returns><see langword="true"/> when the effective value changed.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool ClearValue<TValue>(BindableProperty<TValue> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        ObjectDisposedException.ThrowIf(_disposed, this);
        BindableObject? store = GetPropertyStore(property, createIfMissing: false);
        if (store is null)
        {
            return false;
        }

        if (!ReferenceEquals(store, this))
        {
            return store.ClearValue(property);
        }

        TValue oldValue = GetValue(property);
        if (!_values.Remove(property))
        {
            return false;
        }

        TValue newValue = GetValue(property);
        if (EqualityComparer<TValue>.Default.Equals(oldValue, newValue))
        {
            return false;
        }

        NotifyPropertyChanged(property, oldValue, newValue);
        return true;
    }

    /// <summary>
    /// Removes the local <see cref="DataContext"/>, so this object uses the data context inherited from its parent again.
    /// Assigning <see langword="null"/> to <see cref="DataContext"/> instead sets a local null value, which stops inheritance.
    /// </summary>
    public void ClearDataContext()
    {
        if (!_hasLocalDataContext)
        {
            return;
        }

        object? previous = _localDataContext;
        _localDataContext = null;
        _hasLocalDataContext = false;
        if (!ReferenceEquals(previous, _inheritedDataContext))
        {
            NotifyDataContextChanged(previous, _inheritedDataContext);
        }
    }

    /// <summary>
    /// Attaches a compiled binding to the specified <paramref name="property"/>, replacing any existing binding on that property.
    /// Disposing the returned object detaches the binding.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public IDisposable SetBinding<TProperty, TSource, TValue>(
        BindableProperty<TProperty> property,
        CompiledBinding<TSource, TValue> binding)
        where TSource : class
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(binding);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_bindings.TryGetValue(property, out IBindingSubscription? existing))
        {
            existing.Dispose();
        }

        IDisposable? propertySubscription = (property as ComputedBindableProperty<TProperty>)?.Subscribe(this);
        IBindingSubscription subscription = binding.Subscribe(this, property);
        _bindings[property] = subscription;
        subscription.OnDataContextChanged(DataContext);
        return new BindingRegistration(this, property, subscription, propertySubscription);
    }

    /// <summary>
    /// Propagates an inherited <paramref name="dataContext"/> from a parent element when no local <see cref="DataContext"/> is set.
    /// </summary>
    protected internal void SetInheritedDataContext(object? dataContext)
    {
        object? previous = _inheritedDataContext;
        _inheritedDataContext = dataContext;

        // A local data context keeps taking precedence, but the inherited one is retained for ClearDataContext.
        if (!_hasLocalDataContext && !ReferenceEquals(previous, dataContext))
        {
            NotifyDataContextChanged(previous, dataContext);
        }
    }

    /// <summary>
    /// Returns the object that holds the value of <paramref name="property"/>, which is this object unless a derived type
    /// forwards the property. Returning <see langword="null"/> means no store exists yet, which is allowed only when
    /// <paramref name="createIfMissing"/> is <see langword="false"/>, and the property reads as its default.
    /// </summary>
    protected virtual BindableObject? GetPropertyStore(BindableProperty property, bool createIfMissing)
    {
        return this;
    }

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for a forwarded property whose value changed in its store.
    /// </summary>
    protected void RaisePropertyChanged(BindablePropertyChangedEventArgs args)
    {
        PropertyChanged?.Invoke(this, args);
    }

    /// <summary>
    /// Invoked whenever a <see cref="BindableProperty"/> changes its effective value on this instance.
    /// </summary>
    protected virtual void OnPropertyChanged(BindableProperty property, object? oldValue, object? newValue)
    {
    }

    /// <summary>
    /// Invoked whenever the effective <see cref="DataContext"/> changes.
    /// </summary>
    protected virtual void OnDataContextChanged(DataContextChangedEventArgs args)
    {
    }

    /// <summary>
    /// Releases all active binding subscriptions held by this instance.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (IBindingSubscription subscription in _bindings.Values.ToArray())
            {
                subscription.Dispose();
            }

            _bindings.Clear();
        }

        _disposed = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Raises change notifications for the specified <paramref name="property"/> without storing a value in the property store.
    /// </summary>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="property">The property whose value changed.</param>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    protected void NotifyPropertyChanged<TValue>(
        BindableProperty<TValue> property,
        TValue oldValue,
        TValue newValue)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.NotifyChanged(this, oldValue, newValue);
        OnPropertyChanged(property, oldValue, newValue);
        PropertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property, oldValue, newValue));
    }

    /// <summary>
    /// Updates a snapshot of the bindings, because a change handler that a binding triggers can attach or detach bindings.
    /// </summary>
    private void NotifyDataContextChanged(object? oldDataContext, object? newDataContext)
    {
        foreach (IBindingSubscription subscription in _bindings.Values.ToArray())
        {
            subscription.OnDataContextChanged(newDataContext);
        }

        OnDataContextChanged(new DataContextChangedEventArgs(oldDataContext, newDataContext));
    }

    /// <summary>
    /// Detaches a binding and removes it from its owner, unless a newer binding on the property has replaced it.
    /// </summary>
    private sealed class BindingRegistration(
        BindableObject owner,
        BindableProperty property,
        IBindingSubscription subscription,
        IDisposable? propertySubscription = null) : IDisposable
    {
        public void Dispose()
        {
            subscription.Dispose();
            propertySubscription?.Dispose();
            if (owner._bindings.TryGetValue(property, out IBindingSubscription? current) &&
                ReferenceEquals(current, subscription))
            {
                owner._bindings.Remove(property);
            }
        }
    }
}

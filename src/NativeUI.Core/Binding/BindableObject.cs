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
    /// </summary>
    public event EventHandler<BindablePropertyChangedEventArgs>? PropertyChanged;

    /// <summary>
    /// Gets the current value of the specified strongly typed <paramref name="property"/>.
    /// </summary>
    public TValue GetValue<TValue>(BindableProperty<TValue> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (_values.TryGetValue(property, out object? rawValue) && rawValue is TValue typedValue)
        {
            return typedValue;
        }

        return property.DefaultValue;
    }

    /// <summary>
    /// Sets the value of the specified strongly typed <paramref name="property"/> and notifies listeners only if the value changed.
    /// </summary>
    public bool SetValue<TValue>(BindableProperty<TValue> property, TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);
        TValue oldValue = GetValue(property);
        if (EqualityComparer<TValue>.Default.Equals(oldValue, value))
        {
            _values[property] = value;
            return false;
        }

        _values[property] = value;
        property.NotifyChanged(this, oldValue, value);
        OnPropertyChanged(property, oldValue, value);
        PropertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property, oldValue, value));
        return true;
    }

    /// <summary>
    /// Attaches a compiled binding to the specified <paramref name="property"/>, replacing any existing binding on that property.
    /// </summary>
    public IDisposable SetBinding<TProperty, TSource, TValue>(
        BindableProperty<TProperty> property,
        CompiledBinding<TSource, TValue> binding)
        where TSource : class
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(binding);

        if (_bindings.TryGetValue(property, out IBindingSubscription? existing))
        {
            existing.Dispose();
        }

        IBindingSubscription subscription = binding.Subscribe(this, property);
        _bindings[property] = subscription;
        subscription.OnDataContextChanged(DataContext);
        return subscription;
    }

    /// <summary>
    /// Propagates an inherited <paramref name="dataContext"/> from a parent element when no local <see cref="DataContext"/> is set.
    /// </summary>
    protected internal void SetInheritedDataContext(object? dataContext)
    {
        if (_hasLocalDataContext)
        {
            return;
        }

        object? previous = _inheritedDataContext;
        _inheritedDataContext = dataContext;
        if (!ReferenceEquals(previous, dataContext))
        {
            NotifyDataContextChanged(previous, dataContext);
        }
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
            foreach (IBindingSubscription subscription in _bindings.Values)
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

    private void NotifyDataContextChanged(object? oldDataContext, object? newDataContext)
    {
        foreach (IBindingSubscription subscription in _bindings.Values)
        {
            subscription.OnDataContextChanged(newDataContext);
        }

        OnDataContextChanged(new DataContextChangedEventArgs(oldDataContext, newDataContext));
    }
}

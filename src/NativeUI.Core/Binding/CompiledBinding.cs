using System.ComponentModel;

namespace NativeUI.Core;

/// <summary>
/// Provides static factory methods for creating <see cref="CompiledBinding{TSource, TValue}"/> instances
/// with automatic compile-time type inference.
/// </summary>
public static class CompiledBinding
{
    /// <summary>
    /// Creates a strongly typed <see cref="CompiledBinding{TSource, TValue}"/> with <typeparamref name="TValue"/>
    /// automatically inferred from the getter delegate return type.
    /// </summary>
    public static CompiledBinding<TSource, TValue> Create<TSource, TValue>(
        string propertyName,
        Func<TSource, TValue> getter,
        Action<TSource, TValue>? setter = null,
        BindingMode mode = BindingMode.OneWay)
        where TSource : class
    {
        return new CompiledBinding<TSource, TValue>(propertyName, getter, setter, mode);
    }
}

/// <summary>
/// Represents a compile-time generated, reflection-free property binding between a <typeparamref name="TSource"/> ViewModel
/// and a target <see cref="BindableProperty{TValue}"/>.
/// </summary>
/// <typeparam name="TSource">The source ViewModel type.</typeparam>
/// <typeparam name="TValue">The bound property value type.</typeparam>
public sealed class CompiledBinding<TSource, TValue>
    where TSource : class
{
    /// <summary>
    /// Gets the name of the source property observed on <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the strongly typed getter delegate used to read the current value from <typeparamref name="TSource"/>.
    /// </summary>
    public Func<TSource, TValue> Getter { get; }

    /// <summary>
    /// Gets the optional strongly typed setter delegate used in <see cref="BindingMode.TwoWay"/> bindings.
    /// </summary>
    public Action<TSource, TValue>? Setter { get; }

    /// <summary>
    /// Gets the binding mode controlling data flow direction.
    /// </summary>
    public BindingMode Mode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledBinding{TSource, TValue}"/> class.
    /// </summary>
    public CompiledBinding(
        string propertyName,
        Func<TSource, TValue> getter,
        Action<TSource, TValue>? setter = null,
        BindingMode mode = BindingMode.OneWay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(getter);

        PropertyName = propertyName;
        Getter = getter;
        Setter = setter;
        Mode = mode;
    }

    internal IBindingSubscription Subscribe<TProperty>(
        BindableObject target,
        BindableProperty<TProperty> targetProperty)
    {
        return new Subscription<TProperty>(target, targetProperty, this);
    }

    private sealed class Subscription<TProperty> : IBindingSubscription
    {
        private readonly BindableObject _target;
        private readonly BindableProperty<TProperty> _targetProperty;
        private readonly CompiledBinding<TSource, TValue> _binding;
        private TSource? _currentSource;
        private INotifyPropertyChanged? _observableSource;
        private bool _isUpdating;
        private bool _disposed;

        public Subscription(
            BindableObject target,
            BindableProperty<TProperty> targetProperty,
            CompiledBinding<TSource, TValue> binding)
        {
            _target = target;
            _targetProperty = targetProperty;
            _binding = binding;

            if (_binding.Mode == BindingMode.TwoWay && _binding.Setter is not null)
            {
                _target.PropertyChanged += OnTargetPropertyChanged;
            }
        }

        public void OnDataContextChanged(object? dataContext)
        {
            if (_disposed)
            {
                return;
            }

            DetachFromSource();

            if (dataContext is not TSource typedSource)
            {
                return;
            }

            _currentSource = typedSource;
            PushSourceToTarget(typedSource);

            if (_binding.Mode != BindingMode.OneTime && typedSource is INotifyPropertyChanged notify)
            {
                _observableSource = notify;
                _observableSource.PropertyChanged += OnSourcePropertyChanged;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DetachFromSource();

            if (_binding.Mode == BindingMode.TwoWay && _binding.Setter is not null)
            {
                _target.PropertyChanged -= OnTargetPropertyChanged;
            }
        }

        private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_disposed || _currentSource is null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(e.PropertyName) &&
                !string.Equals(e.PropertyName, _binding.PropertyName, StringComparison.Ordinal))
            {
                return;
            }

            TSource source = _currentSource;
            if (_target.Dispatcher.CheckAccess())
            {
                PushSourceToTarget(source);
            }
            else
            {
                _target.Dispatcher.Invoke(() => PushSourceToTarget(source));
            }
        }

        private void OnTargetPropertyChanged(object? sender, BindablePropertyChangedEventArgs e)
        {
            if (_disposed || _isUpdating || _currentSource is null || _binding.Setter is null)
            {
                return;
            }

            if (!ReferenceEquals(e.Property, _targetProperty))
            {
                return;
            }

            if (e.NewValue is TValue typedValue)
            {
                try
                {
                    _isUpdating = true;
                    _binding.Setter(_currentSource, typedValue);
                }
                finally
                {
                    _isUpdating = false;
                }
            }
            else if (e.NewValue is null && default(TValue) is null)
            {
                try
                {
                    _isUpdating = true;
                    _binding.Setter(_currentSource, default!);
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        private void PushSourceToTarget(TSource source)
        {
            if (_isUpdating)
            {
                return;
            }

            try
            {
                _isUpdating = true;
                TValue value = _binding.Getter(source);
                if (value is TProperty typedPropVal)
                {
                    _target.SetValue(_targetProperty, typedPropVal);
                }
                else if (value is null)
                {
                    _target.SetValue(_targetProperty, default!);
                }
                else
                {
                    _target.SetValue(_targetProperty, (TProperty)(object)value);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void DetachFromSource()
        {
            if (_observableSource is not null)
            {
                _observableSource.PropertyChanged -= OnSourcePropertyChanged;
                _observableSource = null;
            }

            _currentSource = null;
        }
    }
}

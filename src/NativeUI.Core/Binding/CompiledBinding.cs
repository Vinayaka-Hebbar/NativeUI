using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

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
        private BindableObject? _bindableSource;
        private readonly BindingValidation _validation;
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
            _validation = new BindingValidation(target, targetProperty, binding.PropertyName);

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
            _validation.SetSource(typedSource);
            PushSourceToTarget(typedSource);

            if (_binding.Mode == BindingMode.OneTime)
            {
                return;
            }

            if (typedSource is INotifyPropertyChanged notify)
            {
                _observableSource = notify;
                _observableSource.PropertyChanged += OnSourcePropertyChanged;
            }
            else if (typedSource is BindableObject bindable)
            {
                _bindableSource = bindable;
                _bindableSource.PropertyChanged += OnBindableSourcePropertyChanged;
            }
        }

        public IReadOnlyList<ValidationError> Errors => _validation.Errors;

        public void Validate() => _validation.Refresh();

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
            if (string.IsNullOrEmpty(e.PropertyName) ||
                string.Equals(e.PropertyName, _binding.PropertyName, StringComparison.Ordinal))
            {
                UpdateTargetFromSource();
            }
        }

        private void OnBindableSourcePropertyChanged(object? sender, BindablePropertyChangedEventArgs e)
        {
            if (string.Equals(e.Property.Name, _binding.PropertyName, StringComparison.Ordinal))
            {
                UpdateTargetFromSource();
            }
        }

        private void UpdateTargetFromSource()
        {
            if (_disposed || _currentSource is null)
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

            try
            {
                _isUpdating = true;
                TValue value;
                try
                {
                    value = e.NewValue is TValue typedValue ? typedValue : ConvertTargetValue(e.NewValue);
                }
                catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException or ArgumentException)
                {
                    _validation.SetConversionError(exception);
                    return;
                }

                _binding.Setter(_currentSource, value);
                _validation.SetConversionError(null);
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or ValidationException)
            {
                _validation.SetConversionError(exception);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private static TValue ConvertTargetValue(object? value)
        {
            if (value is null && default(TValue) is null)
            {
                return default!;
            }

            Type type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
            object? converted = type.IsEnum && value is string text
                ? Enum.Parse(type, text)
                : Convert.ChangeType(value, type, CultureInfo.CurrentCulture);
            return (TValue)converted!;
        }

        private void PushSourceToTarget(TSource source)
        {
            _validation.Refresh();
            if (_isUpdating || _validation.HasConversionError)
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
                    _target.SetValue(_targetProperty, _targetProperty.ConvertValue(value));
                }
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or ValidationException)
            {
                _validation.SetConversionError(exception);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void DetachFromSource()
        {
            _validation.SetSource(null);
            if (_observableSource is not null)
            {
                _observableSource.PropertyChanged -= OnSourcePropertyChanged;
                _observableSource = null;
            }

            if (_bindableSource is not null)
            {
                _bindableSource.PropertyChanged -= OnBindableSourcePropertyChanged;
                _bindableSource = null;
            }

            _currentSource = null;
        }
    }
}

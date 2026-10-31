namespace NativeUI.Core;

/// <summary>
/// Represents a metadata descriptor for a bindable, styleable property on a <see cref="BindableObject"/>.
/// </summary>
public abstract class BindableProperty
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the CLR type of the property's owner.
    /// </summary>
    public Type OwnerType { get; }

    /// <summary>
    /// Gets the CLR type of the property's value.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindableProperty"/> class.
    /// </summary>
    protected BindableProperty(string name, Type ownerType, Type valueType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(valueType);

        Name = name;
        OwnerType = ownerType;
        ValueType = valueType;
    }

    /// <summary>
    /// Gets the boxed default value for this property.
    /// </summary>
    public abstract object? GetBoxedDefaultValue();

    /// <summary>
    /// Creates a primary <see cref="BindableProperty{TValue}"/> descriptor.
    /// </summary>
    public static BindableProperty<TValue> Create<TValue>(
        string name,
        Type ownerType,
        TValue defaultValue = default!,
        Action<BindableObject, TValue, TValue>? propertyChanged = null)
    {
        return new BindableProperty<TValue>(name, ownerType, defaultValue, propertyChanged);
    }

    /// <summary>
    /// Creates an owner-typed <see cref="BindableProperty{TOwner, TValue}"/> descriptor (which also is a <see cref="BindableProperty{TValue}"/>).
    /// </summary>
    public static BindableProperty<TOwner, TValue> Create<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<TOwner, TValue, TValue>? propertyChanged = null)
        where TOwner : BindableObject
    {
        return new BindableProperty<TOwner, TValue>(name, defaultValue, propertyChanged);
    }
}

/// <summary>
/// Represents the primary strongly typed <see cref="BindableProperty"/> with value type <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="TValue">The value type stored by the property.</typeparam>
public class BindableProperty<TValue> : BindableProperty
{
    private readonly Action<BindableObject, TValue, TValue>? _propertyChanged;

    /// <summary>
    /// Gets the default value of the property when no local or bound value is set.
    /// </summary>
    public TValue DefaultValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindableProperty{TValue}"/> class.
    /// </summary>
    public BindableProperty(
        string name,
        Type ownerType,
        TValue defaultValue = default!,
        Action<BindableObject, TValue, TValue>? propertyChanged = null)
        : base(name, ownerType, typeof(TValue))
    {
        DefaultValue = defaultValue;
        _propertyChanged = propertyChanged;
    }

    /// <inheritdoc />
    public override object? GetBoxedDefaultValue()
    {
        return DefaultValue;
    }

    internal virtual void NotifyChanged(BindableObject owner, TValue oldValue, TValue newValue)
    {
        _propertyChanged?.Invoke(owner, oldValue, newValue);
    }
}

/// <summary>
/// Represents an owner-constrained <see cref="BindableProperty{TValue}"/> used when compile-time owner-type safety is needed.
/// </summary>
/// <typeparam name="TOwner">The owning <see cref="BindableObject"/> type.</typeparam>
/// <typeparam name="TValue">The value type stored by the property.</typeparam>
public sealed class BindableProperty<TOwner, TValue> : BindableProperty<TValue>
    where TOwner : BindableObject
{
    private readonly Action<TOwner, TValue, TValue>? _ownerPropertyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="BindableProperty{TOwner, TValue}"/> class.
    /// </summary>
    public BindableProperty(
        string name,
        TValue defaultValue = default!,
        Action<TOwner, TValue, TValue>? propertyChanged = null)
        : base(name, typeof(TOwner), defaultValue)
    {
        _ownerPropertyChanged = propertyChanged;
    }

    internal override void NotifyChanged(BindableObject owner, TValue oldValue, TValue newValue)
    {
        if (_ownerPropertyChanged is not null && owner is TOwner typedOwner)
        {
            _ownerPropertyChanged(typedOwner, oldValue, newValue);
        }
    }
}

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
    /// Gets a value indicating whether this property is declared by <see cref="OwnerType"/> to be set on other objects,
    /// such as a grid's row on its children.
    /// </summary>
    public bool IsAttached { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindableProperty"/> class.
    /// </summary>
    protected BindableProperty(string name, Type ownerType, Type valueType, bool isAttached = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(valueType);

        Name = name;
        OwnerType = ownerType;
        ValueType = valueType;
        IsAttached = isAttached;
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

    /// <summary>
    /// Creates an attached property, declared by <typeparamref name="TOwner"/> and settable on any <see cref="BindableObject"/>.
    /// </summary>
    /// <param name="name">The property name, without the owner's name.</param>
    /// <param name="defaultValue">The value on objects where the property is not set.</param>
    /// <param name="propertyChanged">Invoked with the object the property is set on, which need not be a <typeparamref name="TOwner"/>.</param>
    public static BindableProperty<TValue> CreateAttached<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<BindableObject, TValue, TValue>? propertyChanged = null)
    {
        return new BindableProperty<TValue>(name, typeof(TOwner), defaultValue, propertyChanged, isAttached: true);
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
        Action<BindableObject, TValue, TValue>? propertyChanged = null,
        bool isAttached = false)
        : base(name, ownerType, typeof(TValue), isAttached)
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

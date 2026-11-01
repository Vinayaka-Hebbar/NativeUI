using System.Collections.Immutable;

namespace NativeUI.Core;

/// <summary>
/// Represents a metadata descriptor for a bindable, styleable property on a <see cref="BindableObject"/>.
/// </summary>
public abstract class BindableProperty
{
    private ImmutableArray<Type> _additionalOwners = [];

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the CLR type of the property's owner.
    /// </summary>
    public Type OwnerType { get;  }

    /// <summary>
    /// Gets the CLR type of the property's value.
    /// </summary>
    public Type ValueType { get;  }

    /// <summary>
    /// Gets a value indicating whether this property is declared by <see cref="OwnerType"/> to be set on other objects,
    /// such as a grid's row on its children.
    /// </summary>
    public bool IsAttached { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindableProperty"/> class. Use the factory methods to create a property.
    /// </summary>
    private protected BindableProperty(string name, Type ownerType, Type valueType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(valueType);

        Name = name;
        OwnerType = ownerType;
        ValueType = valueType;
    }

    /// <summary>
    /// Returns whether <paramref name="ownerType"/> or one of its base types declares this property or registered with it
    /// through <see cref="BindableProperty{TValue}.AddOwner{TOwner}"/>. For an attached property this describes the declaring
    /// hierarchy, not the objects that can store a value.
    /// </summary>
    public bool HasOwner(Type ownerType)
    {
        ArgumentNullException.ThrowIfNull(ownerType);

        if (OwnerType.IsAssignableFrom(ownerType))
        {
            return true;
        }

        foreach (Type additionalOwner in _additionalOwners)
        {
            if (additionalOwner.IsAssignableFrom(ownerType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Records <paramref name="ownerType"/> as another type that exposes this property.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="ownerType"/> is already registered.</exception>
    private protected void RegisterOwner(Type ownerType)
    {
        bool added = OwnerType != ownerType && ImmutableInterlocked.Update(
            ref _additionalOwners,
            static (owners, type) => owners.Contains(type) ? owners : owners.Add(type),
            ownerType);

        if (!added)
        {
            throw new InvalidOperationException($"'{ownerType.Name}' is already an owner of '{Name}'.");
        }
    }

    /// <summary>
    /// Gets the boxed default value for this property.
    /// </summary>
    public abstract object? GetBoxedDefaultValue();

    /// <summary>
    /// Creates a primary <see cref="BindableProperty{TValue}"/> descriptor.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="ownerType"/> does not derive from <see cref="BindableObject"/>.</exception>
    public static BindableProperty<TValue> Create<TValue>(
        string name,
        Type ownerType,
        TValue defaultValue = default!,
        Action<BindableObject, TValue, TValue>? propertyChanged = null)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        if (!typeof(BindableObject).IsAssignableFrom(ownerType))
        {
            throw new ArgumentException("The owner type must derive from BindableObject.", nameof(ownerType));
        }

        return new BindableProperty<TValue>(name, ownerType, defaultValue, propertyChanged);
    }

    /// <summary>
    /// Creates an owner-typed <see cref="BindableProperty{TOwner, TValue}"/> descriptor (which also is a <see cref="BindableProperty{TValue}"/>).
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The value when the property is not set.</param>
    /// <param name="propertyChanged">Invoked when the value changes on an instance of <typeparamref name="TOwner"/>. It does not run
    /// for an object of another type, including one that registered through <see cref="BindableProperty{TValue}.AddOwner{TOwner}"/>
    /// without forwarding its storage to a <typeparamref name="TOwner"/>.</param>
    public static BindableProperty<TOwner, TValue> Create<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<TOwner, TValue, TValue>? propertyChanged = null)
        where TOwner : BindableObject
    {
        return new BindableProperty<TOwner, TValue>(name, defaultValue, propertyChanged);
    }

    /// <summary>
    /// Creates an attached property declared by <paramref name="ownerType"/>, which can be a static class, and settable on any
    /// <see cref="BindableObject"/>.
    /// </summary>
    /// <typeparam name="TValue">The value type stored by the property.</typeparam>
    /// <param name="name">The property name, without the owner's name.</param>
    /// <param name="ownerType">The declaring type.</param>
    /// <param name="defaultValue">The value on objects where the property is not set.</param>
    /// <param name="propertyChanged">Invoked with the object the property is set on.</param>
    public static BindableProperty<TValue> CreateAttached<TValue>(
        string name,
        Type ownerType,
        TValue defaultValue = default!,
        Action<BindableObject, TValue, TValue>? propertyChanged = null)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        return new BindableProperty<TValue>(name, ownerType, defaultValue, propertyChanged) { IsAttached = true };
    }

    /// <summary>
    /// Creates an attached property, declared by <typeparamref name="TOwner"/> and settable on any <see cref="BindableObject"/>.
    /// </summary>
    /// <typeparam name="TOwner">The declaring type, which need not be a <see cref="BindableObject"/>. Use the overload taking a <see cref="Type"/> for a static class.</typeparam>
    /// <typeparam name="TValue">The value type stored by the property.</typeparam>
    /// <param name="name">The property name, without the owner's name.</param>
    /// <param name="defaultValue">The value on objects where the property is not set.</param>
    /// <param name="propertyChanged">Invoked with the object the property is set on, which need not be a <typeparamref name="TOwner"/>.</param>
    public static BindableProperty<TValue> CreateAttached<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<BindableObject, TValue, TValue>? propertyChanged = null)
    {
        return new BindableProperty<TValue>(name, typeof(TOwner), defaultValue, propertyChanged) { IsAttached = true };
    }
}

/// <summary>
/// Represents the primary strongly typed <see cref="BindableProperty"/> with value type <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="TValue">The value type stored by the property.</typeparam>
public class BindableProperty<TValue> : BindableProperty
{
    private readonly Action<BindableObject, TValue, TValue>? _propertyChanged;
    private ImmutableDictionary<Type, TValue> _defaultValueOverrides = ImmutableDictionary<Type, TValue>.Empty;

    /// <summary>
    /// Gets the default value of the property when no local or bound value is set.
    /// </summary>
    public TValue DefaultValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindableProperty{TValue}"/> class.
    /// </summary>
    internal BindableProperty(
        string name,
        Type ownerType,
        TValue defaultValue = default!,
        Action<BindableObject, TValue, TValue>? propertyChanged = null)
        : base(name, ownerType, typeof(TValue))
    {
        DefaultValue = defaultValue;
        _propertyChanged = propertyChanged;
    }

    /// <summary>
    /// Registers <typeparamref name="TOwner"/> as another owner and returns this same property, so both owners share one
    /// identity. The registering type decides where the value lives through <see cref="BindableObject.GetPropertyStore"/>.
    /// Call this once per owner, from a static initializer.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="TOwner"/> is already registered.</exception>
    public BindableProperty<TValue> AddOwner<TOwner>()
        where TOwner : BindableObject
    {
        RegisterOwner(typeof(TOwner));
        return this;
    }

    /// <summary>
    /// Sets the default value of this property on <typeparamref name="TOwner"/> and its derived types.
    /// Call this once per type, from the static constructor of <typeparamref name="TOwner"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="TOwner"/> is not an owner, or already has an override.</exception>
    public void OverrideMetadata<TOwner>(TValue defaultValue)
        where TOwner : BindableObject
    {
        if (!HasOwner(typeof(TOwner)))
        {
            throw new InvalidOperationException($"'{typeof(TOwner).Name}' is not an owner of '{Name}'.");
        }

        if (!ImmutableInterlocked.TryAdd(ref _defaultValueOverrides, typeof(TOwner), defaultValue))
        {
            throw new InvalidOperationException($"'{typeof(TOwner).Name}' already overrides the default of '{Name}'.");
        }
    }

    /// <summary>
    /// Returns the default value on <paramref name="owner"/>'s type, from its closest override or else <see cref="DefaultValue"/>.
    /// The owner is the object the property is read through, not the object storing a forwarded value. So
    /// <c>button.Width</c> uses the default for <c>Button</c>, while <c>button.Style.Width</c> reports only what the style
    /// itself holds. Use the element property, or the computed style, for the effective value.
    /// </summary>
    public virtual TValue GetDefaultValue(BindableObject owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        ImmutableDictionary<Type, TValue> overrides = _defaultValueOverrides;
        if (overrides.IsEmpty)
        {
            return DefaultValue;
        }

        for (Type? type = owner.GetType(); type is not null; type = type.BaseType)
        {
            if (overrides.TryGetValue(type, out TValue? value))
            {
                return value;
            }
        }

        return DefaultValue;
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
/// Its typed callback runs only for instances of <typeparamref name="TOwner"/>, the type that stores the value.
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
    internal BindableProperty(
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

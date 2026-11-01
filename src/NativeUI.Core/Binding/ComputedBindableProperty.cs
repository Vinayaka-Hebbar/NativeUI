namespace NativeUI.Core;

/// <summary>
/// Represents a read-only bindable property whose value is computed on demand rather than stored in a property store.
/// </summary>
/// <typeparam name="TValue">The value type computed by the property.</typeparam>
public class ComputedBindableProperty<TValue> : BindableProperty<TValue>
{
    private readonly Func<BindableObject, TValue> _compute;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputedBindableProperty{TValue}"/> class.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="ownerType">The CLR type of the owning object.</param>
    /// <param name="compute">The function to compute the value for an owner instance.</param>
    public ComputedBindableProperty(
        string name,
        Type ownerType,
        Func<BindableObject, TValue> compute)
        : base(name, ownerType, default!)
    {
        ArgumentNullException.ThrowIfNull(compute);
        _compute = compute;
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
    public ComputedBindableProperty(
        string name,
        Func<TOwner, TValue> compute)
        : base(name, typeof(TOwner), owner => compute((TOwner)owner))
    {
    }
}

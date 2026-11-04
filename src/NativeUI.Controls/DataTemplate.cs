namespace NativeUI.Controls;

/// <summary>
/// Builds the element tree that presents one data item. The built root's <see cref="Core.BindableObject.DataContext"/> is
/// the item, so bindings in the tree resolve against it.
/// </summary>
public abstract class DataTemplate
{
    /// <summary>
    /// Gets the type of the items this template presents.
    /// </summary>
    public abstract Type DataType { get; }

    /// <summary>
    /// Builds a new element tree for <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The data item.</param>
    /// <exception cref="InvalidCastException"><paramref name="item"/> is not a <see cref="DataType"/>.</exception>
    public abstract UIElement Build(object? item);
}

/// <summary>
/// Builds the element tree that presents one item of type <typeparamref name="T"/> through a factory.
/// </summary>
/// <typeparam name="T">The type of the items this template presents.</typeparam>
public sealed class DataTemplate<T> : DataTemplate
{
    private readonly Func<T, UIElement> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataTemplate{T}"/> class.
    /// </summary>
    /// <param name="factory">Creates a new element tree for each item.</param>
    public DataTemplate(Func<T, UIElement> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc />
    public override Type DataType => typeof(T);

    /// <inheritdoc />
    public override UIElement Build(object? item)
    {
        UIElement root = _factory((T)item!);
        root.DataContext = item;
        return root;
    }
}

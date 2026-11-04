namespace NativeUI.Xaml;

/// <summary>
/// Names the property that receives the child elements and the text content of a XAML element of the attributed type.
/// The XAML compiler assigns a single child or the element's text to a property that holds one value, and adds each
/// child to a property that holds a collection.
/// </summary>
/// <remarks>
/// Derived types inherit the attribute. A type without the attribute adds its child elements to <c>Children</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ContentPropertyAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the content property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPropertyAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the content property.</param>
    public ContentPropertyAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}

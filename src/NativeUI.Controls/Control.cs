namespace NativeUI.Controls;

/// <summary>
/// Represents a control whose visual tree is a template of named parts that the control builds. <c>::part()</c> selectors
/// style the parts. The behavior of a control does not depend on its parts, so a derived control can replace the template
/// by overriding <see cref="BuildTemplate"/>. Do not change <see cref="View.Children"/> of a control directly.
/// </summary>
public abstract class Control : View
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Control"/> class and builds its template.
    /// </summary>
    protected Control()
    {
        BuildTemplate();
    }

    /// <summary>
    /// Builds the template of this control by adding parts that <see cref="DefinePart{TElement}"/> names to
    /// <see cref="View.Children"/>. The <see cref="Control"/> constructor calls this method once, before the constructor of
    /// a derived control runs. An override must not read state that a derived constructor initializes.
    /// </summary>
    protected virtual void BuildTemplate()
    {
    }

    /// <summary>
    /// Names <paramref name="element"/> as the template part <paramref name="name"/> of this control and returns it.
    /// The caller adds the part to this control or to another part of its template.
    /// </summary>
    /// <param name="element">The element that forms the part.</param>
    /// <param name="name">The part name that <c>::part()</c> selectors match.</param>
    /// <exception cref="InvalidOperationException"><paramref name="element"/> is already a template part.</exception>
    protected TElement DefinePart<TElement>(TElement element, string name)
        where TElement : UIElement
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        element.SetTemplatePart(this, name);
        return element;
    }

    /// <summary>
    /// Returns the template part named <paramref name="name"/>, or <see langword="null"/> when the template has no such
    /// part of type <typeparamref name="TElement"/>.
    /// </summary>
    /// <param name="name">The part name.</param>
    protected TElement? GetTemplateChild<TElement>(string name)
        where TElement : UIElement
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return FindPart<TElement>(this, name);
    }

    private TElement? FindPart<TElement>(View container, string name)
        where TElement : UIElement
    {
        foreach (UIElement child in container.Children)
        {
            if (!ReferenceEquals(child.TemplatedParent, this))
            {
                continue;
            }

            if (child is TElement part && string.Equals(child.PartName, name, StringComparison.Ordinal))
            {
                return part;
            }

            if (child is View view && FindPart<TElement>(view, name) is TElement nested)
            {
                return nested;
            }
        }

        return null;
    }
}

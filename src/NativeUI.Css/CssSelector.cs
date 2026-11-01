using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Styling;

namespace NativeUI.Css;

/// <summary>
/// Provides factory methods for building compiled <see cref="StyleSelector"/> and <see cref="CompoundSelector"/> instances.
/// </summary>
public static class CssSelector
{
    /// <summary>
    /// Creates a compound selector requiring the element to have the class <paramref name="className"/>.
    /// </summary>
    /// <param name="className">The required class name.</param>
    public static CompoundSelector Class(string className)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        return new CompoundSelector(null, [className]);
    }

    /// <summary>
    /// Creates a compound selector requiring the element to match the tag name <paramref name="elementType"/>.
    /// </summary>
    /// <param name="elementType">The required lowercase element type name.</param>
    public static CompoundSelector Type(string elementType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementType);
        return new CompoundSelector(elementType.ToLowerInvariant(), []);
    }

    /// <summary>
    /// Creates a compound selector requiring the element to be or derive from <typeparamref name="TElement"/>.
    /// </summary>
    /// <typeparam name="TElement">The CLR type required for the element.</typeparam>
    public static CompoundSelector Type<TElement>()
        where TElement : IUIElement
    {
        return new CompoundSelector(null, [], clrType: typeof(TElement));
    }

    /// <summary>
    /// Creates a compound selector requiring the element to match the attribute presence <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public static CompoundSelector Attribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CompoundSelector(null, [], ElementStates.None, attributes: [new AttributeSelector(name)]);
    }

    /// <summary>
    /// Creates a compound selector requiring the element to match the attribute condition <paramref name="name"/>=<paramref name="value"/>.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The required attribute value.</param>
    public static CompoundSelector Attribute(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return new CompoundSelector(null, [], ElementStates.None, attributes: [new AttributeSelector(name, value)]);
    }

    /// <summary>
    /// Creates a universal compound selector matching any element.
    /// </summary>
    public static CompoundSelector Universal()
    {
        return new CompoundSelector("*", []);
    }

    /// <summary>
    /// Creates a universal compound selector matching all elements (<c>*</c>).
    /// </summary>
    public static CompoundSelector All()
    {
        return Universal();
    }

    /// <summary>
    /// Combines two compound selectors into a single compound selector requiring all conditions to match.
    /// </summary>
    /// <param name="first">The first compound selector.</param>
    /// <param name="second">The second compound selector.</param>
    /// <returns>A new compound selector containing the merged conditions.</returns>
    public static CompoundSelector And(CompoundSelector first, CompoundSelector second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return first.And(second);
    }

    /// <summary>
    /// Combines multiple compound selectors into a single compound selector requiring all conditions to match.
    /// </summary>
    /// <param name="selectors">The compound selectors to combine.</param>
    /// <returns>A new compound selector containing the merged conditions.</returns>
    public static CompoundSelector And(params CompoundSelector[] selectors)
    {
        ArgumentNullException.ThrowIfNull(selectors);
        if (selectors.Length == 0)
        {
            throw new ArgumentException("At least one selector must be provided.", nameof(selectors));
        }

        CompoundSelector current = selectors[0];
        for (int i = 1; i < selectors.Length; i++)
        {
            current = current.And(selectors[i]);
        }

        return current;
    }

    /// <summary>
    /// Creates a selector matching any of the specified <paramref name="selectors"/> with zero specificity.
    /// </summary>
    /// <param name="selectors">The inner selectors.</param>
    public static StyleSelector Where(params StyleSelector[] selectors)
    {
        ArgumentNullException.ThrowIfNull(selectors);
        return new StyleSelector(selectors.ToImmutableArray());
    }

    /// <summary>
    /// Creates a selector matching any of the specified <paramref name="selectors"/> with zero specificity.
    /// </summary>
    /// <param name="selectors">The inner compound selectors.</param>
    public static StyleSelector Where(params CompoundSelector[] selectors)
    {
        ArgumentNullException.ThrowIfNull(selectors);
        return new StyleSelector(selectors.Select(static c => (StyleSelector)c).ToImmutableArray());
    }

    /// <summary>
    /// Creates a compound selector requiring the element to have the specified <paramref name="states"/>.
    /// </summary>
    /// <param name="states">The required element states.</param>
    public static CompoundSelector State(ElementStates states)
    {
        return new CompoundSelector(null, [], states);
    }

    /// <summary>
    /// Creates a selector representing the <c>:root</c> pseudo-class.
    /// </summary>
    public static RootClassSelector Root()
    {
        return new RootClassSelector();
    }

    /// <summary>
    /// Creates a complete selector matching <paramref name="subject"/> with a single <paramref name="ancestor"/>.
    /// </summary>
    /// <param name="ancestor">The compound part required on an ancestor.</param>
    /// <param name="subject">The compound part the matched element must satisfy.</param>
    public static StyleSelector Descendant(CompoundSelector ancestor, CompoundSelector subject)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(subject);
        return new StyleSelector(subject, [ancestor]);
    }

    /// <summary>
    /// Creates a complete selector matching <paramref name="subject"/> with multiple <paramref name="ancestors"/>, outermost first.
    /// </summary>
    /// <param name="ancestors">The compound parts required on ancestors, outermost first.</param>
    /// <param name="subject">The compound part the matched element must satisfy.</param>
    public static StyleSelector Descendant(ImmutableArray<CompoundSelector> ancestors, CompoundSelector subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return new StyleSelector(subject, ancestors);
    }
}

using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Styling;

namespace NativeUI.Css;

/// <summary>
/// Identifies the part of an element styled by a <see cref="CssRule"/>.
/// </summary>
public enum CssPseudoElement
{
    /// <summary>
    /// The rule styles the element itself.
    /// </summary>
    None,

    /// <summary>
    /// The rule styles selected text through <c>::selection</c>.
    /// </summary>
    Selection
}

/// <summary>
/// Specifies the color scheme media query condition attached to a <see cref="CssRule"/>.
/// </summary>
public enum CssColorSchemeCondition
{
    /// <summary>
    /// Applies regardless of the active system/application color scheme.
    /// </summary>
    Any,

    /// <summary>
    /// Applies only when <c>@media (prefers-color-scheme: light)</c> is active.
    /// </summary>
    Light,

    /// <summary>
    /// Applies only when <c>@media (prefers-color-scheme: dark)</c> is active.
    /// </summary>
    Dark
}

/// <summary>
/// Represents a compiled CSS style rule with a selector, specificity, optional media query condition,
/// and property declarations.
/// </summary>
public sealed class CssRule
{
    /// <summary>
    /// Gets the normalized selector string (for example, <c>":root"</c>, <c>"text"</c>, <c>".title"</c>, or <c>"text.title"</c>).
    /// </summary>
    public string Selector { get; }

    /// <summary>
    /// Gets the compiled selector that determines which elements this rule matches.
    /// </summary>
    public StyleSelector Target { get; }

    /// <summary>
    /// Gets a value indicating whether this rule targets the <c>:root</c> scope.
    /// Its custom properties are global variables, and its other declarations apply to the root element.
    /// </summary>
    public bool IsRoot { get; }

    /// <summary>
    /// Gets the color scheme media condition for this rule.
    /// </summary>
    public CssColorSchemeCondition ColorScheme { get; }

    /// <summary>
    /// Gets the specificity used to order matching rules. <c>:root</c> counts as a pseudo-class and a pseudo-element as a type.
    /// </summary>
    public SelectorSpecificity Specificity =>
        Target.Specificity + new SelectorSpecificity(
            IsRoot ? 1 : 0,
            PseudoElement != CssPseudoElement.None ? 1 : 0);

    /// <summary>
    /// Gets the compiled property and custom-variable declarations inside this rule block.
    /// </summary>
    public ImmutableDictionary<string, CssValue> Declarations { get; }

    /// <summary>
    /// Gets the part of the matched element styled by this rule.
    /// </summary>
    public CssPseudoElement PseudoElement { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CssRule"/> class.
    /// </summary>
    public CssRule(
        string selector,
        string? elementType,
        ImmutableArray<string> classes,
        bool isRoot,
        IReadOnlyDictionary<string, CssValue> declarations,
        CssColorSchemeCondition colorScheme = CssColorSchemeCondition.Any,
        ImmutableArray<CompoundSelector> ancestors = default,
        ElementStates requiredStates = ElementStates.None,
        CssPseudoElement pseudoElement = CssPseudoElement.None,
        ImmutableArray<AttributeSelector> attributes = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(declarations);

        Selector = selector;
        Target = new StyleSelector(
            new CompoundSelector(elementType, classes, requiredStates, attributes),
            ancestors);
        IsRoot = isRoot;
        Declarations = declarations.ToImmutableDictionary(StringComparer.Ordinal);
        ColorScheme = colorScheme;
        PseudoElement = pseudoElement;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CssRule"/> class with a pre-compiled target selector.
    /// </summary>
    public CssRule(
        string selector,
        StyleSelector target,
        bool isRoot,
        IReadOnlyDictionary<string, CssValue> declarations,
        CssColorSchemeCondition colorScheme = CssColorSchemeCondition.Any,
        CssPseudoElement pseudoElement = CssPseudoElement.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(declarations);

        Selector = selector;
        Target = target;
        IsRoot = isRoot;
        Declarations = declarations.ToImmutableDictionary(StringComparer.Ordinal);
        ColorScheme = colorScheme;
        PseudoElement = pseudoElement;
    }

    /// <summary>
    /// Determines whether this rule's selector matches <paramref name="element"/> and its ancestors under <paramref name="activeScheme"/>.
    /// </summary>
    public bool Matches(IUIElement element, CssColorSchemeCondition activeScheme = CssColorSchemeCondition.Light)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (ColorScheme != CssColorSchemeCondition.Any && ColorScheme != activeScheme)
        {
            return false;
        }

        if (IsRoot && element.Parent is not null)
        {
            return false;
        }

        return Target.Matches(element);
    }
}

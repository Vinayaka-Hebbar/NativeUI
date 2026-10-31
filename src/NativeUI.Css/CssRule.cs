using System.Collections.Immutable;
using NativeUI.Core;

namespace NativeUI.Css;

/// <summary>
/// Represents one compound part of a compiled selector, such as <c>text.title:hover</c>.
/// </summary>
/// <param name="ElementType">The required lowercase element type, or <see langword="null"/> if unconstrained.</param>
/// <param name="Classes">The required class names.</param>
/// <param name="RequiredStates">The states the element must have.</param>
public sealed record CssCompoundSelector(
    string? ElementType,
    ImmutableArray<string> Classes,
    ElementStates RequiredStates = ElementStates.None)
{
    /// <summary>
    /// Determines whether <paramref name="element"/> satisfies this compound part.
    /// </summary>
    public bool Matches(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if ((element.States & RequiredStates) != RequiredStates)
        {
            return false;
        }

        if (ElementType is not null &&
            !string.Equals(ElementType, element.TagName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        IReadOnlyList<string> elementClasses = element.Classes;
        for (int i = 0; i < Classes.Length; i++)
        {
            bool found = false;
            for (int j = 0; j < elementClasses.Count; j++)
            {
                if (string.Equals(Classes[i], elementClasses[j], StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}

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
    /// Gets the compound part that the matched element itself must satisfy.
    /// </summary>
    public CssCompoundSelector Subject { get; }

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
    /// Gets the CSS specificity score used to order matching rules.
    /// </summary>
    public int Specificity { get; }

    /// <summary>
    /// Gets the compiled property and custom-variable declarations inside this rule block.
    /// </summary>
    public IReadOnlyDictionary<string, CssValue> Declarations { get; }

    /// <summary>
    /// Gets the compound parts that must match ancestors of the element, outermost first.
    /// </summary>
    public ImmutableArray<CssCompoundSelector> Ancestors { get; }

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
        int specificity,
        IReadOnlyDictionary<string, CssValue> declarations,
        CssColorSchemeCondition colorScheme = CssColorSchemeCondition.Any,
        ImmutableArray<CssCompoundSelector> ancestors = default,
        ElementStates requiredStates = ElementStates.None,
        CssPseudoElement pseudoElement = CssPseudoElement.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(declarations);

        Selector = selector;
        Subject = new CssCompoundSelector(elementType, classes.IsDefault ? [] : classes, requiredStates);
        IsRoot = isRoot;
        Specificity = specificity;
        Declarations = declarations;
        ColorScheme = colorScheme;
        Ancestors = ancestors.IsDefault ? [] : ancestors;
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

        if (!Subject.Matches(element))
        {
            return false;
        }

        IUIElement? current = element.Parent;
        for (int i = Ancestors.Length - 1; i >= 0; i--)
        {
            while (current is not null && !Ancestors[i].Matches(current))
            {
                current = current.Parent;
            }

            if (current is null)
            {
                return false;
            }

            current = current.Parent;
        }

        return true;
    }
}

namespace NativeUI.Css;

using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Styling;

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
    /// Gets the compiled selector that determines which elements this rule matches.
    /// </summary>
    public StyleSelector Target { get; }

    /// <summary>
    /// Gets a value indicating whether this rule targets the <c>:root</c> scope.
    /// Its custom properties are global variables, and its other declarations apply to the root element.
    /// </summary>
    public bool IsRoot => Target.IsRoot;

    /// <summary>
    /// Gets the optional pseudo-element name attached to this rule's selector.
    /// </summary>
    public string? PseudoElementName => Target.PseudoElementName;

    /// <summary>
    /// Gets the media query condition for this rule.
    /// </summary>
    public CssMedia Media { get; }

    /// <summary>
    /// Gets the specificity used to order matching rules.
    /// </summary>
    public SelectorSpecificity Specificity => Target.Specificity;

    /// <summary>
    /// Gets the compiled property and custom-variable declarations inside this rule block.
    /// </summary>
    public ImmutableDictionary<string, CssValue> Declarations { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CssRule"/> class with a compiled target selector.
    /// </summary>
    /// <param name="target">The compiled selector that determines which elements this rule matches.</param>
    /// <param name="declarations">The compiled property and custom-variable declarations.</param>
    /// <param name="media">The media query condition for this rule.</param>
    public CssRule(
        StyleSelector target,
        IReadOnlyDictionary<string, CssValue> declarations,
        CssMedia media = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(declarations);

        Target = target;
        Declarations = declarations.ToImmutableDictionary(StringComparer.Ordinal);
        Media = media;
    }

    /// <summary>
    /// Determines whether this rule's selector matches <paramref name="element"/> and its ancestors under <paramref name="activeScheme"/>.
    /// </summary>
    public bool Matches(IUIElement element, CssColorSchemeCondition activeScheme = CssColorSchemeCondition.Light)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (!Media.Matches(activeScheme))
        {
            return false;
        }

        return Target.Matches(element);
    }
}

namespace NativeUI.Css;

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
    /// Gets the required element type name (lowercase, such as <c>"text"</c> or <c>"view"</c>), or <see langword="null"/> if unconstrained.
    /// </summary>
    public string? ElementType { get; }

    /// <summary>
    /// Gets the required class names on the element for this rule to match.
    /// </summary>
    public IReadOnlyList<string> Classes { get; }

    /// <summary>
    /// Gets a value indicating whether this rule targets the <c>:root</c> scope.
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
    /// Initializes a new instance of the <see cref="CssRule"/> class.
    /// </summary>
    public CssRule(
        string selector,
        string? elementType,
        IReadOnlyList<string> classes,
        bool isRoot,
        int specificity,
        IReadOnlyDictionary<string, CssValue> declarations,
        CssColorSchemeCondition colorScheme = CssColorSchemeCondition.Any)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(declarations);

        Selector = selector;
        ElementType = elementType;
        Classes = classes;
        IsRoot = isRoot;
        Specificity = specificity;
        Declarations = declarations;
        ColorScheme = colorScheme;
    }

    /// <summary>
    /// Determines whether this rule matches an element with the given <paramref name="elementTypeName"/> and <paramref name="elementClasses"/>.
    /// </summary>
    public bool Matches(
        string elementTypeName,
        IReadOnlyList<string> elementClasses,
        CssColorSchemeCondition activeScheme = CssColorSchemeCondition.Light)
    {
        if (IsRoot)
        {
            return false;
        }

        if (ColorScheme != CssColorSchemeCondition.Any && ColorScheme != activeScheme)
        {
            return false;
        }

        if (ElementType is not null &&
            !string.Equals(ElementType, elementTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (int i = 0; i < Classes.Count; i++)
        {
            string requiredClass = Classes[i];
            bool found = false;
            for (int j = 0; j < elementClasses.Count; j++)
            {
                if (string.Equals(requiredClass, elementClasses[j], StringComparison.Ordinal))
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

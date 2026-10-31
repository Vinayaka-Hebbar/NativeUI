using System.Collections.Immutable;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Styling;

/// <summary>
/// Builds a <see cref="StyleSelector"/> from strongly typed element types, classes, states, and descendants.
/// </summary>
public sealed class SelectorBuilder
{
    private readonly List<CompoundSelector> _ancestors = [];
    private Type? _type;
    private ImmutableArray<string>.Builder _classes = ImmutableArray.CreateBuilder<string>();
    private ElementStates _states;

    internal SelectorBuilder()
    {
    }

    /// <summary>
    /// Requires the element to be or derive from <typeparamref name="TElement"/>.
    /// </summary>
    public SelectorBuilder Type<TElement>()
        where TElement : IUIElement
    {
        _type = typeof(TElement);
        return this;
    }

    /// <summary>
    /// Requires the element to have the class <paramref name="name"/>.
    /// </summary>
    public SelectorBuilder Class(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _classes.Add(name);
        return this;
    }

    /// <summary>
    /// Requires the pointer to be over the element or one of its descendants.
    /// </summary>
    public SelectorBuilder Hover()
    {
        _states |= ElementStates.Hover;
        return this;
    }

    /// <summary>
    /// Makes the selector match a descendant described by <paramref name="descendant"/> of the element described so far.
    /// </summary>
    public SelectorBuilder Descendant(Func<SelectorBuilder, SelectorBuilder> descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);

        StyleSelector selector = descendant(new SelectorBuilder()).Build();
        _ancestors.Add(CreateCompound());
        _ancestors.AddRange(selector.Ancestors);
        _type = selector.Subject.ClrType;
        _classes = selector.Subject.Classes.ToBuilder();
        _states = selector.Subject.RequiredStates;
        return this;
    }

    internal StyleSelector Build()
    {
        return new StyleSelector(CreateCompound(), [.. _ancestors]);
    }

    private CompoundSelector CreateCompound()
    {
        return new CompoundSelector(null, _classes.ToImmutable(), _states, _type);
    }
}

/// <summary>
/// Completes a <see cref="StyleRule"/> by assigning the style applied to its selector's matches.
/// </summary>
public sealed class StyleRuleBuilder
{
    private readonly StyleSelector _target;

    internal StyleRuleBuilder(StyleSelector target)
    {
        _target = target;
    }

    /// <summary>
    /// Creates the rule that applies <paramref name="style"/> to elements matching the selector.
    /// </summary>
    public StyleRule Style(IStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return new StyleRule(_target, style);
    }
}

/// <summary>
/// Represents one rule of a <see cref="StyleSheet"/>: a selector and the values applied to its matches.
/// </summary>
public sealed class StyleRule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StyleRule"/> class from a compiled selector.
    /// </summary>
    public StyleRule(StyleSelector target, IStyle style)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(style);

        Target = target;
        Style = style;
    }

    /// <summary>
    /// Gets the selector that determines which elements this rule matches.
    /// </summary>
    public StyleSelector Target { get; }

    /// <summary>
    /// Gets the values applied to matching elements. Null values leave lower-priority values in effect.
    /// </summary>
    public IStyle Style { get; }

    /// <summary>
    /// Starts a rule from a strongly typed selector.
    /// </summary>
    public static StyleRuleBuilder Selector(Func<SelectorBuilder, SelectorBuilder> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new StyleRuleBuilder(selector(new SelectorBuilder()).Build());
    }
}

/// <summary>
/// Resolves styles from rules defined in C#, without CSS. Matching rules apply by ascending specificity and then
/// rule order, followed by the element's inline style. Assign it to an element's or window's <c>StyleResolver</c>;
/// changes to <see cref="Rules"/> or to a rule's style apply the next time affected elements recompute their styles.
/// </summary>
public sealed class StyleSheet : IStyleResolver
{
    /// <summary>
    /// Gets the rules in this stylesheet.
    /// </summary>
    public IList<StyleRule> Rules { get; } = new List<StyleRule>();

    /// <inheritdoc />
    public ComputedStyle ResolveStyle(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        ComputedStyle computed = ComputedStyle.CreateInherited(element.Parent?.ComputedStyle);
        IEnumerable<StyleRule> matching = Rules
            .Where(rule => rule.Target.Matches(element))
            .OrderBy(static rule => rule.Target.Specificity);
        foreach (StyleRule rule in matching)
        {
            computed = computed.ApplyInline(rule.Style);
        }

        return computed.ApplyInline(element.Style);
    }

    /// <inheritdoc />
    public SelectionStyle ResolveSelectionStyle(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Parent?.SelectionStyle ?? SelectionStyle.Default;
    }

    /// <inheritdoc />
    public Color? ResolveColor(ThemeValue<Color> colorValue, IUIElement element)
    {
        return colorValue.HasValue ? colorValue.Value : null;
    }
}

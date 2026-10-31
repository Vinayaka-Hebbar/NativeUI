using System.Collections.Immutable;
using NativeUI.Core;

namespace NativeUI.Styling;

/// <summary>
/// Represents selector specificity as counts compared lexicographically, so no number of type selectors outranks a class.
/// This covers the supported selector subset, which has no ID selectors.
/// </summary>
/// <param name="Classes">The number of class selectors and pseudo-classes.</param>
/// <param name="Types">The number of type selectors and pseudo-elements.</param>
public readonly record struct SelectorSpecificity(int Classes, int Types) : IComparable<SelectorSpecificity>
{
    /// <summary>
    /// Combines the counts of two selector parts.
    /// </summary>
    public static SelectorSpecificity operator +(SelectorSpecificity left, SelectorSpecificity right)
    {
        return new SelectorSpecificity(left.Classes + right.Classes, left.Types + right.Types);
    }

    /// <inheritdoc />
    public int CompareTo(SelectorSpecificity other)
    {
        int classes = Classes.CompareTo(other.Classes);
        return classes != 0 ? classes : Types.CompareTo(other.Types);
    }
}

/// <summary>
/// Represents one compound part of a selector, such as an element type, its classes, and its required states.
/// </summary>
public sealed record CompoundSelector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompoundSelector"/> class.
    /// </summary>
    /// <param name="elementType">The required lowercase element type name, or <see langword="null"/> if unconstrained.</param>
    /// <param name="classes">The required class names.</param>
    /// <param name="requiredStates">The states the element must have.</param>
    /// <param name="clrType">The CLR type the element must be or derive from, or <see langword="null"/> if unconstrained.</param>
    /// <exception cref="ArgumentException"><paramref name="classes"/> is uninitialized or contains a blank name.</exception>
    public CompoundSelector(
        string? elementType,
        ImmutableArray<string> classes,
        ElementStates requiredStates = ElementStates.None,
        Type? clrType = null)
    {
        if (classes.IsDefault)
        {
            throw new ArgumentException("The class list must be initialized.", nameof(classes));
        }

        foreach (string className in classes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(className, nameof(classes));
        }

        ElementType = elementType;
        Classes = classes;
        RequiredStates = requiredStates;
        ClrType = clrType;
    }

    /// <summary>
    /// Gets the required lowercase element type name, or <see langword="null"/> if unconstrained.
    /// </summary>
    public string? ElementType { get; }

    /// <summary>
    /// Gets the required class names.
    /// </summary>
    public ImmutableArray<string> Classes { get; }

    /// <summary>
    /// Gets the states the element must have.
    /// </summary>
    public ElementStates RequiredStates { get; }

    /// <summary>
    /// Gets the CLR type the element must be or derive from, or <see langword="null"/> if unconstrained.
    /// </summary>
    public Type? ClrType { get; }

    /// <summary>
    /// Gets the specificity of this part. Each required state counts once, because states are flags.
    /// </summary>
    public SelectorSpecificity Specificity
    {
        get
        {
            bool hasType = ElementType is not null || ClrType is not null;
            int stateCount = int.PopCount((int)RequiredStates);

            return new SelectorSpecificity(Classes.Length + stateCount, hasType ? 1 : 0);
        }
    }

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

        if (ClrType is not null && !ClrType.IsInstanceOfType(element))
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
/// Represents a complete selector shared by every styling source: a subject and the parts required on its ancestors.
/// Every relationship is a descendant relationship; other combinators are rejected where selectors are compiled.
/// </summary>
public sealed record StyleSelector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StyleSelector"/> class.
    /// </summary>
    /// <param name="subject">The compound part the matched element must satisfy.</param>
    /// <param name="ancestors">The compound parts required on ancestors of the element, outermost first.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ancestors"/> is uninitialized or contains a null part.</exception>
    public StyleSelector(CompoundSelector subject, ImmutableArray<CompoundSelector> ancestors)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (ancestors.IsDefault || ancestors.Contains(null!))
        {
            throw new ArgumentException("The ancestor list must be initialized and contain no null parts.", nameof(ancestors));
        }

        Subject = subject;
        Ancestors = ancestors;
    }

    /// <summary>
    /// Gets the compound part the matched element must satisfy.
    /// </summary>
    public CompoundSelector Subject { get; }

    /// <summary>
    /// Gets the compound parts required on ancestors of the element, outermost first.
    /// </summary>
    public ImmutableArray<CompoundSelector> Ancestors { get; }

    /// <summary>
    /// Gets the specificity of the complete selector.
    /// </summary>
    public SelectorSpecificity Specificity
    {
        get
        {
            SelectorSpecificity specificity = Subject.Specificity;
            foreach (CompoundSelector ancestor in Ancestors)
            {
                specificity += ancestor.Specificity;
            }

            return specificity;
        }
    }

    /// <summary>
    /// Determines whether this selector matches <paramref name="element"/> and its ancestors.
    /// </summary>
    public bool Matches(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

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

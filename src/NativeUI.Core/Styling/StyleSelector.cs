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
/// Represents an attribute match requirement in a CSS selector, such as <c>[data-platform="macos"]</c>.
/// </summary>
/// <param name="Name">The attribute name.</param>
/// <param name="Value">The required attribute value.</param>
public readonly record struct AttributeSelector(string Name, string Value)
{
    /// <summary>
    /// Gets a value indicating whether this attribute specifies a platform environment condition.
    /// </summary>
    public bool IsPlatformAttribute =>
        string.Equals(Name, "data-platform", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether this platform attribute matches the currently running platform.
    /// </summary>
    public bool MatchesPlatform() =>
        IsPlatformAttribute && Application.Current.IsPlatform(Value);
}

/// <summary>
/// Represents one compound part of a selector, such as an element type, its classes, required states, attributes, and optional CLR type.
/// </summary>
public sealed record CompoundSelector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompoundSelector"/> class.
    /// </summary>
    /// <param name="elementType">The required lowercase element type name, or <see langword="null"/> if unconstrained.</param>
    /// <param name="classes">The required class names.</param>
    /// <param name="requiredStates">The states the element must have.</param>
    /// <param name="attributes">The attribute conditions the element or scope must satisfy.</param>
    /// <param name="clrType">The CLR type the element must be or derive from, or <see langword="null"/> if unconstrained.</param>
    /// <exception cref="ArgumentException"><paramref name="classes"/> is uninitialized or contains a blank name.</exception>
    public CompoundSelector(
        string? elementType,
        ImmutableArray<string> classes,
        ElementStates requiredStates = ElementStates.None,
        ImmutableArray<AttributeSelector> attributes = default,
        Type? clrType = null)
    {
        ElementType = elementType;
        Classes = classes;
        RequiredStates = requiredStates;
        Attributes = attributes;
        ClrType = clrType;

        foreach (string className in Classes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(className, nameof(classes));
        }
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
    /// Gets the attribute selectors required by this compound part.
    /// </summary>
    public ImmutableArray<AttributeSelector> Attributes { get; }

    /// <summary>
    /// Gets the CLR type the element must be or derive from, or <see langword="null"/> if unconstrained.
    /// </summary>
    public Type? ClrType { get; }

    /// <summary>
    /// Gets the required platform name from any platform attribute, or <see langword="null"/> if unconstrained.
    /// </summary>
    public string? Platform
    {
        get
        {
            if (Attributes.IsDefault)
            {
                return null;
            }

            for (int i = 0; i < Attributes.Length; i++)
            {
                if (Attributes[i].IsPlatformAttribute)
                {
                    return Attributes[i].Value;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this compound selector specifies only platform environment attributes.
    /// </summary>
    public bool IsEnvironmentPlatformSelector =>
        ElementType is null &&
        Classes.IsDefaultOrEmpty &&
        RequiredStates == ElementStates.None &&
        ClrType is null &&
        !Attributes.IsDefaultOrEmpty &&
        Attributes.All(static a => a.IsPlatformAttribute);

    /// <summary>
    /// Determines whether all platform attributes in this selector match the currently running platform.
    /// </summary>
    public bool MatchesPlatform() =>
        !Attributes.IsDefaultOrEmpty &&
        Attributes.All(static a => a.MatchesPlatform());

    /// <summary>
    /// Gets the specificity of this part. Each required state and attribute counts once.
    /// </summary>
    public SelectorSpecificity Specificity
    {
        get
        {
            bool hasType = ElementType is not null || ClrType is not null;
            int stateCount = int.PopCount((int)RequiredStates);
            int classAndAttrCount = (Classes.IsDefault ? 0 : Classes.Length) + (Attributes.IsDefault ? 0 : Attributes.Length);

            return new SelectorSpecificity(classAndAttrCount + stateCount, hasType ? 1 : 0);
        }
    }

    /// <summary>
    /// Determines whether <paramref name="element"/> satisfies this compound part.
    /// </summary>
    public bool Matches(IUIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (ClrType is not null && !ClrType.IsAssignableFrom(element.GetType()))
        {
            return false;
        }

        if ((element.States & RequiredStates) != RequiredStates)
        {
            return false;
        }

        if (ElementType is not null &&
            !string.Equals(ElementType, element.TagName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Classes.IsDefault)
        {
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
        }

        if (!Attributes.IsDefault)
        {
            for (int i = 0; i < Attributes.Length; i++)
            {
                AttributeSelector attr = Attributes[i];
                if (attr.IsPlatformAttribute)
                {
                    if (element.Parent is not null || !Application.Current.IsPlatform(attr.Value))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
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
    /// <param name="platform">The target platform name, or <see langword="null"/> if unconstrained.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is null.</exception>
    public StyleSelector(CompoundSelector subject, ImmutableArray<CompoundSelector> ancestors = default, string? platform = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        Subject = subject;
        Ancestors = ancestors;
        Platform = string.IsNullOrWhiteSpace(platform) ? subject.Platform : platform.Trim().ToLowerInvariant();
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
    /// Gets the target platform name, or <see langword="null"/> if unconstrained.
    /// </summary>
    public string? Platform { get; }

    /// <summary>
    /// Gets the specificity of the complete selector.
    /// </summary>
    public SelectorSpecificity Specificity
    {
        get
        {
            SelectorSpecificity specificity = Subject.Specificity;
            if (!Ancestors.IsDefault)
            {
                foreach (CompoundSelector ancestor in Ancestors)
                {
                    specificity += ancestor.Specificity;
                }
            }

            return specificity;
        }
    }

    /// <summary>
    /// Determines whether this selector matches <paramref name="element"/> and its ancestors.
    /// </summary>
    /// <param name="element">The candidate element to match against the subject.</param>
    /// <param name="container">An optional ancestor boundary beyond which ancestor matching does not traverse.</param>
    public bool Matches(IUIElement element, IUIElement? container = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (Platform is not null && !Application.Current.IsPlatform(Platform))
        {
            return false;
        }

        if (!Subject.Matches(element))
        {
            return false;
        }

        if (Ancestors.IsDefaultOrEmpty)
        {
            return true;
        }

        if (container is not null && ReferenceEquals(element, container))
        {
            return false;
        }

        IUIElement? current = element.Parent;
        for (int i = Ancestors.Length - 1; i >= 0; i--)
        {
            CompoundSelector ancestor = Ancestors[i];
            while (current is not null && !ancestor.Matches(current))
            {
                if (ReferenceEquals(current, container))
                {
                    current = null;
                    break;
                }

                current = current.Parent;
            }

            if (current is null)
            {
                if (i == 0 && ancestor.IsEnvironmentPlatformSelector && ancestor.MatchesPlatform())
                {
                    return true;
                }

                return false;
            }

            if (ReferenceEquals(current, container))
            {
                if (i > 0)
                {
                    return false;
                }

                break;
            }

            current = current.Parent;
        }

        return true;
    }

    /// <summary>
    /// Parses a CSS selector string (such as <c>"text.title:hover"</c> or <c>"view.card text.title"</c>) into a <see cref="StyleSelector"/>.
    /// </summary>
    /// <param name="selectorText">The space-separated selector string.</param>
    public static StyleSelector Parse(string selectorText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectorText);

        string[] parts = selectorText.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new ArgumentException("The selector text cannot be empty.", nameof(selectorText));
        }

        List<CompoundSelector> compounds = [];
        for (int i = 0; i < parts.Length; i++)
        {
            CompoundSelector compound = ParseCompound(parts[i]);
            compounds.Add(compound);
        }

        if (compounds.Count == 0)
        {
            compounds.Add(new CompoundSelector(null, []));
        }

        CompoundSelector subject = compounds[^1];
        ImmutableArray<CompoundSelector> ancestors = compounds.Count > 1
            ? [.. compounds.GetRange(0, compounds.Count - 1)]
            : ImmutableArray<CompoundSelector>.Empty;

        return new StyleSelector(subject, ancestors);
    }

    private static CompoundSelector ParseCompound(string text)
    {
        string? elementType = null;
        List<string> classes = [];
        ElementStates states = ElementStates.None;
        List<AttributeSelector> attributes = [];

        int index = 0;
        int length = text.Length;

        if (char.IsLetter(text[0]))
        {
            int start = index;
            while (index < length && text[index] != '.' && text[index] != ':' && text[index] != '[')
            {
                index++;
            }

            elementType = text[start..index].ToLowerInvariant();
        }

        while (index < length)
        {
            if (text[index] == '.')
            {
                index++;
                int start = index;
                while (index < length && text[index] != '.' && text[index] != ':' && text[index] != '[')
                {
                    index++;
                }

                if (index > start)
                {
                    classes.Add(text[start..index]);
                }
            }
            else if (text[index] == ':')
            {
                index++;
                int start = index;
                while (index < length && text[index] != '.' && text[index] != ':' && text[index] != '[')
                {
                    index++;
                }

                if (index > start)
                {
                    string pseudo = text[start..index];
                    if (string.Equals(pseudo, "hover", StringComparison.OrdinalIgnoreCase))
                    {
                        states |= ElementStates.Hover;
                    }
                    else if (string.Equals(pseudo, "active", StringComparison.OrdinalIgnoreCase))
                    {
                        states |= ElementStates.Pressed;
                    }
                    else if (string.Equals(pseudo, "disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        states |= ElementStates.Disabled;
                    }
                    else if (string.Equals(pseudo, "focus", StringComparison.OrdinalIgnoreCase))
                    {
                        states |= ElementStates.Focus;
                    }
                }
            }
            else if (text[index] == '[')
            {
                int closeIndex = text.IndexOf(']', index);
                if (closeIndex > index)
                {
                    string attributeExpr = text[(index + 1)..closeIndex];
                    index = closeIndex + 1;

                    int equalsIndex = attributeExpr.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string attrName = attributeExpr[..equalsIndex].Trim();
                        string attrVal = attributeExpr[(equalsIndex + 1)..].Trim().Trim('"', '\'');
                        attributes.Add(new AttributeSelector(attrName, attrVal));
                    }
                }
                else
                {
                    index++;
                }
            }
            else
            {
                index++;
            }
        }

        return new CompoundSelector(elementType, [.. classes], states, attributes: [.. attributes]);
    }
}

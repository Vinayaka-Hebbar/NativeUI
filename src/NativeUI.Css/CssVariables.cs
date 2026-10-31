using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Stores a scoped collection of CSS custom properties (<c>--*</c>) and resolves <see cref="CssVariableReference"/>
/// and <see cref="ThemeValue{T}"/> instances with parent-scope inheritance and cycle protection.
/// </summary>
public sealed class CssVariables
{
    private readonly Dictionary<string, CssValue> _variables = new(StringComparer.Ordinal);
    private readonly CssVariables? _parent;

    /// <summary>
    /// Occurs whenever a custom property value in this collection is added or updated.
    /// </summary>
    public event EventHandler<string>? VariableChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="CssVariables"/> class with an optional parent scope.
    /// </summary>
    /// <param name="parent">The parent <see cref="CssVariables"/> scope used for inherited lookup.</param>
    public CssVariables(CssVariables? parent = null)
    {
        _parent = parent;
    }

    /// <summary>
    /// Gets the raw <see cref="CssValue"/> declarations defined directly in this scope.
    /// </summary>
    public IReadOnlyDictionary<string, CssValue> Declarations => _variables;

    /// <summary>
    /// Sets a CSS custom property value and notifies listeners if the value changed.
    /// </summary>
    /// <param name="name">The custom property name (with or without a leading <c>--</c> prefix).</param>
    /// <param name="value">The CSS value to assign.</param>
    public void Set(string name, CssValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        string normalized = ThemeValue<object>.NormalizeKey(name);
        if (_variables.TryGetValue(normalized, out CssValue? existing) && Equals(existing, value))
        {
            return;
        }

        _variables[normalized] = value;
        VariableChanged?.Invoke(this, normalized);
    }

    /// <summary>
    /// Attempts to look up a custom property value in this scope or any ancestor scope.
    /// </summary>
    public bool TryGetValue(string name, out CssValue? value)
    {
        string normalized = ThemeValue<object>.NormalizeKey(name);
        if (_variables.TryGetValue(normalized, out value))
        {
            return true;
        }

        if (_parent is not null)
        {
            return _parent.TryGetValue(normalized, out value);
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Resolves any <see cref="CssVariableReference"/> chain into its concrete <see cref="CssValue"/>,
    /// falling back to <see cref="CssVariableReference.Fallback"/> when undefined.
    /// </summary>
    public CssValue? Resolve(CssValue? value)
    {
        return ResolveInternal(value, depth: 0);
    }

    /// <summary>
    /// Resolves a <see cref="ThemeValue{Color}"/> against this variable collection.
    /// </summary>
    public Color? ResolveColor(ThemeValue<Color> themeColor)
    {
        if (themeColor.IsThemeKey && themeColor.Key is not null)
        {
            if (TryGetValue(themeColor.Key, out CssValue? raw) &&
                Resolve(raw) is CssColorValue colorVal)
            {
                return colorVal.Color;
            }
        }

        return themeColor.HasValue ? themeColor.Value : null;
    }

    private CssValue? ResolveInternal(CssValue? value, int depth)
    {
        // Guard against cyclic variable references such as --a: var(--b); --b: var(--a);
        if (value is null || depth > 16)
        {
            return null;
        }

        if (value is not CssVariableReference variableRef)
        {
            return value;
        }

        if (TryGetValue(variableRef.Name, out CssValue? stored) && stored is not null)
        {
            CssValue? resolved = ResolveInternal(stored, depth + 1);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return ResolveInternal(variableRef.Fallback, depth + 1);
    }
}

using System.Diagnostics.CodeAnalysis;
using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Holds the custom-property declarations of one element or theme scope and the environment it inherits from.
/// Each declaration computes in the environment that declares it, so descendants inherit computed values.
/// A declaration in a dependency cycle, or one referencing an undefined property without a fallback, is invalid,
/// and a <c>var()</c> reference to it uses its fallback.
/// </summary>
/// <remarks>
/// Computed values are cached until the next <see cref="Set"/> on any environment. <see cref="Empty"/> and the
/// environments that a <see cref="CssStyleSheet"/> computes for an element are read-only. Instances are not thread-safe.
/// </remarks>
public sealed class CssVariables : ICustomProperties
{
    // Incremented by every Set, which invalidates the computed values cached by all environments.
    private static int s_revision;

    private readonly Dictionary<string, CssValue> _declarations = new(StringComparer.Ordinal);
    private readonly CssVariables? _parent;
    private readonly bool _isReadOnly;
    private Dictionary<string, ComputedVariable>? _computed;
    private int _computedRevision;

    /// <summary>
    /// Occurs when <see cref="Set"/> changes a declaration of this environment. Changes to the inherited environment
    /// do not raise it, and environments that a <see cref="CssStyleSheet"/> computes for an element never raise it.
    /// </summary>
    public event EventHandler<string>? VariableChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="CssVariables"/> class.
    /// </summary>
    /// <param name="parent">The environment whose computed values this environment inherits.</param>
    public CssVariables(CssVariables? parent = null)
    {
        _parent = parent;
    }

    /// <summary>
    /// Initializes a read-only environment that only <see cref="Declare"/> populates.
    /// </summary>
    internal CssVariables(CssVariables? parent, bool isReadOnly)
    {
        _parent = parent;
        _isReadOnly = isReadOnly;
    }

    /// <summary>
    /// Gets a read-only environment without declarations.
    /// </summary>
    public static CssVariables Empty { get; } = new(parent: null, isReadOnly: true);

    /// <summary>
    /// Gets the declarations of this environment, excluding inherited properties.
    /// </summary>
    public IReadOnlyDictionary<string, CssValue> Declarations => _declarations;

    /// <summary>
    /// Gets a value indicating whether <see cref="Set"/> is unavailable, as for <see cref="Empty"/> and the
    /// environments that a <see cref="CssStyleSheet"/> computes for an element.
    /// </summary>
    public bool IsReadOnly => _isReadOnly;

    /// <summary>
    /// Sets a declaration of this environment and raises <see cref="VariableChanged"/> when its value changes.
    /// </summary>
    /// <param name="name">
    /// The case-sensitive custom-property name. As with <see cref="ThemeValue{T}"/> keys, a name without the <c>--</c>
    /// prefix receives it.
    /// </param>
    /// <param name="value">The declared value, which can reference other custom properties.</param>
    /// <exception cref="InvalidOperationException">The environment is read-only.</exception>
    public void Set(string name, CssValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        if (_isReadOnly)
        {
            throw new InvalidOperationException("A read-only custom-property environment cannot be changed.");
        }

        string normalized = ThemeValue<object>.NormalizeKey(name);
        if (_declarations.TryGetValue(normalized, out CssValue? existing) && Equals(existing, value))
        {
            return;
        }

        _declarations[normalized] = value;
        Interlocked.Increment(ref s_revision);
        VariableChanged?.Invoke(this, normalized);
    }

    /// <summary>
    /// Gets the computed value of a custom property declared in this environment or inherited by it.
    /// </summary>
    /// <param name="name">
    /// The case-sensitive custom-property name. As with <see cref="ThemeValue{T}"/> keys, a name without the <c>--</c>
    /// prefix receives it.
    /// </param>
    /// <param name="value">When this method returns <see langword="true"/>, the computed value.</param>
    /// <returns><see langword="true"/> if the property is defined and valid; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(string name, out CssValue? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        value = GetComputedValue(ThemeValue<object>.NormalizeKey(name));
        return value is not null;
    }

    /// <summary>
    /// Substitutes the computed values of custom properties for <paramref name="value"/> and its fallbacks.
    /// </summary>
    /// <param name="value">The value to resolve, which can be a <see cref="CssVariableReference"/>.</param>
    /// <returns>The resolved value, or <see langword="null"/> if a reference and its fallbacks are all invalid.</returns>
    public CssValue? Resolve(CssValue? value)
    {
        int cycleDepth = int.MaxValue;
        return Substitute(value, -1, ref cycleDepth);
    }

    /// <summary>
    /// Resolves a theme color against this environment, using the fallback value of <paramref name="themeColor"/>
    /// when its key is undefined or does not compute to a color.
    /// </summary>
    public Color? ResolveColor(ThemeValue<Color> themeColor)
    {
        if (themeColor.Key is string key && TryGetValue(key, out Color color))
        {
            return color;
        }

        return themeColor.HasValue ? themeColor.Value : null;
    }

    /// <summary>
    /// Gets the computed value of a custom property as <typeparamref name="T"/>. A <see cref="CssValue"/> type receives
    /// the computed value itself. Colors convert to <see cref="Color"/>, numbers and pixel lengths to <see cref="double"/>,
    /// and keywords and strings to <see cref="string"/>. Relative lengths do not convert to <see cref="double"/>, because
    /// their pixel value depends on the element that uses them.
    /// </summary>
    /// <typeparam name="T">The requested value type.</typeparam>
    /// <param name="name">
    /// The case-sensitive custom-property name. As with <see cref="ThemeValue{T}"/> keys, a name without the <c>--</c>
    /// prefix receives it.
    /// </param>
    /// <param name="value">When this method returns <see langword="true"/>, the computed value.</param>
    /// <returns>
    /// <see langword="true"/> if the property is defined and its computed value converts to <typeparamref name="T"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetValue<T>(string name, [MaybeNullWhen(false)] out T value)
    {
        if (!TryGetValue(name, out CssValue? computed))
        {
            value = default;
            return false;
        }

        if (computed is T cssValue)
        {
            value = cssValue;
            return true;
        }

        if (ToValue(computed) is T converted)
        {
            value = converted;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Adds a declaration while a stylesheet builds this environment, keeping an existing declaration of the name.
    /// </summary>
    internal void Declare(string name, CssValue value)
    {
        _declarations.TryAdd(name, value);
    }

    /// <summary>
    /// Determines whether <paramref name="other"/> inherits from the same environment and has equal declarations.
    /// </summary>
    internal bool HasSameDeclarations(CssVariables other)
    {
        if (!ReferenceEquals(_parent, other._parent) || _declarations.Count != other._declarations.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, CssValue> declaration in _declarations)
        {
            if (!other._declarations.TryGetValue(declaration.Key, out CssValue? value) || !Equals(declaration.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    private static object? ToValue(CssValue? value)
    {
        return value switch
        {
            CssColorValue color => color.Color,
            CssNumberValue number => number.Value,
            CssLengthValue { Unit: CssLengthUnit.Px } length => length.Value,
            CssStringValue text => text.Value,
            _ => null
        };
    }

    private CssValue? GetComputedValue(string name)
    {
        int cycleDepth = int.MaxValue;
        return ComputeVariable(name, 0, ref cycleDepth);
    }

    /// <summary>
    /// Computes a declaration, or delegates an undeclared name to the inherited environment. A dependency cycle cannot
    /// span environments, because inherited values never reference descendant declarations.
    /// </summary>
    /// <param name="name">The normalized custom-property name.</param>
    /// <param name="depth">The depth of this declaration in the current resolution within this environment.</param>
    /// <param name="cycleDepth">
    /// Lowered to the depth of the shallowest declaration still being resolved that the computation reaches.
    /// </param>
    private CssValue? ComputeVariable(string name, int depth, ref int cycleDepth)
    {
        if (!_declarations.TryGetValue(name, out CssValue? declaration))
        {
            return _parent?.GetComputedValue(name);
        }

        if (_computed is null)
        {
            _computed = new Dictionary<string, ComputedVariable>(StringComparer.Ordinal);
        }
        else if (_computedRevision != s_revision)
        {
            _computed.Clear();
        }

        _computedRevision = s_revision;
        if (_computed.TryGetValue(name, out ComputedVariable computed))
        {
            if (computed.IsResolving)
            {
                cycleDepth = Math.Min(cycleDepth, computed.ResolvingDepth);
                return null;
            }

            return computed.Value;
        }

        _computed[name] = new ComputedVariable(null, depth);
        int reachedDepth = int.MaxValue;
        CssValue? value = Substitute(declaration, depth, ref reachedDepth);

        // Reaching this declaration or a shallower one that is still resolving places this declaration in a cycle.
        if (reachedDepth <= depth)
        {
            value = null;
        }

        if (reachedDepth < depth)
        {
            cycleDepth = Math.Min(cycleDepth, reachedDepth);
        }

        _computed[name] = new ComputedVariable(value, ComputedVariable.Resolved);
        return value;
    }

    private CssValue? Substitute(CssValue? value, int depth, ref int cycleDepth)
    {
        if (value is not CssVariableReference reference)
        {
            return value;
        }

        CssValue? referenced = ComputeVariable(reference.Name, depth + 1, ref cycleDepth);
        return referenced ?? Substitute(reference.Fallback, depth, ref cycleDepth);
    }

    /// <summary>
    /// Represents the resolution state of a declaration: still resolving at <see cref="ResolvingDepth"/>, or resolved
    /// to <see cref="Value"/>, which is <see langword="null"/> for an invalid declaration.
    /// </summary>
    private readonly record struct ComputedVariable(CssValue? Value, int ResolvingDepth)
    {
        public const int Resolved = -1;

        public bool IsResolving => ResolvingDepth != Resolved;
    }
}

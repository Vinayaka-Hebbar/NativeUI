namespace NativeUI.Media;

/// <summary>
/// Represents a CSS-agnostic value or named theme token of type <typeparamref name="T"/>.
/// Can hold either a direct literal value or a symbolic theme key (such as <c>--color-primary</c>)
/// with an optional fallback value.
/// </summary>
/// <typeparam name="T">The underlying value type (for example, <see cref="Color"/> or <see cref="double"/>).</typeparam>
public readonly record struct ThemeValue<T>
{
    /// <summary>
    /// Gets the theme key or CSS custom property name (normalized with a leading <c>--</c> prefix when present),
    /// or <see langword="null"/> if this instance represents a direct literal value.
    /// </summary>
    public string? Key { get; }

    /// <summary>
    /// Gets the direct literal value, or the fallback value when <see cref="Key"/> is specified.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets a value indicating whether this instance has a literal or fallback value.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Gets a value indicating whether this instance references a named theme variable.
    /// </summary>
    public bool IsThemeKey => Key is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeValue{T}"/> struct with a direct literal value.
    /// </summary>
    /// <param name="value">The literal value.</param>
    public ThemeValue(T value)
    {
        Key = null;
        Value = value;
        HasValue = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeValue{T}"/> struct referencing a named theme key.
    /// </summary>
    /// <param name="key">The theme key or CSS variable name (e.g., <c>"--color-primary"</c> or <c>"color-primary"</c>).</param>
    public ThemeValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = NormalizeKey(key);
        Value = default;
        HasValue = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeValue{T}"/> struct referencing a named theme key with a fallback value.
    /// </summary>
    /// <param name="key">The theme key or CSS variable name.</param>
    /// <param name="fallback">The fallback value used when the theme key is not defined in the active scope.</param>
    public ThemeValue(string key, T fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = NormalizeKey(key);
        Value = fallback;
        HasValue = true;
    }

    /// <summary>
    /// Implicitly converts a literal value of type <typeparamref name="T"/> into a <see cref="ThemeValue{T}"/>.
    /// </summary>
    public static implicit operator ThemeValue<T>(T value)
    {
        return new ThemeValue<T>(value);
    }

    /// <summary>
    /// Normalizes a theme or CSS custom property name so <c>"color-primary"</c> and <c>"--color-primary"</c> map to the same key.
    /// </summary>
    public static string NormalizeKey(string key)
    {
        string trimmed = key.Trim();
        return trimmed.StartsWith("--", StringComparison.Ordinal)
            ? trimmed
            : $"--{trimmed}";
    }
}

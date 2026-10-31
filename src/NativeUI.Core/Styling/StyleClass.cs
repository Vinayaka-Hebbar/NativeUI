using System.Collections;
using System.Runtime.CompilerServices;

namespace NativeUI.Styling;

/// <summary>
/// Represents a set of CSS class names supporting collection expressions and string conversions.
/// </summary>
[CollectionBuilder(typeof(StyleClass), nameof(StyleClass.Create))]
public sealed class StyleClass : IEnumerable<string>, IEquatable<StyleClass>
{
    private static readonly string[] s_empty = [];
    
    private static readonly char[] s_seperator = [];

    /// <summary>
    /// Gets an empty <see cref="StyleClass"/> instance.
    /// </summary>
    public static StyleClass Empty { get; } = new(s_empty);

    private readonly string[] _names;

    private StyleClass(string[] names)
    {
        _names = names;
    }

    /// <summary>
    /// Creates a new <see cref="StyleClass"/> from a span of class names.
    /// </summary>
    /// <param name="names">The class names.</param>
    public static StyleClass Create(ReadOnlySpan<string> names)
    {
        if (names.IsEmpty)
        {
            return Empty;
        }

        return new StyleClass(names.ToArray());
    }

    /// <summary>
    /// Converts a space-separated string of class names to a <see cref="StyleClass"/>.
    /// </summary>
    /// <param name="value">The space-separated class names.</param>
    public static implicit operator StyleClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Empty;
        }

        return new StyleClass(
            value.Split(s_seperator, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator()
    {
        return ((IEnumerable<string>)_names).GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Join(' ', _names);
    }

    /// <inheritdoc />
    public bool Equals(StyleClass? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || _names.Length != other._names.Length)
        {
            return false;
        }

        for (int i = 0; i < _names.Length; i++)
        {
            if (!string.Equals(_names[i], other._names[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is StyleClass other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (string name in _names)
        {
            hash.Add(name, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

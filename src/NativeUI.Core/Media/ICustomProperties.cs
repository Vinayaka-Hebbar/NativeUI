using System.Diagnostics.CodeAnalysis;

namespace NativeUI.Media;

/// <summary>
/// Represents the computed custom properties of an element, which its descendants inherit through
/// <see cref="ComputedStyle.CustomProperties"/>.
/// </summary>
public interface ICustomProperties
{
    /// <summary>
    /// Gets the computed value of a custom property as <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The requested value type, such as <see cref="Color"/>, <see cref="double"/>, or <see cref="string"/>.</typeparam>
    /// <param name="name">The custom-property name, such as <c>--accent</c>.</param>
    /// <param name="value">When this method returns <see langword="true"/>, the computed value.</param>
    /// <returns>
    /// <see langword="true"/> if the property is defined and its computed value converts to <typeparamref name="T"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool TryGetValue<T>(string name, [MaybeNullWhen(false)] out T value);
}

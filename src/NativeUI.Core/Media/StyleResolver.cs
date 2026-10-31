using NativeUI.Core;

namespace NativeUI.Media;

/// <summary>
/// Defines a style resolution provider (such as the CSS stylesheet engine) that resolves
/// base <see cref="ComputedStyle"/> values and <see cref="ThemeValue{T}"/> variables for a given
/// <see cref="Theme"/> without coupling UI controls directly to CSS.
/// </summary>
public interface IStyleResolver
{
    /// <summary>
    /// Resolves the base <see cref="ComputedStyle"/> for an element before inline C# style overrides are applied.
    /// </summary>
    /// <param name="elementTypeName">The control's style type name (for example, <c>"view"</c> or <c>"text"</c>).</param>
    /// <param name="classes">The class names applied to the element.</param>
    /// <param name="scopeName">An optional scope identifier (such as the enclosing view's type name).</param>
    /// <param name="theme">The effective <see cref="Theme"/> for the element.</param>
    ComputedStyle ResolveBaseStyle(
        string elementTypeName,
        IReadOnlyList<string> classes,
        string? scopeName = null,
        Theme theme = Theme.Light);

    /// <summary>
    /// Resolves a <see cref="ThemeValue{T}"/> color token against the active theme variables in the given scope and theme.
    /// </summary>
    Color? ResolveColor(
        ThemeValue<Color> colorValue,
        string? scopeName = null,
        Theme theme = Theme.Light);
}

/// <summary>
/// Provides the default fallback <see cref="IStyleResolver"/> when an element is not attached to a host-specific resolver.
/// </summary>
public static class StyleResolver
{
    /// <summary>
    /// Gets or sets the default <see cref="IStyleResolver"/> used when no resolver is explicitly provided on the host or visual tree.
    /// </summary>
    public static IStyleResolver? Default { get; set; }
}

using NativeUI.Core;

namespace NativeUI.Media;

/// <summary>
/// Defines a style resolution provider (such as the CSS stylesheet engine) that resolves
/// complete <see cref="ComputedStyle"/> values and <see cref="ThemeValue{T}"/> variables for a given
/// <see cref="Theme"/> without coupling UI controls directly to CSS.
/// </summary>
public interface IStyleResolver
{
    /// <summary>
    /// Resolves inherited text properties, matching CSS declarations, and inline style values into a computed snapshot.
    /// </summary>
    /// <param name="element">The element whose style is being computed.</param>
    ComputedStyle ResolveStyle(IUIElement element);

    /// <summary>
    /// Resolves a <see cref="ThemeValue{T}"/> color token against the active theme variables in the given scope and theme.
    /// </summary>
    /// <param name="colorValue">The literal color or theme token to resolve.</param>
    /// <param name="element">The element supplying the stylesheet scope and effective theme.</param>
    Color? ResolveColor(ThemeValue<Color> colorValue, IUIElement element);
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

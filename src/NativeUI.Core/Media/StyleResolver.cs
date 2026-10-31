namespace NativeUI.Media;

/// <summary>
/// Defines an optional style resolution provider (such as the CSS stylesheet engine) that resolves
/// base <see cref="ComputedStyle"/> values and <see cref="ThemeValue{T}"/> variables without coupling
/// UI controls directly to CSS.
/// </summary>
public interface IStyleResolver
{
    /// <summary>
    /// Resolves the base <see cref="ComputedStyle"/> for an element before inline C# style overrides are applied.
    /// </summary>
    /// <param name="elementTypeName">The control's style type name (for example, <c>"view"</c> or <c>"text"</c>).</param>
    /// <param name="classes">The class names applied to the element.</param>
    /// <param name="scopeName">An optional scope identifier (such as the enclosing view's type name).</param>
    ComputedStyle ResolveBaseStyle(string elementTypeName, IReadOnlyList<string> classes, string? scopeName = null);

    /// <summary>
    /// Resolves a <see cref="ThemeValue{T}"/> color token against the active theme variables in the given scope.
    /// </summary>
    Color? ResolveColor(ThemeValue<Color> colorValue, string? scopeName = null);
}

/// <summary>
/// Ambient style resolver hook used by <c>NativeUI.Controls</c> when an optional styling system (such as <c>NativeUI.Css</c>) is active.
/// </summary>
public static class StyleResolver
{
    /// <summary>
    /// Gets or sets the active <see cref="IStyleResolver"/>, or <see langword="null"/> when only C# inline styles are used.
    /// </summary>
    public static IStyleResolver? Current { get; set; }
}

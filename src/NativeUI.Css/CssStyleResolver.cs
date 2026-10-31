using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Implements <see cref="IStyleResolver"/> using compiled <see cref="CssStyleSheet"/> instances from <see cref="CssStyleRegistry"/>.
/// Evaluates <c>:root</c> variables and <c>@media (prefers-color-scheme)</c> rules for the effective <see cref="Theme"/>.
/// </summary>
public sealed class CssStyleResolver : IStyleResolver
{
    /// <summary>
    /// Gets the shared stateless <see cref="CssStyleResolver"/> instance.
    /// </summary>
    public static CssStyleResolver Instance { get; } = new();

    /// <inheritdoc />
    public ComputedStyle ResolveBaseStyle(
        string elementTypeName,
        IReadOnlyList<string> classes,
        string? scopeName = null,
        Theme theme = Theme.Light)
    {
        CssStyleSheet? sheet = ResolveSheet(scopeName);
        if (sheet is null)
        {
            return ComputedStyle.Default;
        }

        CssColorSchemeCondition scheme = ToColorSchemeCondition(theme);
        CssVariables variables = sheet.CreateRootVariables(scheme);
        return sheet.ComputeStyle(elementTypeName, classes, variables, scheme);
    }

    /// <inheritdoc />
    public Color? ResolveColor(
        ThemeValue<Color> colorValue,
        string? scopeName = null,
        Theme theme = Theme.Light)
    {
        CssStyleSheet? sheet = ResolveSheet(scopeName);
        if (sheet is not null)
        {
            CssColorSchemeCondition scheme = ToColorSchemeCondition(theme);
            CssVariables variables = sheet.CreateRootVariables(scheme);
            return variables.ResolveColor(colorValue);
        }

        return colorValue.HasValue ? colorValue.Value : null;
    }

    private static CssColorSchemeCondition ToColorSchemeCondition(Theme theme)
    {
        return theme == Theme.Dark
            ? CssColorSchemeCondition.Dark
            : CssColorSchemeCondition.Light;
    }

    private static CssStyleSheet? ResolveSheet(string? scopeName)
    {
        if (!string.IsNullOrWhiteSpace(scopeName) && CssStyleRegistry.TryGet(scopeName, out CssStyleSheet? matched))
        {
            return matched;
        }

        foreach (CssStyleSheet candidate in CssStyleRegistry.GetAll())
        {
            return candidate;
        }

        return null;
    }
}

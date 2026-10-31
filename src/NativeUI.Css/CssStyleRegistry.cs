using System.Collections.Concurrent;
using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Thread-safe registry for compiled <see cref="CssStyleSheet"/> instances and optional CSS style resolution.
/// </summary>
public static class CssStyleRegistry
{
    private static readonly ConcurrentDictionary<string, CssStyleSheet> s_sheets =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, CssVariables> s_scopedVariables =
        new(StringComparer.OrdinalIgnoreCase);

    private static CssColorSchemeCondition s_colorScheme = CssColorSchemeCondition.Light;

    /// <summary>
    /// Gets or sets the active CSS color scheme (<see cref="CssColorSchemeCondition.Light"/> or <see cref="CssColorSchemeCondition.Dark"/>).
    /// </summary>
    public static CssColorSchemeCondition ColorScheme
    {
        get => s_colorScheme;
        set
        {
            if (s_colorScheme == value)
            {
                return;
            }

            s_colorScheme = value;
            s_scopedVariables.Clear();
        }
    }

    /// <summary>
    /// Registers a compiled <see cref="CssStyleSheet"/> by its identifier and activates the CSS <see cref="IStyleResolver"/>.
    /// </summary>
    public static void Register(CssStyleSheet styleSheet)
    {
        ArgumentNullException.ThrowIfNull(styleSheet);
        s_sheets[styleSheet.Id] = styleSheet;
        s_scopedVariables.TryRemove(styleSheet.Id, out _);
        StyleResolver.Current ??= CssStyleResolver.Instance;
    }

    /// <summary>
    /// Gets all registered <see cref="CssStyleSheet"/> instances.
    /// </summary>
    public static IReadOnlyCollection<CssStyleSheet> GetAll()
    {
        return s_sheets.Values.ToArray();
    }

    /// <summary>
    /// Attempts to retrieve a registered <see cref="CssStyleSheet"/> by its identifier.
    /// </summary>
    public static bool TryGet(string id, out CssStyleSheet? styleSheet)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            styleSheet = null;
            return false;
        }

        return s_sheets.TryGetValue(id, out styleSheet);
    }

    /// <summary>
    /// Gets a registered <see cref="CssStyleSheet"/> by its identifier, or <see langword="null"/> if not found.
    /// </summary>
    public static CssStyleSheet? GetOrDefault(string id)
    {
        return TryGet(id, out CssStyleSheet? sheet) ? sheet : null;
    }

    /// <summary>
    /// Gets the active <see cref="CssVariables"/> for the specified stylesheet <paramref name="scopeId"/>.
    /// </summary>
    public static CssVariables GetVariables(string scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        return s_scopedVariables.GetOrAdd(scopeId, static id =>
        {
            CssStyleSheet? sheet = GetOrDefault(id);
            return sheet is not null ? sheet.CreateRootVariables(s_colorScheme) : new CssVariables();
        });
    }

    private sealed class CssStyleResolver : IStyleResolver
    {
        public static CssStyleResolver Instance { get; } = new();

        public ComputedStyle ResolveBaseStyle(string elementTypeName, IReadOnlyList<string> classes, string? scopeName = null)
        {
            CssStyleSheet? sheet = ResolveSheet(scopeName);
            if (sheet is null)
            {
                return ComputedStyle.Default;
            }

            CssVariables variables = GetVariables(sheet.Id);
            return sheet.ComputeStyle(elementTypeName, classes, variables, s_colorScheme);
        }

        public Color? ResolveColor(ThemeValue<Color> colorValue, string? scopeName = null)
        {
            CssStyleSheet? sheet = ResolveSheet(scopeName);
            if (sheet is not null)
            {
                CssVariables variables = GetVariables(sheet.Id);
                return variables.ResolveColor(colorValue);
            }

            return colorValue.HasValue ? colorValue.Value : null;
        }

        private static CssStyleSheet? ResolveSheet(string? scopeName)
        {
            if (!string.IsNullOrWhiteSpace(scopeName) && TryGet(scopeName, out CssStyleSheet? matched))
            {
                return matched;
            }

            foreach (CssStyleSheet candidate in s_sheets.Values)
            {
                return candidate;
            }

            return null;
        }
    }
}

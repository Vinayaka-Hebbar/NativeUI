using System.Collections.Concurrent;
using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>
/// Thread-safe registry for compiled <see cref="CssStyleSheet"/> instances.
/// </summary>
public static class CssStyleRegistry
{
    private static readonly ConcurrentDictionary<string, CssStyleSheet> s_sheets =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a compiled <see cref="CssStyleSheet"/> by its identifier and ensures <see cref="CssStyleResolver.Instance"/>
    /// is available as the default <see cref="StyleResolver.Default"/>.
    /// </summary>
    public static void Register(CssStyleSheet styleSheet)
    {
        ArgumentNullException.ThrowIfNull(styleSheet);
        s_sheets[styleSheet.Id] = styleSheet;
        StyleResolver.Default ??= CssStyleResolver.Instance;
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
}

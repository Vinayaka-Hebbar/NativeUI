using System.Collections.Concurrent;
using NativeUI.Media;
using SkiaSharp;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Caches reusable SkiaSharp resources such as <see cref="SKTypeface"/> instances keyed by font family and weight.
/// </summary>
public sealed class SkiaResourceCache : IDisposable
{
    private readonly ConcurrentDictionary<(string Family, FontWeight Weight), SKTypeface> _typefaces = new();
    private bool _disposed;

    /// <summary>
    /// Gets the shared default <see cref="SkiaResourceCache"/> instance.
    /// </summary>
    public static SkiaResourceCache Shared { get; } = new();

    /// <summary>
    /// Gets or creates a cached <see cref="SKTypeface"/> for the specified <paramref name="fontFamily"/> and <paramref name="fontWeight"/>.
    /// </summary>
    public SKTypeface GetTypeface(string? fontFamily, FontWeight fontWeight)
    {
        string normalizedFamily = string.IsNullOrWhiteSpace(fontFamily) ? string.Empty : fontFamily.Trim();
        return _typefaces.GetOrAdd((normalizedFamily, fontWeight), static key =>
        {
            SKFontStyleWeight skWeight = (SKFontStyleWeight)((int)key.Weight);
            SKFontStyle style = new(skWeight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

            if (!string.IsNullOrEmpty(key.Family))
            {
                SKTypeface? custom = SKTypeface.FromFamilyName(key.Family, style);
                if (custom is not null)
                {
                    return custom;
                }
            }

            return SKTypeface.FromFamilyName(null, style) ?? SKTypeface.Default;
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (SKTypeface typeface in _typefaces.Values)
        {
            if (!ReferenceEquals(typeface, SKTypeface.Default))
            {
                typeface.Dispose();
            }
        }

        _typefaces.Clear();
    }
}

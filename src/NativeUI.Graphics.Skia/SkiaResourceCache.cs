using System.Collections.Concurrent;
using HarfBuzzSharp;
using NativeUI.Media;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Caches reusable SkiaSharp <see cref="SKTypeface"/> and HarfBuzz <see cref="Face"/> resources keyed by font family and weight.
/// </summary>
public sealed class SkiaResourceCache : IDisposable
{
    private readonly ConcurrentDictionary<(string Family, FontWeight Weight), SKTypeface> _typefaces = new();
    private readonly ConcurrentDictionary<SKTypeface, (Blob? Blob, Face? Face)> _harfBuzzFaces = new();
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

    /// <summary>
    /// Resolves a fallback <see cref="SKTypeface"/> containing a glyph for <paramref name="codePoint"/> when the primary typeface lacks it.
    /// </summary>
    public SKTypeface ResolveTypefaceForCodePoint(SKTypeface primaryTypeface, int codePoint, FontWeight fontWeight)
    {
        ArgumentNullException.ThrowIfNull(primaryTypeface);
        if (primaryTypeface.GetGlyph(codePoint) != 0)
        {
            return primaryTypeface;
        }

        SKFontStyleWeight skWeight = (SKFontStyleWeight)((int)fontWeight);
        SKFontStyle style = new(skWeight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        SKTypeface? fallback = SKFontManager.Default.MatchCharacter(primaryTypeface.FamilyName, style, null, codePoint)
            ?? SKFontManager.Default.MatchCharacter(null, style, null, codePoint);

        if (fallback is not null && fallback.GetGlyph(codePoint) != 0)
        {
            return _typefaces.GetOrAdd((fallback.FamilyName, fontWeight), _ => fallback);
        }

        return primaryTypeface;
    }

    /// <summary>
    /// Gets or creates a cached HarfBuzz <see cref="Face"/> for the specified <paramref name="typeface"/>.
    /// </summary>
    public Face? GetHarfBuzzFace(SKTypeface typeface)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        return _harfBuzzFaces.GetOrAdd(typeface, static tf =>
        {
            using SKStreamAsset? stream = tf.OpenStream(out int ttcIndex);
            if (stream is null)
            {
                return (null, null);
            }

            Blob blob = stream.ToHarfBuzzBlob();
            Face face = new(blob, ttcIndex)
            {
                UnitsPerEm = tf.UnitsPerEm
            };
            return (blob, face);
        }).Face;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach ((Blob? blob, Face? face) in _harfBuzzFaces.Values)
        {
            face?.Dispose();
            blob?.Dispose();
        }

        _harfBuzzFaces.Clear();

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

using SkiaSharp;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Manages an offscreen or window-backed SkiaSharp <see cref="SKSurface"/>.
/// </summary>
public sealed class SkiaSurface : IDisposable
{
    private readonly SKSurface _surface;
    private bool _disposed;

    /// <summary>
    /// Gets the physical pixel width of the surface.
    /// </summary>
    public int PixelWidth { get; }

    /// <summary>
    /// Gets the physical pixel height of the surface.
    /// </summary>
    public int PixelHeight { get; }

    /// <summary>
    /// Gets the underlying <see cref="SKCanvas"/> used to draw onto this surface.
    /// </summary>
    public SKCanvas Canvas => _surface.Canvas;

    /// <summary>
    /// Initializes a new raster <see cref="SkiaSurface"/> with the specified pixel dimensions.
    /// </summary>
    public SkiaSurface(int pixelWidth, int pixelHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;

        SKImageInfo info = new(
            pixelWidth,
            pixelHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        _surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException("Failed to create the Skia surface.");
    }

    /// <summary>
    /// Captures an immutable <see cref="SKImage"/> snapshot of the current surface contents.
    /// </summary>
    public SKImage Snapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _surface.Snapshot();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _surface.Dispose();
    }
}

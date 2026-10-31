using SkiaSharp;

namespace NativeUI.Graphics.Skia;

/// <summary>
/// Manages an offscreen or window-backed SkiaSharp <see cref="SKSurface"/> and its <see cref="SkiaDrawingContext"/>.
/// </summary>
public sealed class SkiaSurface : IDisposable
{
    private readonly SKSurface _surface;
    private readonly SkiaDrawingContext _drawingContext;
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
    /// Gets the <see cref="SkiaDrawingContext"/> used to issue drawing commands onto this surface.
    /// </summary>
    public SkiaDrawingContext DrawingContext => _drawingContext;

    /// <summary>
    /// Initializes a new raster <see cref="SkiaSurface"/> with the specified pixel dimensions.
    /// </summary>
    public SkiaSurface(int pixelWidth, int pixelHeight, SkiaTextLayout? textLayout = null)
    {
        PixelWidth = Math.Max(1, pixelWidth);
        PixelHeight = Math.Max(1, pixelHeight);

        SKImageInfo info = new(PixelWidth, PixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info);
        _drawingContext = new SkiaDrawingContext(_surface.Canvas, textLayout);
    }

    /// <summary>
    /// Captures an immutable <see cref="SKImage"/> snapshot of the current surface contents.
    /// </summary>
    public SKImage Snapshot()
    {
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

using SkiaSharp;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Draws frames with Skia into the <c>CAMetalLayer</c> that backs a window's content view and presents them.
/// A frame starts with <see cref="BeginFrame"/> and ends with exactly one call to <see cref="EndFrame"/> or
/// <see cref="DiscardFrame"/>, on the main thread.
/// </summary>
internal sealed class SkiaMetalLayer
{
    private readonly MetalDevice _device;
    private readonly nint _layer;
    private int _pixelWidth;
    private int _pixelHeight;
    private double _scale;
    private nint _autoreleasePool;
    private nint _drawable;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;

    /// <summary>
    /// Initializes a presenter for the layer of <paramref name="view"/>, which must be backed by a <c>CAMetalLayer</c>
    /// on <paramref name="device"/>.
    /// </summary>
    public SkiaMetalLayer(MetalDevice device, nint view)
    {
        _device = device;
        _layer = AppKitInterop.SendMessage(view, AppKitInterop.GetSelector("layer"));
    }

    /// <summary>
    /// Acquires the next drawable at the given pixel size and returns a canvas drawing into it.
    /// </summary>
    /// <param name="pixelWidth">The drawable width in pixels.</param>
    /// <param name="pixelHeight">The drawable height in pixels.</param>
    /// <param name="scale">The number of pixels per logical point.</param>
    /// <returns>The frame canvas, or null when the layer has no drawable available, in which case the frame has ended.</returns>
    public SKCanvas? BeginFrame(int pixelWidth, int pixelHeight, double scale)
    {
        if (_surface is not null)
        {
            throw new InvalidOperationException("The previous frame has not ended.");
        }

        if (pixelWidth != _pixelWidth || pixelHeight != _pixelHeight)
        {
            _pixelWidth = pixelWidth;
            _pixelHeight = pixelHeight;
            MetalInterop.SetDrawableSize(_layer, pixelWidth, pixelHeight);
        }

        if (scale != _scale)
        {
            _scale = scale;
            MetalInterop.SetContentsScale(_layer, scale);
        }

        // The drawable and its texture are autoreleased objects that must stay alive until the frame is presented.
        _autoreleasePool = MetalInterop.AutoreleasePoolPush();
        _drawable = MetalInterop.NextDrawable(_layer);
        if (_drawable == nint.Zero)
        {
            ReleaseFrame();
            return null;
        }

        GRMtlTextureInfo texture = new(MetalInterop.GetTexture(_drawable));
        _renderTarget = new GRBackendRenderTarget(pixelWidth, pixelHeight, texture);
        _surface = SKSurface.Create(_device.Context, _renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
        if (_surface is null)
        {
            ReleaseFrame();
            return null;
        }

        return _surface.Canvas;
    }

    /// <summary>
    /// Submits the frame's drawing and presents the drawable.
    /// </summary>
    public void EndFrame()
    {
        if (_surface is null)
        {
            return;
        }

        _device.Context.Flush(submit: true, synchronous: false);
        MetalInterop.Present(_device.CommandQueue, _drawable);
        ReleaseFrame();
    }

    /// <summary>
    /// Ends the frame without presenting it, leaving the previous frame on screen.
    /// </summary>
    public void DiscardFrame()
    {
        ReleaseFrame();
    }

    private void ReleaseFrame()
    {
        _surface?.Dispose();
        _surface = null;
        _renderTarget?.Dispose();
        _renderTarget = null;
        _drawable = nint.Zero;
        if (_autoreleasePool != nint.Zero)
        {
            MetalInterop.AutoreleasePoolPop(_autoreleasePool);
            _autoreleasePool = nint.Zero;
        }
    }
}

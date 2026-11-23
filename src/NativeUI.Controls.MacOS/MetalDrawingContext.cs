using NativeUI.Graphics.Skia;
using NativeUI.Graphics3D;
using NativeUI.Layout;
using SkiaSharp;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Draws a window frame with Skia on Metal, and renders 3D scenes with the window's <see cref="MetalSceneRenderer"/>.
/// </summary>
internal sealed class MetalDrawingContext : SkiaDrawingContext, ISceneDrawingContext
{
    private readonly MetalSceneRenderer _sceneRenderer;

    /// <summary>
    /// Initializes a context drawing onto <paramref name="canvas"/>, which must draw into a Metal render target.
    /// </summary>
    public MetalDrawingContext(SKCanvas canvas, SkiaTextShaper textShaper, MetalSceneRenderer sceneRenderer)
        : base(canvas, textShaper)
    {
        _sceneRenderer = sceneRenderer;
    }

    /// <inheritdoc />
    public void DrawScene(Scene3D scene, PerspectiveCamera camera, Rect bounds)
    {
        _sceneRenderer.Render(Canvas, scene, camera, bounds);
    }
}

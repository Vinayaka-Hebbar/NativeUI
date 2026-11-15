using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Graphics;

/// <summary>
/// Specifies how a clip region combines with the current clip.
/// </summary>
public enum ClipMode
{
    /// <summary>
    /// Restricts drawing to the region.
    /// </summary>
    Intersect,

    /// <summary>
    /// Prevents drawing inside the region.
    /// </summary>
    Exclude
}

/// <summary>
/// Provides a platform- and graphics-backend-agnostic 2D drawing abstraction for rendering laid-out UI elements.
/// </summary>
public abstract class DrawingContext : IDisposable
{
    private DrawingResourceCache _resources = new();
    private bool _ownsResources = true;

    /// <summary>Gets the backend resource cache used by the current drawing pass.</summary>
    public DrawingResourceCache Resources => _resources;

    /// <summary>Uses externally owned resources retained by a visual tree renderer.</summary>
    public void UseResources(DrawingResourceCache resources)
    {
        if (ReferenceEquals(_resources, resources)) return;
        if (_ownsResources) _resources.Dispose();
        _resources = resources;
        _ownsResources = false;
    }

    /// <summary>Gets the decoder for this backend, if image presentation is supported.</summary>
    public virtual IImageDecoder? ImageDecoder => null;

    /// <summary>Draws an owned decoded image into a logical destination rectangle.</summary>
    public virtual void DrawImage(IDecodedImage image, Rect destination, double opacity = 1) => throw new NotSupportedException("Image drawing is unavailable on this backend.");

    /// <summary>Draws immutable path geometry using independent fill and stroke brushes.</summary>
    public virtual void DrawGeometry(PathGeometry geometry, Brush? fill, Brush? stroke, double strokeThickness, double opacity = 1) => throw new NotSupportedException("Vector drawing is unavailable on this backend.");

    /// <summary>Draws an immutable SVG definition in its native vector coordinate system.</summary>
    public virtual void DrawSvg(SvgNode definition, Rect viewport, double opacity = 1) => throw new NotSupportedException("SVG drawing is unavailable on this backend.");

    /// <summary>Releases resources owned directly by this context.</summary>
    public virtual void Dispose()
    {
        if (_ownsResources) _resources.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Saves the current transform and clip state onto the internal stack.
    /// Every call to <see cref="Save"/> must be balanced by a matching call to <see cref="Restore"/>.
    /// </summary>
    public abstract void Save();

    /// <summary>
    /// Saves an offscreen layer for compositing subsequent drawing commands with group <paramref name="opacity"/>.
    /// Every call to <see cref="SaveLayer"/> must be balanced by a matching call to <see cref="Restore"/>.
    /// </summary>
    /// <param name="bounds">The logical bounds of the offscreen layer.</param>
    /// <param name="opacity">The compositing group opacity in the range [0.0, 1.0].</param>
    public abstract void SaveLayer(Rect bounds, double opacity);

    /// <summary>
    /// Restores the most recently saved transform, clip, or layer state.
    /// </summary>
    public abstract void Restore();

    /// <summary>
    /// Translates the coordinate space by <paramref name="dx"/> horizontally and <paramref name="dy"/> vertically.
    /// </summary>
    /// <param name="dx">The horizontal translation delta in logical pixels.</param>
    /// <param name="dy">The vertical translation delta in logical pixels.</param>
    public abstract void Translate(double dx, double dy);

    /// <summary>
    /// Applies horizontal and vertical scaling factors to subsequent drawing operations.
    /// </summary>
    /// <param name="scaleX">The horizontal scale factor.</param>
    /// <param name="scaleY">The vertical scale factor.</param>
    public abstract void Scale(float scaleX, float scaleY);

    /// <summary>
    /// Rotates the coordinate space clockwise by <paramref name="degrees"/>.
    /// </summary>
    /// <param name="degrees">The clockwise rotation angle in degrees.</param>
    public virtual void Rotate(float degrees)
    {
    }

    /// <summary>
    /// Clears the drawing surface with the specified <paramref name="color"/>.
    /// </summary>
    /// <param name="color">The background color used to clear the surface.</param>
    public abstract void Clear(Color color);

    /// <summary>
    /// Clips subsequent drawing to <paramref name="rect"/> until the matching <see cref="Restore"/>.
    /// </summary>
    /// <param name="rect">The clipping rectangle in logical coordinates.</param>
    public abstract void ClipRect(Rect rect);

    /// <summary>
    /// Clips subsequent drawing to a rounded rectangle with corner <paramref name="radius"/> until the matching <see cref="Restore"/>.
    /// </summary>
    /// <param name="rect">The clipping rectangle in logical coordinates.</param>
    /// <param name="radius">The corner radius in logical pixels.</param>
    public abstract void ClipRoundedRectangle(Rect rect, double radius);

    /// <summary>
    /// Clips subsequent drawing to a rounded rectangle with the corner radii <paramref name="radius"/> until the matching
    /// <see cref="Restore"/>. Radii whose sum exceeds a side of <paramref name="rect"/> are scaled down together.
    /// </summary>
    /// <param name="rect">The clipping rectangle in logical coordinates.</param>
    /// <param name="radius">The corner radii in logical pixels.</param>
    /// <exception cref="NotSupportedException">The corners differ and the backend draws only uniform corners.</exception>
    public virtual void ClipRoundedRectangle(Rect rect, CornerRadius radius)
    {
        ClipRoundedRectangle(rect, GetUniformRadius(radius));
    }

    /// <summary>
    /// Clips subsequent drawing against the union of <paramref name="region"/> until the matching <see cref="Restore"/>.
    /// Intersect and Exclude clips of the same region cover each pixel exactly once.
    /// </summary>
    /// <param name="region">The list of rectangles defining the clip region.</param>
    /// <param name="mode">Whether to restrict to or exclude the region.</param>
    public abstract void ClipRegion(IReadOnlyList<Rect> region, ClipMode mode);

    /// <summary>
    /// Fills a rectangle at <paramref name="rect"/> using <paramref name="paint"/>.
    /// </summary>
    /// <param name="rect">The rectangle geometry to fill.</param>
    /// <param name="paint">The painting parameters including color and opacity.</param>
    public abstract void DrawRectangle(Rect rect, Paint paint);

    /// <summary>
    /// Fills a rounded rectangle at <paramref name="rect"/> with corner <paramref name="radius"/> using <paramref name="paint"/>.
    /// </summary>
    /// <param name="rect">The rectangle geometry to fill.</param>
    /// <param name="radius">The corner radius in logical pixels.</param>
    /// <param name="paint">The painting parameters including color and opacity.</param>
    public abstract void DrawRoundedRectangle(Rect rect, double radius, Paint paint);

    /// <summary>
    /// Fills a rounded rectangle at <paramref name="rect"/> with the corner radii <paramref name="radius"/> using
    /// <paramref name="paint"/>. Radii whose sum exceeds a side of <paramref name="rect"/> are scaled down together.
    /// </summary>
    /// <param name="rect">The rectangle geometry to fill.</param>
    /// <param name="radius">The corner radii in logical pixels.</param>
    /// <param name="paint">The painting parameters including color and opacity.</param>
    /// <exception cref="NotSupportedException">The corners differ and the backend draws only uniform corners.</exception>
    public virtual void DrawRoundedRectangle(Rect rect, CornerRadius radius, Paint paint)
    {
        DrawRoundedRectangle(rect, GetUniformRadius(radius), paint);
    }

    /// <summary>
    /// Strokes the outline of a rounded rectangle, centered on the edge of <paramref name="rect"/>.
    /// </summary>
    /// <param name="rect">The rectangle geometry to outline.</param>
    /// <param name="radius">The corner radius in logical pixels; zero draws square corners.</param>
    /// <param name="strokeWidth">The line width in logical pixels.</param>
    /// <param name="paint">The painting parameters including color and opacity.</param>
    public abstract void StrokeRoundedRectangle(Rect rect, double radius, double strokeWidth, Paint paint);

    /// <summary>
    /// Strokes the outline of a rounded rectangle with the corner radii <paramref name="radius"/>, centered on the edge of
    /// <paramref name="rect"/>. Radii whose sum exceeds a side of <paramref name="rect"/> are scaled down together.
    /// </summary>
    /// <param name="rect">The rectangle geometry to outline.</param>
    /// <param name="radius">The corner radii in logical pixels. Zero radii draw square corners.</param>
    /// <param name="strokeWidth">The line width in logical pixels.</param>
    /// <param name="paint">The painting parameters including color and opacity.</param>
    /// <exception cref="NotSupportedException">The corners differ and the backend draws only uniform corners.</exception>
    public virtual void StrokeRoundedRectangle(Rect rect, CornerRadius radius, double strokeWidth, Paint paint)
    {
        StrokeRoundedRectangle(rect, GetUniformRadius(radius), strokeWidth, paint);
    }

    /// <summary>
    /// Fills a polygon defined by the specified points.
    /// </summary>
    /// <param name="points">
    /// The polygon vertices in logical coordinates. The polygon is closed automatically.
    /// Fewer than three points produce no output.
    /// </param>
    /// <param name="paint">The paint used to fill the polygon.</param>
    public abstract void FillPolygon(IReadOnlyList<Point> points, Paint paint);

    /// <summary>
    /// Fills a rectangle with a linear gradient.
    /// </summary>
    /// <param name="rect">The rectangle geometry to fill.</param>
    /// <param name="start">The point at which the gradient starts.</param>
    /// <param name="end">The point at which the gradient ends.</param>
    /// <param name="stops">The colors along the gradient, in ascending order of offset.</param>
    public abstract void FillLinearGradient(Rect rect, Point start, Point end, IReadOnlyList<GradientStop> stops);

    /// <summary>
    /// Draws a shaped <see cref="ITextLayout"/> at <paramref name="origin"/> using <paramref name="paint"/>.
    /// </summary>
    /// <param name="layout">The shaped text layout to render.</param>
    /// <param name="origin">The top-left origin coordinates.</param>
    /// <param name="paint">The painting parameters including text color and opacity.</param>
    public abstract void DrawTextLayout(ITextLayout layout, Point origin, Paint paint);

    /// <summary>
    /// Draws text with an explicit foreground overriding formatted-run colors, as for selected text.
    /// </summary>
    /// <param name="layout">The shaped paragraph.</param>
    /// <param name="origin">The paragraph's top-left origin.</param>
    /// <param name="foreground">The foreground applied to all runs.</param>
    /// <param name="opacity">The compositing opacity.</param>
    public virtual void DrawTextLayout(ITextLayout layout, Point origin, Color foreground, double opacity)
    {
        DrawTextLayout(layout, origin, new Paint(foreground, opacity));
    }

    /// <summary>
    /// Draws one shadow of a rounded rectangle. An outer shadow is drawn only outside <paramref name="rect"/>, the border
    /// box. An inset shadow is drawn only inside <paramref name="rect"/>, the padding box. A shadow without a color is black.
    /// </summary>
    /// <param name="rect">The border box for an outer shadow, or the padding box for an inset shadow.</param>
    /// <param name="radius">The corner radii of <paramref name="rect"/>.</param>
    /// <param name="shadow">The offsets, blur, spread, color, and kind of the shadow.</param>
    /// <param name="opacity">The opacity applied to the shadow color.</param>
    /// <exception cref="NotSupportedException">The backend does not draw shadows.</exception>
    public virtual void DrawBoxShadow(Rect rect, CornerRadius radius, BoxShadow shadow, double opacity = 1)
    {
        throw new NotSupportedException("Box shadows are unavailable on this backend.");
    }

    /// <summary>
    /// Returns the radius of uniform corners for a backend that overrides only the single-radius members.
    /// </summary>
    /// <exception cref="NotSupportedException">The corners differ.</exception>
    private static double GetUniformRadius(CornerRadius radius)
    {
        if (!radius.IsUniform)
        {
            throw new NotSupportedException("Rounded rectangles with different corner radii are unavailable on this backend.");
        }

        return radius.TopLeft;
    }
}

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
public abstract class DrawingContext
{
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
    /// Strokes the outline of a rounded rectangle, centered on the edge of <paramref name="rect"/>.
    /// </summary>
    /// <param name="rect">The rectangle geometry to outline.</param>
    /// <param name="radius">The corner radius in logical pixels; zero draws square corners.</param>
    /// <param name="strokeWidth">The line width in logical pixels.</param>
    /// <param name="paint">The painting parameters including color and opacity.</param>
    public abstract void StrokeRoundedRectangle(Rect rect, double radius, double strokeWidth, Paint paint);

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
}

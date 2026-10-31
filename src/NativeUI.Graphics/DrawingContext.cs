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
    /// Clips subsequent drawing against the union of <paramref name="region"/> until the matching <see cref="Restore"/>.
    /// Intersect and Exclude clips of the same region cover each pixel exactly once.
    /// </summary>
    public abstract void ClipRegion(IReadOnlyList<Rect> region, ClipMode mode);

    /// <summary>
    /// Saves the current transform and clip state onto the internal stack.
    /// </summary>
    public abstract void Save();

    /// <summary>
    /// Restores the most recently saved transform and clip state.
    /// </summary>
    public abstract void Restore();

    /// <summary>
    /// Applies a uniform or non-uniform scale factor to subsequent drawing operations.
    /// </summary>
    public abstract void Scale(float scaleX, float scaleY);

    /// <summary>
    /// Clears the entire drawing surface with the specified <paramref name="color"/>.
    /// </summary>
    public abstract void Clear(Color color);

    /// <summary>
    /// Fills a rectangle at <paramref name="rect"/> with <paramref name="color"/> and <paramref name="opacity"/>.
    /// </summary>
    public abstract void DrawRectangle(Rect rect, Color color, double opacity = 1.0);

    /// <summary>
    /// Fills a rounded rectangle at <paramref name="rect"/> with corner <paramref name="borderRadius"/>,
    /// <paramref name="color"/>, and <paramref name="opacity"/>.
    /// </summary>
    public abstract void DrawRoundedRectangle(Rect rect, double borderRadius, Color color, double opacity = 1.0);

    /// <summary>
    /// Draws a shaped <see cref="ITextLayout"/> at <paramref name="origin"/> with <paramref name="color"/> and <paramref name="opacity"/>.
    /// </summary>
    public abstract void DrawTextLayout(
        ITextLayout textLayout,
        Point origin,
        Color color,
        double opacity = 1.0);

    /// <summary>
    /// Draws text inside <paramref name="contentRect"/> using the specified typographic parameters.
    /// </summary>
    public abstract void DrawText(
        string text,
        Rect contentRect,
        Color color,
        double fontSize,
        FontWeight fontWeight,
        string? fontFamily,
        TextAlign textAlign,
        double opacity = 1.0);
}

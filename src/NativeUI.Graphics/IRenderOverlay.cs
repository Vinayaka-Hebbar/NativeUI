using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Graphics;

/// <summary>
/// Describes a completed layout pass to an <see cref="IRenderOverlay"/>.
/// </summary>
/// <param name="Root">The root layout box, in logical coordinates.</param>
/// <param name="Viewport">The logical size of the drawing area.</param>
/// <param name="LayoutDuration">The time this pass spent computing layout.</param>
/// <param name="TextMeasurer">Creates text layouts for the overlay, which the overlay owns and disposes.</param>
public readonly record struct RenderPass(LayoutBox Root, Size Viewport, TimeSpan LayoutDuration, ITextMeasurer TextMeasurer);

/// <summary>
/// Identifies the pointer input delivered to an <see cref="IRenderOverlay"/>.
/// </summary>
public enum OverlayPointerAction
{
    /// <summary>
    /// The pointer moved.
    /// </summary>
    Moved,

    /// <summary>
    /// The primary button was pressed.
    /// </summary>
    Pressed,

    /// <summary>
    /// The primary button was released after <see cref="IRenderOverlay.HandlePointer"/> accepted its press.
    /// </summary>
    Released,

    /// <summary>
    /// The pointer left the host, or the host cancelled pointer input.
    /// </summary>
    Exited
}

/// <summary>
/// Draws on top of a rendered visual tree, in logical coordinates. An overlay has no layout. Input members default
/// to ignoring input, so an overlay that only draws implements <see cref="Draw"/> alone.
/// </summary>
public interface IRenderOverlay
{
    /// <summary>
    /// Draws the overlay. Must not invalidate the visual tree, because it runs during rendering.
    /// </summary>
    void Draw(DrawingContext context, RenderPass pass);

    /// <summary>
    /// Returns whether the overlay covers <paramref name="point"/>, which hides that point from the visual tree.
    /// </summary>
    bool ContainsPoint(Point point) => false;

    /// <summary>
    /// Handles pointer input. For <see cref="OverlayPointerAction.Pressed"/>, returning <see langword="true"/> sends
    /// following moves and the release to this overlay instead of the visual tree. Other return values are ignored.
    /// </summary>
    bool HandlePointer(OverlayPointerAction action, Point point) => false;

    /// <summary>
    /// Handles a key press before the host does, and returns whether the overlay consumed it.
    /// </summary>
    bool HandleKey(string key, PointerModifiers modifiers) => false;
}

/// <summary>
/// Represents a platform host that renders a visual tree and draws <see cref="IRenderOverlay"/> instances over it.
/// </summary>
public interface IRenderHost
{
    /// <summary>
    /// Gets the overlays drawn after the visual tree, in order.
    /// </summary>
    IList<IRenderOverlay> Overlays { get; }

    /// <summary>
    /// Requests a new frame after an overlay's appearance changes.
    /// </summary>
    void InvalidateRender();
}

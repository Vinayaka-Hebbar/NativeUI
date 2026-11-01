using NativeUI.Graphics;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Provides a surface for custom two-dimensional drawing. Size it with <see cref="Style"/> or a stylesheet like any other
/// element; without a height it has no area and does not draw. Request a repaint with <see cref="UIElement.InvalidateVisual"/>.
/// Drawing starts at (0, 0) in logical coordinates and is clipped to the canvas. The <see cref="DrawingContext"/> is valid
/// only during the draw callback, on the UI thread.
/// </summary>
public class CanvasView : View
{
    /// <summary>
    /// Occurs when the canvas is drawn, after <see cref="OnDraw"/>.
    /// </summary>
    public event EventHandler<CanvasDrawEventArgs>? Draw;

    /// <inheritdoc />
    public override string TagName => "canvas";

    /// <summary>
    /// Executes custom drawing operations, before the <see cref="Draw"/> event.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="bounds">The canvas bounds in logical coordinates.</param>
    protected virtual void OnDraw(DrawingContext context, Rect bounds)
    {
    }

    /// <summary>
    /// Executes the drawing callbacks. Platform renderers call this; applications use <see cref="UIElement.InvalidateVisual"/>.
    /// </summary>
    internal void RenderCanvas(DrawingContext context, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);

        OnDraw(context, bounds);
        Draw?.Invoke(this, new CanvasDrawEventArgs(context, bounds));
    }

    /// <inheritdoc />
    protected override IReadOnlyList<ILayoutNode> GetLayoutChildren()
    {
        return [];
    }
}

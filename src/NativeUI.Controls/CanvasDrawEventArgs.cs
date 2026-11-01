using NativeUI.Graphics;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Provides event data for <see cref="CanvasView.Draw"/>.
/// </summary>
/// <param name="context">The platform drawing context.</param>
/// <param name="bounds">The logical bounds of the canvas surface.</param>
public sealed class CanvasDrawEventArgs(
    DrawingContext context,
    Rect bounds) : EventArgs
{
    /// <summary>
    /// Gets the drawing context used to record drawing commands.
    /// </summary>
    public DrawingContext Context { get; } = context;

    /// <summary>
    /// Gets the logical bounds of the canvas surface.
    /// </summary>
    public Rect Bounds { get; } = bounds;
}

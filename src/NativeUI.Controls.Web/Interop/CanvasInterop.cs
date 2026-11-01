using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NativeUI.Controls.Web;

/// <summary>
/// Provides 2D drawing on a browser <c>canvas</c> element via <see cref="JSImportAttribute"/> and <see cref="JSObject"/>.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class CanvasInterop
{
    /// <summary>
    /// Invokes <paramref name="dispatch"/> when a canvas first has a size and whenever its size changes.
    /// </summary>
    [JSImport("observeResize", "nativeui-canvas")]
    public static partial void ObserveResize(JSObject canvas, [JSMarshalAs<JSType.Function>] Action dispatch);

    /// <summary>
    /// Stops observing a canvas's size.
    /// </summary>
    [JSImport("unobserveResize", "nativeui-canvas")]
    public static partial void UnobserveResize(JSObject canvas);

    /// <summary>
    /// Reads the <c>width</c> and <c>height</c> of a canvas's content box, in CSS pixels.
    /// </summary>
    [JSImport("getContentSize", "nativeui-canvas")]
    public static partial JSObject GetContentSize(JSObject canvas);

    /// <summary>
    /// Prepares a canvas for drawing with device pixel ratio scaling.
    /// </summary>
    [JSImport("begin", "nativeui-canvas")]
    public static partial void Begin(JSObject canvas, double width, double height);

    /// <summary>
    /// Finalizes drawing on a canvas.
    /// </summary>
    [JSImport("end", "nativeui-canvas")]
    public static partial void End(JSObject canvas);

    /// <summary>
    /// Saves the current 2D context state.
    /// </summary>
    [JSImport("save", "nativeui-canvas")]
    public static partial void Save(JSObject canvas);

    /// <summary>
    /// Restores the most recently saved 2D context state.
    /// </summary>
    [JSImport("restore", "nativeui-canvas")]
    public static partial void Restore(JSObject canvas);

    /// <summary>
    /// Translates the canvas coordinate space.
    /// </summary>
    [JSImport("translate", "nativeui-canvas")]
    public static partial void Translate(JSObject canvas, double dx, double dy);

    /// <summary>
    /// Scales the canvas coordinate space.
    /// </summary>
    [JSImport("scale", "nativeui-canvas")]
    public static partial void Scale(JSObject canvas, double sx, double sy);

    /// <summary>
    /// Clears the canvas with a solid color.
    /// </summary>
    [JSImport("clear", "nativeui-canvas")]
    public static partial void Clear(JSObject canvas, string color);

    /// <summary>
    /// Clips the canvas to a rectangular region.
    /// </summary>
    [JSImport("clipRect", "nativeui-canvas")]
    public static partial void ClipRect(JSObject canvas, double x, double y, double w, double h);

    /// <summary>
    /// Clips the canvas to a rounded rectangle.
    /// </summary>
    [JSImport("clipRoundRect", "nativeui-canvas")]
    public static partial void ClipRoundRect(JSObject canvas, double x, double y, double w, double h, double r);

    /// <summary>
    /// Draws a filled rectangle on the canvas.
    /// </summary>
    [JSImport("drawRect", "nativeui-canvas")]
    public static partial void DrawRect(JSObject canvas, double x, double y, double w, double h, string color, double opacity);

    /// <summary>
    /// Draws a filled rounded rectangle on the canvas.
    /// </summary>
    [JSImport("drawRoundRect", "nativeui-canvas")]
    public static partial void DrawRoundRect(JSObject canvas, double x, double y, double w, double h, double r, string color, double opacity);

    /// <summary>
    /// Draws text on the canvas.
    /// </summary>
    [JSImport("drawText", "nativeui-canvas")]
    public static partial void DrawText(JSObject canvas, string text, double x, double y, string color, double opacity);
}

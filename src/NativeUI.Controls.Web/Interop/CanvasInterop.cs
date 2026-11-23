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
    /// Creates a native HTML canvas.
    /// </summary>
    [JSImport("createElement", "nativeui-canvas")]
    internal static partial JSObject CreateElement();

    /// <summary>
    /// Invokes <paramref name="dispatch"/> when a canvas first has a size and whenever its size changes.
    /// </summary>
    /// <returns>The native observer, which the caller disconnects and disposes.</returns>
    [JSImport("observeResize", "nativeui-canvas")]
    public static partial JSObject ObserveResize(JSObject canvas, [JSMarshalAs<JSType.Function>] Action dispatch);

    /// <summary>
    /// Disconnects a native canvas resize observer.
    /// </summary>
    [JSImport("unobserveResize", "nativeui-canvas")]
    public static partial void UnobserveResize(JSObject observer);

    /// <summary>
    /// Reads the <c>width</c> and <c>height</c> of a canvas's content box, in CSS pixels.
    /// </summary>
    [JSImport("getContentSize", "nativeui-canvas")]
    public static partial JSObject GetContentSize(JSObject canvas);

    /// <summary>
    /// Acquires a retained 2D context for a drawing pass.
    /// </summary>
    /// <returns>The owned context handle, or null when 2D rendering is unavailable.</returns>
    [JSImport("createContext2D", "nativeui-canvas")]
    internal static partial JSObject? CreateContext2D(JSObject canvas);

    /// <summary>
    /// Creates an offscreen bitmap and acquires its retained 2D context.
    /// </summary>
    /// <param name="width">The bitmap width in pixels.</param>
    /// <param name="height">The bitmap height in pixels.</param>
    /// <returns>The owned context handle, or null when 2D rendering is unavailable.</returns>
    [JSImport("create", "nativeui-canvas-context-2d")]
    internal static partial JSObject? CreateOffscreenContext2D(int width, int height);

    /// <summary>
    /// Prepares a canvas for drawing with device pixel ratio scaling.
    /// </summary>
    [JSImport("begin", "nativeui-canvas-context-2d")]
    public static partial void Begin(JSObject context, double width, double height);

    /// <summary>
    /// Finalizes drawing on a canvas.
    /// </summary>
    [JSImport("end", "nativeui-canvas-context-2d")]
    public static partial void End(JSObject context);

    /// <summary>
    /// Saves the current 2D context state.
    /// </summary>
    [JSImport("save", "nativeui-canvas-context-2d")]
    public static partial void Save(JSObject context);

    /// <summary>
    /// Restores the most recently saved 2D context state.
    /// </summary>
    [JSImport("restore", "nativeui-canvas-context-2d")]
    public static partial void Restore(JSObject context);

    /// <summary>
    /// Translates the canvas coordinate space.
    /// </summary>
    [JSImport("translate", "nativeui-canvas-context-2d")]
    public static partial void Translate(JSObject context, double dx, double dy);

    /// <summary>
    /// Scales the canvas coordinate space.
    /// </summary>
    [JSImport("scale", "nativeui-canvas-context-2d")]
    public static partial void Scale(JSObject context, double sx, double sy);

    /// <summary>
    /// Clears the canvas with a solid color.
    /// </summary>
    [JSImport("clear", "nativeui-canvas-context-2d")]
    public static partial void Clear(JSObject context, string color);

    /// <summary>
    /// Clips the canvas to a rectangular region.
    /// </summary>
    [JSImport("clipRect", "nativeui-canvas-context-2d")]
    public static partial void ClipRect(JSObject context, double x, double y, double w, double h);

    /// <summary>
    /// Clips the canvas to a rounded rectangle.
    /// </summary>
    [JSImport("clipRoundRect", "nativeui-canvas-context-2d")]
    public static partial void ClipRoundRect(JSObject context, double x, double y, double w, double h, double r);

    /// <summary>
    /// Draws a filled rectangle on the canvas.
    /// </summary>
    [JSImport("drawRect", "nativeui-canvas-context-2d")]
    public static partial void DrawRect(JSObject context, double x, double y, double w, double h, string color, double opacity);

    /// <summary>
    /// Draws a filled rounded rectangle on the canvas.
    /// </summary>
    [JSImport("drawRoundRect", "nativeui-canvas-context-2d")]
    public static partial void DrawRoundRect(JSObject context, double x, double y, double w, double h, double r, string color, double opacity);

    /// <summary>
    /// Fills a rectangle with a linear gradient on the canvas.
    /// </summary>
    [JSImport("fillLinearGradient", "nativeui-canvas-context-2d")]
    public static partial void FillLinearGradient(
        JSObject context,
        double x,
        double y,
        double w,
        double h,
        double startX,
        double startY,
        double endX,
        double endY,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] offsets,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] colors);

    /// <summary>
    /// Fills a closed polygon on the canvas. <paramref name="coordinates"/> holds alternating x and y values.
    /// </summary>
    [JSImport("fillPolygon", "nativeui-canvas-context-2d")]
    public static partial void FillPolygon(
        JSObject context,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] coordinates,
        string color,
        double opacity);

    /// <summary>
    /// Strokes the outline of a rounded rectangle on the canvas.
    /// </summary>
    [JSImport("strokeRoundRect", "nativeui-canvas-context-2d")]
    public static partial void StrokeRoundRect(JSObject context, double x, double y, double w, double h, double r, double lineWidth, string color, double opacity);

    /// <summary>
    /// Draws text on the canvas.
    /// </summary>
    [JSImport("drawText", "nativeui-canvas-context-2d")]
    public static partial void DrawText(JSObject context, string text, double x, double y, string color, double opacity);
}

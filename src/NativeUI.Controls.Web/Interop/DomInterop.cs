using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NativeUI.Controls.Web;

/// <summary>
/// Provides minimal browser DOM interoperability via <see cref="JSImportAttribute"/> and <see cref="JSObject"/>.
/// All component lifecycle, Virtual DOM reconciliation, and observable bindings remain in C#.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class DomInterop
{
    /// <summary>
    /// Queries a DOM element in the current document by CSS selector.
    /// </summary>
    [JSImport("querySelector", "nativeui-dom")]
    public static partial JSObject? QuerySelector(string selector);

    /// <summary>
    /// Creates a new HTML DOM element with the specified <paramref name="tagName"/> (<c>"div"</c>, <c>"span"</c>, etc.).
    /// </summary>
    [JSImport("createElement", "nativeui-dom")]
    public static partial JSObject CreateElement(string tagName);

    /// <summary>
    /// Gets an attribute value, or null when the attribute is absent.
    /// </summary>
    [JSImport("getAttribute", "nativeui-dom")]
    public static partial string? GetAttribute(JSObject element, string name);

    /// <summary>
    /// Gets an inline CSS declaration, or an empty string when it is absent.
    /// </summary>
    [JSImport("getStyleProperty", "nativeui-dom")]
    public static partial string GetStyleProperty(JSObject element, string propertyName);

    /// <summary>
    /// Gets the current text content.
    /// </summary>
    [JSImport("getTextContent", "nativeui-dom")]
    public static partial string GetTextContent(JSObject element);

    /// <summary>
    /// Gets the current CSS class names.
    /// </summary>
    [JSImport("getClassName", "nativeui-dom")]
    public static partial string GetClassName(JSObject element);

    /// <summary>
    /// Sets the <c>textContent</c> property of a DOM element.
    /// </summary>
    [JSImport("setTextContent", "nativeui-dom")]
    public static partial void SetTextContent(JSObject element, string text);

    /// <summary>
    /// Sets the <c>className</c> property of a DOM element.
    /// </summary>
    [JSImport("setClassName", "nativeui-dom")]
    public static partial void SetClassName(JSObject element, string className);

    /// <summary>
    /// Sets an HTML attribute on a DOM element.
    /// </summary>
    [JSImport("setAttribute", "nativeui-dom")]
    public static partial void SetAttribute(JSObject element, string name, string value);

    /// <summary>
    /// Removes an attribute so browser defaults and CSS can apply again.
    /// </summary>
    [JSImport("removeAttribute", "nativeui-dom")]
    public static partial void RemoveAttribute(JSObject element, string name);

    /// <summary>
    /// Sets an inline CSS style property on a DOM element.
    /// </summary>
    [JSImport("setStyleProperty", "nativeui-dom")]
    public static partial void SetStyleProperty(JSObject element, string propertyName, string value);

    /// <summary>
    /// Removes an inline CSS declaration.
    /// </summary>
    [JSImport("removeStyleProperty", "nativeui-dom")]
    public static partial void RemoveStyleProperty(JSObject element, string propertyName);

    /// <summary>
    /// Appends a child DOM element to a parent DOM element.
    /// </summary>
    [JSImport("appendChild", "nativeui-dom")]
    public static partial void AppendChild(JSObject parent, JSObject child);

    /// <summary>
    /// Inserts or moves a child before a reference node, or appends when the reference is null.
    /// </summary>
    [JSImport("insertBefore", "nativeui-dom")]
    public static partial void InsertBefore(JSObject parent, JSObject child, JSObject? reference);

    /// <summary>
    /// Removes a child DOM element from a parent DOM element.
    /// </summary>
    [JSImport("removeChild", "nativeui-dom")]
    public static partial void RemoveChild(JSObject parent, JSObject child);

    /// <summary>
    /// Detaches an element from its DOM parent.
    /// </summary>
    [JSImport("removeElement", "nativeui-dom")]
    public static partial void RemoveElement(JSObject element);

    /// <summary>
    /// Associates a DOM element with the identifier reported for pointer events targeting it.
    /// </summary>
    [JSImport("setNodeId", "nativeui-dom")]
    public static partial void SetNodeId(JSObject element, int nodeId);

    /// <summary>
    /// Sets the callback that a <c>native-ui</c> element invokes with the node identifier of each scrolled descendant.
    /// The element adds its listener when connected and removes it when disconnected.
    /// </summary>
    [JSImport("setScrollDispatcher", "nativeui-dom")]
    public static partial void SetScrollDispatcher(
        JSObject root,
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> dispatch);

    /// <summary>
    /// Scrolls an element to the given offset, which the browser limits to the scrollable range.
    /// </summary>
    [JSImport("scrollTo", "nativeui-dom")]
    public static partial void ScrollTo(JSObject element, double x, double y);

    /// <summary>
    /// Reads an element's <c>scrollLeft</c>, <c>scrollTop</c>, <c>scrollWidth</c>, <c>scrollHeight</c>, <c>clientWidth</c>, and <c>clientHeight</c>.
    /// </summary>
    [JSImport("getScrollMetrics", "nativeui-dom")]
    public static partial JSObject GetScrollMetrics(JSObject element);

    /// <summary>
    /// Invokes <paramref name="dispatch"/> whenever the browser's color scheme preference changes, for the application's lifetime.
    /// </summary>
    [JSImport("observeColorScheme", "nativeui-dom")]
    public static partial void ObserveColorScheme([JSMarshalAs<JSType.Function>] Action dispatch);

    /// <summary>
    /// Gets whether the browser reports a dark color scheme preference.
    /// </summary>
    [JSImport("prefersDarkColorScheme", "nativeui-dom")]
    public static partial bool PrefersDarkColorScheme();

    /// <summary>
    /// Sets the callback that a <c>native-ui</c> element invokes with each forwarded pointer event.
    /// The element adds its delegated listeners when connected and removes them when disconnected.
    /// </summary>
    [JSImport("setPointerDispatcher", "nativeui-dom")]
    public static partial void SetPointerDispatcher(
        JSObject root,
        [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> dispatch);

    /// <summary>
    /// Sets the callback that a <c>native-ui</c> element invokes with the node identifier and value of each edited input.
    /// The element adds its listener when connected and removes it when disconnected.
    /// </summary>
    [JSImport("setInputDispatcher", "nativeui-dom")]
    public static partial void SetInputDispatcher(
        JSObject root,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.String>>] Action<int, string> dispatch);

    /// <summary>
    /// Sets the callback that a <c>native-ui</c> element invokes with the node identifier and new state of each input
    /// that gains or loses focus. The element adds its listeners when connected and removes them when disconnected.
    /// </summary>
    [JSImport("setFocusDispatcher", "nativeui-dom")]
    public static partial void SetFocusDispatcher(
        JSObject root,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Boolean>>] Action<int, bool> dispatch);

    /// <summary>
    /// Sets the callback that a <c>native-ui</c> element invokes with the node identifier of each closed dialog element.
    /// The element adds its listener when connected and removes it when disconnected.
    /// </summary>
    [JSImport("setCloseDispatcher", "nativeui-dom")]
    public static partial void SetCloseDispatcher(
        JSObject root,
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> dispatch);

    /// <summary>
    /// Sets the callback that a <c>native-ui</c> element invokes with the node identifier and new bounds of each resized descendant.
    /// </summary>
    [JSImport("setResizeDispatcher", "nativeui-dom")]
    public static partial void SetResizeDispatcher(
        JSObject root,
        [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> dispatch);

    /// <summary>
    /// Observes resize changes on the specified DOM element using <c>ResizeObserver</c>.
    /// </summary>
    [JSImport("observeResize", "nativeui-dom")]
    public static partial void ObserveResize(JSObject element);

    /// <summary>
    /// Stops observing resize changes on the specified DOM element.
    /// </summary>
    [JSImport("unobserveResize", "nativeui-dom")]
    public static partial void UnobserveResize(JSObject element);

    /// <summary>
    /// Appends <paramref name="element"/> to the <c>native-ui</c> element containing <paramref name="parent"/>,
    /// and returns false when there is none.
    /// </summary>
    [JSImport("mountPortal", "nativeui-dom")]
    public static partial bool MountPortal(JSObject parent, JSObject element);

    /// <summary>
    /// Opens a dialog element, as a modal one in the browser's top layer when <paramref name="isModal"/> is true.
    /// </summary>
    [JSImport("showDialog", "nativeui-dom")]
    public static partial void ShowDialog(JSObject element, bool isModal);

    /// <summary>
    /// Reads the bounds of an element relative to the viewport, as an object with <c>x</c>, <c>y</c>, <c>width</c>, and <c>height</c>.
    /// </summary>
    [JSImport("getBounds", "nativeui-dom")]
    public static partial JSObject GetBounds(JSObject element);

    /// <summary>
    /// Reads the viewport size, as an object with <c>width</c> and <c>height</c>.
    /// </summary>
    [JSImport("getViewportSize", "nativeui-dom")]
    public static partial JSObject GetViewportSize();

    /// <summary>
    /// Plays a <c>@keyframes</c> rule on an element and invokes <paramref name="dispatch"/> when it ends or is cancelled.
    /// Returns a handle for <see cref="CancelAnimation"/>, or null when the browser found no such rule, after invoking the callback.
    /// </summary>
    [JSImport("animate", "nativeui-dom")]
    public static partial JSObject? Animate(
        JSObject element,
        string name,
        double durationMilliseconds,
        string easing,
        double iterations,
        [JSMarshalAs<JSType.Function>] Action dispatch);

    /// <summary>
    /// Cancels an animation returned by <see cref="Animate"/>.
    /// </summary>
    [JSImport("cancelAnimation", "nativeui-dom")]
    public static partial void CancelAnimation(JSObject animation);

    /// <summary>
    /// Gets the current value of an input element.
    /// </summary>
    [JSImport("getValue", "nativeui-dom")]
    public static partial string GetValue(JSObject element);

    /// <summary>
    /// Sets the current value of an input element.
    /// </summary>
    [JSImport("setValue", "nativeui-dom")]
    public static partial void SetValue(JSObject element, string value);

    /// <summary>
    /// Moves keyboard focus to an element.
    /// </summary>
    [JSImport("focus", "nativeui-dom")]
    public static partial void Focus(JSObject element);

    /// <summary>
    /// Injects or updates a <c>&lt;style&gt;</c> tag in <c>document.head</c> for a compiled <c>CssStyleSheet</c>.
    /// </summary>
    [JSImport("injectStyleSheet", "nativeui-dom")]
    public static partial void InjectStyleSheet(string id, string cssText);

    /// <summary>
    /// Removes a stylesheet added by <see cref="InjectStyleSheet"/>.
    /// </summary>
    [JSImport("removeStyleSheet", "nativeui-dom")]
    public static partial void RemoveStyleSheet(string id);

    /// <summary>
    /// Requests an animation frame and invokes <paramref name="dispatch"/>.
    /// </summary>
    [JSImport("requestAnimationFrame", "nativeui-dom")]
    public static partial void RequestAnimationFrame([JSMarshalAs<JSType.Function>] Action dispatch);
}

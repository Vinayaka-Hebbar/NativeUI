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
    [JSImport("nativeui.querySelector", "nativeui-dom")]
    public static partial JSObject? QuerySelector(string selector);

    /// <summary>
    /// Creates a new HTML DOM element with the specified <paramref name="tagName"/> (<c>"div"</c>, <c>"span"</c>, etc.).
    /// </summary>
    [JSImport("nativeui.createElement", "nativeui-dom")]
    public static partial JSObject CreateElement(string tagName);

    /// <summary>
    /// Gets an attribute value, or null when the attribute is absent.
    /// </summary>
    [JSImport("nativeui.getAttribute", "nativeui-dom")]
    public static partial string? GetAttribute(JSObject element, string name);

    /// <summary>
    /// Gets an inline CSS declaration, or an empty string when it is absent.
    /// </summary>
    [JSImport("nativeui.getStyleProperty", "nativeui-dom")]
    public static partial string GetStyleProperty(JSObject element, string propertyName);

    /// <summary>
    /// Gets the current text content.
    /// </summary>
    [JSImport("nativeui.getTextContent", "nativeui-dom")]
    public static partial string GetTextContent(JSObject element);

    /// <summary>
    /// Gets the current CSS class names.
    /// </summary>
    [JSImport("nativeui.getClassName", "nativeui-dom")]
    public static partial string GetClassName(JSObject element);

    /// <summary>
    /// Sets the <c>textContent</c> property of a DOM element.
    /// </summary>
    [JSImport("nativeui.setTextContent", "nativeui-dom")]
    public static partial void SetTextContent(JSObject element, string text);

    /// <summary>
    /// Sets the <c>className</c> property of a DOM element.
    /// </summary>
    [JSImport("nativeui.setClassName", "nativeui-dom")]
    public static partial void SetClassName(JSObject element, string className);

    /// <summary>
    /// Sets an HTML attribute on a DOM element.
    /// </summary>
    [JSImport("nativeui.setAttribute", "nativeui-dom")]
    public static partial void SetAttribute(JSObject element, string name, string value);

    /// <summary>
    /// Removes an attribute so browser defaults and CSS can apply again.
    /// </summary>
    [JSImport("nativeui.removeAttribute", "nativeui-dom")]
    public static partial void RemoveAttribute(JSObject element, string name);

    /// <summary>
    /// Sets an inline CSS style property on a DOM element.
    /// </summary>
    [JSImport("nativeui.setStyleProperty", "nativeui-dom")]
    public static partial void SetStyleProperty(JSObject element, string propertyName, string value);

    /// <summary>
    /// Removes an inline CSS declaration.
    /// </summary>
    [JSImport("nativeui.removeStyleProperty", "nativeui-dom")]
    public static partial void RemoveStyleProperty(JSObject element, string propertyName);

    /// <summary>
    /// Appends a child DOM element to a parent DOM element.
    /// </summary>
    [JSImport("nativeui.appendChild", "nativeui-dom")]
    public static partial void AppendChild(JSObject parent, JSObject child);

    /// <summary>
    /// Inserts or moves a child before a reference node, or appends when the reference is null.
    /// </summary>
    [JSImport("nativeui.insertBefore", "nativeui-dom")]
    public static partial void InsertBefore(JSObject parent, JSObject child, JSObject? reference);

    /// <summary>
    /// Removes a child DOM element from a parent DOM element.
    /// </summary>
    [JSImport("nativeui.removeChild", "nativeui-dom")]
    public static partial void RemoveChild(JSObject parent, JSObject child);

    /// <summary>
    /// Detaches an element from its DOM parent.
    /// </summary>
    [JSImport("nativeui.removeElement", "nativeui-dom")]
    public static partial void RemoveElement(JSObject element);

    /// <summary>
    /// Associates a DOM element with the identifier reported for pointer events targeting it.
    /// </summary>
    [JSImport("nativeui.setNodeId", "nativeui-dom")]
    public static partial void SetNodeId(JSObject element, int nodeId);

    /// <summary>
    /// Sets the callback that a <c>native-ui</c> element invokes when the browser's color scheme preference changes,
    /// with <see langword="true"/> for dark. The element observes the preference only while connected.
    /// </summary>
    [JSImport("nativeui.setColorSchemeDispatcher", "nativeui-dom")]
    public static partial void SetColorSchemeDispatcher(
        JSObject root,
        [JSMarshalAs<JSType.Function<JSType.Boolean>>] Action<bool> dispatch);

    /// <summary>
    /// Gets whether the browser reports a dark color scheme preference.
    /// </summary>
    [JSImport("nativeui.prefersDarkColorScheme", "nativeui-dom")]
    public static partial bool PrefersDarkColorScheme();

    /// <summary>
    /// Sets the callback that a <c>native-ui</c> element invokes with each forwarded pointer event.
    /// The element adds its delegated listeners when connected and removes them when disconnected.
    /// </summary>
    [JSImport("nativeui.setPointerDispatcher", "nativeui-dom")]
    public static partial void SetPointerDispatcher(
        JSObject root,
        [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> dispatch);

    /// <summary>
    /// Injects or updates a <c>&lt;style&gt;</c> tag in <c>document.head</c> for a compiled <c>CssStyleSheet</c>.
    /// </summary>
    [JSImport("nativeui.injectStyleSheet", "nativeui-dom")]
    public static partial void InjectStyleSheet(string id, string cssText);

    /// <summary>
    /// Removes a stylesheet added by <see cref="InjectStyleSheet"/>.
    /// </summary>
    [JSImport("nativeui.removeStyleSheet", "nativeui-dom")]
    public static partial void RemoveStyleSheet(string id);
}

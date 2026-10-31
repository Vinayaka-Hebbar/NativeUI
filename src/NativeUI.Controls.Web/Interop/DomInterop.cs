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
    /// Sets an inline CSS style property on a DOM element.
    /// </summary>
    [JSImport("nativeui.setStyleProperty", "nativeui-dom")]
    public static partial void SetStyleProperty(JSObject element, string propertyName, string value);

    /// <summary>
    /// Appends a child DOM element to a parent DOM element.
    /// </summary>
    [JSImport("nativeui.appendChild", "nativeui-dom")]
    public static partial void AppendChild(JSObject parent, JSObject child);

    /// <summary>
    /// Removes a child DOM element from a parent DOM element.
    /// </summary>
    [JSImport("nativeui.removeChild", "nativeui-dom")]
    public static partial void RemoveChild(JSObject parent, JSObject child);

    /// <summary>
    /// Injects or updates a <c>&lt;style&gt;</c> tag in <c>document.head</c> for a compiled <c>CssStyleSheet</c>.
    /// </summary>
    [JSImport("nativeui.injectStyleSheet", "nativeui-dom")]
    public static partial void InjectStyleSheet(string id, string cssText);
}

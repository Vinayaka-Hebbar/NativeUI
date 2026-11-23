using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NativeUI.Controls.Web;

[SupportedOSPlatform("browser")]
internal static partial class ImageInterop
{
    /// <summary>Creates a native HTML image.</summary>
    [JSImport("createElement", "nativeui-image")]
    internal static partial JSObject CreateElement();

    [JSImport("decodeImage", "nativeui-image")]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    internal static partial Task<JSObject> DecodeImage(string base64, string contentType);

    [JSImport("cloneImage", "nativeui-image")]
    internal static partial JSObject CloneImage(JSObject image);

    [JSImport("releaseImage", "nativeui-image")]
    internal static partial void ReleaseImage(JSObject image);

    [JSImport("createSvgElement", "nativeui-image")]
    internal static partial JSObject CreateSvgElement(string tagName);

    [JSImport("replaceElement", "nativeui-image")]
    internal static partial void ReplaceElement(JSObject previous, JSObject replacement);

    [JSImport("createTextNode", "nativeui-image")]
    internal static partial JSObject CreateTextNode(string text);

    [JSImport("getChildNode", "nativeui-image")]
    internal static partial JSObject GetChildNode(JSObject parent, int index);

    [JSImport("getSvgClassName", "nativeui-image")]
    internal static partial string GetSvgClassName(JSObject element);

    [JSImport("setSvgClassName", "nativeui-image")]
    internal static partial void SetSvgClassName(JSObject element, string className);

    [JSImport("baseUri", "nativeui-image")]
    internal static partial string BaseUri();
}

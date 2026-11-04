using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Graphics;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

[SupportedOSPlatform("browser")]
internal sealed class BrowserImageDecoder : IImageDecoder
{
    internal static BrowserImageDecoder Shared { get; } = new();

    public async Task<IDecodedImage> DecodeAsync(byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        JSObject element = await ImageInterop.DecodeImage(Convert.ToBase64String(bytes), contentType);
        BrowserDecodedImage result = new(element);
        if (cancellationToken.IsCancellationRequested)
        {
            result.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        return result;
    }
}

[SupportedOSPlatform("browser")]
internal sealed class BrowserDecodedImage(JSObject element) : IDecodedImage
{
    private bool _disposed;
    public JSObject Element { get; } = element;
    public Size Size { get; } = new(element.GetPropertyAsDouble("naturalWidth"), element.GetPropertyAsDouble("naturalHeight"));
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ImageInterop.ReleaseImage(Element);
        Element.Dispose();
    }
}

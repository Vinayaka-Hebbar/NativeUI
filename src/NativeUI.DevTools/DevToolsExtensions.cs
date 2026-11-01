using NativeUI.Graphics;

namespace NativeUI.DevTools;

/// <summary>
/// Enables developer tools on a Skia-rendered host. Browser developer tools cover the Web target.
/// </summary>
public static class DevToolsExtensions
{
    /// <summary>
    /// Adds a <see cref="DevToolsOverlay"/> to <paramref name="host"/> and returns it so its tools can be switched at runtime.
    /// </summary>
    public static DevToolsOverlay AttachDevTools(this IRenderHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        DevToolsOverlay overlay = new(host);
        host.Overlays.Add(overlay);
        host.InvalidateRender();
        return overlay;
    }
}

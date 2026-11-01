using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Runs a shared control tree in the browser DOM.
/// The application runs inside .NET WebAssembly; HTTP hosting is provided by external tooling.
/// </summary>
public static class WebHost
{
    /// <summary>
    /// Mounts the UI and keeps the application's entry point alive.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">The application is not running in a browser.</exception>
    public static Task RunAsync(UIElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException("Run this application with the WebAssembly SDK in a browser.");
        }

        if (Application.Current is not WebApplication)
        {
            _ = new WebApplication { MainView = root as IView };
        }

        HtmlDomRoot domRoot = new();
        domRoot.Mount(root);
        return domRoot.Stopped;
    }
}

using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Represents an application running in the browser with a single <see cref="Application.MainView"/>, and supplies the
/// browser's color scheme preference as its system theme. Constructing an instance makes it <see cref="Application.Current"/>.
/// Deriving from this class is optional; <see cref="WebHost"/> creates an instance when none exists.
/// </summary>
[SupportedOSPlatform("browser")]
public class WebApplication : Application
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebApplication"/> class.
    /// </summary>
    public WebApplication()
    {
        DomInterop.ObserveColorScheme(NotifySystemThemeChanged);
        Current = this;
    }

    /// <inheritdoc />
    protected sealed override Theme SystemTheme => DomInterop.PrefersDarkColorScheme() ? Theme.Dark : Theme.Light;

    /// <summary>
    /// Mounts <see cref="Application.MainView"/> and keeps the application's entry point alive.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Application.MainView"/> is not set to a mountable view.</exception>
    public Task RunAsync()
    {
        if (MainView is not UIElement mainView)
        {
            throw new InvalidOperationException($"Set {nameof(MainView)} to a {nameof(View)} before running the application.");
        }

        return WebHost.RunAsync(mainView);
    }
}

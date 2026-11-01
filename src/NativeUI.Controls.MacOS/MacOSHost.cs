using NativeUI.Core;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Manages the macOS application lifecycle (<c>NSApplication</c>) and hosts a primary <see cref="Window"/>.
/// </summary>
public sealed class MacOSHost
{
    /// <summary>
    /// Displays <paramref name="mainWindow"/> with the current <see cref="MacOSApplication"/>, creating one when none
    /// exists, and starts the macOS event loop.
    /// </summary>
    public void Run(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        MacOSApplication application = Application.Current as MacOSApplication
            ?? new MacOSApplication { MainView = mainWindow.Content as IView };
        if (!application.Windows.Contains(mainWindow))
        {
            application.Windows.Add(mainWindow);
        }

        application.Run();
    }
}

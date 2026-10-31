namespace NativeUI.Controls.MacOS;

/// <summary>
/// Manages the macOS application lifecycle (<c>NSApplication</c>) and hosts a primary <see cref="Window"/>.
/// </summary>
public sealed class MacOSHost
{
    private const ulong ActivationPolicyRegular = 0UL;

    /// <summary>
    /// Initializes the native AppKit application, displays <paramref name="mainWindow"/>, and starts the macOS event loop.
    /// </summary>
    public void Run(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("MacOSHost.Run is only supported on macOS.");
        }

        AppKitInterop.EnsureFrameworksLoaded();
        nint nsAppClass = AppKitInterop.GetClass("NSApplication");
        nint sharedApp = AppKitInterop.SendMessage(nsAppClass, AppKitInterop.GetSelector("sharedApplication"));
        _ = AppKitInterop.SendMessage(sharedApp, AppKitInterop.GetSelector("setActivationPolicy:"), (nint)ActivationPolicyRegular);

        mainWindow.Show();

        AppKitInterop.SendMessageVoid(sharedApp, AppKitInterop.GetSelector("activateIgnoringOtherApps:"), true);
        _ = AppKitInterop.SendMessage(sharedApp, AppKitInterop.GetSelector("run"));
    }
}

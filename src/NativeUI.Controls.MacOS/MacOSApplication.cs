using NativeUI.Core;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Represents an application running on macOS with a list of <see cref="Windows"/>, and supplies the system appearance
/// as its system theme. Constructing an instance makes it <see cref="Application.Current"/>.
/// Deriving from this class is optional; <see cref="MacOSHost"/> creates an instance when none exists.
/// </summary>
public class MacOSApplication : Application
{
    private const ulong ActivationPolicyRegular = 0UL;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacOSApplication"/> class.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">The application is not running on macOS.</exception>
    public MacOSApplication()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException($"{nameof(MacOSApplication)} is only supported on macOS.");
        }

        AppKitInterop.EnsureFrameworksLoaded();
        MacOSPlatformSettings.Instance.ThemeChanged += OnSystemThemeChanged;
        Current = this;
    }

    /// <summary>
    /// Gets the windows that <see cref="Run"/> displays, in display order.
    /// </summary>
    public IList<Window> Windows { get; } = [];

    /// <inheritdoc />
    protected sealed override Theme SystemTheme => MacOSPlatformSettings.Instance.GetSystemTheme();

    /// <summary>
    /// Initializes the native AppKit application, displays <see cref="Windows"/>, and starts the macOS event loop.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Windows"/> is empty.</exception>
    public void Run()
    {
        if (Windows.Count == 0)
        {
            throw new InvalidOperationException($"Add a window to {nameof(Windows)} before running the application.");
        }

        nint nsAppClass = AppKitInterop.GetClass("NSApplication");
        nint sharedApp = AppKitInterop.SendMessage(nsAppClass, AppKitInterop.GetSelector("sharedApplication"));
        _ = AppKitInterop.SendMessage(sharedApp, AppKitInterop.GetSelector("setActivationPolicy:"), (nint)ActivationPolicyRegular);

        foreach (Window window in Windows)
        {
            window.Show();
        }

        AppKitInterop.SendMessageVoid(sharedApp, AppKitInterop.GetSelector("activateIgnoringOtherApps:"), true);
        _ = AppKitInterop.SendMessage(sharedApp, AppKitInterop.GetSelector("run"));
    }

    private void OnSystemThemeChanged(object? sender, Theme systemTheme)
    {
        NotifySystemThemeChanged();
    }
}

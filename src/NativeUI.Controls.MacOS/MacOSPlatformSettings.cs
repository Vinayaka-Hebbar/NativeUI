using NativeUI.Core;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Provides macOS AppKit system appearance settings (<see cref="IPlatformSettings"/>).
/// </summary>
public sealed class MacOSPlatformSettings : IPlatformSettings
{
    /// <summary>
    /// Gets the default macOS platform settings provider.
    /// </summary>
    public static MacOSPlatformSettings Instance { get; } = new();

    /// <inheritdoc />
    public event EventHandler<Theme>? ThemeChanged;

    /// <inheritdoc />
    public Theme GetSystemTheme()
    {
        return AppKitInterop.IsSystemDarkMode() ? Theme.Dark : Theme.Light;
    }

    /// <summary>
    /// Raises <see cref="ThemeChanged"/> with the current macOS system appearance.
    /// </summary>
    public void NotifySystemThemeChanged()
    {
        ThemeChanged?.Invoke(this, GetSystemTheme());
    }
}

namespace NativeUI.Core;

/// <summary>
/// Provides operating system or browser appearance and accessibility settings to a platform host.
/// </summary>
public interface IPlatformSettings
{
    /// <summary>
    /// Occurs when the operating system or browser theme changes at runtime.
    /// </summary>
    event EventHandler<Theme>? ThemeChanged;

    /// <summary>
    /// Gets the current system theme (<see cref="Theme.Light"/> or <see cref="Theme.Dark"/>).
    /// </summary>
    Theme GetSystemTheme();
}

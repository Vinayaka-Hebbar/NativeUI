namespace NativeUI.Core;

/// <summary>
/// Provides the previous and new <see cref="Application.CurrentTheme"/>.
/// </summary>
/// <param name="oldTheme">The previous effective theme.</param>
/// <param name="newTheme">The new effective theme.</param>
public sealed class ThemeChangedEventArgs(
    Theme oldTheme,
    Theme newTheme) : EventArgs
{
    /// <summary>
    /// Gets the previous effective theme.
    /// </summary>
    public Theme OldTheme { get; } = oldTheme;

    /// <summary>
    /// Gets the new effective theme.
    /// </summary>
    public Theme NewTheme { get; } = newTheme;
}

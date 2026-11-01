using NativeUI.Core;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Requests application themes for a test and restores <see cref="Theme.Default"/> on disposal.
/// </summary>
internal sealed class TestTheme : IDisposable
{
    /// <summary>
    /// Requests <paramref name="theme"/> and restyles <paramref name="root"/>, the role of a platform host.
    /// </summary>
    public void Apply(UIElement root, Theme theme)
    {
        Application.Current.RequestedTheme = theme;
        root.InvalidateStyle();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Application.Current.RequestedTheme = Theme.Default;
    }
}

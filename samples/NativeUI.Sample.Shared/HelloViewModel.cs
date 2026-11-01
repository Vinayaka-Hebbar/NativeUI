using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NativeUI.Sample;

/// <summary>
/// Sample ViewModel demonstrating <see cref="ObservableObject"/> from <c>CommunityToolkit.Mvvm</c>
/// bound to a compiled NativeUI XAML view across Web and macOS.
/// </summary>
public partial class HelloViewModel : ObservableObject
{
    [ObservableProperty]
    private string _frameworkBadge = "NATIVEUI • .NET 10";

    [ObservableProperty]
    private string _title = "Cross-Platform UI with Compiled XAML & CSS";

    [ObservableProperty]
    private string _subtitle = "Shared SkiaSharp rendering on Desktop and real HTML DOM elements on Web.";

    [ObservableProperty]
    private string _liveStatus = "Theme variable --color-primary resolved via oklch(0.55 0.22 255)";

    [ObservableProperty]
    private string _themeButtonText = "Switch to Dark Mode";

    [ObservableProperty]
    private bool _isDarkMode;

    /// <summary>
    /// Occurs when the user requests a theme toggle.
    /// </summary>
    public event EventHandler? ThemeToggled;

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeToggled?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        ThemeButtonText = value ? "Switch to Light Mode" : "Switch to Dark Mode";
        LiveStatus = value
            ? "Dark theme active: --color-surface resolved to #0f172a"
            : "Light theme active: --color-surface resolved to #f8fafc";
    }
}

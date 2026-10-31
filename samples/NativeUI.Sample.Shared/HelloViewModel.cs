using CommunityToolkit.Mvvm.ComponentModel;

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
}

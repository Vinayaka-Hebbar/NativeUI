using NativeUI.Controls.MacOS;
#if DEBUG
using NativeUI.DevTools;
#endif
using NativeUI.Sample;

HelloViewModel viewModel = new();
HelloView view = new()
{
    DataContext = viewModel
};

using Window window = new()
{
    Title = "NativeUI — macOS SkiaSharp + Compiled XAML/CSS Sample",
    Width = 680,
    Height = 760,
    Content = view
};

#if DEBUG
window.AttachDevTools();
#endif

MacOSHost host = new();
host.Run(window);

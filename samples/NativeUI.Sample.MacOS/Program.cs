using NativeUI.Controls.MacOS;
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
    Height = 360,
    Content = view
};

MacOSHost host = new();
host.Run(window);

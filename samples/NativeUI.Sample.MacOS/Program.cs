using NativeUI.Controls.MacOS;
#if DEBUG
using NativeUI.DevTools;
#endif
using NativeUI.Sample;

GalleryViewModel viewModel = new();
GalleryView view = new()
{
    DataContext = viewModel
};

using Window window = new()
{
    Title = "NativeUI — Component Gallery",
    Width = 1140,
    Height = 840,
    Content = view
};

#if DEBUG
window.AttachDevTools();
#endif

MacOSHost host = new();
host.Run(window);

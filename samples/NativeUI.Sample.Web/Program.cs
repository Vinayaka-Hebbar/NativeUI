using NativeUI.Controls.Web;
using NativeUI.Sample;

GalleryViewModel viewModel = new();
GalleryView view = new()
{
    DataContext = viewModel
};


await WebHost.RunAsync(view);

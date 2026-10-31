using NativeUI.Controls.Web;
using NativeUI.Sample;

HelloViewModel viewModel = new();
HelloView view = new()
{
    DataContext = viewModel
};


await WebHost.RunAsync(view);

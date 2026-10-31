using NativeUI.Controls.Web;
using NativeUI.Sample;

HelloViewModel viewModel = new();
HelloView view = new()
{
    DataContext = viewModel
};

using WebHost host = new();
host.Run(view);

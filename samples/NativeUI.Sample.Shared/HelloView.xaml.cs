using NativeUI.Media;

namespace NativeUI.Sample;

/// <summary>
/// Shared sample view compiled from <c>HelloView.xaml</c> and styled by <c>HelloView.xaml.css</c>.
/// </summary>
public partial class HelloView
{
    private HelloViewModel? _observedViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="HelloView"/> class.
    /// </summary>
    public HelloView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(object? newDataContext)
    {
        base.OnDataContextChanged(newDataContext);

        if (_observedViewModel is not null)
        {
            _observedViewModel.ThemeToggled -= OnThemeToggled;
        }

        if (newDataContext is HelloViewModel vm)
        {
            _observedViewModel = vm;
            vm.ThemeToggled += OnThemeToggled;
        }
        else
        {
            _observedViewModel = null;
        }
    }

    private void OnThemeToggled(object? sender, EventArgs e)
    {
        Theme = Theme == NativeUI.Core.Theme.Light ? NativeUI.Core.Theme.Dark : NativeUI.Core.Theme.Light;
    }
}

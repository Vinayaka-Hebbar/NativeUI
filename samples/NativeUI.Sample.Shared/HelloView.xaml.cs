using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Sample;

/// <summary>
/// Shared sample view compiled from <c>HelloView.xaml</c> and styled by <c>HelloView.xaml.css</c>.
/// </summary>
public partial class HelloView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HelloView"/> class.
    /// </summary>
    public HelloView()
    {
        InitializeComponent();
        ActualThemeChanged += OnActualThemeChanged;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);

        if (args.OldValue is HelloViewModel oldViewModel)
        {
            oldViewModel.ThemeToggled -= OnThemeToggled;
        }

        if (args.NewValue is HelloViewModel newViewModel)
        {
            newViewModel.ThemeToggled += OnThemeToggled;
            newViewModel.IsDarkMode = ActualTheme == NativeUI.Core.Theme.Dark;
        }
    }

    private void OnThemeToggled(object? sender, EventArgs e)
    {
        Theme = ActualTheme == NativeUI.Core.Theme.Light ? NativeUI.Core.Theme.Dark : NativeUI.Core.Theme.Light;
    }

    private void OnActualThemeChanged(object? sender, EventArgs e)
    {
        if (DataContext is HelloViewModel viewModel)
        {
            viewModel.IsDarkMode = ActualTheme == NativeUI.Core.Theme.Dark;
        }
    }
}

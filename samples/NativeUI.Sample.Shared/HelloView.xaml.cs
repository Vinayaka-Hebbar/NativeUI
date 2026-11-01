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

        ScrollTopButton.Click += (_, _) => LogScroll.ScrollTo(0, 0);
        ScrollBottomButton.Click += (_, _) => LogScroll.ScrollTo(0, LogScroll.ExtentHeight);
        ScrollRightButton.Click += (_, _) => WideScroll.ScrollTo(WideScroll.ExtentWidth, 0);
        LogScroll.ScrollChanged += (_, _) =>
            ScrollStatus.Value = $"Scrolled {LogScroll.VerticalOffset:0}px of {LogScroll.ExtentHeight - LogScroll.ViewportHeight:0}px";
    }
}

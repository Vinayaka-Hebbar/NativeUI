using NativeUI.Controls;
using NativeUI.Graphics;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Sample;

/// <summary>
/// Shared sample view compiled from <c>HelloView.xaml</c> and styled by <c>HelloView.xaml.css</c>.
/// </summary>
public partial class HelloView
{
    private const string NavItemClass = "nav-item";
    private const string NavItemActiveClass = "nav-item-active";
    private const int BarCount = 8;

    private readonly double[] _barValues = new double[BarCount];
    private readonly List<(Button Nav, View Page)> _pages;

    /// <summary>
    /// Initializes a new instance of the <see cref="HelloView"/> class.
    /// </summary>
    public HelloView()
    {
        InitializeComponent();

        _pages =
        [
            (NavStack, StackPage),
            (NavGrid, GridPage),
            (NavScroll, ScrollPage),
            (NavCanvas, CanvasPage),
            (NavInput, InputPage),
            (NavDialog, DialogPage),
            (NavAnimation, AnimationPage),
            (NavScene3D, Scene3DPage)
        ];

        foreach ((Button nav, View page) in _pages)
        {
            nav.Click += (_, _) => ShowPage(page);
        }

        ShowPage(StackPage);

        ScrollTopButton.Click += (_, _) => LogScroll.ScrollTo(0, 0);
        ScrollBottomButton.Click += (_, _) => LogScroll.ScrollTo(0, LogScroll.ExtentHeight);
        ScrollRightButton.Click += (_, _) => WideScroll.ScrollTo(WideScroll.ExtentWidth, 0);
        LogScroll.ScrollChanged += (_, _) =>
            ScrollStatus.Value = $"Scrolled {LogScroll.VerticalOffset:0}px of {LogScroll.ExtentHeight - LogScroll.ViewportHeight:0}px";

        ShuffleBars();
        ShapesCanvas.Draw += (_, args) => DrawShapes(args.Context);
        TransformCanvas.Draw += (_, args) => DrawTransforms(args.Context);
        ChartCanvas.Draw += (_, args) => DrawChart(args.Context, args.Bounds);
        ShuffleButton.Click += (_, _) =>
        {
            ShuffleBars();
            ChartCanvas.InvalidateVisual();
        };

        FocusNameButton.Click += (_, _) => NameInput.Focus();
        NameInput.GotFocus += (_, _) => FocusStatus.Value = "The field has focus";
        NameInput.LostFocus += (_, _) => FocusStatus.Value = "The field does not have focus";
    }

    private void ShowPage(View selectedPage)
    {
        foreach ((Button nav, View page) in _pages)
        {
            bool isSelected = ReferenceEquals(page, selectedPage);
            page.IsVisible = isSelected;
            nav.Class = isSelected ? [NavItemClass, NavItemActiveClass] : [NavItemClass];
        }

        ExamplesScroll.ScrollTo(0, 0);
    }

    private void ShuffleBars()
    {
        for (int i = 0; i < BarCount; i++)
        {
            _barValues[i] = 0.2 + (Random.Shared.NextDouble() * 0.8);
        }
    }

    private static void DrawShapes(DrawingContext context)
    {
        context.DrawRectangle(new Rect(16, 16, 88, 88), new Paint(Colors.Blue));
        context.DrawRoundedRectangle(new Rect(120, 16, 88, 88), 16, new Paint(Colors.Green));
        context.DrawRoundedRectangle(new Rect(224, 16, 88, 88), 44, new Paint(Colors.Red));

        // Overlapping shapes show that opacity blends with what is already drawn.
        context.DrawRoundedRectangle(new Rect(328, 16, 72, 72), 12, new Paint(Colors.Indigo));
        context.DrawRoundedRectangle(new Rect(356, 32, 72, 72), 12, new Paint(Colors.Red, 0.6));
    }

    private static void DrawTransforms(DrawingContext context)
    {
        // Each square uses the same coordinates; only the translation and scale change.
        for (int i = 0; i < 4; i++)
        {
            context.Save();
            context.Translate(16 + (i * 48), 16 + (i * 8));
            context.Scale(1f + (i * 0.25f), 1f + (i * 0.25f));
            context.DrawRoundedRectangle(new Rect(0, 0, 32, 32), 6, new Paint(Colors.Blue, 0.4 + (i * 0.2)));
            context.Restore();
        }

        // Stripes are drawn past the rounded clip, which trims them to its corners.
        context.Save();
        context.ClipRoundedRectangle(new Rect(300, 16, 144, 108), 24);
        context.DrawRectangle(new Rect(300, 16, 144, 108), new Paint(Colors.Indigo));
        for (int i = 0; i < 6; i++)
        {
            context.DrawRectangle(new Rect(300 + (i * 24), 16, 12, 108), new Paint(Colors.White, 0.35));
        }

        context.Restore();
    }

    private void DrawChart(DrawingContext context, Rect bounds)
    {
        const double Margin = 16;
        const double Gap = 8;

        double chartHeight = bounds.Height - (2 * Margin);
        double barWidth = (bounds.Width - (2 * Margin) - (Gap * (BarCount - 1))) / BarCount;

        context.DrawRectangle(new Rect(Margin, bounds.Height - Margin, bounds.Width - (2 * Margin), 1), new Paint(Colors.Black, 0.3));

        for (int i = 0; i < BarCount; i++)
        {
            double barHeight = chartHeight * _barValues[i];
            double x = Margin + (i * (barWidth + Gap));
            double y = bounds.Height - Margin - barHeight;
            context.DrawRoundedRectangle(new Rect(x, y, barWidth, barHeight), 4, new Paint(Colors.Blue));
        }
    }
}

using NativeUI.Controls;
using NativeUI.Core;

namespace NativeUI.Sample;

/// <summary>
/// Demonstrates the <see cref="SplitView"/> control with configurable display modes and pane placement.
/// </summary>
public partial class SplitViewSampleView
{
    private const string ModeBtnClass = "mode-btn";
    private const string ModeBtnActiveClass = "mode-btn-active";

    /// <summary>
    /// Initializes a new instance of the <see cref="SplitViewSampleView"/> class.
    /// </summary>
    public SplitViewSampleView()
    {
        InitializeComponent();

        ModeInlineButton.Click += (_, _) => SetMode(SplitViewDisplayMode.Inline, ModeInlineButton);
        ModeCompactInlineButton.Click += (_, _) => SetMode(SplitViewDisplayMode.CompactInline, ModeCompactInlineButton);
        ModeOverlayButton.Click += (_, _) => SetMode(SplitViewDisplayMode.Overlay, ModeOverlayButton);
        ModeCompactOverlayButton.Click += (_, _) => SetMode(SplitViewDisplayMode.CompactOverlay, ModeCompactOverlayButton);

        PaneOpenCheckBox.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, CheckBox.IsCheckedProperty))
            {
                DemoSplitView.IsPaneOpen = PaneOpenCheckBox.IsChecked == true;
            }
        };

        PaneCollapseButton.Click += (_, _) => TogglePane();
        DemoMenuButton.Click += (_, _) => TogglePane();

        DemoSplitView.PaneOpened += (_, _) => PaneOpenCheckBox.IsChecked = true;
        DemoSplitView.PaneClosed += (_, _) => PaneOpenCheckBox.IsChecked = false;

        DemoNavHome.Click += (_, _) => SelectNav(DemoNavHome);
        DemoNavProjects.Click += (_, _) => SelectNav(DemoNavProjects);
        DemoNavSettings.Click += (_, _) => SelectNav(DemoNavSettings);
    }

    private void TogglePane()
    {
        DemoSplitView.IsPaneOpen = !DemoSplitView.IsPaneOpen;
    }

    private void SetMode(SplitViewDisplayMode mode, Button activeBtn)
    {
        DemoSplitView.DisplayMode = mode;
        ModeInlineButton.Class = ReferenceEquals(activeBtn, ModeInlineButton) ? [ModeBtnClass, ModeBtnActiveClass] : [ModeBtnClass];
        ModeCompactInlineButton.Class = ReferenceEquals(activeBtn, ModeCompactInlineButton) ? [ModeBtnClass, ModeBtnActiveClass] : [ModeBtnClass];
        ModeOverlayButton.Class = ReferenceEquals(activeBtn, ModeOverlayButton) ? [ModeBtnClass, ModeBtnActiveClass] : [ModeBtnClass];
        ModeCompactOverlayButton.Class = ReferenceEquals(activeBtn, ModeCompactOverlayButton) ? [ModeBtnClass, ModeBtnActiveClass] : [ModeBtnClass];
    }

    private void SelectNav(Button selected)
    {
        DemoNavHome.Class = ReferenceEquals(selected, DemoNavHome) ? ["demo-nav-item", "demo-nav-item-active"] : ["demo-nav-item"];
        DemoNavProjects.Class = ReferenceEquals(selected, DemoNavProjects) ? ["demo-nav-item", "demo-nav-item-active"] : ["demo-nav-item"];
        DemoNavSettings.Class = ReferenceEquals(selected, DemoNavSettings) ? ["demo-nav-item", "demo-nav-item-active"] : ["demo-nav-item"];
    }
}

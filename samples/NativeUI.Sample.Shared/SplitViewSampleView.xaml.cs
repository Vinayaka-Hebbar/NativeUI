using NativeUI.Controls;
using NativeUI.Core;
using NativeUI.Media;
using Path = NativeUI.Controls.Shapes.Path;

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

        UpdateModeButton(ModeInlineButton, ModeInlineIcon, ReferenceEquals(activeBtn, ModeInlineButton));
        UpdateModeButton(ModeCompactInlineButton, ModeCompactInlineIcon, ReferenceEquals(activeBtn, ModeCompactInlineButton));
        UpdateModeButton(ModeOverlayButton, ModeOverlayIcon, ReferenceEquals(activeBtn, ModeOverlayButton));
        UpdateModeButton(ModeCompactOverlayButton, ModeCompactOverlayIcon, ReferenceEquals(activeBtn, ModeCompactOverlayButton));
    }

    private static void UpdateModeButton(Button btn, Path icon, bool isActive)
    {
        btn.Class = isActive ? [ModeBtnClass, ModeBtnActiveClass] : [ModeBtnClass];
        icon.Stroke = isActive ? Brushes.White : new SolidColorBrush(Color.FromHex("#0f172a"));
    }

    private void SelectNav(Button selected)
    {
        UpdateNavButton(DemoNavHome, DemoNavHomeIcon, ReferenceEquals(selected, DemoNavHome));
        UpdateNavButton(DemoNavProjects, DemoNavProjectsIcon, ReferenceEquals(selected, DemoNavProjects));
        UpdateNavButton(DemoNavSettings, DemoNavSettingsIcon, ReferenceEquals(selected, DemoNavSettings));
    }

    private static void UpdateNavButton(Button btn, Path icon, bool isSelected)
    {
        btn.Class = isSelected ? ["demo-nav-item", "demo-nav-item-active"] : ["demo-nav-item"];
        icon.Stroke = isSelected ? Brushes.White : new SolidColorBrush(Color.FromHex("#94a3b8"));
    }
}

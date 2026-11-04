using NativeUI.Controls;

namespace NativeUI.Sample;

/// <summary>Demonstrates Separator, Badge, Avatar, Breadcrumb, and ButtonGroup styled from the stylesheet.</summary>
public partial class DisplaySampleView
{
    /// <summary>Creates the examples and reports breadcrumb clicks in the status text.</summary>
    public DisplaySampleView()
    {
        InitializeComponent();

        PathBreadcrumb.ItemsSource = new[] { "Home", "Components", "Breadcrumb" };
        ChevronBreadcrumb.ItemsSource = new[] { "Projects", "NativeUI", "src", "Breadcrumb.cs" };
        PathBreadcrumb.ItemClick += OnBreadcrumbItemClick;
        ChevronBreadcrumb.ItemClick += OnBreadcrumbItemClick;
    }

    private void OnBreadcrumbItemClick(object? sender, ItemClickEventArgs args)
    {
        BreadcrumbStatus.Value = $"Clicked {args.ClickedItem} at index {args.Index}";
    }
}

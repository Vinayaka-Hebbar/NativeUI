using NativeUI.Sample;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>Verifies that compiled gallery navigation exposes every sample page.</summary>
internal static class GalleryTests
{
    internal static void VerifyNavigation()
    {
        using GalleryView gallery = new();
        GalleryViewModel model = (GalleryViewModel)(gallery.DataContext ?? throw new InvalidOperationException("Missing gallery model."));
        (string Tag, Type PageType)[] examples =
        [
            ("SplitView", typeof(SplitViewSampleView)),
            ("StackPanel", typeof(StackPanelSampleView)),
            ("Grid", typeof(GridSampleView)),
            ("Scrolling", typeof(ScrollSampleView)),
            ("Canvas", typeof(CanvasSampleView)),
            ("Images", typeof(ImageSampleView)),
            ("Input", typeof(InputSampleView)),
            ("Text", typeof(TextSampleView)),
            ("Toggles", typeof(ToggleSampleView)),
            ("Select", typeof(SelectSampleView)),
            ("Display", typeof(DisplaySampleView)),
            ("Popups", typeof(PopupSampleView)),
            ("Tabs", typeof(TabSampleView)),
            ("Forms", typeof(FormSampleView)),
            ("Collections", typeof(CollectionSampleView)),
            ("Dialog", typeof(DialogSampleView)),
            ("Animation", typeof(AnimationSampleView)),
            ("Scene3D", typeof(Scene3DSampleView)),
            ("ResizeObserver", typeof(ResizeSampleView))
        ];
        Assert.Equal(examples.Length, model.NavigationItems.Count);
        foreach ((string tag, Type pageType) in examples)
        {
            NavigationItemViewModel entry = model.NavigationItems.Single(item => item.Tag == tag);
            gallery.AppNav.SelectedItem = entry;
            Assert.Same(entry, model.SelectedPage ?? throw new InvalidOperationException("Missing selection."));
            View page = model.CurrentPage as View ?? throw new InvalidOperationException($"Missing page for {tag}.");
            Assert.Equal(pageType, page.GetType());
            Assert.True(page.Children.Count > 0);
            Assert.True(page.Parent is not null);
        }
    }
}

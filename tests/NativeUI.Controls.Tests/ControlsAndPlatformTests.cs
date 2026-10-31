using NativeUI.Controls;
using NativeUI.Controls.MacOS;
using NativeUI.Controls.Web;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Tests;

int exitCode = TestRunner.RunSuite("NativeUI.Controls.Tests",
[
    ("Control supports CSS Class styling, inline C# Style overrides, and Theme/ActualTheme inheritance", TestClassAndInlineStyleSystem),
    ("Desktop LayoutEngine and SkiaSurface render HelloView and re-render on ViewModel change", TestDesktopSkiaRenderingAndBindingUpdate),
    ("WebHost mounts real DOM hierarchy and performs targeted incremental updates", TestWebHostIncrementalDomUpdates)
]);

return exitCode;

static void TestClassAndInlineStyleSystem()
{
    HelloView view = new();

    // 1. Verify CSS class styling from HelloView.xaml.css via optional StyleResolver
    Assert.Equal("card", view.Class);
    Assert.Equal("title", view.TitleText.Class);
    Assert.Equal(28.0, view.TitleText.ComputedStyle.FontSize);
    Assert.Equal(FontWeight.Bold, view.TitleText.ComputedStyle.FontWeight);

    // 2. Verify inline Style = new() { ... } works standalone and overrides CSS class properties
    Text styledBadge = new()
    {
        Class = "subtitle",
        Value = "Submit",
        Style = new()
        {
            BackgroundColor = Colors.Blue,
            Color = Colors.White,
            Padding = new(12, 8),
            BorderRadius = 6
        }
    };
    view.Children.Add(styledBadge);

    Assert.Equal(Colors.Blue, styledBadge.ComputedStyle.BackgroundColor);
    Assert.Equal(Colors.White, styledBadge.ComputedStyle.TextColor);
    Assert.Equal(new Thickness(12, 8), styledBadge.ComputedStyle.Padding);
    Assert.Equal(6.0, styledBadge.ComputedStyle.BorderRadius);
    // Retains FontSize from .subtitle class because inline Style did not override FontSize
    Assert.Equal(16.0, styledBadge.ComputedStyle.FontSize);

    // 3. Mutating inline Style triggers immediate recomputation
    styledBadge.Style.BorderRadius = 10;
    Assert.Equal(10.0, styledBadge.ComputedStyle.BorderRadius);

    // 4. Setting Theme on parent View propagates ActualTheme to children and re-evaluates @media (prefers-color-scheme: dark)
    view.Theme = Theme.Light;
    Assert.Equal(Theme.Light, view.TitleText.ActualTheme);
    Color? lightTitleColor = view.TitleText.ComputedStyle.TextColor;

    view.Theme = Theme.Dark;
    Assert.Equal(Theme.Dark, view.TitleText.ActualTheme);
    Color? darkTitleColor = view.TitleText.ComputedStyle.TextColor;

    Assert.False(EqualityComparer<Color?>.Default.Equals(lightTitleColor, darkTitleColor));
}

static void TestDesktopSkiaRenderingAndBindingUpdate()
{
    HelloViewModel viewModel = new();
    HelloView view = new()
    {
        DataContext = viewModel
    };

    using Window window = new()
    {
        Width = 640,
        Height = 360,
        ScaleFactor = 2.0f,
        Content = view
    };

    Assert.True(window.RenderCount >= 1);
    Assert.NotNull(window.LastLayout);

    LayoutBox initialTitleBox = window.LastLayout!.Children[1];
    Assert.True(initialTitleBox.ContentBox.Width > 50, "Expected non-zero measured Skia text width.");

    int countBeforeUpdate = window.RenderCount;

    // Mutate observable ViewModel property and verify incremental frame repaint without recreating controls
    Text originalTitleInstance = view.TitleText;
    viewModel.Title = "Updated Skia & DOM Title";

    Assert.Same(originalTitleInstance, view.TitleText);
    Assert.Equal("Updated Skia & DOM Title", view.TitleText.Value);
    Assert.True(window.RenderCount > countBeforeUpdate, "Expected desktop window to repaint on bound property change.");
}

static void TestWebHostIncrementalDomUpdates()
{
    HelloViewModel viewModel = new();
    HelloView view = new()
    {
        DataContext = viewModel
    };

    Text inlineStyledText = new()
    {
        Value = "Submit",
        Style = new()
        {
            BackgroundColor = Colors.Blue,
            Color = Colors.White,
            Padding = new(12, 8),
            BorderRadius = 6
        }
    };
    view.Children.Add(inlineStyledText);

    using WebHost webHost = new(useBrowserInterop: false);
    webHost.MountHeadless(view);

    // Verify initial mount created stylesheet, <div> for View, and <span> for Text controls, plus inline styles
    Assert.True(webHost.MutationLog.Any(static m => m.Operation == "InjectStyleSheet" && m.Detail == "HelloView"));
    Assert.True(webHost.MutationLog.Any(static m => m.Operation == "CreateElement" && m.TargetTag == "div" && m.Detail == "card"));
    Assert.True(webHost.MutationLog.Any(static m => m.Operation == "CreateElement" && m.TargetTag == "span" && m.Detail == "title"));
    Assert.True(webHost.MutationLog.Any(static m => m.Operation == "SetStyleProperty" && m.TargetTag == "span" && m.Detail == $"background-color:{Colors.Blue.ToCssString()}"));
    Assert.True(webHost.MutationLog.Any(static m => m.Operation == "SetStyleProperty" && m.TargetTag == "span" && m.Detail == "padding:8px 12px 8px 12px"));
    Assert.True(webHost.MutationLog.Any(static m => m.Operation == "SetStyleProperty" && m.TargetTag == "span" && m.Detail == "border-radius:6px"));

    webHost.ClearMutationLog();

    // Update observable ViewModel property -> must ONLY emit a single targeted SetTextContent mutation
    viewModel.Title = "Incremental Web Update!";

    Assert.Equal(1, webHost.MutationLog.Count);
    DomMutationRecord mutation = webHost.MutationLog[0];
    Assert.Equal("SetTextContent", mutation.Operation);
    Assert.Equal("span", mutation.TargetTag);
    Assert.Equal("Incremental Web Update!", mutation.Detail);
}

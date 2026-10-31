using System.Collections.Immutable;
using NativeUI.Controls;
using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Tests;

int exitCode = TestRunner.RunSuite("NativeUI.Controls.Tests",
[
    ("Control supports CSS Class styling, inline C# Style overrides, and Theme/ActualTheme inheritance", TestClassAndInlineStyleSystem),
    ("Desktop LayoutEngine and SkiaSurface render HelloView and re-render on ViewModel change", TestDesktopSkiaRenderingAndBindingUpdate),
    ("HarfBuzz SkiaTextShaper and SkiaTextLayout support hit-testing, word boundaries, wrapped selection rects, and disposal", TestHarfBuzzTextShapingAndHitTesting),
    ("Selection follows font bounds with balanced line spacing and accurate cluster hit-testing", NativeUI.Controls.Tests.SelectionGeometryTests.Verify),
    ("Text inherits its parent's CSS font family and preserves local overrides", NativeUI.Controls.Tests.FontInheritanceTests.Verify),
    ("Style resolution inherits text properties and respects explicit CSS and inline overrides", NativeUI.Controls.Tests.StyleResolutionTests.VerifyInheritanceAndOverrides),
    ("Relative CSS lengths use inherited and final font sizes", NativeUI.Controls.Tests.StyleResolutionTests.VerifyRelativeLengths),
    ("Unchanged styles skip repaint while inherited and theme changes invalidate affected elements", NativeUI.Controls.Tests.StyleResolutionTests.VerifyInvalidation),
    ("Desktop Window supports pointer drag selection, double-click word selection, and async clipboard copy", TestDesktopWindowTextSelectionAndClipboard)
]);

return exitCode;

static void TestClassAndInlineStyleSystem()
{
    HelloView view = new();

    // Verify CSS class styling from HelloView.xaml.css via optional StyleResolver
    Assert.Equal("card", view.Class);
    Assert.Equal("title", view.TitleText.Class);
    Assert.Equal(26.0, view.TitleText.ComputedStyle.FontSize);
    Assert.Equal(FontWeight.Bold, view.TitleText.ComputedStyle.FontWeight);
    Assert.Equal("Arial", view.TitleText.ComputedStyle.FontFamily);

    // Verify inline Style = new() { ... } works standalone and overrides CSS class properties
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

    // Mutating inline Style triggers immediate recomputation
    styledBadge.Style.BorderRadius = 10;
    Assert.Equal(10.0, styledBadge.ComputedStyle.BorderRadius);

    // Setting Theme on parent View propagates ActualTheme to children and re-evaluates @media (prefers-color-scheme: dark)
    view.Theme = Theme.Light;
    Assert.Equal(Theme.Light, view.TitleText.ActualTheme);
    Color? lightTitleColor = view.TitleText.ComputedStyle.TextColor;
    IUIElement element = view.TitleText;
    ThemeValue<Color> textColorToken = new("--color-text");
    Assert.Equal(lightTitleColor, CssStyleResolver.Instance.ResolveColor(textColorToken, element));
    ComputedStyle beforeResolution = element.ComputedStyle;
    Assert.Equal(beforeResolution, CssStyleResolver.Instance.ResolveStyle(element));
    Assert.Same(beforeResolution, element.ComputedStyle);

    view.Theme = Theme.Dark;
    Assert.Equal(Theme.Dark, view.TitleText.ActualTheme);
    Color? darkTitleColor = view.TitleText.ComputedStyle.TextColor;
    Assert.Equal(darkTitleColor, CssStyleResolver.Instance.ResolveColor(textColorToken, element));

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

static void TestHarfBuzzTextShapingAndHitTesting()
{
    ComputedStyle style = new()
    {
        FontSize = 16.0,
        FontWeight = FontWeight.Normal
    };

    Size measured = SkiaTextShaper.Shared.MeasureText("Hello NativeUI World", new Size(400, 100), style);
    Assert.True(measured.Width > 50);
    Assert.True(measured.Height > 10);

    ITextLayout layout = SkiaTextShaper.Shared.CreateTextLayout(
        "Hello NativeUI World\nSecond Line Text",
        new Size(400, 200),
        style);

    Assert.Equal(2, layout.Lines.Length);

    TextHitTestResult hitStart = layout.HitTestPoint(new Point(1, 4));
    Assert.Equal(0, hitStart.Range.Start);
    Assert.True(hitStart.IsInside);

    TextRange word = layout.GetWordRangeAt(7);
    Assert.Equal("NativeUI", layout.Text.Substring(word.Start, word.Length));

    ImmutableArray<Rect> multiLineRects = layout.HitTestTextRange(6, layout.Text.Length - 6);
    Assert.Equal(2, multiLineRects.Length);

    if (layout is IDisposable disposableLayout)
    {
        disposableLayout.Dispose();
    }
}

static void TestDesktopWindowTextSelectionAndClipboard()
{
    InMemoryClipboard clipboard = new();
    HelloViewModel viewModel = new();
    HelloView view = new()
    {
        DataContext = viewModel
    };

    Assert.True(view.TitleText.IsTextSelectionEnabled);

    using Window window = new(clipboard: clipboard)
    {
        Width = 640,
        Height = 360,
        ScaleFactor = 2.0f,
        Content = view
    };

    LayoutBox titleBox = window.LastLayout!.Children[1];
    Point doubleClickPoint = new(titleBox.ContentBox.X + 10, titleBox.ContentBox.Y + (titleBox.ContentBox.Height * 0.5));

    // Double-click selects the first word of TitleText ("Cross")
    window.HandlePointerPressed(doubleClickPoint, clickCount: 2);
    Assert.True(view.TitleText.HasSelection);
    Assert.Equal("Cross", view.TitleText.SelectedText);

    // Cmd+C / Ctrl+C copies the selected text to the window's IClipboard
    bool copied = window.HandleKeyDownAsync("c", commandOrControl: true).GetAwaiter().GetResult();
    Assert.True(copied);
    Assert.Equal("Cross", clipboard.GetTextAsync().GetAwaiter().GetResult());

    // Drag selection across TitleText
    Point dragStart = new(titleBox.ContentBox.X + 2, titleBox.ContentBox.Y + (titleBox.ContentBox.Height * 0.5));
    Point dragEnd = new(titleBox.ContentBox.X + titleBox.ContentBox.Width - 2, titleBox.ContentBox.Y + (titleBox.ContentBox.Height * 0.5));
    window.HandlePointerPressed(dragStart, clickCount: 1);
    window.HandlePointerMoved(dragEnd, isPressed: true);
    window.HandlePointerReleased(dragEnd);
    Assert.True(view.TitleText.SelectionLength > 5);
}

file sealed class InMemoryClipboard : IClipboard
{
    private string? _text;

    public ValueTask SetTextAsync(string text)
    {
        _text = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetTextAsync()
    {
        return ValueTask.FromResult(_text);
    }
}

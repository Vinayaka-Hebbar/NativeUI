using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Tests;
using SkiaSharp;

namespace NativeUI.Controls.Tests;

internal static class TextInlineTests
{
    public static void VerifyPlainTextAndPrecedence()
    {
        using Text text = new() { Value = "fallback" };
        ITextSource source = text;
        Assert.Equal(0, source.TextRuns.Count);
        text.SelectAll();
        Assert.Equal("fallback", text.SelectedText);
        Run first = new() { Value = "Hello " };
        Span span = new();
        span.Inlines.Add(new Run { Value = "world" });
        text.Inlines.Add(first);
        text.Inlines.Add(span);
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new Run { Value = "next" });
        Assert.Equal("Hello world\nnext", source.Text);
        Assert.Equal("fallback", text.Value);
        text.Select(3, 10);
        Assert.Equal("lo world\nn", text.SelectedText);
        first.Value = "Hi ";
        Assert.Equal("Hi world\nnext", source.Text);
        text.Inlines.Clear();
        Assert.Equal("fallback", source.Text);
        Assert.Equal(0, source.TextRuns.Count);
        text.SelectAll();
        Assert.Equal("fallback", text.SelectedText);
    }

    public static void VerifyCompiledContentAndBindings()
    {
        using TextSampleView sample = new();
        TextSampleViewModel model = (TextSampleViewModel)sample.DataContext!;
        Assert.Equal("Hello world", sample.PlainText.Value);
        Assert.Equal(0, sample.PlainText.Inlines.Count);
        Assert.Equal("Hello formatted world!", ((ITextSource)sample.FormattedText).Text);
        Assert.Equal("Ada Lovelace", ((ITextSource)sample.NameText).Text);
        Assert.Equal("First line\nSecond line", ((ITextSource)sample.BreakText).Text);
        Assert.Equal("Read our terms and conditions.", ((ITextSource)sample.LinkText).Text);
        Assert.Equal(new Uri("https://example.com"), sample.TermsLink.NavigateUri);
        Run first = (Run)sample.NameText.Inlines[0];
        Run last = (Run)sample.NameText.Inlines[2];
        IReadOnlyList<TextRun> unaffected = ((ITextSource)sample.FormattedText).TextRuns;
        model.FirstName = "Grace";
        Assert.Equal("Grace Lovelace", ((ITextSource)sample.NameText).Text);
        Assert.Same(first, sample.NameText.Inlines[0]);
        Assert.Same(last, sample.NameText.Inlines[2]);
        Assert.Same(unaffected, ((ITextSource)sample.FormattedText).TextRuns);
        IReadOnlyList<TextRun> styles = ((ITextSource)sample.FormattedText).TextRuns;
        Assert.Equal(FontWeight.Bold, styles[1].Style.FontWeight);
        Assert.Equal(FontWeight.Bold, styles[2].Style.FontWeight);
        Assert.Equal(22d, styles[2].Style.FontSize);
    }

    public static void VerifyOwnershipAndLifetime()
    {
        using Text first = new();
        using Text second = new();
        Span outer = new();
        Span inner = new();
        outer.Inlines.Add(inner);
        first.Inlines.Add(outer);
        ExpectFailure(() => second.Inlines.Add(outer));
        ExpectFailure(() => inner.Inlines.Add(outer));
        first.Inlines.Remove(outer);
        second.Inlines.Add(outer);
        Run run = new() { Value = "one", Style = new Style { FontSize = 18 } };
        inner.Inlines.Add(run);
        int invalidations = 0;
        second.VisualInvalidated += (_, _) => invalidations++;
        run.Value = "two";
        Assert.True(invalidations > 0);
        inner.Inlines.Remove(run);
        invalidations = 0;
        run.Value = "detached";
        run.Style!.FontSize = 20;
        Assert.Equal(0, invalidations);
        second.Dispose();
        Assert.True(outer.IsDisposed);
        Assert.True(inner.IsDisposed);
        Assert.False(run.IsDisposed);
        run.Dispose();
    }

    public static void VerifyShapingAndUnicodeSelection()
    {
        using Text text = new() { Style = new Style { FontSize = 20 } };
        text.Inlines.Add(new Run { Value = "Latin " });
        text.Inlines.Add(new Run { Value = "العربية", Style = new Style { Color = Colors.Red } });
        text.Inlines.Add(new Run { Value = " e" });
        text.Inlines.Add(new Run { Value = "\u0301 👨‍👩‍👧‍👦", Style = new Style { Color = Colors.Blue } });
        ITextSource source = text;
        using SkiaTextLayout formatted = (SkiaTextLayout)SkiaTextShaper.Shared.CreateTextLayout(source,
            new Size(1000, 1000), text.ComputedStyle);
        using SkiaTextLayout plain = SkiaTextShaper.Shared.ShapeText(source.Text!, new Size(1000, 1000), text.ComputedStyle);
        Assert.True(Math.Abs(formatted.Size.Width - plain.Size.Width) < 0.01);
        Assert.Equal(plain.Lines[0].Clusters.Length, formatted.Lines[0].Clusters.Length);
        int accent = source.Text!.IndexOf("e\u0301", StringComparison.Ordinal);
        text.Select(accent + 1, 1);
        Assert.Equal("e\u0301", text.SelectedText);
        int family = source.Text.IndexOf("👨", StringComparison.Ordinal);
        text.Select(family + 2, 1);
        Assert.Equal("👨‍👩‍👧‍👦", text.SelectedText);
        Assert.True(formatted.HitTestTextRange(accent, 2).Length > 0);
        using SkiaTextLayout wrapped = (SkiaTextLayout)SkiaTextShaper.Shared.CreateTextLayout(source,
            new Size(80, 1000), text.ComputedStyle);
        Assert.True(wrapped.Lines.Length > 1);
        Assert.Equal(source.Text, wrapped.Text);
    }

    public static void VerifyMixedFontMetricsAndDrawing()
    {
        using Text text = new() { Style = new Style { FontSize = 16, Color = Colors.Black } };
        text.Inlines.Add(new Run { Value = "Large", Style = new Style { FontSize = 32, Color = Colors.Red } });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new Run { Value = "Small", Style = new Style { Color = Colors.Blue } });
        using SkiaTextLayout layout = (SkiaTextLayout)SkiaTextShaper.Shared.CreateTextLayout((ITextSource)text,
            new Size(400, 300), text.ComputedStyle);
        Assert.True(layout.Lines[0].Bounds.Height > layout.Lines[1].Bounds.Height);
        using SKBitmap bitmap = new(400, 150);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.White);
        layout.Draw(canvas, new Point(0, 0), Colors.Black);
        int red = 0;
        int blue = 0;
        foreach (SKColor pixel in bitmap.Pixels)
        {
            red += pixel.Red > 180 && pixel.Green < 100 && pixel.Blue < 100 ? 1 : 0;
            blue += pixel.Blue > 180 && pixel.Red < 100 && pixel.Green < 100 ? 1 : 0;
        }

        Assert.True(red > 20);
        Assert.True(blue > 20);
    }

    public static void VerifyHyperlinkInput()
    {
        using Text text = new();
        Hyperlink first = new() { NavigateUri = new Uri("https://example.com/first") };
        Hyperlink second = new() { NavigateUri = new Uri("https://example.com/second") };
        first.Inlines.Add(new Run { Value = "First" });
        second.Inlines.Add(new Run { Value = "Second" });
        text.Inlines.Add(first);
        text.Inlines.Add(new Run { Value = " " });
        text.Inlines.Add(second);
        using Window window = new() { Content = text, Width = 400, Height = 100 };
        int firstClicks = 0;
        int secondClicks = 0;
        first.Click += (_, args) => { firstClicks++; args.Handled = true; };
        second.Click += (_, args) => { secondClicks++; args.Handled = true; };
        Assert.True(text.Focus(FocusState.Keyboard));
        text.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.Enter));
        Assert.Equal(1, firstClicks);
        KeyEventArgs tab = new(Key.Tab);
        text.RaiseEvent(UIElement.KeyDownEvent, tab);
        Assert.True(tab.Handled);
        text.RaiseEvent(UIElement.KeyDownEvent, new KeyEventArgs(Key.Enter));
        Assert.Equal(1, secondClicks);
        window.RenderFrame()?.Dispose();
        window.HandlePointerPressed(new Point(4, 8));
        window.HandlePointerReleased(new Point(4, 8));
        Assert.Equal(2, firstClicks);
        window.HandlePointerPressed(new Point(4, 8));
        window.HandlePointerMoved(new Point(25, 8));
        window.HandlePointerReleased(new Point(25, 8));
        Assert.True(text.HasSelection);
        Assert.Equal(2, firstClicks);
    }

    public static void VerifyHyperlinkHoverUnderline()
    {
        using Text text = new() { Style = new Style { FontSize = 20, Color = Colors.Blue } };
        Hyperlink link = new();
        link.Inlines.Add(new Run { Value = "License terms and conditions" });
        text.Inlines.Add(link);
        using Window window = new() { Content = text, Width = 100, Height = 220 };
        using SKImage initial = window.RenderFrame()!;
        int before = CountBluePixels(initial);
        window.HandlePointerMoved(new Point(4, 8), isPressed: false);
        using SKImage hovered = window.RenderFrame()!;
        Assert.True(CountBluePixels(hovered) > before + 30, "Hover must underline the wrapped link.");
        window.HandlePointerMoved(new Point(150, 200), isPressed: false);
        using SKImage exited = window.RenderFrame()!;
        Assert.Equal(before, CountBluePixels(exited));
    }

    private static int CountBluePixels(SKImage image)
    {
        using SKBitmap bitmap = SKBitmap.FromImage(image);
        return bitmap.Pixels.Count(static pixel => pixel.Blue > 180 && pixel.Red < 100 && pixel.Green < 100);
    }

    private static void ExpectFailure(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("Expected inline ownership rejection.");
    }
}

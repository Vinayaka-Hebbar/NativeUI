using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Graphics;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;
using SkiaSharp;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies selection style resolution, clipped selection painting, and shaped layout reuse.
/// </summary>
internal static class SelectionStyleTests
{
    private static readonly Color s_red = Color.FromRgbBytes(220, 38, 38);

    internal static void VerifyResolution()
    {
        CssStyleRegistry.Register(new CssStyleSheet(nameof(SelectionView), string.Empty,
        [
            new CssRule(":root", null, [], true, new Dictionary<string, CssValue>
            {
                ["--selection"] = new CssColorValue(Colors.Indigo)
            }),
            new CssRule(".scoped", null, ["scoped"], false, new Dictionary<string, CssValue>
            {
                ["--selection"] = new CssColorValue(Colors.Green)
            }),
            SelectionRule("parent", new Dictionary<string, CssValue>
            {
                ["background-color"] = new CssColorValue(Colors.Blue),
                ["color"] = new CssColorValue(Colors.White)
            }),
            SelectionRule("child", new Dictionary<string, CssValue>
            {
                ["color"] = new CssColorValue(s_red)
            }),
            SelectionRule("scoped", new Dictionary<string, CssValue>
            {
                ["background-color"] = new CssVariableReference("--selection")
            }),
            new CssRule(".strong.weak::selection", null, ["strong", "weak"], false, new Dictionary<string, CssValue>
            {
                ["color"] = new CssColorValue(Colors.Green)
            }, pseudoElement: CssPseudoElement.Selection),
            SelectionRule("weak", new Dictionary<string, CssValue>
            {
                ["color"] = new CssColorValue(Colors.Black)
            }),
            SelectionRule("themed", new Dictionary<string, CssValue>
            {
                ["color"] = new CssColorValue(Colors.Black)
            }),
            new CssRule(".themed::selection", null, ["themed"], false, new Dictionary<string, CssValue>
            {
                ["color"] = new CssColorValue(Colors.White)
            }, CssColorSchemeCondition.Dark, pseudoElement: CssPseudoElement.Selection)
        ]));

        using SelectionView parent = new() { Class = "parent" };
        Text child = new() { Class = "child" };
        Text scoped = new() { Class = "scoped" };
        Text specific = new() { Class = "strong weak" };
        Text themed = new() { Class = "themed" };
        parent.Children.Add(child);
        parent.Children.Add(scoped);
        parent.Children.Add(specific);
        parent.Children.Add(themed);

        Assert.Equal<Color?>(s_red, child.SelectionStyle.TextColor);
        Assert.Equal<Color?>(Colors.Blue, child.SelectionStyle.BackgroundColor);
        Assert.Equal<Color?>(Colors.Green, scoped.SelectionStyle.BackgroundColor);
        Assert.Equal<Color?>(Colors.White, scoped.SelectionStyle.TextColor);
        Assert.Equal<Color?>(Colors.Green, specific.SelectionStyle.TextColor);

        Assert.Equal<Color?>(null, child.ComputedStyle.TextColor);
        Assert.Equal<Color?>(null, child.ComputedStyle.BackgroundColor);

        using TestTheme testTheme = new();
        testTheme.Apply(parent, Theme.Light);
        Assert.Equal<Color?>(Colors.Black, themed.SelectionStyle.TextColor);
        testTheme.Apply(parent, Theme.Dark);
        Assert.Equal<Color?>(Colors.White, themed.SelectionStyle.TextColor);
    }

    internal static void VerifyPaintingOrderAndLayoutReuse()
    {
        CssStyleRegistry.Register(new CssStyleSheet(nameof(PaintView), string.Empty,
        [
            SelectionRule("painted", new Dictionary<string, CssValue>
            {
                ["background-color"] = new CssColorValue(Colors.Blue),
                ["color"] = new CssColorValue(Colors.White)
            })
        ]));

        using PaintView root = new();
        Text text = new()
        {
            Class = "painted",
            Value = "Wrapped selection text with שלום עולם inside it, long enough to wrap across several lines",
            Style = new Style { Color = Colors.Black }
        };
        root.Children.Add(text);

        using VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        RecordingContext context = new();
        Size viewport = new(160, 400);
        LayoutBox box = renderer.Render(context, root, viewport).Children[0];
        ITextLayout layout = box.TextLayout!;
        Assert.Equal(1, context.TextDraws.Count);

        context = new RecordingContext();
        text.Select(0, text.Value.Length);
        LayoutBox selectedBox = renderer.Render(context, root, viewport).Children[0];
        Assert.Same(layout, selectedBox.TextLayout!);
        Assert.True(layout.HitTestTextRange(0, text.Value.Length).Length > 1, "The selection must span several rectangles.");
        Assert.Equal(
            "Save,Scale,Clear,Save,Clip:Intersect,Rect,Restore,Save,Clip:Exclude,Text,Restore,Save,Clip:Intersect,Text,Restore,Restore",
            string.Join(',', context.Commands));
        Assert.Same(context.TextDraws[0].Layout, context.TextDraws[1].Layout);
        Assert.Equal(context.TextDraws[0].Origin, context.TextDraws[1].Origin);
        Assert.Equal(Colors.Black, context.TextDraws[0].Color);
        Assert.Equal(Colors.White, context.TextDraws[1].Color);

        text.Style!.Color = Colors.Green;
        LayoutBox recoloredBox = renderer.Render(new RecordingContext(), root, viewport).Children[0];
        Assert.Same(layout, recoloredBox.TextLayout!);

        SkiaTextLayout replaced = (SkiaTextLayout)layout;
        text.Value = "Replacement";
        LayoutBox replacedBox = renderer.Render(new RecordingContext(), root, viewport).Children[0];
        Assert.False(ReferenceEquals(layout, replacedBox.TextLayout));

        using SkiaSurface surface = new(10, 10);
        bool disposed = false;
        try
        {
            replaced.Draw(surface.Canvas, Point.Zero, Colors.Black);
        }
        catch (ObjectDisposedException)
        {
            disposed = true;
        }

        Assert.True(disposed, "Replaced layouts must be disposed.");

        LayoutBox resizedBox = renderer.Render(new RecordingContext(), root, new Size(80, 400)).Children[0];
        Assert.False(ReferenceEquals(replacedBox.TextLayout, resizedBox.TextLayout));
    }

    internal static void VerifyClipPaintsTranslucentRegionOnce()
    {
        using SkiaSurface surface = new(20, 20);
        surface.Canvas.Clear(SKColors.White);
        SkiaDrawingContext context = new(surface.Canvas);
        Rect[] overlapping = [new Rect(0, 0, 10, 10), new Rect(5, 0, 10, 10)];

        context.Save();
        context.ClipRegion(overlapping, ClipMode.Intersect);
        context.DrawRectangle(new Rect(0, 0, 15, 10), new Paint(Color.FromRgbBytes(0, 0, 0, 128)));
        context.Restore();

        context.Save();
        context.ClipRegion(overlapping, ClipMode.Exclude);
        context.DrawRectangle(new Rect(0, 0, 20, 20), new Paint(Colors.Black));
        context.Restore();

        using SKImage image = surface.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);
        SKColor single = bitmap.GetPixel(2, 5);
        SKColor overlap = bitmap.GetPixel(7, 5);
        SKColor outside = bitmap.GetPixel(18, 5);
        Assert.Equal(single, overlap);
        Assert.True(single.Red is > 120 and < 135, $"Expected one translucent layer, got {single}.");
        Assert.Equal(new SKColor(0, 0, 0, 255), outside);
    }

    private static CssRule SelectionRule(string className, Dictionary<string, CssValue> declarations)
    {
        return new CssRule(
            $".{className}::selection",
            null,
            [className],
            false,
            declarations,
            pseudoElement: CssPseudoElement.Selection);
    }

    private sealed class SelectionView : View;

    private sealed class PaintView : View;

    private sealed class RecordingContext : DrawingContext
    {
        public List<string> Commands { get; } = [];

        public List<(ITextLayout Layout, Point Origin, Color Color)> TextDraws { get; } = [];

        public override void ClipRegion(IReadOnlyList<Rect> region, ClipMode mode)
        {
            Commands.Add($"Clip:{mode}");
        }

        public override void ClipRect(Rect rect)
        {
            Commands.Add("ClipRect");
        }

        public override void ClipRoundedRectangle(Rect rect, double radius)
        {
            Commands.Add("ClipRoundedRectangle");
        }

        public override void SaveLayer(Rect bounds, double opacity)
        {
            Commands.Add("SaveLayer");
        }

        public override void Save()
        {
            Commands.Add("Save");
        }

        public override void Translate(double dx, double dy)
        {
            Commands.Add($"Translate:{dx},{dy}");
        }

        public override void Restore()
        {
            Commands.Add("Restore");
        }

        public override void Scale(float scaleX, float scaleY)
        {
            Commands.Add("Scale");
        }

        public override void Clear(Color color)
        {
            Commands.Add("Clear");
        }

        public override void DrawRectangle(Rect rect, Paint paint)
        {
            Commands.Add("Rect");
        }

        public override void StrokeRoundedRectangle(Rect rect, double radius, double strokeWidth, Paint paint) { }

        public override void FillPolygon(IReadOnlyList<Point> points, Paint paint) { }

        public override void FillLinearGradient(Rect rect, Point start, Point end, IReadOnlyList<GradientStop> stops) { }

        public override void DrawRoundedRectangle(Rect rect, double radius, Paint paint)
        {
            Commands.Add("RoundedRect");
        }

        public override void DrawTextLayout(ITextLayout layout, Point origin, Paint paint)
        {
            Commands.Add("Text");
            TextDraws.Add((layout, origin, paint.Color));
        }
    }
}

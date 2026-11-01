using NativeUI.Graphics;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies <see cref="CanvasView"/> custom 2D drawing, event dispatch, invalidation, and desktop rendering.
/// </summary>
internal static class CanvasViewTests
{
    public static void VerifyVirtualOnDraw()
    {
        ChartView chart = new();

        TestDrawingContext context = new();
        chart.RenderCanvas(context, new Rect(0, 0, 600, 400));

        Assert.Equal(1, context.Rectangles.Count);
        Assert.Equal(new Rect(0, 0, 600, 400), context.Rectangles[0].Rect);
        Assert.Equal(Colors.Blue, context.Rectangles[0].Paint.Color);
    }

    public static void VerifyDrawEvent()
    {
        CanvasView canvas = new();

        bool eventRaised = false;
        canvas.Draw += (sender, args) =>
        {
            eventRaised = true;
            Assert.Same(canvas, sender!);
            Assert.Equal(new Rect(0, 0, 400, 300), args.Bounds);
            args.Context.DrawRectangle(
                new Rect(10, 10, 120, 60),
                new Paint { Color = Colors.Red });
            args.Context.DrawRoundedRectangle(
                new Rect(250, 20, 150, 100),
                12,
                new Paint { Color = Colors.Red });
        };

        TestDrawingContext context = new();
        canvas.RenderCanvas(context, new Rect(0, 0, 400, 300));

        Assert.True(eventRaised);
        Assert.Equal(1, context.Rectangles.Count);
        Assert.Equal(new Rect(10, 10, 120, 60), context.Rectangles[0].Rect);
        Assert.Equal(Colors.Red, context.Rectangles[0].Paint.Color);
        Assert.Equal(1, context.RoundedRectangles.Count);
        Assert.Equal(12.0, context.RoundedRectangles[0].Radius);
    }

    public static void VerifyOnDrawRunsBeforeDrawEvent()
    {
        ChartView chart = new();
        int rectanglesWhenEventRaised = -1;
        TestDrawingContext context = new();
        chart.Draw += (_, _) => rectanglesWhenEventRaised = context.Rectangles.Count;

        chart.RenderCanvas(context, new Rect(0, 0, 100, 100));

        Assert.Equal(1, rectanglesWhenEventRaised);
    }

    public static void VerifyInvalidateVisual()
    {
        CanvasView canvas = new();

        int invalidations = 0;
        canvas.VisualInvalidated += (_, _) => invalidations++;

        canvas.InvalidateVisual();
        canvas.InvalidateVisual();

        Assert.Equal(2, invalidations);
    }

    public static void VerifyVisualTreeRendererDrawsCanvas()
    {
        CanvasView canvas = new()
        {
            Style = new Style { Width = 300, Height = 200 }
        };
        canvas.Draw += (_, args) =>
            args.Context.DrawRectangle(new Rect(0, 0, args.Bounds.Width, args.Bounds.Height), new Paint { Color = Colors.Green });
        canvas.InvalidateStyle();

        using VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        TestDrawingContext context = new();
        renderer.Render(context, canvas, new Size(800, 600));

        Assert.True(context.Operations.Any(static op => op.StartsWith("Translate", StringComparison.Ordinal)));
        Assert.True(context.Operations.Any(static op => op.StartsWith("ClipRect", StringComparison.Ordinal)));
        Assert.Equal(1, context.Rectangles.Count);
        Assert.Equal(new Rect(0, 0, 300, 200), context.Rectangles[0].Rect);
        Assert.Equal(Colors.Green, context.Rectangles[0].Paint.Color);
    }

    public static void VerifyDefaultHeightAndFillWidth()
    {
        CanvasView canvas = new();
        Rect? bounds = null;
        canvas.Draw += (_, args) => bounds = args.Bounds;
        canvas.InvalidateStyle();

        using VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        renderer.Render(new TestDrawingContext(), canvas, new Size(800, 600));

        Assert.Equal<Rect?>(new Rect(0, 0, 800, 150), bounds);
    }

    private sealed class ChartView : CanvasView
    {
        protected override void OnDraw(DrawingContext context, Rect bounds)
        {
            context.DrawRectangle(
                new Rect(0, 0, bounds.Width, bounds.Height),
                new Paint
                {
                    Color = Colors.Blue
                });
        }
    }

    private sealed class TestDrawingContext : DrawingContext
    {
        public List<string> Operations { get; } = [];
        public List<(Rect Rect, Paint Paint)> Rectangles { get; } = [];
        public List<(Rect Rect, double Radius, Paint Paint)> RoundedRectangles { get; } = [];

        public override void Save() => Operations.Add("Save");
        public override void Restore() => Operations.Add("Restore");
        public override void Translate(double dx, double dy) => Operations.Add($"Translate {dx},{dy}");
        public override void Scale(float scaleX, float scaleY) => Operations.Add($"Scale {scaleX},{scaleY}");
        public override void SaveLayer(Rect bounds, double opacity) => Operations.Add($"SaveLayer {bounds}");
        public override void Clear(Color color) => Operations.Add($"Clear {color}");
        public override void ClipRect(Rect rect) => Operations.Add($"ClipRect {rect}");
        public override void ClipRoundedRectangle(Rect rect, double radius) => Operations.Add($"ClipRoundedRectangle {rect} r={radius}");
        public override void ClipRegion(IReadOnlyList<Rect> region, ClipMode mode) => Operations.Add("ClipRegion");

        public override void DrawRectangle(Rect rect, Paint paint)
        {
            Operations.Add($"DrawRectangle {rect} {paint.Color}");
            Rectangles.Add((rect, paint));
        }

        public override void DrawRoundedRectangle(Rect rect, double radius, Paint paint)
        {
            Operations.Add($"DrawRoundedRectangle {rect} r={radius} {paint.Color}");
            RoundedRectangles.Add((rect, radius, paint));
        }

        public override void DrawTextLayout(ITextLayout layout, Point origin, Paint paint)
        {
            Operations.Add($"DrawTextLayout {origin}");
        }
    }
}

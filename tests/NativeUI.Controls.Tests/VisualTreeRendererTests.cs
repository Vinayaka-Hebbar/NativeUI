using NativeUI.Controls;
using NativeUI.Core;
using NativeUI.Graphics;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

public static class VisualTreeRendererTests
{
    public static void VerifyLayoutFailureRollbackPreservesRetainedLayouts()
    {
        TrackingTextMeasurer inner = new(SkiaTextShaper.Shared);
        using VisualTreeRenderer renderer = new(inner);
        MockDrawingContext context = new();

        View root = new();
        Text text1 = new() { Value = "Initial retained text" };
        root.Children.Add(text1);

        LayoutBox firstPass = renderer.Render(context, root, new Size(200, 200));
        ITextLayout retainedLayout = firstPass.Children[0].TextLayout!;
        Assert.NotNull(retainedLayout);
        Assert.False(inner.DisposedLayouts.Contains(retainedLayout));

        // In second pass, layout will throw
        inner.ThrowOnNextCreate = true;
        Text text2 = new() { Value = "New text that will fail" };
        root.Children.Add(text2);

        AssertThrows<InvalidOperationException>(() =>
        {
            renderer.Render(context, root, new Size(200, 200));
        });

        // The previously retained layout must still be alive!
        Assert.False(inner.DisposedLayouts.Contains(retainedLayout), "Previous cache must be preserved on layout failure.");
    }

    public static void VerifyReliableContextRestoreOnException()
    {
        using VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        MockDrawingContext context = new() { ThrowOnDraw = true };

        View root = new()
        {
            Style = new Style { BackgroundColor = Colors.Blue }
        };

        AssertThrows<InvalidOperationException>(() =>
        {
            renderer.Render(context, root, new Size(200, 200));
        });

        Assert.Equal(context.SaveCount, context.RestoreCount);
    }

    public static void VerifyRoundedContainerClipping()
    {
        using VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        MockDrawingContext context = new();

        View container = new()
        {
            ClipToBounds = true,
            Style = new Style { BorderRadius = 12 }
        };
        container.Children.Add(new Text { Value = "Inside clipped rounded view" });

        renderer.Render(context, container, new Size(200, 200));

        Assert.True(context.ClippedRoundedRectangles.Count > 0, "Container with ClipToBounds and BorderRadius must invoke ClipRoundedRectangle.");
        Assert.Equal(12.0, context.ClippedRoundedRectangles[0].Radius);
        Assert.Equal(context.SaveCount, context.RestoreCount);
    }

    public static void VerifyGroupOpacityCompositing()
    {
        using VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        MockDrawingContext context = new();

        View group = new()
        {
            Style = new Style { Opacity = 0.5 }
        };
        group.Children.Add(new Text { Value = "Child 1" });
        group.Children.Add(new Text { Value = "Child 2" });

        renderer.Render(context, group, new Size(200, 200));

        Assert.True(context.SavedLayers.Count > 0, "Container with Opacity < 1.0 and children must call SaveLayer.");
        Assert.Equal(0.5, context.SavedLayers[0].Opacity);
        Assert.Equal(context.SaveCount + context.SavedLayers.Count, context.RestoreCount);
    }

    public static void VerifyDisposedRendererThrows()
    {
        VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        renderer.Dispose();

        View root = new();
        MockDrawingContext context = new();

        AssertThrows<ObjectDisposedException>(() =>
        {
            renderer.Render(context, root, new Size(200, 200));
        });
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        bool thrown = false;
        try
        {
            action();
        }
        catch (TException)
        {
            thrown = true;
        }

        Assert.True(thrown, $"Expected {typeof(TException).Name}.");
    }

    private sealed class TrackingTextMeasurer(ITextMeasurer inner) : ITextMeasurer
    {
        public bool ThrowOnNextCreate { get; set; }
        public HashSet<ITextLayout> DisposedLayouts { get; } = new(ReferenceEqualityComparer.Instance);

        public Size MeasureText(string text, Size availableSize, ComputedStyle style)
        {
            return inner.MeasureText(text, availableSize, style);
        }

        public ITextLayout CreateTextLayout(string text, Size availableSize, ComputedStyle style)
        {
            if (ThrowOnNextCreate)
            {
                throw new InvalidOperationException("Simulated layout creation failure");
            }

            ITextLayout layout = inner.CreateTextLayout(text, availableSize, style);
            return new TrackingLayout(layout, this);
        }

        private sealed class TrackingLayout(ITextLayout innerLayout, TrackingTextMeasurer owner) : ITextLayout, IDisposable
        {
            public string Text => innerLayout.Text;
            public Size Size => innerLayout.Size;
            public System.Collections.Immutable.ImmutableArray<TextLine> Lines => innerLayout.Lines;

            public System.Collections.Immutable.ImmutableArray<Rect> HitTestTextRange(int start, int length)
            {
                return innerLayout.HitTestTextRange(start, length);
            }

            public TextHitTestResult HitTestPoint(Point point)
            {
                return innerLayout.HitTestPoint(point);
            }

            public TextRange GetWordRangeAt(int offset)
            {
                return innerLayout.GetWordRangeAt(offset);
            }

            public void Dispose()
            {
                owner.DisposedLayouts.Add(this);
                (innerLayout as IDisposable)?.Dispose();
            }
        }
    }

    private sealed class MockDrawingContext : DrawingContext
    {
        public bool ThrowOnDraw { get; set; }
        public int SaveCount { get; private set; }
        public int RestoreCount { get; private set; }
        public List<(Rect Bounds, double Opacity)> SavedLayers { get; } = [];
        public List<(Rect Rect, double Radius)> ClippedRoundedRectangles { get; } = [];

        public override void Save() => SaveCount++;
        public override void Restore() => RestoreCount++;

        public override void SaveLayer(Rect bounds, double opacity)
        {
            SavedLayers.Add((bounds, opacity));
        }

        public override void ClipRect(Rect rect) { }

        public override void ClipRoundedRectangle(Rect rect, double radius)
        {
            ClippedRoundedRectangles.Add((rect, radius));
        }

        public override void ClipRegion(IReadOnlyList<Rect> region, ClipMode mode) { }
        public override void Scale(float scaleX, float scaleY) { }
        public override void Clear(Color color) { }

        public override void DrawRectangle(Rect rect, Paint paint)
        {
            if (ThrowOnDraw)
            {
                throw new InvalidOperationException("Simulated drawing error");
            }
        }

        public override void DrawRoundedRectangle(Rect rect, double radius, Paint paint) { }
        public override void DrawTextLayout(ITextLayout layout, Point origin, Paint paint) { }
    }
}

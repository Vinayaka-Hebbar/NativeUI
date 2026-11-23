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

    public static void VerifyDrawingUsesCompletedLayout()
    {
        TrackingTextMeasurer measurer = new(SkiaTextShaper.Shared);
        using VisualTreeRenderer renderer = new(measurer);
        MockDrawingContext context = new();
        View root = new() { Style = new Style { Width = 200, Height = 100 } };
        Text text = new() { Value = "Completed layout" };
        root.Children.Add(text);
        int sizeChanges = 0;
        root.SizeChanged += (_, _) => sizeChanges++;
        TrackingOverlay overlay = new();
        renderer.Overlays.Add(overlay);

        RenderPass pass = renderer.ComputeLayout(root, new Size(400, 300));
        int measurements = measurer.LayoutCount;
        Assert.Equal(1, sizeChanges);
        Assert.Equal(0, context.SaveCount);
        Assert.Equal(new Size(200, 100), root.ActualSize);

        text.Value = "A pending change must not cause drawing to compute a new layout";
        renderer.Draw(context, pass);
        renderer.Draw(context, pass);

        Assert.Equal(measurements, measurer.LayoutCount);
        Assert.Equal(1, sizeChanges);
        Assert.Equal(2, overlay.DrawCount);
        Assert.True(ReferenceEquals(pass.Root, overlay.LastPass.Root));
        Assert.True(ReferenceEquals(pass.Root.Children[0].TextLayout, context.LastTextLayout));
        Assert.Equal("Completed layout", context.LastTextLayout?.Text);
        Assert.Equal(context.SaveCount, context.RestoreCount);
    }

    public static void VerifyCompletedPortalGeometry()
    {
        using VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        MockDrawingContext context = new();
        View root = new();
        Portal portal = new();
        portal.Children.Add(new Text { Value = "Portal content" });
        root.Children.Add(portal);
        portal.Show();
        TrackingOverlay overlay = new();
        renderer.Overlays.Add(overlay);

        RenderPass pass = renderer.ComputeLayout(root, new Size(400, 300));
        Assert.Equal(1, pass.PortalBoxes.Count);
        Assert.True(ReferenceEquals(pass.PortalBoxes, renderer.PortalBoxes));
        renderer.Draw(context, pass);

        Assert.True(ReferenceEquals(pass.PortalBoxes, overlay.LastPass.PortalBoxes));
        Assert.True(ReferenceEquals(pass.PortalBoxes[0], renderer.PortalBoxes[0]));
        Assert.Equal(pass.PortalBoxes[0].BorderBox, portal.Bounds);
    }

    public static void VerifyLayoutWithoutDrawingReleasesText()
    {
        TrackingTextMeasurer measurer = new(SkiaTextShaper.Shared);
        VisualTreeRenderer renderer = new(measurer);
        Text root = new() { Value = "Measured without drawing" };
        RenderPass pass = renderer.ComputeLayout(root, new Size(200, 100));
        ITextLayout textLayout = pass.Root.TextLayout ?? throw new InvalidOperationException("Missing text layout.");

        renderer.Dispose();

        Assert.True(measurer.DisposedLayouts.Contains(textLayout));
        AssertThrows<ObjectDisposedException>(() => renderer.ComputeLayout(root, new Size(200, 100)));
        AssertThrows<ObjectDisposedException>(() => renderer.Draw(new MockDrawingContext(), pass));
    }

    private sealed class TrackingOverlay : IRenderOverlay
    {
        public int DrawCount { get; private set; }
        public RenderPass LastPass { get; private set; }

        public void Draw(DrawingContext context, RenderPass pass)
        {
            DrawCount++;
            LastPass = pass;
        }
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

    public static void VerifySizeChangedEvent()
    {
        View root = new()
        {
            Style = new Style { Width = 300, Height = 200 }
        };
        View child = new()
        {
            Style = new Style { Width = 150, Height = 80 }
        };
        root.Children.Add(child);

        int rootEvents = 0;
        Size rootPrev = default;
        Size rootNew = default;
        root.SizeChanged += (_, args) =>
        {
            rootEvents++;
            rootPrev = args.PreviousSize;
            rootNew = args.NewSize;
        };

        int childEvents = 0;
        Size childPrev = default;
        Size childNew = default;
        child.SizeChanged += (_, args) =>
        {
            childEvents++;
            childPrev = args.PreviousSize;
            childNew = args.NewSize;
        };

        using VisualTreeRenderer renderer = new(SkiaTextShaper.Shared);
        MockDrawingContext context = new();

        renderer.Render(context, root, new Size(500, 500));
        Assert.Equal(1, rootEvents);
        Assert.Equal(new Size(0, 0), rootPrev);
        Assert.Equal(new Size(300, 200), rootNew);
        Assert.Equal(new Size(300, 200), root.ActualSize);
        Assert.Equal(300, root.ActualWidth);
        Assert.Equal(200, root.ActualHeight);
        Assert.Equal(new Rect(0, 0, 300, 200), root.Bounds);

        Assert.Equal(1, childEvents);
        Assert.Equal(new Size(0, 0), childPrev);
        Assert.Equal(new Size(150, 80), childNew);
        Assert.Equal(new Size(150, 80), child.ActualSize);
        Assert.Equal(150, child.ActualWidth);
        Assert.Equal(80, child.ActualHeight);

        renderer.Render(context, root, new Size(500, 500));
        Assert.Equal(1, rootEvents);
        Assert.Equal(1, childEvents);

        // Position-only change does NOT raise SizeChanged
        root.UpdateRenderedBounds(new Rect(50, 60, 300, 200));
        Assert.Equal(1, rootEvents);
        Assert.Equal(new Rect(50, 60, 300, 200), root.Bounds);

        child.Style = new Style { Width = 250, Height = 100 };
        renderer.Render(context, root, new Size(500, 500));
        Assert.Equal(1, rootEvents);
        Assert.Equal(2, childEvents);
        Assert.Equal(new Size(150, 80), childPrev);
        Assert.Equal(new Size(250, 100), childNew);
        Assert.Equal(new Size(250, 100), child.ActualSize);
        Assert.Equal(250, child.ActualWidth);
        Assert.Equal(100, child.ActualHeight);

        int sizeChangedCount = 0;
        Size observedPrev = default;
        Size observedNew = default;
        EventHandler<SizeChangedEventArgs> handler = (_, e) =>
        {
            sizeChangedCount++;
            observedPrev = e.PreviousSize;
            observedNew = e.NewSize;
        };

        child.SizeChanged += handler;
        child.UpdateRenderedBounds(new Rect(10, 20, 300, 150));
        Assert.Equal(1, sizeChangedCount);
        Assert.Equal(new Size(250, 100), observedPrev);
        Assert.Equal(new Size(300, 150), observedNew);

        child.SizeChanged -= handler;
        child.UpdateRenderedBounds(new Rect(10, 20, 350, 180));
        Assert.Equal(1, sizeChangedCount);
    }

    private sealed class TrackingTextMeasurer(ITextMeasurer inner) : ITextMeasurer
    {
        public bool ThrowOnNextCreate { get; set; }
        public int LayoutCount { get; private set; }
        public HashSet<ITextLayout> DisposedLayouts { get; } = new(ReferenceEqualityComparer.Instance);

        public Size MeasureText(string text, Size availableSize, ComputedStyle style)
        {
            return inner.MeasureText(text, availableSize, style);
        }

        public ITextLayout CreateTextLayout(string text, Size availableSize, ComputedStyle style)
        {
            LayoutCount++;
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
        public ITextLayout? LastTextLayout { get; private set; }
        public int SaveCount { get; private set; }
        public int RestoreCount { get; private set; }
        public List<(Rect Bounds, double Opacity)> SavedLayers { get; } = [];
        public List<(Rect Rect, double Radius)> ClippedRoundedRectangles { get; } = [];

        public override void Save() => SaveCount++;
        public override void Restore() => RestoreCount++;
        public override void Translate(double dx, double dy) { }

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
        public override void StrokeRoundedRectangle(Rect rect, double radius, double strokeWidth, Paint paint) { }
        public override void FillPolygon(IReadOnlyList<Point> points, Paint paint) { }
        public override void FillLinearGradient(Rect rect, Point start, Point end, IReadOnlyList<GradientStop> stops) { }
        public override void DrawTextLayout(ITextLayout layout, Point origin, Paint paint)
        {
            LastTextLayout = layout;
        }
    }
}
